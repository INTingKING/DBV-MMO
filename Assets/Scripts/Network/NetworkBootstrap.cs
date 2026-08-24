using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NetworkBootstrap
{
    public const string GameSceneName = "SampleScene";
    public const string MainMenuSceneName = "MainMenu";

    private static bool _subscribed;

    /// <summary>
    /// RuntimeInitialize AfterSceneLoad only fires for the FIRST scene in a play session.
    /// MainMenu → SampleScene needs SceneManager.sceneLoaded or the pending Host/Client is ignored.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _subscribed = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnFirstSceneLoad()
    {
        EnsureSceneLoadedHook();
        TryHandleActiveScene("AfterSceneLoad");
    }

    private static void EnsureSceneLoadedHook()
    {
        if (_subscribed)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        _subscribed = true;
        Debug.Log("[NetworkBootstrap] Subscribed to SceneManager.sceneLoaded");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryHandleScene(scene, $"sceneLoaded({mode})");
    }

    private static void TryHandleActiveScene(string reason)
    {
        TryHandleScene(SceneManager.GetActiveScene(), reason);
    }

    private static void TryHandleScene(Scene scene, string reason)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (scene.name != GameSceneName && scene.buildIndex != 1)
            return;

        Debug.Log($"[NetworkBootstrap] Handle game scene '{scene.name}' via {reason}");

        GameSettings.Load();
        GameSettings.Apply(save: false);
        UIEventSystem.Ensure();

        PendingNetworkStart.LoadFromPrefsIfNeeded();

        bool dedicatedCli = ShouldStartDedicatedServer(out ushort cliPort, out string bindAddress);
        bool menuDriven = PendingNetworkStart.HasPending;

        Debug.Log(
            $"[NetworkBootstrap] pending={menuDriven} mode={PendingNetworkStart.Mode} " +
            $"addr={PendingNetworkStart.Address}:{PendingNetworkStart.Port} cliServer={dedicatedCli}");

        // Avoid double-start if we already began from a previous callback this session.
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient))
        {
            Debug.Log("[NetworkBootstrap] Network already running — skip auto-start.");
            NetworkConnectionUI.EnsureExists().SetMenuDrivenMode(true);
            GameOptionsUI.EnsureExists();
            ClassSelectUI.EnsureExists();
            return;
        }

        if (GameObject.Find("NetworkSessionStarter") != null)
        {
            Debug.Log("[NetworkBootstrap] Starter already exists — skip.");
            return;
        }

        GameObject starterGo = new GameObject("NetworkSessionStarter");
        GameObject.DontDestroyOnLoad(starterGo);
        NetworkSessionStarter starter = starterGo.AddComponent<NetworkSessionStarter>();

        if (dedicatedCli)
        {
            Debug.Log($"[NetworkBootstrap] Dedicated server CLI. port={cliPort}");
            PendingNetworkStart.Clear();
            NetworkConnectionUI.EnsureExists().HideForDedicatedServer();
            starter.Begin(PendingNetworkMode.Server, "127.0.0.1", cliPort, "0.0.0.0", hideUi: true, returnToMenuOnFail: false);
            return;
        }

        if (menuDriven)
        {
            PendingNetworkMode mode = PendingNetworkStart.Mode;
            // If static was cleared but prefs exist, Mode may still be None until LoadFromPrefs.
            if (mode == PendingNetworkMode.None)
                PendingNetworkStart.LoadFromPrefsIfNeeded();
            mode = PendingNetworkStart.Mode;

            string address = PendingNetworkStart.Address;
            ushort port = PendingNetworkStart.Port;
            PendingNetworkStart.Clear();

            if (mode == PendingNetworkMode.None)
            {
                Debug.LogWarning("[NetworkBootstrap] HasPending was true but Mode is None — fallback UI.");
                GameObject.Destroy(starterGo);
                NetworkConnectionUI.EnsureExists().SetMenuDrivenMode(false);
                GameOptionsUI.EnsureExists();
                return;
            }

            Debug.Log($"[NetworkBootstrap] Starting from menu: {mode} {address}:{port}");
            NetworkConnectionUI.EnsureExists().SetMenuDrivenMode(true);
            GameOptionsUI.EnsureExists();
            starter.Begin(mode, address, port, "0.0.0.0", hideUi: true, returnToMenuOnFail: true);
            return;
        }

        Debug.Log("[NetworkBootstrap] No menu pending — connection UI (direct SampleScene play).");
        GameObject.Destroy(starterGo);
        NetworkConnectionUI ui = NetworkConnectionUI.EnsureExists();
        GameOptionsUI.EnsureExists();
        ui.SetMenuDrivenMode(false);
    }

    public static bool IsGameSceneLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && (s.name == GameSceneName || s.buildIndex == 1))
                return true;
        }
        return false;
    }

    public static void ReturnToMainMenu(string error = null)
    {
        // Full reset then load a clean MainMenu.
        SessionReset.ReturnToMainMenuFresh(error);
    }

    /// <summary>Legacy name — full session scrub for main menu.</summary>
    public static void ScrubAllGameplayUi()
    {
        SessionReset.ResetForMainMenu(keepPendingError: true);
    }

    private static bool ShouldStartDedicatedServer(out ushort port, out string bindAddress)
    {
        port = 42069;
        bindAddress = "0.0.0.0";

        string[] args = System.Environment.GetCommandLineArgs();
        bool server = Application.isBatchMode;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "-server", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-dedicatedServer", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-dedicated", System.StringComparison.OrdinalIgnoreCase))
            {
                server = true;
            }
            else if (string.Equals(arg, "-port", System.StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (ushort.TryParse(args[i + 1], out ushort parsed))
                    port = parsed;
            }
            else if (string.Equals(arg, "-bind", System.StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                bindAddress = args[i + 1];
            }
        }

        return server;
    }

    private sealed class NetworkSessionStarter : MonoBehaviour
    {
        private PendingNetworkMode _mode;
        private string _address;
        private ushort _port;
        private string _listen;
        private bool _hideUi;
        private bool _returnToMenuOnFail;

        public void Begin(
            PendingNetworkMode mode,
            string address,
            ushort port,
            string listenAddress,
            bool hideUi,
            bool returnToMenuOnFail)
        {
            _mode = mode;
            _address = address;
            _port = port;
            _listen = listenAddress;
            _hideUi = hideUi;
            _returnToMenuOnFail = returnToMenuOnFail;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;
            yield return null;

            NetworkManager nm = null;
            float wait = 0f;
            while (nm == null && wait < 5f)
            {
                nm = NetworkManager.Singleton;
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            if (nm == null)
            {
                Fail("NetworkManager missing in game scene.");
                yield break;
            }

            if (nm.IsServer || nm.IsClient)
            {
                Debug.Log("[NetworkBootstrap] Network already running.");
                FinishSuccess(nm);
                yield break;
            }

            NetworkConfigNormalizer.Apply(nm);

            UnityTransport transport = nm.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Fail("UnityTransport missing on NetworkManager.");
                yield break;
            }

            if (_mode == PendingNetworkMode.Client)
                transport.SetConnectionData(_address, _port);
            else
                transport.SetConnectionData(_address, _port, "0.0.0.0");

            Debug.Log($"[NetworkBootstrap] Start{_mode} {_address}:{_port}");

            bool startOk;
            switch (_mode)
            {
                case PendingNetworkMode.Host:
                    startOk = nm.StartHost();
                    break;
                case PendingNetworkMode.Client:
                    startOk = nm.StartClient();
                    break;
                case PendingNetworkMode.Server:
                    startOk = nm.StartServer();
                    break;
                default:
                    startOk = false;
                    break;
            }

            if (!startOk)
            {
                Fail($"{_mode} failed to start (port in use?).");
                yield break;
            }

            // Host/Server are local — ready as soon as Start* succeeds.
            if (_mode == PendingNetworkMode.Host)
            {
                if (nm.IsHost)
                {
                    FinishSuccess(nm);
                    yield break;
                }
                Fail("Host started but IsHost is false.");
                yield break;
            }

            if (_mode == PendingNetworkMode.Server)
            {
                if (nm.IsServer)
                {
                    FinishSuccess(nm);
                    yield break;
                }
                Fail("Server started but IsServer is false.");
                yield break;
            }

            // Client: must actually reach a host. StartClient() alone is NOT success.
            float timeout = 6f;
            float elapsed = 0f;
            bool success = false;
            bool failed = false;
            string failReason = $"No host/server at {_address}:{_port}.";

            void OnClientConnected(ulong clientId)
            {
                if (clientId == nm.LocalClientId)
                    success = true;
            }

            void OnDisconnected(ulong clientId)
            {
                if (success)
                    return;
                // Disconnect before we ever connected = refused / no host.
                if (clientId == nm.LocalClientId || !nm.IsClient)
                {
                    failed = true;
                    failReason = $"Could not connect to {_address}:{_port}. Is a host running?";
                }
            }

            void OnTransportFail()
            {
                failed = true;
                failReason = $"Transport failure connecting to {_address}:{_port}.";
            }

            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnDisconnected;
            nm.OnTransportFailure += OnTransportFail;

            while (elapsed < timeout && !success && !failed)
            {
                // Approved connection: local client id assigned and (usually) player object.
                if (nm.IsClient && nm.LocalClient != null &&
                    nm.LocalClientId != ulong.MaxValue)
                {
                    if (nm.LocalClient.PlayerObject != null)
                    {
                        success = true;
                        break;
                    }
                }

                // Still running client handshake?
                if (!nm.IsClient && elapsed > 0.5f)
                {
                    failed = true;
                    failReason = $"Could not connect to {_address}:{_port}. Is a host running?";
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnDisconnected;
            nm.OnTransportFailure -= OnTransportFail;

            if (!success)
            {
                if (nm.IsServer || nm.IsClient)
                    nm.Shutdown();

                // Always leave SampleScene if join failed.
                Fail(failed
                    ? failReason
                    : $"Timed out joining {_address}:{_port}. No host/server found.");
                yield break;
            }

            FinishSuccess(nm);
        }

        private void FinishSuccess(NetworkManager nm)
        {
            Debug.Log($"[NetworkBootstrap] {_mode} OK IsHost={nm.IsHost} IsClient={nm.IsClient}");

            if (_hideUi)
                NetworkConnectionUI.EnsureExists().SetMenuDrivenMode(true);

            HubBootstrap.EnsureExists();
            EnemySpawner.EnsureExists();
            BossSpawner.EnsureExists();
            GameOptionsUI.EnsureExists();
            UIEventSystem.Ensure();

            StartCoroutine(AttachClassSelectWhenReady());
        }

        private IEnumerator AttachClassSelectWhenReady()
        {
            for (int i = 0; i < 180; i++)
            {
                PlayerClass pc = ClassSelectUI.FindLocalPlayerClass();
                if (pc != null && pc.IsSpawned)
                {
                    ClassSelectUI.EnsureOnPlayer(pc);
                    Debug.Log($"[NetworkBootstrap] ClassSelect on {pc.name}");
                    Destroy(gameObject);
                    yield break;
                }
                yield return null;
            }

            Debug.LogWarning("[NetworkBootstrap] Timed out waiting for local player.");
            Destroy(gameObject);
        }

        private void Fail(string message)
        {
            Debug.LogWarning($"[NetworkBootstrap] FAIL: {message}");
            if (_returnToMenuOnFail)
                ReturnToMainMenu(message);
            Destroy(gameObject);
        }
    }
}

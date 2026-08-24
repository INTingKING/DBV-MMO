using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class NetworkConnectionUI : MonoBehaviour
{
    public static NetworkConnectionUI Instance { get; private set; }

    [SerializeField] private string defaultAddress = "127.0.0.1";
    [SerializeField] private ushort defaultPort = 42069;

    private GameObject _panelRoot;
    private GameObject _connectedRoot;
    private TMP_InputField _addressField;
    private TMP_InputField _portField;
    private TMP_Text _statusText;
    private TMP_Text _connectedStatusText;
    private bool _subscribed;
    private bool _menuDriven;
    private bool _forceHidden;

    public static NetworkConnectionUI EnsureExists()
    {
        if (Instance != null)
            return Instance;
        return RuntimeSingleton.Ensure<NetworkConnectionUI>("NetworkConnectionUI");
    }

    private static void EnsureWorldSystems()
    {
        HubBootstrap.EnsureExists();
        EnemySpawner.EnsureExists();
        BossSpawner.EnsureExists();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SetStatus("Disconnected");
        ShowConnectionPanel(true);
    }

    public void SetMenuDrivenMode(bool menuDriven)
    {
        _menuDriven = menuDriven;
        if (menuDriven)
            HideConnectionPanels();
        else
            RefreshVisibilityFromNetworkState();
    }

    public void HideConnectionPanels()
    {
        _forceHidden = true;
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        if (_connectedRoot != null)
            _connectedRoot.SetActive(false);

        // Stop this entire canvas from eating clicks meant for class select / inventory.
        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
            canvas.enabled = false;

        GraphicRaycaster raycaster = GetComponentInChildren<GraphicRaycaster>(true);
        if (raycaster != null)
            raycaster.enabled = false;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!_subscribed)
            TrySubscribe();

        RefreshVisibilityFromNetworkState();
    }

    public void HideForDedicatedServer()
    {
        _menuDriven = true;
        HideConnectionPanels();
    }

    private void TrySubscribe()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager nm = NetworkManager.Singleton;

        NetworkConfigNormalizer.Apply(nm);

        nm.OnClientConnectedCallback += HandleClientConnected;
        nm.OnClientDisconnectCallback += HandleClientDisconnected;
        nm.OnServerStarted += HandleServerStarted;
        nm.OnTransportFailure += HandleTransportFailure;
        _subscribed = true;

        DisableDefaultNetworkHud();
        RefreshVisibilityFromNetworkState();
    }

    private void Unsubscribe()
    {
        if (!_subscribed || NetworkManager.Singleton == null)
            return;

        NetworkManager nm = NetworkManager.Singleton;
        nm.OnClientConnectedCallback -= HandleClientConnected;
        nm.OnClientDisconnectCallback -= HandleClientDisconnected;
        nm.OnServerStarted -= HandleServerStarted;
        nm.OnTransportFailure -= HandleTransportFailure;
        _subscribed = false;
    }

    private static void DisableDefaultNetworkHud()
    {

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName == "NetworkManagerHUD" || typeName == "UnityTransportHUD")
                behaviour.enabled = false;
        }
    }

    private void RefreshVisibilityFromNetworkState()
    {
        if (_menuDriven || _forceHidden)
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
            if (_connectedRoot != null)
                _connectedRoot.SetActive(false);
            return;
        }

        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null)
        {
            ShowConnectionPanel(true);
            SetStatus("Waiting for NetworkManager...");
            return;
        }

        bool active = nm.IsServer || nm.IsClient;
        ShowConnectionPanel(!active);

        if (!active)
            return;

        if (nm.IsHost)
            SetConnectedStatus("Connected as Host");
        else if (nm.IsServer)
            SetConnectedStatus("Running as Dedicated Server");
        else if (nm.IsClient)
            SetConnectedStatus($"Connected as Client (id {nm.LocalClientId})");
    }

    private void ShowConnectionPanel(bool showConnect)
    {
        if (_menuDriven || _forceHidden)
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
            if (_connectedRoot != null)
                _connectedRoot.SetActive(false);
            return;
        }

        if (_panelRoot != null)
            _panelRoot.SetActive(showConnect);
        if (_connectedRoot != null)
            _connectedRoot.SetActive(!showConnect);
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
            _statusText.text = message;
        Debug.Log($"[NetworkUI] {message}");
    }

    private void SetConnectedStatus(string message)
    {
        if (_connectedStatusText != null)
            _connectedStatusText.text = message;
    }

    private void OnClickHost()
    {
        if (!TryPrepareTransport(listen: true))
            return;

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            SetStatus("Already started.");
            return;
        }

        SetStatus("Starting Host...");
        bool ok = NetworkManager.Singleton.StartHost();
        SetStatus(ok ? "Host starting..." : "StartHost failed.");
        if (ok)
            EnsureWorldSystems();
    }

    private void OnClickServer()
    {
        if (!TryPrepareTransport(listen: true))
            return;

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            SetStatus("Already started.");
            return;
        }

        SetStatus("Starting Dedicated Server...");
        bool ok = NetworkManager.Singleton.StartServer();
        SetStatus(ok ? "Server starting..." : "StartServer failed.");
        if (ok)
            EnsureWorldSystems();
    }

    private void OnClickClient()
    {
        if (!TryPrepareTransport(listen: false))
            return;

        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            SetStatus("Already started.");
            return;
        }

        SetStatus($"Connecting to {ReadAddress()}:{ReadPort()}...");
        bool ok = NetworkManager.Singleton.StartClient();
        SetStatus(ok ? "Client connecting..." : "StartClient failed.");
    }

    private void OnClickDisconnect()
    {
        GameOptionsUI.DisconnectToMainMenu();
    }

    private bool TryPrepareTransport(bool listen)
    {
        if (NetworkManager.Singleton == null)
        {
            SetStatus("NetworkManager missing in scene.");
            return false;
        }

        NetworkManager nm = NetworkManager.Singleton;

        NetworkConfigNormalizer.Apply(nm);

        UnityTransport transport = nm.GetComponent<UnityTransport>();
        if (transport == null)
        {
            SetStatus("UnityTransport missing on NetworkManager.");
            return false;
        }

        string address = ReadAddress();
        ushort port = ReadPort();

        if (listen)
        {

            transport.SetConnectionData(address, port, "0.0.0.0");
        }
        else
        {
            transport.SetConnectionData(address, port);
        }

        ulong hash = nm.NetworkConfig.GetConfig(cache: false);
        Debug.Log($"[NetworkUI] Pre-start ConfigHash={hash} (host and client must match)");

        return true;
    }

    private string ReadAddress()
    {
        if (_addressField == null || string.IsNullOrWhiteSpace(_addressField.text))
            return defaultAddress;
        return _addressField.text.Trim();
    }

    private ushort ReadPort()
    {
        if (_portField == null || !ushort.TryParse(_portField.text, out ushort port))
            return defaultPort;
        return port;
    }

    private void HandleClientConnected(ulong clientId)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && nm.IsClient && clientId == nm.LocalClientId)
            SetStatus($"Client connected (id {clientId})");
        else if (nm != null && nm.IsServer)
            SetStatus($"Client {clientId} joined");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null)
        {
            SetStatus("Disconnected");
            ShowConnectionPanel(true);
            return;
        }

        if (!nm.IsServer && !nm.IsClient)
        {
            SetStatus($"Disconnected (client {clientId})");
            ShowConnectionPanel(true);
        }
        else if (nm.IsServer)
        {
            SetStatus($"Client {clientId} left");
        }
    }

    private void HandleServerStarted()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && nm.IsHost)
            SetStatus("Host ready");
        else
            SetStatus("Dedicated server ready");
    }

    private void HandleTransportFailure()
    {
        SetStatus("Transport failure");
        ShowConnectionPanel(true);
    }

    #region UI Build

    private void BuildUI()
    {
        UIEventSystem.Ensure();

        GameObject canvasGo = UiFactory.CreateOverlayCanvas(transform, "ConnectionCanvas", 200);

        _panelRoot = UiFactory.CreatePanel("ConnectPanel", canvasGo.transform,
            UiFactory.Center, UiFactory.Center,
            Vector2.zero, new Vector2(460f, 360f),
            new Color(0f, 0f, 0f, 0.82f));

        UiFactory.CreateLabel("Title", _panelRoot.transform, "Multiplayer", 28f, new Vector2(0f, 140f), new Vector2(400f, 40f));

        UiFactory.CreateLabel("AddressLabel", _panelRoot.transform, "Address", 16f, new Vector2(0f, 90f), new Vector2(400f, 24f));
        _addressField = UiFactory.CreateInputField("AddressInput", _panelRoot.transform, defaultAddress, new Vector2(0f, 55f), new Vector2(360f, 36f));

        UiFactory.CreateLabel("PortLabel", _panelRoot.transform, "Port", 16f, new Vector2(0f, 10f), new Vector2(400f, 24f));
        _portField = UiFactory.CreateInputField("PortInput", _panelRoot.transform, defaultPort.ToString(), new Vector2(0f, -25f), new Vector2(360f, 36f));

        UiFactory.CreateButton("HostButton", _panelRoot.transform, "Host", new Vector2(-120f, -90f), new Vector2(110f, 40f), OnClickHost);
        UiFactory.CreateButton("ServerButton", _panelRoot.transform, "Server", new Vector2(0f, -90f), new Vector2(110f, 40f), OnClickServer);
        UiFactory.CreateButton("ClientButton", _panelRoot.transform, "Client", new Vector2(120f, -90f), new Vector2(110f, 40f), OnClickClient);

        _statusText = UiFactory.CreateLabel("Status", _panelRoot.transform, "Disconnected", 16f, new Vector2(0f, -145f), new Vector2(420f, 28f));

        _connectedRoot = UiFactory.CreatePanel("ConnectedPanel", canvasGo.transform,
            Vector2.one, Vector2.one,
            new Vector2(-20f, -20f), new Vector2(320f, 90f),
            new Color(0f, 0f, 0f, 0.7f),
            new Vector2(1f, 1f));

        _connectedStatusText = UiFactory.CreateLabel("ConnectedStatus", _connectedRoot.transform, "Connected", 16f, new Vector2(0f, 18f), new Vector2(280f, 28f));
        UiFactory.CreateButton("DisconnectButton", _connectedRoot.transform, "Disconnect", new Vector2(0f, -20f), new Vector2(140f, 36f), OnClickDisconnect);

        _connectedRoot.SetActive(false);
    }

    #endregion
}

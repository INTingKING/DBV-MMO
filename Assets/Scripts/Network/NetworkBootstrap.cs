using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NetworkBootstrap
{
    private const string GameSceneName = "SampleScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.name != GameSceneName && active.buildIndex != 1)
            return;

        NetworkConnectionUI ui = NetworkConnectionUI.EnsureExists();

        if (ShouldStartDedicatedServer(out ushort port, out string bindAddress))
        {
            Debug.Log($"[NetworkBootstrap] Dedicated server mode. bind={bindAddress} port={port}");
            ui.HideForDedicatedServer();

            var runner = new GameObject("DedicatedServerStarter").AddComponent<DedicatedServerStarter>();
            runner.Begin(port, bindAddress);
        }
    }

    private static bool ShouldStartDedicatedServer(out ushort port, out string bindAddress)
    {
        port = 42069;
        bindAddress = "0.0.0.0";

        string[] args = Environment.GetCommandLineArgs();
        bool server = Application.isBatchMode;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "-server", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-dedicatedServer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-dedicated", StringComparison.OrdinalIgnoreCase))
            {
                server = true;
            }
            else if (string.Equals(arg, "-port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (ushort.TryParse(args[i + 1], out ushort parsed))
                    port = parsed;
            }
            else if (string.Equals(arg, "-bind", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                bindAddress = args[i + 1];
            }
        }

        return server;
    }

    private sealed class DedicatedServerStarter : MonoBehaviour
    {
        private ushort _port;
        private string _bind;
        private int _frames;

        public void Begin(ushort port, string bind)
        {
            _port = port;
            _bind = bind;
            _frames = 0;
        }

        private void Update()
        {
            _frames++;
            if (_frames < 2)
                return;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                if (_frames > 120)
                {
                    Debug.LogError("[NetworkBootstrap] NetworkManager never appeared.");
                    Destroy(gameObject);
                }
                return;
            }

            if (nm.IsServer || nm.IsClient)
            {
                Destroy(gameObject);
                return;
            }

            NetworkConfigNormalizer.Apply(nm);

            UnityTransport transport = nm.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData("127.0.0.1", _port, _bind);

            bool ok = nm.StartServer();
            Debug.Log(ok
                ? $"[NetworkBootstrap] StartServer OK on port {_port}"
                : "[NetworkBootstrap] StartServer FAILED");
            Destroy(gameObject);
        }
    }
}

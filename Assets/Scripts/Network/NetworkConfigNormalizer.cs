using System.Reflection;
using Unity.Netcode;
using UnityEngine;

public static class NetworkConfigNormalizer
{

    public const ushort ProtocolVersion = 0;
    public const uint TickRate = 30;
    public const bool ConnectionApproval = false;
    public const bool ForceSamePrefabs = false;
    // Must be false: MainMenu loads SampleScene with SceneManager.LoadScene, then StartHost/Client.
    // NGO scene management fights that path and breaks host play from the menu.
    public const bool EnableSceneManagement = false;
    public const bool EnsureNetworkVariableLengthSafety = false;
    public const HashSize RpcHashSize = HashSize.VarIntFourBytes;

    public static void Apply(NetworkManager nm)
    {
        if (nm == null || nm.NetworkConfig == null)
            return;

        NetworkConfig cfg = nm.NetworkConfig;

        cfg.ProtocolVersion = ProtocolVersion;
        cfg.TickRate = TickRate;
        cfg.ConnectionApproval = ConnectionApproval;
        cfg.ForceSamePrefabs = ForceSamePrefabs;
        cfg.EnableSceneManagement = EnableSceneManagement;
        cfg.EnsureNetworkVariableLengthSafety = EnsureNetworkVariableLengthSafety;
        cfg.RpcHashSize = RpcHashSize;

        ClearCachedConfigHash(cfg);

        ulong hash = cfg.GetConfig(cache: false);
        ClearCachedConfigHash(cfg);

        Debug.Log(
            "[NetworkConfig] Normalized " +
            $"ForceSamePrefabs={cfg.ForceSamePrefabs}, " +
            $"TickRate={cfg.TickRate}, " +
            $"ConnectionApproval={cfg.ConnectionApproval}, " +
            $"EnableSceneManagement={cfg.EnableSceneManagement}, " +
            $"EnsureNetworkVariableLengthSafety={cfg.EnsureNetworkVariableLengthSafety}, " +
            $"RpcHashSize={cfg.RpcHashSize}, " +
            $"ProtocolVersion={cfg.ProtocolVersion}, " +
            $"ConfigHash={hash}");
    }

    private static void ClearCachedConfigHash(NetworkConfig cfg)
    {

        MethodInfo clearMethod = typeof(NetworkConfig).GetMethod(
            "ClearConfigHash",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (clearMethod != null)
        {
            clearMethod.Invoke(cfg, null);
            return;
        }

        FieldInfo cacheField = typeof(NetworkConfig).GetField(
            "m_ConfigHash",
            BindingFlags.Instance | BindingFlags.NonPublic);

        cacheField?.SetValue(cfg, null);
    }
}

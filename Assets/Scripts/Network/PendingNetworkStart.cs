using UnityEngine;

public enum PendingNetworkMode
{
    None = 0,
    Host = 1,
    Client = 2,
    Server = 3
}

public static class PendingNetworkStart
{
    private const string PrefMode = "dbv.pending.net.mode";
    private const string PrefAddress = "dbv.pending.net.address";
    private const string PrefPort = "dbv.pending.net.port";
    private const string PrefError = "dbv.pending.net.error";

    public static PendingNetworkMode Mode = PendingNetworkMode.None;
    public static string Address = "127.0.0.1";
    public static ushort Port = 42069;
    public static string LastError = "";

    public static bool HasPending => Mode != PendingNetworkMode.None || PlayerPrefs.GetInt(PrefMode, 0) != 0;

    public static void Set(PendingNetworkMode mode, string address, ushort port)
    {
        Mode = mode;
        Address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        Port = port == 0 ? (ushort)42069 : port;
        LastError = "";

        // PlayerPrefs is the real handoff channel MainMenu → SampleScene.
        PlayerPrefs.SetInt(PrefMode, (int)mode);
        PlayerPrefs.SetString(PrefAddress, Address);
        PlayerPrefs.SetInt(PrefPort, Port);
        PlayerPrefs.DeleteKey(PrefError);
        PlayerPrefs.Save();

        Debug.Log($"[PendingNetwork] SET mode={Mode} {Address}:{Port} (prefs saved)");
    }

    public static void LoadFromPrefsIfNeeded()
    {
        int modeInt = PlayerPrefs.GetInt(PrefMode, 0);

        // Prefer prefs when present so scene load always sees the menu choice.
        if (modeInt != 0)
        {
            Mode = (PendingNetworkMode)modeInt;
            Address = PlayerPrefs.GetString(PrefAddress, "127.0.0.1");
            int port = PlayerPrefs.GetInt(PrefPort, 42069);
            Port = (ushort)Mathf.Clamp(port, 1, 65535);
            Debug.Log($"[PendingNetwork] LOADED prefs mode={Mode} {Address}:{Port}");
            return;
        }

        if (Mode != PendingNetworkMode.None)
        {
            Debug.Log($"[PendingNetwork] Using static mode={Mode} {Address}:{Port}");
            return;
        }

        Debug.Log("[PendingNetwork] No pending mode in prefs or statics");
    }

    public static void Clear()
    {
        Mode = PendingNetworkMode.None;
        PlayerPrefs.DeleteKey(PrefMode);
        PlayerPrefs.DeleteKey(PrefAddress);
        PlayerPrefs.DeleteKey(PrefPort);
        PlayerPrefs.Save();
    }

    public static void SetError(string message)
    {
        LastError = message ?? "";
        Clear();
        if (!string.IsNullOrEmpty(LastError))
        {
            PlayerPrefs.SetString(PrefError, LastError);
            PlayerPrefs.Save();
        }
    }

    public static string ConsumeError()
    {
        string err = LastError;
        if (string.IsNullOrEmpty(err))
            err = PlayerPrefs.GetString(PrefError, "");

        LastError = "";
        PlayerPrefs.DeleteKey(PrefError);
        PlayerPrefs.Save();
        return err;
    }
}

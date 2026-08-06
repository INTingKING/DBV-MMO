using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Full soft-reset when returning to / opening the main menu.
/// Wipes DontDestroyOnLoad gameplay objects and session statics.
/// </summary>
public static class SessionReset
{
    public static void ResetForMainMenu(bool keepPendingError = true)
    {
        string savedError = keepPendingError ? PendingNetworkStart.LastError : "";
        if (keepPendingError && string.IsNullOrEmpty(savedError))
            savedError = PlayerPrefs.GetString("dbv.pending.net.error", "");

        // Stop networking first.
        if (NetworkManager.Singleton != null)
        {
            try
            {
                if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
                    NetworkManager.Singleton.Shutdown();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SessionReset] Network shutdown: {ex.Message}");
            }
        }

        // Nuke every DontDestroyOnLoad root we own (clean slate).
        DestroyAllDontDestroyOnLoadObjects();

        // Clear session statics / prefs (except optional disconnect error).
        PendingNetworkStart.Clear();
        if (keepPendingError && !string.IsNullOrEmpty(savedError))
            PendingNetworkStart.SetError(savedError);

        // Known statics that may linger after Destroy (Unity delays destroy to end of frame).
        ClearKnownStaticInstances();

        // Fresh EventSystem for the menu scene.
        UIEventSystem.EnsureForMainMenu();

        Debug.Log("[SessionReset] Main menu session reset complete.");
    }

    public static void ReturnToMainMenuFresh(string error = null)
    {
        if (!string.IsNullOrEmpty(error))
            PendingNetworkStart.SetError(error);
        else
            PendingNetworkStart.Clear();

        ResetForMainMenu(keepPendingError: true);
        SceneManager.LoadScene(NetworkBootstrap.MainMenuSceneName);
    }

    private static void ClearKnownStaticInstances()
    {
        GameOptionsUI.ForceClosed();

        // Hub must rebuild interactables (E prompts) on the next host.
        HubBootstrap.ResetSession();
        WorldInteractable.ClearRegistry();
    }

    private static void DestroyAllDontDestroyOnLoadObjects()
    {
        // Probe the DontDestroyOnLoad scene, then destroy every root in it.
        GameObject probe = new GameObject("__SessionResetProbe");
        Object.DontDestroyOnLoad(probe);
        Scene ddol = probe.scene;

        GameObject[] roots = ddol.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root == probe)
                continue;

            // Never destroy NetworkManager if somehow still here mid-shutdown — already shut down.
            Object.Destroy(root);
        }

        Object.Destroy(probe);
    }
}

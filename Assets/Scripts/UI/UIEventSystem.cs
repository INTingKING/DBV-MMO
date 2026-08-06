using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class UIEventSystem
{
    private static EventSystem _persistent;

    public static void Ensure()
    {
        EventSystem es = FindBestEventSystem();
        if (es == null)
        {
            GameObject go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        EnsureInputModule(es.gameObject);
        es.enabled = true;
        es.gameObject.SetActive(true);

        // Keep one EventSystem; destroy other copies.
        CleanupDuplicates(es);
        _persistent = es;
    }

    /// <summary>
    /// Main menu: prefer the scene EventSystem (serialized Input System actions).
    /// Destroy leftover DontDestroyOnLoad EventSystems from gameplay.
    /// </summary>
    public static void EnsureForMainMenu()
    {
        EventSystem sceneEs = null;
        EventSystem[] all = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            EventSystem es = all[i];
            if (es == null)
                continue;

            if (IsDontDestroyOnLoad(es.gameObject))
            {
                Object.Destroy(es.gameObject);
                continue;
            }

            if (sceneEs == null)
                sceneEs = es;
            else
                Object.Destroy(es.gameObject);
        }

        _persistent = null;

        if (sceneEs == null)
        {
            GameObject go = new GameObject("EventSystem");
            sceneEs = go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        EnsureInputModule(sceneEs.gameObject);
        sceneEs.enabled = true;
        sceneEs.gameObject.SetActive(true);
        _persistent = sceneEs;
    }

    private static EventSystem FindBestEventSystem()
    {
        if (_persistent != null)
            return _persistent;

        if (EventSystem.current != null)
            return EventSystem.current;

        return Object.FindFirstObjectByType<EventSystem>();
    }

    private static bool IsDontDestroyOnLoad(GameObject go)
    {
        return go != null && go.scene.IsValid() && go.scene.name == "DontDestroyOnLoad";
    }

    private static void EnsureInputModule(GameObject go)
    {
        if (go == null)
            return;

        InputSystemUIInputModule inputModule = go.GetComponent<InputSystemUIInputModule>();
        StandaloneInputModule standalone = go.GetComponent<StandaloneInputModule>();

        if (inputModule == null && standalone == null)
            inputModule = go.AddComponent<InputSystemUIInputModule>();

        if (inputModule != null)
            inputModule.enabled = true;

        if (standalone != null)
            standalone.enabled = inputModule == null;
    }

    private static void CleanupDuplicates(EventSystem keep)
    {
        EventSystem[] all = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i] == keep)
                continue;
            Object.Destroy(all[i].gameObject);
        }
    }
}

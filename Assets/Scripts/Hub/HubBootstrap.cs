using UnityEngine;

public class HubBootstrap : MonoBehaviour
{
    private static bool _built;
    private bool _hubSpawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _built = false;
    }

    /// <summary>Call when returning to main menu so the next host rebuilds the hub.</summary>
    public static void ResetSession()
    {
        _built = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!NetworkBootstrap.IsGameSceneLoaded())
            return;

        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (!NetworkBootstrap.IsGameSceneLoaded())
            return;

        // If the hub was destroyed with the last SampleScene unload, allow rebuild.
        HubBootstrap existing = FindFirstObjectByType<HubBootstrap>();
        if (existing != null)
        {
            _built = true;
            return;
        }

        _built = false;

        GameObject root = new GameObject("HubBootstrap");
        root.AddComponent<HubBootstrap>();
        _built = true;
        Debug.Log("[HubBootstrap] Created hub for this session.");
    }

    private void Start()
    {
        if (_hubSpawned)
            return;

        BuildHub();
        _hubSpawned = true;
    }

    private void OnDestroy()
    {
        // Scene unload / disconnect — next EnsureExists must rebuild.
        _built = false;
    }

    private void BuildHub()
    {
        // Avoid double hub if Start runs twice.
        if (GameObject.Find("GatheringHub") != null)
            return;

        GameObject hubRoot = new GameObject("GatheringHub");
        hubRoot.AddComponent<HubArea>();

        GameObject fountainGo = new GameObject("Fountain");
        fountainGo.transform.SetParent(hubRoot.transform, false);
        FountainInteractable fountain = fountainGo.AddComponent<FountainInteractable>();
        fountain.Setup(Vector3.zero);

        GameObject boardGo = new GameObject("NoticeBoard");
        boardGo.transform.SetParent(hubRoot.transform, false);
        NoticeBoardInteractable board = boardGo.AddComponent<NoticeBoardInteractable>();
        board.Setup(Vector3.zero);

        GameObject questNpcGo = new GameObject("QuestNpc");
        questNpcGo.transform.SetParent(hubRoot.transform, false);
        QuestNpcInteractable questNpc = questNpcGo.AddComponent<QuestNpcInteractable>();
        questNpc.Setup();

        Debug.Log("[HubBootstrap] Hub interactables spawned (fountain, board, quest NPC).");
    }
}

using UnityEngine;

public class HubBootstrap : MonoBehaviour
{
    private bool _hubSpawned;

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

        if (FindFirstObjectByType<HubBootstrap>() != null)
            return;

        GameObject root = new GameObject("HubBootstrap");
        root.AddComponent<HubBootstrap>();
        Debug.Log("[HubBootstrap] Created hub for this session.");
    }

    private void Start()
    {
        if (_hubSpawned)
            return;

        BuildHub();
        _hubSpawned = true;
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

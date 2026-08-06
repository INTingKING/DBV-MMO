using UnityEngine;

public class HubBootstrap : MonoBehaviour
{
    private static bool _built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _built = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_built)
            return;

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "SampleScene" && scene.buildIndex != 1)
            return;

        if (FindFirstObjectByType<HubBootstrap>() != null)
        {
            _built = true;
            return;
        }

        _built = true;
        GameObject root = new GameObject("HubBootstrap");
        root.AddComponent<HubBootstrap>();
    }

    private void Start()
    {
        BuildHub();
    }

    private void BuildHub()
    {
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
    }
}

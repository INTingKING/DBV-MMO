using TMPro;
using UnityEngine;

public class NpcDialogueUI : MonoBehaviour
{
    public static NpcDialogueUI Instance { get; private set; }

    private PlayerQuest _quest;
    private GameObject _root;
    private TMP_Text _title;
    private TMP_Text _body;
    private TMP_Text _primaryLabel;
    private bool _canAccept;
    private bool _canTurnIn;

    public static NpcDialogueUI EnsureExists()
    {
        if (Instance != null)
            return Instance;
        return RuntimeSingleton.Ensure<NpcDialogueUI>("NpcDialogueUI");
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUI();
        Close();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Open(PlayerQuest quest)
    {
        _quest = quest;
        if (_quest == null)
            return;

        Refresh();
        if (_root != null)
            _root.SetActive(true);
    }

    public void Close()
    {
        if (_root != null)
            _root.SetActive(false);
        _quest = null;
    }

    private void Refresh()
    {
        if (_quest == null)
            return;

        _quest.GetDialogue(out string body, out string primary, out _canAccept, out _canTurnIn);
        if (_title != null)
            _title.text = PlayerQuest.NpcName;
        if (_body != null)
            _body.text = body;
        if (_primaryLabel != null)
            _primaryLabel.text = primary;
    }

    private void OnPrimary()
    {
        if (_quest == null)
            return;

        if (_canAccept)
        {
            _quest.AcceptQuestServerRpc();

            StartCoroutine(RefreshAfterDelay(0.25f, close: false));
            return;
        }

        if (_canTurnIn)
        {
            _quest.TurnInQuestServerRpc();
            StartCoroutine(RefreshAfterDelay(0.25f, close: true));
            return;
        }

        Close();
    }

    private System.Collections.IEnumerator RefreshAfterDelay(float delay, bool close)
    {
        yield return new WaitForSeconds(delay);
        if (_quest == null)
            yield break;
        Refresh();
        if (close)
            Close();
    }

    private void BuildUI()
    {
        UIEventSystem.Ensure();

        GameObject canvasGo = UiFactory.CreateOverlayCanvas(transform, "NpcDialogueCanvas", 170);

        _root = UiFactory.CreatePanel("DialoguePanel", canvasGo.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 180f), new Vector2(640f, 260f),
            new Color(0.05f, 0.06f, 0.1f, 0.94f));

        _title = UiFactory.CreateLabel("Title", _root.transform, PlayerQuest.NpcName, 26f, new Vector2(0f, 95f), new Vector2(600f, 36f));
        _body = UiFactory.CreateLabel("Body", _root.transform, "", 18f, new Vector2(0f, 10f), new Vector2(600f, 140f), TextAlignmentOptions.TopLeft);

        UiFactory.CreateButton("PrimaryBtn", _root.transform, "Accept", new Vector2(-90f, -95f), new Vector2(180f, 40f), OnPrimary, out _primaryLabel);
        UiFactory.CreateButton("CloseBtn", _root.transform, "Close", new Vector2(110f, -95f), new Vector2(140f, 40f), Close);
    }
}

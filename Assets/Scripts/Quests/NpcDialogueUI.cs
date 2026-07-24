using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        NpcDialogueUI existing = FindFirstObjectByType<NpcDialogueUI>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("NpcDialogueUI");
        return go.AddComponent<NpcDialogueUI>();
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
        EnsureEventSystem();

        GameObject canvasGo = new GameObject("NpcDialogueCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 170;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        _root = CreatePanel("DialoguePanel", canvasGo.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 180f), new Vector2(640f, 260f),
            new Color(0.05f, 0.06f, 0.1f, 0.94f));

        _title = CreateLabel("Title", _root.transform, PlayerQuest.NpcName, 26f, new Vector2(0f, 95f), new Vector2(600f, 36f));
        _body = CreateLabel("Body", _root.transform, "", 18f, new Vector2(0f, 10f), new Vector2(600f, 140f));
        _body.alignment = TextAlignmentOptions.TopLeft;

        CreateButton("PrimaryBtn", _root.transform, "Accept", new Vector2(-90f, -95f), new Vector2(180f, 40f), OnPrimary, out _primaryLabel);
        CreateButton("CloseBtn", _root.transform, "Close", new Vector2(110f, -95f), new Vector2(140f, 40f), Close, out _);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static TMP_Text CreateLabel(string name, Transform parent, string text, float size, Vector2 pos, Vector2 dim)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = dim;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static void CreateButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick, out TMP_Text labelText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.45f, 0.8f, 1f);
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        labelText = tmp;
    }
}

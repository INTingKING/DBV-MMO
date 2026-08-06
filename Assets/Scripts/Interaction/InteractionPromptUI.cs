using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InteractionPromptUI : MonoBehaviour
{
    public static InteractionPromptUI Instance { get; private set; }

    private GameObject _promptRoot;
    private TMP_Text _promptText;
    private GameObject _boardRoot;
    private TMP_Text _boardBody;

    public static InteractionPromptUI EnsureExists()
    {
        // Unity fake-null: destroyed Instance must not be reused after disconnect.
        if (Instance != null)
            return Instance;

        InteractionPromptUI existing = FindFirstObjectByType<InteractionPromptUI>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("InteractionPromptUI");
        return go.AddComponent<InteractionPromptUI>();
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
        SetPrompt(null);
        HideNoticeBoard();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetPrompt(string text)
    {
        if (_promptRoot == null || _promptText == null)
            return;

        bool show = !string.IsNullOrEmpty(text);
        _promptRoot.SetActive(show);
        if (show)
            _promptText.text = text;
    }

    public void ShowNoticeBoard(string body)
    {
        if (_boardRoot == null || _boardBody == null)
            return;

        _boardBody.text = body;
        _boardRoot.SetActive(true);
    }

    public void HideNoticeBoard()
    {
        if (_boardRoot != null)
            _boardRoot.SetActive(false);
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        GameObject canvasGo = new GameObject("InteractionCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        _promptRoot = CreatePanel("Prompt", canvasGo.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 120f), new Vector2(420f, 36f),
            new Color(0f, 0f, 0f, 0.65f));
        _promptText = CreateLabel("PromptText", _promptRoot.transform, "[E] Interact", 18f, Vector2.zero, new Vector2(400f, 28f));

        _boardRoot = CreatePanel("NoticeBoard", canvasGo.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(520f, 340f),
            new Color(0.05f, 0.05f, 0.08f, 0.92f));

        CreateLabel("BoardTitle", _boardRoot.transform, "Notice Board", 26f, new Vector2(0f, 130f), new Vector2(480f, 36f));
        _boardBody = CreateLabel("BoardBody", _boardRoot.transform, "", 18f, new Vector2(0f, 10f), new Vector2(460f, 200f));
        _boardBody.alignment = TextAlignmentOptions.TopLeft;

        CreateButton("CloseBoard", _boardRoot.transform, "Close", new Vector2(0f, -130f), new Vector2(140f, 40f), HideNoticeBoard);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
        return go;
    }

    private static TMP_Text CreateLabel(string name, Transform parent, string text, float fontSize, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static void CreateButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.25f, 0.45f, 0.8f, 1f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
    }
}

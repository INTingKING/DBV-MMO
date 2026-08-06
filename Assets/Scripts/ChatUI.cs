using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ChatUI : MonoBehaviour
{
    public static ChatUI Instance { get; private set; }

    public bool IsOpen { get; private set; }

    public event Action<string> OnMessageSubmit;

    [SerializeField] private int maxVisibleLines = 10;
    [SerializeField] private int maxMessageLength = 120;

    private readonly List<string> _lines = new List<string>();

    private GameObject _root;
    private GameObject _inputRow;
    private TMP_Text _logText;
    private TMP_InputField _inputField;

    public static ChatUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        ChatUI existing = FindFirstObjectByType<ChatUI>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("ChatUI");
        return go.AddComponent<ChatUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        CloseChat(clearDraft: true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseChat(clearDraft: true);
            return;
        }

        if (GameOptionsUI.IsOpen)
            return;

        if (!IsOpen &&
            (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            OpenChat();
        }
    }

    public void AddMessage(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        _lines.Add(line.Trim());
        while (_lines.Count > maxVisibleLines)
            _lines.RemoveAt(0);

        RefreshLog();
    }

    public void OpenChat()
    {
        IsOpen = true;
        if (_inputRow != null)
            _inputRow.SetActive(true);

        if (_inputField != null)
        {
            _inputField.text = string.Empty;
            _inputField.ActivateInputField();
            _inputField.Select();
        }
    }

    public void CloseChat(bool clearDraft)
    {
        IsOpen = false;

        if (_inputField != null)
        {
            if (clearDraft)
                _inputField.text = string.Empty;
            _inputField.DeactivateInputField();
        }

        if (_inputRow != null)
            _inputRow.SetActive(false);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void TrySubmit(string _)
    {
        if (_inputField == null)
            return;

        string message = _inputField.text != null ? _inputField.text.Trim() : string.Empty;
        if (message.Length == 0)
        {
            CloseChat(clearDraft: true);
            return;
        }

        if (message.Length > maxMessageLength)
            message = message.Substring(0, maxMessageLength);

        OnMessageSubmit?.Invoke(message);
        _inputField.text = string.Empty;

        StartCoroutine(RefocusInputNextFrame());
    }

    private System.Collections.IEnumerator RefocusInputNextFrame()
    {
        yield return null;
        if (IsOpen && _inputField != null)
        {
            _inputField.ActivateInputField();
            _inputField.Select();
        }
    }

    private void RefreshLog()
    {
        if (_logText == null)
            return;

        _logText.text = string.Join("\n", _lines);
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        _root = new GameObject("ChatCanvas", typeof(RectTransform));
        _root.transform.SetParent(transform, false);

        Canvas canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _root.AddComponent<GraphicRaycaster>();

        GameObject logPanel = CreatePanel("LogPanel", _root.transform,
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(20f, 70f), new Vector2(520f, 220f),
            new Color(0f, 0f, 0f, 0.45f));

        GameObject logTextGo = new GameObject("LogText", typeof(RectTransform));
        logTextGo.transform.SetParent(logPanel.transform, false);
        RectTransform logRt = logTextGo.GetComponent<RectTransform>();
        StretchFull(logRt, 8f);

        _logText = logTextGo.AddComponent<TextMeshProUGUI>();
        _logText.fontSize = 18f;
        _logText.color = Color.white;
        _logText.alignment = TextAlignmentOptions.BottomLeft;
        _logText.textWrappingMode = TextWrappingModes.Normal;
        _logText.raycastTarget = false;
        ApplyDefaultFont(_logText);

        _inputRow = CreatePanel("InputRow", _root.transform,
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(20f, 20f), new Vector2(520f, 42f),
            new Color(0f, 0f, 0f, 0.7f));

        GameObject inputGo = new GameObject("InputField", typeof(RectTransform));
        inputGo.transform.SetParent(_inputRow.transform, false);
        RectTransform inputRt = inputGo.GetComponent<RectTransform>();
        StretchFull(inputRt, 6f);

        Image inputBg = inputGo.AddComponent<Image>();
        inputBg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
        textArea.transform.SetParent(inputGo.transform, false);
        RectTransform textAreaRt = textArea.GetComponent<RectTransform>();
        StretchFull(textAreaRt, 6f);
        textArea.AddComponent<RectMask2D>();

        GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGo.transform.SetParent(textArea.transform, false);
        StretchFull(placeholderGo.GetComponent<RectTransform>(), 0f);
        TMP_Text placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
        placeholder.text = "Press Enter to chat...";
        placeholder.fontSize = 18f;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(1f, 1f, 1f, 0.4f);
        placeholder.raycastTarget = false;
        ApplyDefaultFont(placeholder);

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(textArea.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>(), 0f);
        TMP_Text inputText = textGo.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 18f;
        inputText.color = Color.white;
        inputText.raycastTarget = false;
        ApplyDefaultFont(inputText);

        _inputField = inputGo.AddComponent<TMP_InputField>();
        _inputField.textViewport = textAreaRt;
        _inputField.textComponent = inputText;
        _inputField.placeholder = placeholder;
        _inputField.characterLimit = maxMessageLength;
        _inputField.lineType = TMP_InputField.LineType.SingleLine;
        _inputField.onSubmit.AddListener(TrySubmit);

        GameObject hintGo = new GameObject("Hint", typeof(RectTransform));
        hintGo.transform.SetParent(_root.transform, false);
        RectTransform hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(0f, 0f);
        hintRt.pivot = new Vector2(0f, 0f);
        hintRt.anchoredPosition = new Vector2(20f, 24f);
        hintRt.sizeDelta = new Vector2(400f, 28f);

        TMP_Text hint = hintGo.AddComponent<TextMeshProUGUI>();
        hint.text = "Enter = Chat";
        hint.fontSize = 16f;
        hint.color = new Color(1f, 1f, 1f, 0.55f);
        hint.raycastTarget = false;
        ApplyDefaultFont(hint);

        _hintObject = hintGo;
    }

    private GameObject _hintObject;

    private void LateUpdate()
    {
        if (_hintObject != null)
            _hintObject.SetActive(!IsOpen);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPos,
        Vector2 size,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        image.color = color;
        return go;
    }

    private static void StretchFull(RectTransform rt, float padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    private static void ApplyDefaultFont(TMP_Text text)
    {
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
    }
}

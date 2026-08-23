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

    public static bool IsChatOpen => Instance != null && Instance.IsOpen;

    public bool IsOpen { get; private set; }

    public event Action<string> OnMessageSubmit;

    [SerializeField] private int maxVisibleLines = 10;
    [SerializeField] private int maxMessageLength = 120;

    private readonly List<string> _lines = new List<string>();

    private GameObject _root;
    private GameObject _inputRow;
    private GameObject _hintObject;
    private TMP_Text _logText;
    private TMP_InputField _inputField;

    public static ChatUI EnsureExists()
    {
        if (Instance != null)
            return Instance;
        return RuntimeSingleton.Ensure<ChatUI>("ChatUI");
    }

    public static void AddSystem(string message)
    {
        if (Instance == null || string.IsNullOrWhiteSpace(message))
            return;
        Instance.AddMessage("System: " + message.Trim());
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

    private void LateUpdate()
    {
        if (_hintObject != null)
            _hintObject.SetActive(!IsOpen);
    }

    private void BuildUI()
    {
        UIEventSystem.Ensure();

        _root = UiFactory.CreateOverlayCanvas(transform, "ChatCanvas", 100);

        GameObject logPanel = UiFactory.CreatePanel(
            "LogPanel", _root.transform,
            UiFactory.BottomLeft, UiFactory.BottomLeft,
            new Vector2(20f, 70f), new Vector2(520f, 220f),
            new Color(0f, 0f, 0f, 0.45f),
            UiFactory.BottomLeft);

        RectTransform logRt = UiFactory.CreateRect("LogText", logPanel.transform);
        UiFactory.Stretch(logRt, 8f);
        _logText = logRt.gameObject.AddComponent<TextMeshProUGUI>();
        _logText.fontSize = 18f;
        _logText.color = Color.white;
        _logText.alignment = TextAlignmentOptions.BottomLeft;
        _logText.textWrappingMode = TextWrappingModes.Normal;
        _logText.raycastTarget = false;
        UiFactory.ApplyDefaultFont(_logText);

        _inputRow = UiFactory.CreatePanel(
            "InputRow", _root.transform,
            UiFactory.BottomLeft, UiFactory.BottomLeft,
            new Vector2(20f, 20f), new Vector2(520f, 42f),
            new Color(0f, 0f, 0f, 0.7f),
            UiFactory.BottomLeft);

        RectTransform inputRt = UiFactory.CreateRect("InputField", _inputRow.transform);
        UiFactory.Stretch(inputRt, 6f);
        Image inputBg = inputRt.gameObject.AddComponent<Image>();
        inputBg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        RectTransform textAreaRt = UiFactory.CreateRect("Text Area", inputRt);
        UiFactory.Stretch(textAreaRt, 6f);
        textAreaRt.gameObject.AddComponent<RectMask2D>();

        RectTransform placeholderRt = UiFactory.CreateRect("Placeholder", textAreaRt);
        UiFactory.Stretch(placeholderRt);
        TMP_Text placeholder = placeholderRt.gameObject.AddComponent<TextMeshProUGUI>();
        placeholder.text = "Press Enter to chat...";
        placeholder.fontSize = 18f;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(1f, 1f, 1f, 0.4f);
        placeholder.raycastTarget = false;
        UiFactory.ApplyDefaultFont(placeholder);

        RectTransform textGoRt = UiFactory.CreateRect("Text", textAreaRt);
        UiFactory.Stretch(textGoRt);
        TMP_Text inputText = textGoRt.gameObject.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 18f;
        inputText.color = Color.white;
        inputText.raycastTarget = false;
        UiFactory.ApplyDefaultFont(inputText);

        _inputField = inputRt.gameObject.AddComponent<TMP_InputField>();
        _inputField.textViewport = textAreaRt;
        _inputField.textComponent = inputText;
        _inputField.placeholder = placeholder;
        _inputField.characterLimit = maxMessageLength;
        _inputField.lineType = TMP_InputField.LineType.SingleLine;
        _inputField.onSubmit.AddListener(TrySubmit);

        RectTransform hintRt = UiFactory.CreateRect("Hint", _root.transform);
        hintRt.anchorMin = UiFactory.BottomLeft;
        hintRt.anchorMax = UiFactory.BottomLeft;
        hintRt.pivot = UiFactory.BottomLeft;
        hintRt.anchoredPosition = new Vector2(20f, 24f);
        hintRt.sizeDelta = new Vector2(400f, 28f);

        TMP_Text hint = hintRt.gameObject.AddComponent<TextMeshProUGUI>();
        hint.text = "Enter = Chat";
        hint.fontSize = 16f;
        hint.color = new Color(1f, 1f, 1f, 0.55f);
        hint.raycastTarget = false;
        UiFactory.ApplyDefaultFont(hint);
        _hintObject = hintRt.gameObject;
    }
}

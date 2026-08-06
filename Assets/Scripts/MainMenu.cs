using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public const string GameSceneName = "SampleScene";

    [SerializeField] private string defaultAddress = "127.0.0.1";
    [SerializeField] private ushort defaultPort = 42069;

    private GameObject _mainPanel;
    private GameObject _settingsPanel;
    private GameObject _multiplayerPanel;
    private GameObject _creditsPanel;
    private TMP_InputField _addressField;
    private TMP_InputField _portField;
    private TMP_Text _statusText;
    private OptionsUI _optionsUi;
    private Transform _canvas;
    private Button _hostBtn;
    private Button _joinBtn;
    private Button _serverBtn;
    private string _pendingError;

    private void Awake()
    {
        // Always open main menu from a clean session (no leftover network/UI).
        // Preserve disconnect error text if any.
        _pendingError = PendingNetworkStart.ConsumeError();
        SessionReset.ResetForMainMenu(keepPendingError: false);

        GameSettings.Load();
        GameSettings.Apply(save: false);
        UIEventSystem.EnsureForMainMenu();

        ResolvePanels();
        // Remove runtime panels from a previous MainMenu instance if the scene was additive (shouldn't be).
        DestroyRuntimePanelsIfAny();

        BuildMultiplayerPanel();
        BuildCreditsPanel();
        InjectSettingsContent();
        WireExistingButtons();

        if (_multiplayerPanel != null)
            _multiplayerPanel.SetActive(false);
        if (_creditsPanel != null)
            _creditsPanel.SetActive(false);
        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);

        if (!string.IsNullOrEmpty(_pendingError))
        {
            ShowMultiplayer();
            SetStatus(_pendingError);
        }
        else
        {
            ShowMain();
        }
    }

    private void Start()
    {
        UIEventSystem.EnsureForMainMenu();
        WireExistingButtons();

        // Ensure default main view after everything woke up.
        if (string.IsNullOrEmpty(_pendingError))
            ShowMain();
        else
            ShowMultiplayer();
    }

    private void DestroyRuntimePanelsIfAny()
    {
        if (_canvas == null)
            return;

        Transform mp = _canvas.Find("Multiplayer Panel");
        if (mp != null)
            Destroy(mp.gameObject);

        Transform cr = _canvas.Find("Credits Panel");
        if (cr != null)
            Destroy(cr.gameObject);

        Transform rs = null;
        if (_settingsPanel != null)
            rs = _settingsPanel.transform.Find("RuntimeSettings");
        if (rs != null)
            Destroy(rs.gameObject);

        Transform sb = null;
        if (_settingsPanel != null)
            sb = _settingsPanel.transform.Find("SettingsBack");
        if (sb != null)
            Destroy(sb.gameObject);
    }

    public void LoadGame()
    {
        ShowMultiplayer();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ShowMain()
    {
        SetOnly(_mainPanel);
        EnableConnectButtons(true);
    }

    public void ShowMultiplayer()
    {
        SetOnly(_multiplayerPanel);
        EnableConnectButtons(true);
        if (string.IsNullOrEmpty(_pendingError))
            SetStatus("Host = play + server. Join = connect. Server = dedicated (no local player).");
    }

    public void ShowSettings()
    {
        SetOnly(_settingsPanel);
        if (_optionsUi != null)
            _optionsUi.RefreshFromSettings();
    }

    public void ShowCredits()
    {
        SetOnly(_creditsPanel);
    }

    private void EnableConnectButtons(bool on)
    {
        if (_hostBtn != null) _hostBtn.interactable = on;
        if (_joinBtn != null) _joinBtn.interactable = on;
        if (_serverBtn != null) _serverBtn.interactable = on;
    }

    private void SetOnly(GameObject active)
    {
        if (_mainPanel != null) _mainPanel.SetActive(active == _mainPanel);
        if (_settingsPanel != null) _settingsPanel.SetActive(active == _settingsPanel);
        if (_multiplayerPanel != null) _multiplayerPanel.SetActive(active == _multiplayerPanel);
        if (_creditsPanel != null) _creditsPanel.SetActive(active == _creditsPanel);
    }

    private void ResolvePanels()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
            _canvas = canvas.transform;

        _mainPanel = FindDeep("Main Panel");
        _settingsPanel = FindDeep("Settings Panel");

        if (_mainPanel == null && _canvas != null)
        {
            _mainPanel = new GameObject("Main Panel", typeof(RectTransform));
            _mainPanel.transform.SetParent(_canvas, false);
            StretchFull(_mainPanel.GetComponent<RectTransform>());
        }
    }

    private GameObject FindDeep(string name)
    {
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
                return all[i].gameObject;
        }
        return null;
    }

    private void WireExistingButtons()
    {
        WireButton("Start", ShowMultiplayer);
        WireButton("Settings", ShowSettings);
        WireButton("Credits", ShowCredits);
        WireButton("Quit", QuitGame);
        WireButton("Back", ShowMain);
        WireButton("SettingsBack", ShowMain);
        WireButton("MpBack", ShowMain);
        WireButton("CreditsBack", ShowMain);

        SetActiveIfFound("Video", false);
        SetActiveIfFound("Audio", false);
        SetActiveIfFound("Controls", false);
    }

    private void WireButton(string name, UnityEngine.Events.UnityAction action)
    {
        Button button = FindButton(name);
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        button.interactable = true;
    }

    private Button FindButton(string name)
    {
        if (_canvas != null)
        {
            Button[] underCanvas = _canvas.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < underCanvas.Length; i++)
            {
                if (underCanvas[i] != null && underCanvas[i].gameObject.name == name)
                    return underCanvas[i];
            }
        }

        GameObject go = FindDeep(name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private void SetActiveIfFound(string name, bool active)
    {
        GameObject go = FindDeep(name);
        if (go != null)
            go.SetActive(active);
    }

    private void InjectSettingsContent()
    {
        if (_settingsPanel == null)
            return;

        Transform contentParent = _settingsPanel.transform;
        GameObject host = new GameObject("RuntimeSettings", typeof(RectTransform));
        host.transform.SetParent(contentParent, false);
        host.transform.SetAsFirstSibling();
        RectTransform rt = host.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 30f);
        rt.sizeDelta = new Vector2(560f, 420f);

        Image hostBlocker = host.AddComponent<Image>();
        hostBlocker.color = new Color(0f, 0f, 0f, 0f);
        hostBlocker.raycastTarget = false;

        _optionsUi = host.AddComponent<OptionsUI>();
        _optionsUi.BuildInto(host.transform);

        CreateButton("SettingsBack", contentParent, "Back", new Vector2(0f, -240f), new Vector2(160f, 44f), ShowMain);

        Transform oldBack = contentParent.Find("Back");
        if (oldBack != null)
            oldBack.gameObject.SetActive(false);
    }

    private void BuildMultiplayerPanel()
    {
        if (_canvas == null)
            return;

        _multiplayerPanel = CreatePanel("Multiplayer Panel", _canvas,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(520f, 420f),
            new Color(0.05f, 0.05f, 0.08f, 0.94f));

        CreateLabel("MpTitle", _multiplayerPanel.transform, "Multiplayer", 30f, new Vector2(0f, 160f), new Vector2(440f, 40f));
        CreateLabel("AddrLabel", _multiplayerPanel.transform, "Address", 16f, new Vector2(0f, 110f), new Vector2(400f, 24f));
        _addressField = CreateInputField("AddressInput", _multiplayerPanel.transform, defaultAddress, new Vector2(0f, 75f), new Vector2(380f, 36f));
        CreateLabel("PortLabel", _multiplayerPanel.transform, "Port", 16f, new Vector2(0f, 30f), new Vector2(400f, 24f));
        _portField = CreateInputField("PortInput", _multiplayerPanel.transform, defaultPort.ToString(), new Vector2(0f, -5f), new Vector2(380f, 36f));

        _hostBtn = CreateButton("HostBtn", _multiplayerPanel.transform, "Host", new Vector2(-140f, -70f), new Vector2(120f, 44f), OnHost);
        _joinBtn = CreateButton("JoinBtn", _multiplayerPanel.transform, "Join", new Vector2(0f, -70f), new Vector2(120f, 44f), OnJoin);
        _serverBtn = CreateButton("ServerBtn", _multiplayerPanel.transform, "Server", new Vector2(140f, -70f), new Vector2(120f, 44f), OnServer);

        _statusText = CreateLabel("MpStatus", _multiplayerPanel.transform,
            "Host = play + server. Join = connect. Server = dedicated.",
            14f, new Vector2(0f, -130f), new Vector2(480f, 48f));

        CreateButton("MpBack", _multiplayerPanel.transform, "Back", new Vector2(0f, -175f), new Vector2(140f, 40f), ShowMain);
        _multiplayerPanel.SetActive(false);
    }

    private void BuildCreditsPanel()
    {
        if (_canvas == null)
            return;

        _creditsPanel = CreatePanel("Credits Panel", _canvas,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(480f, 320f),
            new Color(0.05f, 0.05f, 0.08f, 0.94f));

        CreateLabel("CreditsTitle", _creditsPanel.transform, "Credits", 30f, new Vector2(0f, 110f), new Vector2(400f, 40f));
        CreateLabel("CreditsBody", _creditsPanel.transform,
            "DBV-MMO\nBachelor thesis multiplayer prototype\n\nUnity 6 · Netcode for GameObjects\nWarrior / Mage · Combat · Quests · Loot",
            16f, new Vector2(0f, 0f), new Vector2(420f, 160f));
        CreateButton("CreditsBack", _creditsPanel.transform, "Back", new Vector2(0f, -120f), new Vector2(140f, 40f), ShowMain);
        _creditsPanel.SetActive(false);
    }

    private void OnHost() => BeginNetwork(PendingNetworkMode.Host);
    private void OnJoin() => BeginNetwork(PendingNetworkMode.Client);
    private void OnServer() => BeginNetwork(PendingNetworkMode.Server);

    private void BeginNetwork(PendingNetworkMode mode)
    {
        string address = _addressField != null && !string.IsNullOrWhiteSpace(_addressField.text)
            ? _addressField.text.Trim()
            : defaultAddress;

        ushort port = defaultPort;
        if (_portField != null && ushort.TryParse(_portField.text, out ushort parsed) && parsed > 0)
            port = parsed;

        PendingNetworkStart.Set(mode, address, port);

        int check = PlayerPrefs.GetInt("dbv.pending.net.mode", 0);
        Debug.Log($"[MainMenu] Saved pending mode check={check} expected={(int)mode}");

        if (mode == PendingNetworkMode.Client)
        {
            SetStatus($"Connecting to {address}:{port}...\n(returning here if no host)");
        }
        else
        {
            SetStatus($"Starting {mode}...");
        }

        EnableConnectButtons(false);

        // Host/Server create the session in SampleScene.
        // Client also loads SampleScene to use NetworkManager, but NetworkBootstrap
        // will send you straight back to MainMenu if nothing is listening.
        Debug.Log($"[MainMenu] LoadScene({GameSceneName}) mode={mode} {address}:{port}");
        SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
            _statusText.text = message;
    }

    #region UI helpers

    private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
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
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static TMP_InputField CreateInputField(string name, Transform parent, string value, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
        textArea.transform.SetParent(go.transform, false);
        RectTransform textAreaRt = textArea.GetComponent<RectTransform>();
        Stretch(textAreaRt, 8f);
        textArea.AddComponent<RectMask2D>();

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(textArea.transform, false);
        Stretch(textGo.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 18f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGo.transform.SetParent(textArea.transform, false);
        Stretch(placeholderGo.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
        placeholder.text = value;
        placeholder.fontSize = 18f;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        placeholder.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            placeholder.font = TMP_Settings.defaultFontAsset;

        TMP_InputField field = go.AddComponent<TMP_InputField>();
        field.textViewport = textAreaRt;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.text = value;
        field.pointSize = 18f;
        return field;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.45f, 0.85f, 1f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return button;
    }

    private static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    #endregion
}

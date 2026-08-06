using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOptionsUI : MonoBehaviour
{
    public const string MainMenuSceneName = "MainMenu";

    public static GameOptionsUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    private GameObject _root;
    private GameObject _menuPanel;
    private GameObject _settingsPanel;
    private OptionsUI _optionsUi;
    private bool _built;

    public static GameOptionsUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameOptionsUI existing = FindFirstObjectByType<GameOptionsUI>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("GameOptionsUI");
        return go.AddComponent<GameOptionsUI>();
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
        Close();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsOpen = false;
        }
    }

    private void Update()
    {
        if (!_built)
            return;

        // Never open/close options on the main menu scene.
        if (IsMainMenuScene())
        {
            if (IsOpen)
                Close();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (!keyboard.escapeKey.wasPressedThisFrame)
            return;

        if (ChatUI.Instance != null && ChatUI.Instance.IsOpen)
            return;

        if (IsOpen)
        {
            if (_settingsPanel != null && _settingsPanel.activeSelf)
                ShowMenu();
            else
                Close();
            return;
        }

        Open();
    }

    public void Open()
    {
        if (IsMainMenuScene())
            return;

        if (!_built)
            BuildUI();

        IsOpen = true;
        if (_root != null)
            _root.SetActive(true);
        ShowMenu();
        if (_optionsUi != null)
            _optionsUi.RefreshFromSettings();
    }

    public void Close()
    {
        IsOpen = false;
        if (_root != null)
            _root.SetActive(false);
    }

    public static void ForceClosed()
    {
        IsOpen = false;
        if (Instance != null)
            Instance.Close();
    }

    private static bool IsMainMenuScene()
    {
        string name = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return name == MainMenuSceneName || name == "MainMenu";
    }

    private void ShowMenu()
    {
        if (_menuPanel != null)
            _menuPanel.SetActive(true);
        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);
    }

    private void ShowSettings()
    {
        if (_menuPanel != null)
            _menuPanel.SetActive(false);
        if (_settingsPanel != null)
            _settingsPanel.SetActive(true);
        if (_optionsUi != null)
            _optionsUi.RefreshFromSettings();
    }

    public static void DisconnectToMainMenu()
    {
        NetworkBootstrap.ReturnToMainMenu();
    }

    private void OnQuitDesktop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BuildUI()
    {
        OptionsUI.EnsureEventSystem();

        GameObject canvasGo = new GameObject("GameOptionsCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        RectTransform rootRt = _root.GetComponent<RectTransform>();
        Stretch(rootRt, 0f);

        Image dim = _root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);

        _menuPanel = CreatePanel("MenuPanel", _root.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(380f, 360f),
            new Color(0.05f, 0.05f, 0.08f, 0.94f));

        CreateLabel("Title", _menuPanel.transform, "Options", 30f, new Vector2(0f, 130f), new Vector2(320f, 40f));
        CreateButton("Resume", _menuPanel.transform, "Resume", new Vector2(0f, 60f), new Vector2(240f, 44f), Close);
        CreateButton("Settings", _menuPanel.transform, "Settings", new Vector2(0f, 0f), new Vector2(240f, 44f), ShowSettings);
        CreateButton("Disconnect", _menuPanel.transform, "Disconnect", new Vector2(0f, -60f), new Vector2(240f, 44f), DisconnectToMainMenu);
        CreateButton("Quit", _menuPanel.transform, "Quit Desktop", new Vector2(0f, -120f), new Vector2(240f, 44f), OnQuitDesktop);

        _settingsPanel = CreatePanel("SettingsPanel", _root.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(520f, 420f),
            new Color(0.05f, 0.05f, 0.08f, 0.96f));

        GameObject settingsBody = new GameObject("SettingsBody", typeof(RectTransform));
        settingsBody.transform.SetParent(_settingsPanel.transform, false);
        RectTransform bodyRt = settingsBody.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(0f, 56f);
        bodyRt.offsetMax = new Vector2(0f, 0f);

        _optionsUi = settingsBody.AddComponent<OptionsUI>();
        _optionsUi.BuildInto(settingsBody.transform);

        CreateButton("BackSettings", _settingsPanel.transform, "Back", new Vector2(0f, -170f), new Vector2(160f, 40f), ShowMenu);

        _built = true;
    }

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

    private static void CreateLabel(string name, Transform parent, string text, float fontSize, Vector2 pos, Vector2 size)
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
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
    }

    private static void CreateButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
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
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
    }

    private static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }
}

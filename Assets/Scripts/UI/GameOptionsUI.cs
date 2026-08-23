using UnityEngine;
using UnityEngine.InputSystem;
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
        return RuntimeSingleton.Ensure<GameOptionsUI>("GameOptionsUI");
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

        if (ChatUI.IsChatOpen)
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
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == MainMenuSceneName;
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
        UIEventSystem.Ensure();

        GameObject canvasGo = UiFactory.CreateOverlayCanvas(transform, "GameOptionsCanvas", 400);

        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        UiFactory.Stretch(_root.GetComponent<RectTransform>());

        Image dim = _root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);

        _menuPanel = UiFactory.CreatePanel("MenuPanel", _root.transform,
            UiFactory.Center, UiFactory.Center,
            Vector2.zero, new Vector2(380f, 360f),
            new Color(0.05f, 0.05f, 0.08f, 0.94f));

        UiFactory.CreateLabel("Title", _menuPanel.transform, "Options", 30f, new Vector2(0f, 130f), new Vector2(320f, 40f));
        UiFactory.CreateButton("Resume", _menuPanel.transform, "Resume", new Vector2(0f, 60f), new Vector2(240f, 44f), Close);
        UiFactory.CreateButton("Settings", _menuPanel.transform, "Settings", new Vector2(0f, 0f), new Vector2(240f, 44f), ShowSettings);
        UiFactory.CreateButton("Disconnect", _menuPanel.transform, "Disconnect", new Vector2(0f, -60f), new Vector2(240f, 44f), DisconnectToMainMenu);
        UiFactory.CreateButton("Quit", _menuPanel.transform, "Quit Desktop", new Vector2(0f, -120f), new Vector2(240f, 44f), OnQuitDesktop);

        _settingsPanel = UiFactory.CreatePanel("SettingsPanel", _root.transform,
            UiFactory.Center, UiFactory.Center,
            Vector2.zero, new Vector2(520f, 420f),
            new Color(0.05f, 0.05f, 0.08f, 0.96f));

        RectTransform bodyRt = UiFactory.CreateRect("SettingsBody", _settingsPanel.transform);
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(0f, 56f);
        bodyRt.offsetMax = Vector2.zero;

        _optionsUi = bodyRt.gameObject.AddComponent<OptionsUI>();
        _optionsUi.BuildInto(bodyRt);

        UiFactory.CreateButton("BackSettings", _settingsPanel.transform, "Back", new Vector2(0f, -170f), new Vector2(160f, 40f), ShowMenu);

        _built = true;
    }
}

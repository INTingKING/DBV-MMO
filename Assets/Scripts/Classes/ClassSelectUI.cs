using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ClassSelectUI : MonoBehaviour
{
    public static ClassSelectUI Instance { get; private set; }

    private PlayerClass _boundClass;
    private PlayerSkills _boundSkills;

    private GameObject _selectRoot;
    private GameObject _hudRoot;
    private TMP_Text _hudText;

    public static ClassSelectUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        ClassSelectUI existing = FindFirstObjectByType<ClassSelectUI>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("ClassSelectUI");
        return go.AddComponent<ClassSelectUI>();
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
        ShowSelect(false);
        ShowHud(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Bind(PlayerClass playerClass)
    {
        if (_boundClass != null)
            _boundClass.ClassChanged -= HandleClassChanged;

        _boundClass = playerClass;
        _boundSkills = playerClass != null ? playerClass.GetComponent<PlayerSkills>() : null;

        if (_boundClass != null)
            _boundClass.ClassChanged += HandleClassChanged;

        Refresh();
    }

    public void Unbind(PlayerClass playerClass)
    {
        if (_boundClass != playerClass)
            return;

        if (_boundClass != null)
            _boundClass.ClassChanged -= HandleClassChanged;

        _boundClass = null;
        _boundSkills = null;
        ShowSelect(false);
        ShowHud(false);
    }

    private void Update()
    {
        if (_boundClass == null || !_boundClass.IsOwner)
        {
            ShowSelect(false);
            ShowHud(false);
            return;
        }

        Refresh();
        RefreshHudText();
    }

    private void HandleClassChanged(PlayerClassType type)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_boundClass == null || !_boundClass.IsSpawned || !_boundClass.IsOwner)
        {
            ShowSelect(false);
            ShowHud(false);
            return;
        }

        bool needSelect = !_boundClass.HasSelectedClass;
        ShowSelect(needSelect);
        ShowHud(!needSelect);
    }

    private void RefreshHudText()
    {
        if (_hudText == null || _boundClass == null || !_boundClass.HasSelectedClass)
            return;

        if (!ClassDefinition.TryGet(_boundClass.CurrentClass, out ClassDefinition.Data data))
            return;

        string cd = "Ready";
        if (_boundSkills != null && _boundSkills.IsOnCooldown)
            cd = $"{_boundSkills.CooldownRemaining:0.0}s";

        _hudText.text = $"{data.DisplayName}  |  [1] {data.SkillName}: {cd}";
    }

    private void OnPickWarrior()
    {
        _boundClass?.RequestSelectClass(PlayerClassType.Warrior);
    }

    private void OnPickMage()
    {
        _boundClass?.RequestSelectClass(PlayerClassType.Mage);
    }

    private void ShowSelect(bool show)
    {
        if (_selectRoot != null)
            _selectRoot.SetActive(show);
    }

    private void ShowHud(bool show)
    {
        if (_hudRoot != null)
            _hudRoot.SetActive(show);
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        GameObject canvasGo = new GameObject("ClassCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        _selectRoot = CreatePanel("SelectPanel", canvasGo.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(520f, 320f),
            new Color(0f, 0f, 0f, 0.85f));

        CreateLabel("Title", _selectRoot.transform, "Choose Your Class", 30f, new Vector2(0f, 110f), new Vector2(480f, 40f));
        CreateLabel("Hint", _selectRoot.transform, "You must pick a class before fighting", 16f, new Vector2(0f, 70f), new Vector2(480f, 28f));

        CreateButton("WarriorBtn", _selectRoot.transform, "Warrior\nSlam (upgrade via quest)",
            new Vector2(-120f, -20f), new Vector2(200f, 100f), new Color(0.15f, 0.35f, 0.85f, 1f), OnPickWarrior);
        CreateButton("MageBtn", _selectRoot.transform, "Mage\nFirebolt (upgrade via quest)",
            new Vector2(120f, -20f), new Vector2(200f, 100f), new Color(0.55f, 0.2f, 0.8f, 1f), OnPickMage);

        _hudRoot = CreatePanel("ClassHud", canvasGo.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 70f), new Vector2(420f, 40f),
            new Color(0f, 0f, 0f, 0.55f));
        _hudText = CreateLabel("HudText", _hudRoot.transform, "Class", 18f, Vector2.zero, new Vector2(400f, 32f));
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

    private static void CreateButton(string name, Transform parent, string label, Vector2 pos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
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
        image.color = color;
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

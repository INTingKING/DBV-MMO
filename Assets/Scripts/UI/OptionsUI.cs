using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OptionsUI : MonoBehaviour
{
    public enum Page
    {
        Audio,
        Video,
        Controls
    }

    private RectTransform _contentRoot;
    private Page _page = Page.Audio;
    private Slider _volumeSlider;
    private TMP_Text _volumeValueLabel;
    private Toggle _fullscreenToggle;
    private TMP_Dropdown _resolutionDropdown;
    private List<GameSettings.ResolutionOption> _resolutions = new List<GameSettings.ResolutionOption>();
    private TMP_Text _controlsText;
    private GameObject _audioPage;
    private GameObject _videoPage;
    private GameObject _controlsPage;
    private Image _audioTab;
    private Image _videoTab;
    private Image _controlsTab;

    public void BuildInto(Transform parent)
    {
        ClearChildren(parent);

        GameObject shell = CreatePanel("OptionsShell", parent,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0f), raycast: false);
        Stretch(shell.GetComponent<RectTransform>(), 0f);

        GameObject tabs = CreatePanel("Tabs", shell.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -8f), new Vector2(0f, 44f),
            new Color(0f, 0f, 0f, 0f), raycast: false);
        RectTransform tabsRt = tabs.GetComponent<RectTransform>();
        tabsRt.pivot = new Vector2(0.5f, 1f);
        tabsRt.anchorMin = new Vector2(0f, 1f);
        tabsRt.anchorMax = new Vector2(1f, 1f);
        tabsRt.offsetMin = new Vector2(12f, -52f);
        tabsRt.offsetMax = new Vector2(-12f, -8f);

        _audioTab = CreateTabButton("AudioTab", tabs.transform, "Audio", new Vector2(0.12f, 0.5f), () => ShowPage(Page.Audio));
        _videoTab = CreateTabButton("VideoTab", tabs.transform, "Video", new Vector2(0.5f, 0.5f), () => ShowPage(Page.Video));
        _controlsTab = CreateTabButton("ControlsTab", tabs.transform, "Controls", new Vector2(0.88f, 0.5f), () => ShowPage(Page.Controls));

        GameObject content = CreatePanel("Content", shell.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.08f, 0.08f, 0.1f, 0.92f), raycast: true);
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.offsetMin = new Vector2(12f, 12f);
        contentRt.offsetMax = new Vector2(-12f, -60f);
        _contentRoot = contentRt;

        BuildAudioPage();
        BuildVideoPage();
        BuildControlsPage();
        ShowPage(Page.Audio);
    }

    public void RefreshFromSettings()
    {
        if (_volumeSlider != null)
            _volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        UpdateVolumeLabel(GameSettings.MasterVolume);

        if (_fullscreenToggle != null)
            _fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);

        RefreshResolutionDropdown();
    }

    private void BuildAudioPage()
    {
        _audioPage = CreatePanel("AudioPage", _contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0), raycast: false);
        Stretch(_audioPage.GetComponent<RectTransform>(), 16f);

        CreateLabel("AudioTitle", _audioPage.transform, "Audio", 24f, new Vector2(0f, 110f), new Vector2(360f, 36f));
        CreateLabel("MasterLabel", _audioPage.transform, "Master Volume", 18f, new Vector2(0f, 50f), new Vector2(360f, 28f));

        GameObject sliderGo = new GameObject("VolumeSlider", typeof(RectTransform));
        sliderGo.transform.SetParent(_audioPage.transform, false);
        RectTransform sliderRt = sliderGo.GetComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRt.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRt.sizeDelta = new Vector2(320f, 28f);
        sliderRt.anchoredPosition = new Vector2(0f, 10f);

        Image bg = sliderGo.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.22f, 1f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
        Stretch(fillAreaRt, 4f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        Stretch(fillRt, 0f);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.25f, 0.55f, 0.95f, 1f);

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGo.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>(), 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(18f, 24f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;

        _volumeSlider = sliderGo.AddComponent<Slider>();
        _volumeSlider.fillRect = fillRt;
        _volumeSlider.handleRect = handleRt;
        _volumeSlider.targetGraphic = handleImage;
        _volumeSlider.direction = Slider.Direction.LeftToRight;
        _volumeSlider.minValue = 0f;
        _volumeSlider.maxValue = 1f;
        _volumeSlider.wholeNumbers = false;
        _volumeSlider.value = GameSettings.MasterVolume;
        _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

        _volumeValueLabel = CreateLabel("VolumeValue", _audioPage.transform, "100%", 18f, new Vector2(0f, -30f), new Vector2(120f, 28f));
        UpdateVolumeLabel(GameSettings.MasterVolume);
    }

    private void BuildVideoPage()
    {
        _videoPage = CreatePanel("VideoPage", _contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0), raycast: false);
        Stretch(_videoPage.GetComponent<RectTransform>(), 16f);

        CreateLabel("VideoTitle", _videoPage.transform, "Video", 24f, new Vector2(0f, 110f), new Vector2(360f, 36f));

        GameObject toggleGo = new GameObject("FullscreenToggle", typeof(RectTransform));
        toggleGo.transform.SetParent(_videoPage.transform, false);
        RectTransform toggleRt = toggleGo.GetComponent<RectTransform>();
        toggleRt.anchorMin = new Vector2(0.5f, 0.5f);
        toggleRt.anchorMax = new Vector2(0.5f, 0.5f);
        toggleRt.sizeDelta = new Vector2(280f, 32f);
        toggleRt.anchoredPosition = new Vector2(0f, 50f);

        GameObject box = new GameObject("Background", typeof(RectTransform));
        box.transform.SetParent(toggleGo.transform, false);
        RectTransform boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0f, 0.5f);
        boxRt.anchorMax = new Vector2(0f, 0.5f);
        boxRt.pivot = new Vector2(0f, 0.5f);
        boxRt.sizeDelta = new Vector2(28f, 28f);
        boxRt.anchoredPosition = Vector2.zero;
        Image boxImage = box.AddComponent<Image>();
        boxImage.color = new Color(0.2f, 0.2f, 0.22f, 1f);

        GameObject check = new GameObject("Checkmark", typeof(RectTransform));
        check.transform.SetParent(box.transform, false);
        Stretch(check.GetComponent<RectTransform>(), 4f);
        Image checkImage = check.AddComponent<Image>();
        checkImage.color = new Color(0.3f, 0.85f, 0.45f, 1f);

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(toggleGo.transform, false);
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(40f, 0f);
        labelRt.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "Fullscreen";
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        _fullscreenToggle = toggleGo.AddComponent<Toggle>();
        _fullscreenToggle.targetGraphic = boxImage;
        _fullscreenToggle.graphic = checkImage;
        _fullscreenToggle.isOn = GameSettings.Fullscreen;
        _fullscreenToggle.onValueChanged.AddListener(v => GameSettings.SetFullscreen(v));

        CreateLabel("ResLabel", _videoPage.transform, "Resolution", 18f, new Vector2(0f, 5f), new Vector2(360f, 28f));

        GameObject dropdownGo = new GameObject("ResolutionDropdown", typeof(RectTransform));
        dropdownGo.transform.SetParent(_videoPage.transform, false);
        RectTransform ddRt = dropdownGo.GetComponent<RectTransform>();
        ddRt.anchorMin = new Vector2(0.5f, 0.5f);
        ddRt.anchorMax = new Vector2(0.5f, 0.5f);
        ddRt.sizeDelta = new Vector2(320f, 34f);
        ddRt.anchoredPosition = new Vector2(0f, -30f);
        Image ddBg = dropdownGo.AddComponent<Image>();
        ddBg.color = new Color(0.18f, 0.18f, 0.2f, 1f);

        GameObject labelTextGo = new GameObject("Label", typeof(RectTransform));
        labelTextGo.transform.SetParent(dropdownGo.transform, false);
        Stretch(labelTextGo.GetComponent<RectTransform>(), 8f);
        TextMeshProUGUI ddLabel = labelTextGo.AddComponent<TextMeshProUGUI>();
        ddLabel.fontSize = 16f;
        ddLabel.color = Color.white;
        ddLabel.alignment = TextAlignmentOptions.MidlineLeft;
        if (TMP_Settings.defaultFontAsset != null)
            ddLabel.font = TMP_Settings.defaultFontAsset;

        GameObject template = CreateDropdownTemplate(dropdownGo.transform);
        template.SetActive(false);

        _resolutionDropdown = dropdownGo.AddComponent<TMP_Dropdown>();
        _resolutionDropdown.targetGraphic = ddBg;
        _resolutionDropdown.captionText = ddLabel;
        _resolutionDropdown.template = template.GetComponent<RectTransform>();
        _resolutionDropdown.itemText = template.GetComponentInChildren<TMP_Text>(true);
        RefreshResolutionDropdown();
        _resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        CreateButton("ApplyVideo", _videoPage.transform, "Apply", new Vector2(0f, -90f), new Vector2(140f, 40f), OnApplyVideo);
    }

    private void BuildControlsPage()
    {
        _controlsPage = CreatePanel("ControlsPage", _contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0), raycast: false);
        Stretch(_controlsPage.GetComponent<RectTransform>(), 16f);

        CreateLabel("ControlsTitle", _controlsPage.transform, "Controls", 24f, new Vector2(0f, 120f), new Vector2(360f, 36f));

        GameObject textGo = new GameObject("ControlsBody", typeof(RectTransform));
        textGo.transform.SetParent(_controlsPage.transform, false);
        RectTransform rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 220f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        _controlsText = textGo.AddComponent<TextMeshProUGUI>();
        _controlsText.text = GameSettings.ControlsHelpText;
        _controlsText.fontSize = 16f;
        _controlsText.alignment = TextAlignmentOptions.TopLeft;
        _controlsText.color = Color.white;
        _controlsText.textWrappingMode = TextWrappingModes.Normal;
        _controlsText.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            _controlsText.font = TMP_Settings.defaultFontAsset;
    }

    private void ShowPage(Page page)
    {
        _page = page;
        if (_audioPage != null) _audioPage.SetActive(page == Page.Audio);
        if (_videoPage != null) _videoPage.SetActive(page == Page.Video);
        if (_controlsPage != null) _controlsPage.SetActive(page == Page.Controls);
        Color active = new Color(0.25f, 0.5f, 0.9f, 1f);
        Color idle = new Color(0.2f, 0.2f, 0.25f, 1f);
        if (_audioTab != null) _audioTab.color = page == Page.Audio ? active : idle;
        if (_videoTab != null) _videoTab.color = page == Page.Video ? active : idle;
        if (_controlsTab != null) _controlsTab.color = page == Page.Controls ? active : idle;
    }

    private void OnVolumeChanged(float value)
    {
        GameSettings.SetMasterVolume(value);
        UpdateVolumeLabel(value);
        GameSettings.Save();
    }

    private void UpdateVolumeLabel(float value)
    {
        if (_volumeValueLabel != null)
            _volumeValueLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= _resolutions.Count)
            return;
        GameSettings.ResolutionOption o = _resolutions[index];
        GameSettings.SetResolution(o.Width, o.Height, o.RefreshRate);
    }

    private void OnApplyVideo()
    {
        GameSettings.Apply(save: true);
        RefreshResolutionDropdown();
    }

    private void RefreshResolutionDropdown()
    {
        if (_resolutionDropdown == null)
            return;

        _resolutions = GameSettings.GetUniqueResolutions();
        _resolutionDropdown.ClearOptions();
        var labels = new List<string>(_resolutions.Count);
        for (int i = 0; i < _resolutions.Count; i++)
            labels.Add(_resolutions[i].Label);
        _resolutionDropdown.AddOptions(labels);
        int idx = GameSettings.FindResolutionIndex(_resolutions);
        _resolutionDropdown.SetValueWithoutNotify(idx);
        _resolutionDropdown.RefreshShownValue();
    }

    private static GameObject CreateDropdownTemplate(Transform parent)
    {
        GameObject template = new GameObject("Template", typeof(RectTransform));
        template.transform.SetParent(parent, false);
        RectTransform templateRt = template.GetComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0f, 0f);
        templateRt.anchorMax = new Vector2(1f, 0f);
        templateRt.pivot = new Vector2(0.5f, 1f);
        templateRt.anchoredPosition = new Vector2(0f, 2f);
        templateRt.sizeDelta = new Vector2(0f, 160f);
        Image templateBg = template.AddComponent<Image>();
        templateBg.color = new Color(0.12f, 0.12f, 0.14f, 0.98f);
        template.AddComponent<ScrollRect>();

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(template.transform, false);
        Stretch(viewport.GetComponent<RectTransform>(), 2f);
        viewport.AddComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 32f);

        GameObject item = new GameObject("Item", typeof(RectTransform));
        item.transform.SetParent(content.transform, false);
        RectTransform itemRt = item.GetComponent<RectTransform>();
        itemRt.anchorMin = new Vector2(0f, 0.5f);
        itemRt.anchorMax = new Vector2(1f, 0.5f);
        itemRt.sizeDelta = new Vector2(0f, 28f);
        Toggle itemToggle = item.AddComponent<Toggle>();
        Image itemBg = item.AddComponent<Image>();
        itemBg.color = new Color(0.2f, 0.2f, 0.24f, 1f);
        itemToggle.targetGraphic = itemBg;

        GameObject itemLabel = new GameObject("Item Label", typeof(RectTransform));
        itemLabel.transform.SetParent(item.transform, false);
        Stretch(itemLabel.GetComponent<RectTransform>(), 8f);
        TextMeshProUGUI itemText = itemLabel.AddComponent<TextMeshProUGUI>();
        itemText.fontSize = 15f;
        itemText.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            itemText.font = TMP_Settings.defaultFontAsset;

        ScrollRect sr = template.GetComponent<ScrollRect>();
        sr.content = contentRt;
        sr.viewport = viewport.GetComponent<RectTransform>();
        sr.horizontal = false;
        sr.vertical = true;

        return template;
    }

    #region UI helpers

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }

    private static Image CreateTabButton(string name, Transform parent, string label, Vector2 anchorX, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorX.x, 0.5f);
        rt.anchorMax = new Vector2(anchorX.x, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 36f);
        rt.anchoredPosition = Vector2.zero;

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        Stretch(textGo.GetComponent<RectTransform>(), 0f);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 16f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return image;
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Color color, bool raycast = true)
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
        image.raycastTarget = raycast;
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
        tmp.raycastTarget = false;
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

    public static void EnsureEventSystem()
    {
        UIEventSystem.Ensure();
    }

    #endregion
}

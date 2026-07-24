using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatCastBarUI : MonoBehaviour
{
    public static CombatCastBarUI Instance { get; private set; }

    private const float BarWidth = 280f;
    private const float BarHeight = 28f;
    private const float InnerPad = 3f;

    private GameObject _root;
    private RectTransform _fillRt;
    private Image _fillImage;
    private TMP_Text _label;
    private float _duration;
    private float _elapsed;
    private bool _active;
    private Coroutine _completeRoutine;

    public static CombatCastBarUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        CombatCastBarUI existing = FindFirstObjectByType<CombatCastBarUI>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("CombatCastBarUI");
        return go.AddComponent<CombatCastBarUI>();
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
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!_active)
            return;

        _elapsed += Time.deltaTime;
        float t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
        SetProgress(t);
    }

    public void BeginCast(string spellName, float duration)
    {
        if (_completeRoutine != null)
        {
            StopCoroutine(_completeRoutine);
            _completeRoutine = null;
        }

        if (_root == null)
            BuildUI();

        _duration = Mathf.Max(0.05f, duration);
        _elapsed = 0f;
        _active = true;
        _root.SetActive(true);

        if (_label != null)
            _label.text = string.IsNullOrEmpty(spellName) ? "Casting..." : spellName;

        SetProgress(0f);
    }

    public void Complete()
    {
        if (_completeRoutine != null)
            StopCoroutine(_completeRoutine);

        SetProgress(1f);
        _elapsed = _duration;
        _active = false;
        _completeRoutine = StartCoroutine(HideAfterFull());
    }

    public void Interrupt()
    {
        if (_completeRoutine != null)
        {
            StopCoroutine(_completeRoutine);
            _completeRoutine = null;
        }

        HideImmediate();
    }

    public void Hide()
    {
        Interrupt();
    }

    private void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);
        if (_fillRt == null)
            return;

        _fillRt.anchorMin = new Vector2(0f, 0f);
        _fillRt.anchorMax = new Vector2(t, 1f);
        _fillRt.offsetMin = new Vector2(InnerPad, InnerPad);
        _fillRt.offsetMax = new Vector2(t <= 0.0001f ? -BarWidth : -InnerPad, -InnerPad);

        if (t <= 0.0001f)
        {
            _fillRt.anchorMax = new Vector2(0f, 1f);
            _fillRt.offsetMin = new Vector2(InnerPad, InnerPad);
            _fillRt.offsetMax = new Vector2(-BarWidth + InnerPad, -InnerPad);
        }
    }

    private IEnumerator HideAfterFull()
    {
        yield return new WaitForSeconds(0.1f);
        HideImmediate();
        _completeRoutine = null;
    }

    private void HideImmediate()
    {
        _active = false;
        _elapsed = 0f;
        _duration = 0f;
        SetProgress(0f);
        if (_root != null)
            _root.SetActive(false);
    }

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("CastBarCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 160;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        _root = new GameObject("CastBar", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        RectTransform rootRt = _root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0f);
        rootRt.anchorMax = new Vector2(0.5f, 0f);
        rootRt.pivot = new Vector2(0.5f, 0f);
        rootRt.anchoredPosition = new Vector2(0f, 160f);
        rootRt.sizeDelta = new Vector2(BarWidth, BarHeight);

        Image bg = _root.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.05f, 0.92f);

        GameObject trackGo = new GameObject("Track", typeof(RectTransform));
        trackGo.transform.SetParent(_root.transform, false);
        RectTransform trackRt = trackGo.GetComponent<RectTransform>();
        trackRt.anchorMin = Vector2.zero;
        trackRt.anchorMax = Vector2.one;
        trackRt.offsetMin = new Vector2(InnerPad, InnerPad);
        trackRt.offsetMax = new Vector2(-InnerPad, -InnerPad);
        Image track = trackGo.AddComponent<Image>();
        track.color = new Color(0.12f, 0.12f, 0.12f, 1f);

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(_root.transform, false);
        _fillRt = fillGo.GetComponent<RectTransform>();
        _fillImage = fillGo.AddComponent<Image>();
        _fillImage.color = new Color(0.95f, 0.75f, 0.15f, 1f);
        _fillImage.type = Image.Type.Simple;
        SetProgress(0f);

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(_root.transform, false);
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        _label = labelGo.AddComponent<TextMeshProUGUI>();
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontSize = 16f;
        _label.color = Color.white;
        _label.raycastTarget = false;
        _label.text = "Casting...";
        if (TMP_Settings.defaultFontAsset != null)
            _label.font = TMP_Settings.defaultFontAsset;
    }
}

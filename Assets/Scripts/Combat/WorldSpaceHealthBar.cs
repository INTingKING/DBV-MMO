using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldSpaceHealthBar : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.85f, 0f);
    [SerializeField] private float width = 1.1f;
    [SerializeField] private float height = 0.14f;
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    [SerializeField] private Color healthyColor = new Color(0.2f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color hurtColor = new Color(0.9f, 0.2f, 0.15f, 1f);

    private NetworkHealth _health;
    private Transform _background;
    private Transform _fill;
    private SpriteRenderer _fillRenderer;
    private static Sprite _unitSprite;

    public void Initialize(NetworkHealth health)
    {
        _health = health;
        if (_health == null)
            return;

        ApplyBossStyleIfNeeded();
        EnsureVisuals();
        _health.HealthChanged += HandleHealthChanged;
        Refresh(_health.CurrentHealth, _health.MaxHealth);
    }

    private void ApplyBossStyleIfNeeded()
    {
        EnemyAI ai = _health.GetComponent<EnemyAI>();
        if (ai == null || !ai.IsBoss)
            return;

        width = 1.85f;
        height = 0.18f;
        offset = new Vector3(0f, 1.55f, 0f);
        healthyColor = new Color(0.95f, 0.78f, 0.22f, 1f);
        hurtColor = new Color(0.85f, 0.18f, 0.12f, 1f);

        GameObject nameGo = new GameObject("BossName", typeof(RectTransform));
        nameGo.transform.SetParent(transform, false);
        nameGo.transform.localPosition = new Vector3(0f, 0.28f, 0f);
        nameGo.SetActive(false);

        RectTransform nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.sizeDelta = new Vector2(220f, 48f);
        nameRt.localScale = Vector3.one * 0.025f;

        Canvas canvas = nameGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 52;

        TextMeshProUGUI nameLabel = UiFactory.AddTmp<TextMeshProUGUI>(nameGo);
        if (nameLabel.font == null)
        {
            Destroy(nameGo);
            return;
        }

        nameLabel.fontSize = 28f;
        nameLabel.alignment = TextAlignmentOptions.Center;
        nameLabel.color = new Color(1f, 0.88f, 0.45f, 1f);
        nameLabel.raycastTarget = false;
        nameLabel.text = ai.DisplayName;
        nameGo.SetActive(true);
        UiFactory.SetOutline(nameLabel, 0.18f, new Color(0f, 0f, 0f, 0.9f));
    }

    private void OnDestroy()
    {
        if (_health != null)
            _health.HealthChanged -= HandleHealthChanged;
    }

    private void LateUpdate()
    {
        if (_health == null)
            return;

        transform.position = _health.transform.position + offset;
        transform.rotation = Quaternion.identity;

        Refresh(_health.CurrentHealth, _health.MaxHealth);
    }

    private void HandleHealthChanged(NetworkHealth health, int previous, int current)
    {
        Refresh(current, health.MaxHealth);
    }

    private void Refresh(int current, int max)
    {
        if (_fill == null)
            return;

        float t = max <= 0 ? 0f : Mathf.Clamp01(current / (float)max);
        _fill.localScale = new Vector3(width * t, height, 1f);

        _fill.localPosition = new Vector3(-width * 0.5f + (width * t) * 0.5f, 0f, -0.01f);

        if (_fillRenderer != null)
            _fillRenderer.color = Color.Lerp(hurtColor, healthyColor, t);

        gameObject.SetActive(current > 0);
    }

    private void EnsureVisuals()
    {
        if (_background != null)
            return;

        Sprite sprite = GetUnitSprite();

        GameObject bgGo = new GameObject("HpBackground");
        bgGo.transform.SetParent(transform, false);
        bgGo.transform.localPosition = Vector3.zero;
        bgGo.transform.localScale = new Vector3(width, height, 1f);
        SpriteRenderer bgRenderer = bgGo.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = sprite;
        bgRenderer.color = backgroundColor;
        bgRenderer.sortingOrder = 50;
        _background = bgGo.transform;

        GameObject fillGo = new GameObject("HpFill");
        fillGo.transform.SetParent(transform, false);
        fillGo.transform.localPosition = Vector3.zero;
        fillGo.transform.localScale = new Vector3(width, height, 1f);
        _fillRenderer = fillGo.AddComponent<SpriteRenderer>();
        _fillRenderer.sprite = sprite;
        _fillRenderer.color = healthyColor;
        _fillRenderer.sortingOrder = 51;
        _fill = fillGo.transform;
    }

    private static Sprite GetUnitSprite()
    {
        if (_unitSprite != null)
            return _unitSprite;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _unitSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _unitSprite;
    }
}

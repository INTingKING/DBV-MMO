using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FloatingChatText : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.25f, 0f);
    [SerializeField] private float riseSpeed = 0.15f;
    [SerializeField] private float fadeStartNormalized = 0.55f;

    private Transform _follow;
    private TMP_Text _text;
    private float _age;
    private float _duration;
    private Vector3 _extraRise;

    public static void Show(Transform followTarget, string message, float duration = 3.5f)
    {
        if (followTarget == null || string.IsNullOrWhiteSpace(message))
            return;

        FloatingChatText existing = followTarget.GetComponentInChildren<FloatingChatText>();
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject go = new GameObject("FloatingChatText");
        go.transform.SetParent(null, false);
        FloatingChatText bubble = go.AddComponent<FloatingChatText>();
        bubble.Setup(followTarget, message.Trim(), duration);
    }

    private void Setup(Transform followTarget, string message, float duration)
    {
        _follow = followTarget;
        _duration = Mathf.Max(0.5f, duration);
        _age = 0f;
        _extraRise = Vector3.zero;

        GameObject labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(transform, false);
        labelGo.SetActive(false);

        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.sizeDelta = new Vector2(280f, 90f);
        labelRt.localScale = Vector3.one * 0.02f;

        Canvas canvas = labelGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        _text = UiFactory.AddTmp<TextMeshProUGUI>(labelGo);
        if (_text.font == null)
        {
            Destroy(gameObject);
            return;
        }

        _text.fontSize = 28f;
        _text.alignment = TextAlignmentOptions.Center;
        _text.color = Color.white;
        _text.raycastTarget = false;
        _text.textWrappingMode = TextWrappingModes.Normal;
        _text.text = message;
        labelGo.SetActive(true);
        UiFactory.SetOutline(_text, 0.2f, new Color(0f, 0f, 0f, 0.85f));

        UpdatePosition();
    }

    private void LateUpdate()
    {
        if (_follow == null)
        {
            Destroy(gameObject);
            return;
        }

        _age += Time.deltaTime;
        _extraRise += Vector3.up * (riseSpeed * Time.deltaTime);
        UpdatePosition();

        float t = _age / _duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        if (_text != null && t >= fadeStartNormalized)
        {
            float fadeT = Mathf.InverseLerp(fadeStartNormalized, 1f, t);
            Color c = _text.color;
            c.a = 1f - fadeT;
            _text.color = c;
        }
    }

    private void UpdatePosition()
    {
        transform.position = _follow.position + offset + _extraRise;
        transform.rotation = Quaternion.identity;
    }
}

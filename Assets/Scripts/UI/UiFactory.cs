using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class UiFactory
{
    public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    public static readonly Vector2 BottomLeft = Vector2.zero;
    public static readonly Color ButtonBlue = new Color(0.2f, 0.45f, 0.85f, 1f);
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    public static void Stretch(RectTransform rt, float pad = 0f)
    {
        if (rt == null)
            return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    public static void ApplyDefaultFont(TMP_Text text)
    {
        if (text != null && TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    public static GameObject CreateOverlayCanvas(Transform parent, string name, int sortingOrder)
    {
        GameObject canvasGo = new GameObject(name, typeof(RectTransform));
        canvasGo.transform.SetParent(parent, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;

        canvasGo.AddComponent<GraphicRaycaster>();
        return canvasGo;
    }

    public static GameObject CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPos,
        Vector2 size,
        Color color,
        Vector2? pivot = null,
        bool raycast = true)
    {
        RectTransform rt = CreateRect(name, parent);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot ?? Center;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return rt.gameObject;
    }

    public static TMP_Text CreateLabel(
        string name,
        Transform parent,
        string text,
        float fontSize,
        Vector2 pos,
        Vector2 size,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        Color? color = null,
        bool raycast = false)
    {
        RectTransform rt = CreateRect(name, parent);
        rt.anchorMin = Center;
        rt.anchorMax = Center;
        rt.pivot = Center;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color ?? Color.white;
        tmp.raycastTarget = raycast;
        ApplyDefaultFont(tmp);
        return tmp;
    }

    public static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 pos,
        Vector2 size,
        UnityAction onClick,
        Color? color = null,
        float fontSize = 18f)
    {
        return CreateButton(name, parent, label, pos, size, onClick, out _, color, fontSize);
    }

    public static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 pos,
        Vector2 size,
        UnityAction onClick,
        out TMP_Text labelText,
        Color? color = null,
        float fontSize = 18f)
    {
        RectTransform rt = CreateRect(name, parent);
        rt.anchorMin = Center;
        rt.anchorMax = Center;
        rt.pivot = Center;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color ?? ButtonBlue;

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null)
            button.onClick.AddListener(onClick);

        RectTransform textRt = CreateRect("Text", rt);
        Stretch(textRt);

        TextMeshProUGUI tmp = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        ApplyDefaultFont(tmp);
        labelText = tmp;
        return button;
    }

    public static TMP_InputField CreateInputField(
        string name,
        Transform parent,
        string value,
        Vector2 pos,
        Vector2 size)
    {
        RectTransform rt = CreateRect(name, parent);
        rt.anchorMin = Center;
        rt.anchorMax = Center;
        rt.pivot = Center;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        RectTransform textAreaRt = CreateRect("Text Area", rt);
        Stretch(textAreaRt, 8f);
        textAreaRt.gameObject.AddComponent<RectMask2D>();

        RectTransform textRt = CreateRect("Text", textAreaRt);
        Stretch(textRt);
        TextMeshProUGUI text = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 18f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        ApplyDefaultFont(text);

        RectTransform placeholderRt = CreateRect("Placeholder", textAreaRt);
        Stretch(placeholderRt);
        TextMeshProUGUI placeholder = placeholderRt.gameObject.AddComponent<TextMeshProUGUI>();
        placeholder.text = value;
        placeholder.fontSize = 18f;
        placeholder.fontStyle = FontStyles.Italic;
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        placeholder.raycastTarget = false;
        ApplyDefaultFont(placeholder);

        TMP_InputField field = rt.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = textAreaRt;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.text = value;
        field.pointSize = 18f;
        return field;
    }
}

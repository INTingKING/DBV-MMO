using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    private PlayerInventory _inventory;
    private GameObject _root;
    private readonly TMP_Text[] _bagLabels = new TMP_Text[PlayerInventory.BagSize];
    private readonly Image[] _bagIcons = new Image[PlayerInventory.BagSize];
    private readonly Image[] _bagFrames = new Image[PlayerInventory.BagSize];
    private readonly TMP_Text[] _equipLabels = new TMP_Text[5];
    private readonly Image[] _equipIcons = new Image[5];
    private readonly Image[] _equipFrames = new Image[5];
    private readonly EquipSlot[] _equipOrder =
    {
        EquipSlot.Head,
        EquipSlot.Weapon,
        EquipSlot.Chest,
        EquipSlot.Legs,
        EquipSlot.Accessory
    };

    private static Sprite _fallbackSprite;
    private static readonly Color EmptySlot = new Color(0.12f, 0.11f, 0.1f, 0.95f);
    private static readonly Color FrameIdle = new Color(0.22f, 0.2f, 0.16f, 1f);

    public static InventoryUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        InventoryUI existing = FindFirstObjectByType<InventoryUI>();
        if (existing != null)
            return existing;

        GameObject go = new GameObject("InventoryUI");
        return go.AddComponent<InventoryUI>();
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
        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (_inventory == null || !_inventory.IsOwner)
            return;

        if (ChatUI.Instance != null && ChatUI.Instance.IsOpen)
            return;

        if (GameOptionsUI.IsOpen)
            return;

        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.iKey.wasPressedThisFrame || kb.bKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame)
            Toggle();
    }

    public void Bind(PlayerInventory inventory)
    {
        if (_inventory != null)
            _inventory.Changed -= Refresh;

        _inventory = inventory;
        if (_inventory != null)
        {
            _inventory.Changed += Refresh;
            Refresh();
            StartCoroutine(RefreshSoon());
        }
    }

    private System.Collections.IEnumerator RefreshSoon()
    {
        yield return null;
        Refresh();
        yield return new WaitForSeconds(0.15f);
        Refresh();
    }

    public void Unbind(PlayerInventory inventory)
    {
        if (_inventory != inventory)
            return;

        _inventory.Changed -= Refresh;
        _inventory = null;
        SetOpen(false);
    }

    public void Toggle()
    {
        if (_root == null)
            return;
        SetOpen(!_root.activeSelf);
    }

    private void SetOpen(bool open)
    {
        if (_root != null)
            _root.SetActive(open);
        if (open)
            Refresh();
    }

    private void Refresh()
    {
        if (_inventory == null)
            return;

        for (int i = 0; i < PlayerInventory.BagSize; i++)
            ApplySlotVisual(_inventory.GetBagItem(i), _bagFrames[i], _bagIcons[i], _bagLabels[i], false);

        for (int e = 0; e < _equipOrder.Length; e++)
        {
            EquipSlot slot = _equipOrder[e];
            ushort id = _inventory.GetEquipped(slot);
            ApplySlotVisual(id, _equipFrames[e], _equipIcons[e], _equipLabels[e], true, SlotShortName(slot));
        }
    }

    private static void ApplySlotVisual(
        ushort id,
        Image frame,
        Image icon,
        TMP_Text label,
        bool showEmptySlotName,
        string emptyName = "")
    {
        if (id == ItemCatalog.Empty || !ItemCatalog.TryGet(id, out ItemDefinition def))
        {
            if (frame != null)
                frame.color = FrameIdle;
            if (icon != null)
            {
                icon.enabled = false;
                icon.sprite = null;
                icon.color = Color.white;
            }
            if (label != null)
            {
                label.text = showEmptySlotName ? emptyName : "";
                label.color = new Color(0.75f, 0.7f, 0.55f, 1f);
            }
            return;
        }

        if (frame != null)
            frame.color = FrameIdle;

        bool hasIcon = def.Icon != null;
        if (icon != null)
        {
            if (hasIcon)
            {
                icon.enabled = true;
                icon.sprite = def.Icon;
                icon.color = Color.white;
                icon.preserveAspect = true;
            }
            else
            {
                icon.enabled = true;
                icon.sprite = GetFallbackSprite();
                icon.color = def.IconColor;
                icon.preserveAspect = true;
            }
        }

        if (label != null)
        {
            if (hasIcon)
            {
                label.text = showEmptySlotName ? def.Name : "";
                label.color = new Color(1f, 1f, 1f, 0.9f);
            }
            else
            {
                label.text = def.Name;
                label.color = Color.white;
            }
        }
    }

    private static string SlotShortName(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Head: return "Head";
            case EquipSlot.Weapon: return "Main Hand";
            case EquipSlot.Chest: return "Chest";
            case EquipSlot.Legs: return "Legs";
            case EquipSlot.Accessory: return "Trinket";
            default: return slot.ToString();
        }
    }

    private void OnBagLeftClick(int index)
    {
        if (_inventory == null)
            return;
        _inventory.EquipFromBagServerRpc(index);
    }

    private void OnBagRightClick(int index)
    {
        if (_inventory == null)
            return;
        if (_inventory.GetBagItem(index) == ItemCatalog.Empty)
            return;
        _inventory.DropFromBagServerRpc(index);
    }

    private void OnEquipClicked(int equipIndex)
    {
        if (_inventory == null || equipIndex < 0 || equipIndex >= _equipOrder.Length)
            return;
        _inventory.UnequipToBagServerRpc((byte)_equipOrder[equipIndex]);
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        GameObject canvasGo = new GameObject("InventoryCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 160;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        _root = new GameObject("InventoryRoot", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        RectTransform rootRt = _root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(1f, 0f);
        rootRt.anchorMax = new Vector2(1f, 0f);
        rootRt.pivot = new Vector2(1f, 0f);
        rootRt.anchoredPosition = new Vector2(-24f, 24f);
        rootRt.sizeDelta = new Vector2(760f, 520f);

        Color panelBg = new Color(0.08f, 0.07f, 0.06f, 0.94f);

        GameObject charPanel = CreatePanel("CharacterFrame", _root.transform,
            new Vector2(10f, 10f), new Vector2(300f, 500f), panelBg);

        GameObject bagPanel = CreatePanel("BagFrame", _root.transform,
            new Vector2(320f, 10f), new Vector2(430f, 500f), panelBg);

        CreateLabel("CharTitle", charPanel.transform, "Character", 20f,
            new Vector2(0f, 220f), new Vector2(260f, 28f));
        CreateLabel("CharHint", charPanel.transform, "Click slot to unequip", 12f,
            new Vector2(0f, 192f), new Vector2(260f, 20f));

        float[] equipY = { 130f, 60f, -10f, -80f, -150f };
        for (int e = 0; e < _equipOrder.Length; e++)
        {
            int idx = e;
            CreateItemSlot(
                $"Equip{e}",
                charPanel.transform,
                new Vector2(0f, equipY[e]),
                new Vector2(240f, 56f),
                () => OnEquipClicked(idx),
                null,
                out _equipFrames[e],
                out _equipIcons[e],
                out _equipLabels[e],
                12f,
                true);
        }

        CreateLabel("BagTitle", bagPanel.transform, "Bags", 20f,
            new Vector2(0f, 220f), new Vector2(380f, 28f));
        CreateLabel("BagHint", bagPanel.transform, "LMB equip  ·  RMB drop (public)  ·  I/B/C", 12f,
            new Vector2(0f, 192f), new Vector2(400f, 20f));

        const float cell = 88f;
        const float gap = 8f;
        float gridW = 4 * cell + 3 * gap;
        float startX = -gridW * 0.5f + cell * 0.5f;
        float startY = 120f;

        for (int i = 0; i < PlayerInventory.BagSize; i++)
        {
            int idx = i;
            int col = i % 4;
            int row = i / 4;
            float x = startX + col * (cell + gap);
            float y = startY - row * (cell + gap);
            CreateItemSlot(
                $"Bag{i}",
                bagPanel.transform,
                new Vector2(x, y),
                new Vector2(cell, cell),
                () => OnBagLeftClick(idx),
                () => OnBagRightClick(idx),
                out _bagFrames[idx],
                out _bagIcons[idx],
                out _bagLabels[idx],
                11f,
                false);
        }

        CreateButton("CloseBtn", bagPanel.transform, "Close",
            new Vector2(0f, -220f), new Vector2(120f, 34f), () => SetOpen(false));
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static TMP_Text CreateLabel(string name, Transform parent, string text, float size, Vector2 pos, Vector2 dim)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = dim;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = new Color(1f, 0.82f, 0.2f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static void CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 pos,
        Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.22f, 0.15f, 1f);
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
    }

    private static void CreateItemSlot(
        string name,
        Transform parent,
        Vector2 pos,
        Vector2 size,
        UnityEngine.Events.UnityAction onLeftClick,
        UnityEngine.Events.UnityAction onRightClick,
        out Image frame,
        out Image icon,
        out TMP_Text label,
        float fontSize,
        bool wideLabel)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        frame = go.AddComponent<Image>();
        frame.color = FrameIdle;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = frame;
        if (onLeftClick != null)
            btn.onClick.AddListener(onLeftClick);

        if (onRightClick != null)
        {
            EventTrigger trigger = go.AddComponent<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerClick
            };
            entry.callback.AddListener(data =>
            {
                if (data is PointerEventData ped && ped.button == PointerEventData.InputButton.Right)
                    onRightClick.Invoke();
            });
            trigger.triggers.Add(entry);
        }

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(go.transform, false);
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        if (wideLabel)
        {
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(8f, 0f);
            iconRt.sizeDelta = new Vector2(40f, 40f);
        }
        else
        {
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(size.x - 16f, size.y - 16f);
        }
        icon = iconGo.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        icon.enabled = false;

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        if (wideLabel)
        {
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(56f, 4f);
            tr.offsetMax = new Vector2(-8f, -4f);
        }
        else
        {
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 0.35f);
            tr.offsetMin = new Vector2(2f, 2f);
            tr.offsetMax = new Vector2(-2f, 0f);
        }
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = fontSize;
        tmp.alignment = wideLabel ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        label = tmp;
    }

    private static Sprite GetFallbackSprite()
    {
        if (_fallbackSprite != null)
            return _fallbackSprite;

        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        return _fallbackSprite;
    }
}

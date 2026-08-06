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
    private TMP_Text _charTitle;
    private TMP_Text _bagTitle;
    private readonly TMP_Text[] _bagLabels = new TMP_Text[PlayerInventory.BagSize];
    private readonly Button[] _bagButtons = new Button[PlayerInventory.BagSize];
    private readonly TMP_Text[] _equipLabels = new TMP_Text[5];
    private readonly Button[] _equipButtons = new Button[5];
    private readonly EquipSlot[] _equipOrder =
    {
        EquipSlot.Head,
        EquipSlot.Weapon,
        EquipSlot.Chest,
        EquipSlot.Legs,
        EquipSlot.Accessory
    };

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
        {
            ushort id = _inventory.GetBagItem(i);
            if (_bagLabels[i] != null)
            {
                if (id == ItemCatalog.Empty)
                    _bagLabels[i].text = "";
                else if (ItemCatalog.TryGet(id, out ItemDefinition def))
                    _bagLabels[i].text = def.Name;
                else
                    _bagLabels[i].text = "?";
            }

            if (_bagButtons[i] != null)
            {
                Image img = _bagButtons[i].targetGraphic as Image;
                if (img != null)
                {
                    if (ItemCatalog.TryGet(id, out ItemDefinition def))
                        img.color = def.IconColor;
                    else
                        img.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);
                }
            }
        }

        for (int e = 0; e < _equipOrder.Length; e++)
        {
            EquipSlot slot = _equipOrder[e];
            ushort id = _inventory.GetEquipped(slot);
            if (_equipLabels[e] == null)
                continue;

            string slotName = SlotShortName(slot);
            if (id == ItemCatalog.Empty)
                _equipLabels[e].text = slotName;
            else
                _equipLabels[e].text = $"{slotName}\n{ItemCatalog.GetName(id)}";

            if (_equipButtons[e] != null)
            {
                Image img = _equipButtons[e].targetGraphic as Image;
                if (img != null)
                {
                    if (ItemCatalog.TryGet(id, out ItemDefinition def))
                        img.color = def.IconColor;
                    else
                        img.color = new Color(0.18f, 0.16f, 0.12f, 0.95f);
                }
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

    private void OnBagClicked(int index)
    {
        if (_inventory == null)
            return;
        _inventory.EquipFromBagServerRpc(index);
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
        Color frameBg = new Color(0.12f, 0.11f, 0.09f, 0.98f);

        GameObject charPanel = CreatePanel("CharacterFrame", _root.transform,
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(10f, 10f), new Vector2(300f, 500f), panelBg);
        RectTransform charRt = charPanel.GetComponent<RectTransform>();
        charRt.anchorMin = new Vector2(0f, 0f);
        charRt.anchorMax = new Vector2(0f, 0f);
        charRt.pivot = new Vector2(0f, 0f);

        GameObject bagPanel = CreatePanel("BagFrame", _root.transform,
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(320f, 10f), new Vector2(430f, 500f), panelBg);
        RectTransform bagRt = bagPanel.GetComponent<RectTransform>();
        bagRt.anchorMin = new Vector2(0f, 0f);
        bagRt.anchorMax = new Vector2(0f, 0f);
        bagRt.pivot = new Vector2(0f, 0f);

        _charTitle = CreateLabel("CharTitle", charPanel.transform, "Character", 20f,
            new Vector2(0f, 220f), new Vector2(260f, 28f));
        CreateLabel("CharHint", charPanel.transform, "Click slot to unequip", 12f,
            new Vector2(0f, 192f), new Vector2(260f, 20f));

        float[] equipY = { 130f, 60f, -10f, -80f, -150f };
        for (int e = 0; e < _equipOrder.Length; e++)
        {
            int idx = e;
            CreateButton($"Equip{e}", charPanel.transform, SlotShortName(_equipOrder[e]),
                new Vector2(0f, equipY[e]), new Vector2(240f, 56f),
                () => OnEquipClicked(idx), out _equipLabels[e], 13f);
            _equipButtons[e] = _equipLabels[e].GetComponentInParent<Button>();
            Image eqImg = _equipButtons[e].targetGraphic as Image;
            if (eqImg != null)
                eqImg.color = frameBg;
        }

        _bagTitle = CreateLabel("BagTitle", bagPanel.transform, "Bags", 20f,
            new Vector2(0f, 220f), new Vector2(380f, 28f));
        CreateLabel("BagHint", bagPanel.transform, "Click item to equip   ·   I / B / C close", 12f,
            new Vector2(0f, 192f), new Vector2(380f, 20f));

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
            CreateButton($"Bag{i}", bagPanel.transform, "",
                new Vector2(x, y), new Vector2(cell, cell),
                () => OnBagClicked(idx), out _bagLabels[idx], 12f);
            _bagButtons[idx] = _bagLabels[idx].GetComponentInParent<Button>();
        }

        CreateButton("CloseBtn", bagPanel.transform, "Close",
            new Vector2(0f, -220f), new Vector2(120f, 34f), () => SetOpen(false), out _, 14f);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
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
        UnityEngine.Events.UnityAction onClick,
        out TMP_Text labelText,
        float fontSize = 14f)
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
        img.color = new Color(0.18f, 0.16f, 0.12f, 0.95f);
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(4f, 2f);
        tr.offsetMax = new Vector2(-4f, -2f);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        labelText = tmp;
    }
}

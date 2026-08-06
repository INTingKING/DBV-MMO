using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "DBV-MMO/Item Definition", order = 0)]
public class ItemDefinition : ScriptableObject
{
    [SerializeField] private ushort id = 1;
    [SerializeField] private string displayName = "New Item";
    [SerializeField] private EquipSlot slot = EquipSlot.Weapon;
    [SerializeField] private int dropWeight = 10;
    [SerializeField] private int bonusMaxHp;
    [SerializeField] private int bonusAutoAttackDamage;
    [SerializeField] private int bonusSkillDamage;
    [SerializeField] [Range(0f, 0.5f)] private float bonusArmorPercent;
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] private Sprite icon;

    public ushort Id => id;
    public string Name => string.IsNullOrEmpty(displayName) ? name : displayName;
    public EquipSlot Slot => slot;
    public int DropWeight => Mathf.Max(0, dropWeight);
    public int BonusMaxHp => bonusMaxHp;
    public int BonusAutoAttackDamage => bonusAutoAttackDamage;
    public int BonusSkillDamage => bonusSkillDamage;
    public float BonusArmorPercent => bonusArmorPercent;
    public Color IconColor => iconColor;
    public Sprite Icon => icon;
}

public static class ItemCatalog
{
    public const ushort Empty = 0;
    public const string ResourcesFolder = "Items";

    private static Dictionary<ushort, ItemDefinition> _byId;
    private static ItemDefinition[] _all;
    private static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded && _byId != null)
            return;

        _byId = new Dictionary<ushort, ItemDefinition>();
        ItemDefinition[] loaded = Resources.LoadAll<ItemDefinition>(ResourcesFolder);
        if (loaded == null || loaded.Length == 0)
            loaded = Resources.LoadAll<ItemDefinition>("");

        List<ItemDefinition> list = new List<ItemDefinition>();
        for (int i = 0; i < loaded.Length; i++)
        {
            ItemDefinition item = loaded[i];
            if (item == null || item.Id == Empty)
                continue;
            if (_byId.ContainsKey(item.Id))
            {
                Debug.LogWarning($"[ItemCatalog] Duplicate item id {item.Id}: {item.name}");
                continue;
            }
            _byId.Add(item.Id, item);
            list.Add(item);
        }

        _all = list.ToArray();
        _loaded = true;
    }

    public static bool TryGet(ushort id, out ItemDefinition def)
    {
        EnsureLoaded();
        if (id == Empty)
        {
            def = null;
            return false;
        }
        return _byId.TryGetValue(id, out def) && def != null;
    }

    public static string GetName(ushort id)
    {
        return TryGet(id, out ItemDefinition def) ? def.Name : "Empty";
    }

    public static bool IsEquippable(ushort id)
    {
        return TryGet(id, out ItemDefinition def) && def.Slot != EquipSlot.None;
    }

    public static ushort RollWeightedItemId()
    {
        EnsureLoaded();
        if (_all == null || _all.Length == 0)
            return Empty;

        int total = 0;
        for (int i = 0; i < _all.Length; i++)
            total += _all[i].DropWeight;

        if (total <= 0)
            return Empty;

        int roll = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < _all.Length; i++)
        {
            ItemDefinition item = _all[i];
            if (item.DropWeight <= 0)
                continue;
            acc += item.DropWeight;
            if (roll < acc)
                return item.Id;
        }

        return Empty;
    }

    public static IReadOnlyList<ItemDefinition> All
    {
        get
        {
            EnsureLoaded();
            return _all;
        }
    }
}

using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    public const int BagSize = 16;

    private NetworkList<int> _bag;

    private readonly NetworkVariable<int> _weapon = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _head = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _chest = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _legs = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _accessory = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private PlayerGearStats _gearStats;
    private NetworkHealth _health;
    private PlayerClass _playerClass;

    public event Action Changed;

    public int BagCapacity => BagSize;

    private void Awake()
    {
        _bag = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        _gearStats = GetComponent<PlayerGearStats>();
        _health = GetComponent<NetworkHealth>();
        _playerClass = GetComponent<PlayerClass>();

        _bag.OnListChanged += HandleBagChanged;
        _weapon.OnValueChanged += HandleEquipChanged;
        _head.OnValueChanged += HandleEquipChanged;
        _chest.OnValueChanged += HandleEquipChanged;
        _legs.OnValueChanged += HandleEquipChanged;
        _accessory.OnValueChanged += HandleEquipChanged;

        if (IsServer)
            EnsureBagInitialized();

        if (IsOwner)
            InventoryUI.EnsureExists().Bind(this);

        Changed?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        _bag.OnListChanged -= HandleBagChanged;
        _weapon.OnValueChanged -= HandleEquipChanged;
        _head.OnValueChanged -= HandleEquipChanged;
        _chest.OnValueChanged -= HandleEquipChanged;
        _legs.OnValueChanged -= HandleEquipChanged;
        _accessory.OnValueChanged -= HandleEquipChanged;

        if (IsOwner)
            InventoryUI.EnsureExists().Unbind(this);
    }

    private void EnsureBagInitialized()
    {
        if (!IsServer || !IsSpawned)
            return;

        if (_bag == null)
            _bag = new NetworkList<int>();

        bool wasEmpty = _bag.Count == 0;
        while (_bag.Count < BagSize)
            _bag.Add(0);

        if (wasEmpty)
            RecomputeGearStats();
    }

    public ushort GetBagItem(int index)
    {
        if (_bag == null || index < 0 || index >= _bag.Count)
            return ItemCatalog.Empty;
        return (ushort)Mathf.Max(0, _bag[index]);
    }

    public ushort GetEquipped(EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.Weapon: return (ushort)_weapon.Value;
            case EquipSlot.Head: return (ushort)_head.Value;
            case EquipSlot.Chest: return (ushort)_chest.Value;
            case EquipSlot.Legs: return (ushort)_legs.Value;
            case EquipSlot.Accessory: return (ushort)_accessory.Value;
            default: return ItemCatalog.Empty;
        }
    }

    public bool ServerTryAddItem(ushort itemId, bool notify = true)
    {
        if (!IsServer || !IsSpawned || !ItemCatalog.TryGet(itemId, out ItemDefinition def))
            return false;

        EnsureBagInitialized();

        for (int i = 0; i < _bag.Count; i++)
        {
            if (_bag[i] == 0)
            {
                _bag[i] = itemId;
                if (notify)
                    NotifyOwnerClientRpc($"Looted: {def.Name}");
                return true;
            }
        }

        if (notify)
            NotifyOwnerClientRpc("Inventory full.");
        return false;
    }

    public bool ServerHasBagSpace()
    {
        if (!IsServer || !IsSpawned)
            return false;
        EnsureBagInitialized();
        for (int i = 0; i < _bag.Count; i++)
        {
            if (_bag[i] == 0)
                return true;
        }
        return false;
    }

    [ServerRpc]
    public void EquipFromBagServerRpc(int bagIndex)
    {
        if (!IsServer || !IsSpawned)
            return;
        EnsureBagInitialized();
        if (bagIndex < 0 || bagIndex >= _bag.Count)
            return;

        ushort itemId = (ushort)_bag[bagIndex];
        if (!ItemCatalog.TryGet(itemId, out ItemDefinition def))
            return;

        ushort currentlyEquipped = GetEquipped(def.Slot);
        SetEquipped(def.Slot, itemId);
        _bag[bagIndex] = currentlyEquipped;
        RecomputeGearStats();
        NotifyOwnerClientRpc($"Equipped: {def.Name}");
    }

    [ServerRpc]
    public void UnequipToBagServerRpc(byte slotByte)
    {
        if (!IsServer || !IsSpawned)
            return;
        EnsureBagInitialized();

        EquipSlot slot = (EquipSlot)slotByte;
        ushort equipped = GetEquipped(slot);
        if (equipped == ItemCatalog.Empty)
            return;

        int empty = FindEmptyBagIndex();
        if (empty < 0)
        {
            NotifyOwnerClientRpc("Inventory full.");
            return;
        }

        _bag[empty] = equipped;
        SetEquipped(slot, ItemCatalog.Empty);
        RecomputeGearStats();
        NotifyOwnerClientRpc($"Unequipped: {ItemCatalog.GetName(equipped)}");
    }

    [ServerRpc]
    public void DropFromBagServerRpc(int bagIndex)
    {
        if (!IsServer || !IsSpawned)
            return;
        EnsureBagInitialized();
        if (bagIndex < 0 || bagIndex >= _bag.Count)
            return;

        ushort itemId = (ushort)_bag[bagIndex];
        if (!ItemCatalog.TryGet(itemId, out ItemDefinition def))
            return;

        _bag[bagIndex] = 0;

        Vector3 dropPos = transform.position + new Vector3(
            UnityEngine.Random.Range(-0.4f, 0.4f),
            UnityEngine.Random.Range(-0.4f, 0.4f),
            0f);

        LootDrop drop = LootDrop.Spawn(itemId, dropPos, LootDrop.PublicLootOwner);
        if (drop == null)
        {
            _bag[bagIndex] = itemId;
            NotifyOwnerClientRpc("Could not drop item.");
            return;
        }

        NotifyOwnerClientRpc($"Dropped: {def.Name} (public)");
    }

    private int FindEmptyBagIndex()
    {
        for (int i = 0; i < _bag.Count; i++)
        {
            if (_bag[i] == 0)
                return i;
        }
        return -1;
    }

    private void SetEquipped(EquipSlot slot, ushort itemId)
    {
        switch (slot)
        {
            case EquipSlot.Weapon: _weapon.Value = itemId; break;
            case EquipSlot.Head: _head.Value = itemId; break;
            case EquipSlot.Chest: _chest.Value = itemId; break;
            case EquipSlot.Legs: _legs.Value = itemId; break;
            case EquipSlot.Accessory: _accessory.Value = itemId; break;
        }
    }

    private void RecomputeGearStats()
    {
        if (!IsServer)
            return;

        int hp = 0, aa = 0, skill = 0;
        float armor = 0f;

        Accumulate(GetEquipped(EquipSlot.Weapon), ref hp, ref aa, ref skill, ref armor);
        Accumulate(GetEquipped(EquipSlot.Head), ref hp, ref aa, ref skill, ref armor);
        Accumulate(GetEquipped(EquipSlot.Chest), ref hp, ref aa, ref skill, ref armor);
        Accumulate(GetEquipped(EquipSlot.Legs), ref hp, ref aa, ref skill, ref armor);
        Accumulate(GetEquipped(EquipSlot.Accessory), ref hp, ref aa, ref skill, ref armor);

        if (_gearStats == null)
            _gearStats = GetComponent<PlayerGearStats>();
        _gearStats?.ServerSetBonuses(hp, aa, skill, armor);

        ApplyMaxHealthWithGear(hp);
    }

    private static void Accumulate(ushort itemId, ref int hp, ref int aa, ref int skill, ref float armor)
    {
        if (!ItemCatalog.TryGet(itemId, out ItemDefinition def))
            return;
        hp += def.BonusMaxHp;
        aa += def.BonusAutoAttackDamage;
        skill += def.BonusSkillDamage;
        armor += def.BonusArmorPercent;
    }

    private void ApplyMaxHealthWithGear(int bonusHp)
    {
        if (_health == null)
            _health = GetComponent<NetworkHealth>();
        if (_playerClass == null)
            _playerClass = GetComponent<PlayerClass>();
        if (_health == null || !_health.IsSpawned)
            return;

        int baseHp = 50;
        if (_playerClass != null &&
            ClassDefinition.TryGet(_playerClass.CurrentClass, out ClassDefinition.Data data))
        {
            baseHp = data.MaxHealth;
        }

        int newMax = Mathf.Max(1, baseHp + bonusHp);
        if (newMax == _health.MaxHealth)
            return;

        _health.SetMaxHealth(newMax, healToFull: false);
    }

    private void HandleBagChanged(NetworkListEvent<int> changeEvent)
    {
        Changed?.Invoke();
    }

    private void HandleEquipChanged(int previous, int current)
    {
        Changed?.Invoke();
    }

    [ClientRpc]
    private void NotifyOwnerClientRpc(string message)
    {
        if (!IsOwner)
            return;
        ChatUI.AddSystem(message);
    }
}

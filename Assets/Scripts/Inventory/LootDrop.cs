using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LootDrop : NetworkBehaviour
{
    public const ulong PublicLootOwner = ulong.MaxValue;

    private static readonly List<LootDrop> ActiveDrops = new List<LootDrop>();
    private static Sprite _sharedBagIcon;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite bagIcon;
    [SerializeField] private float lifetimeSeconds = 60f;
    [SerializeField] private float pickupRadius = 1.75f;

    private readonly NetworkVariable<int> _itemId = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> _ownerClientId = new NetworkVariable<ulong>(
        PublicLootOwner, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float _despawnAt;
    private bool _pickedUp;

    public ushort ItemId => (ushort)Mathf.Max(0, _itemId.Value);
    public bool IsPublic => _ownerClientId.Value == PublicLootOwner;
    public float PickupRadius => pickupRadius;

    public static LootDrop Spawn(ushort itemId, Vector3 worldPosition, ulong ownerClientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return null;

        GameObject prefab = FindLootPrefab();
        if (prefab == null)
        {
            Debug.LogError("[LootDrop] Prefab 'LootDrop' not found in NetworkPrefabs.");
            return null;
        }

        TryRegisterPrefab(prefab);

        worldPosition.z = -10f;
        GameObject go = Instantiate(prefab, worldPosition, Quaternion.identity);
        LootDrop drop = go.GetComponent<LootDrop>();
        NetworkObject net = go.GetComponent<NetworkObject>();
        if (drop == null || net == null)
        {
            Destroy(go);
            return null;
        }

        net.Spawn(true);
        drop.ServerInitialize(itemId, ownerClientId);
        return drop;
    }

    private static void TryRegisterPrefab(GameObject prefab)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || prefab == null)
            return;

        try
        {
            if (!nm.NetworkConfig.Prefabs.Contains(prefab))
                nm.AddNetworkPrefab(prefab);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LootDrop] AddNetworkPrefab: {ex.Message}");
        }
    }

    private static GameObject FindLootPrefab()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || nm.NetworkConfig?.Prefabs?.Prefabs == null)
            return null;

        foreach (NetworkPrefab entry in nm.NetworkConfig.Prefabs.Prefabs)
        {
            if (entry?.Prefab == null)
                continue;
            if (entry.Prefab.name == "LootDrop" || entry.Prefab.GetComponent<LootDrop>() != null)
                return entry.Prefab;
        }

        return null;
    }

    public void ServerInitialize(ushort itemId, ulong ownerClientId)
    {
        if (!IsServer || !IsSpawned)
            return;

        _itemId.Value = itemId;
        _ownerClientId.Value = ownerClientId;
        _despawnAt = Time.time + lifetimeSeconds;
        _pickedUp = false;
        ApplyVisual(itemId);
    }

    public override void OnNetworkSpawn()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (!ActiveDrops.Contains(this))
            ActiveDrops.Add(this);

        _itemId.OnValueChanged += HandleItemChanged;
        _ownerClientId.OnValueChanged += HandleOwnerChanged;
        ApplyVisual((ushort)Mathf.Max(0, _itemId.Value));
        UpdateVisibilityForLocalClient();

        if (IsServer)
            _despawnAt = Time.time + lifetimeSeconds;
    }

    public override void OnNetworkDespawn()
    {
        _itemId.OnValueChanged -= HandleItemChanged;
        _ownerClientId.OnValueChanged -= HandleOwnerChanged;
        ActiveDrops.Remove(this);
    }

    public bool IsAvailableFor(ulong clientId)
    {
        if (_pickedUp || _itemId.Value <= 0)
            return false;

        return IsPublic || _ownerClientId.Value == clientId;
    }

    public bool IsInPickupRange(Vector3 worldPosition)
    {
        float r = pickupRadius;
        return ((Vector2)worldPosition - (Vector2)transform.position).sqrMagnitude <= r * r;
    }

    public string GetPickupPrompt()
    {
        string name = ItemCatalog.GetName(ItemId);
        if (string.IsNullOrEmpty(name) || name == "Unknown")
            name = "Loot";
        return $"[E] Pick up {name}";
    }

    public static LootDrop FindNearestFor(ulong clientId, Vector3 worldPosition)
    {
        LootDrop best = null;
        float bestDistSq = float.MaxValue;

        for (int i = ActiveDrops.Count - 1; i >= 0; i--)
        {
            LootDrop drop = ActiveDrops[i];
            if (drop == null)
            {
                ActiveDrops.RemoveAt(i);
                continue;
            }

            if (!drop.IsSpawned || !drop.IsAvailableFor(clientId))
                continue;

            if (!drop.IsInPickupRange(worldPosition))
                continue;

            float distSq = ((Vector2)worldPosition - (Vector2)drop.transform.position).sqrMagnitude;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = drop;
            }
        }

        return best;
    }

    public bool ServerTryPickup(PlayerInventory inventory)
    {
        if (!IsServer || !IsSpawned || _pickedUp)
            return false;

        if (inventory == null || !inventory.IsSpawned)
            return false;

        if (!IsAvailableFor(inventory.OwnerClientId))
            return false;

        if (!IsInPickupRange(inventory.transform.position))
            return false;

        if (_itemId.Value <= 0)
            return false;

        if (!inventory.ServerHasBagSpace())
            return false;

        ushort id = (ushort)_itemId.Value;
        if (!inventory.ServerTryAddItem(id))
            return false;

        _pickedUp = true;
        _itemId.Value = 0;
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
        return true;
    }

    private void HandleItemChanged(int previous, int current)
    {
        ApplyVisual((ushort)Mathf.Max(0, current));
    }

    private void HandleOwnerChanged(ulong previous, ulong current)
    {
        UpdateVisibilityForLocalClient();
    }

    private void UpdateVisibilityForLocalClient()
    {
        if (spriteRenderer == null || NetworkManager.Singleton == null)
            return;

        bool show = IsPublic ||
                    NetworkManager.Singleton.LocalClientId == _ownerClientId.Value ||
                    NetworkManager.Singleton.IsServer;

        spriteRenderer.enabled = show;
    }

    private void ApplyVisual(ushort itemId)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.sprite = ResolveBagIcon();

        if (ItemCatalog.TryGet(itemId, out ItemDefinition def))
            spriteRenderer.color = def.IconColor;
        else
            spriteRenderer.color = new Color(1f, 0.85f, 0.2f, 1f);

        UpdateVisibilityForLocalClient();
    }

    private Sprite ResolveBagIcon()
    {
        if (bagIcon != null)
            return bagIcon;

        if (_sharedBagIcon != null)
            return _sharedBagIcon;

        Sprite fromResources = Resources.Load<Sprite>("UI/LootBag");
        if (fromResources != null)
        {
            _sharedBagIcon = fromResources;
            return _sharedBagIcon;
        }

        _sharedBagIcon = CreateBagSprite();
        return _sharedBagIcon;
    }

    private static Sprite CreateBagSprite()
    {
        const int w = 16;
        const int h = 16;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color solid = Color.white;
        Color[] px = new Color[w * h];
        for (int i = 0; i < px.Length; i++)
            px[i] = clear;

        void Plot(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h)
                return;
            px[y * w + x] = solid;
        }

        for (int y = 3; y <= 13; y++)
            for (int x = 3; x <= 12; x++)
                Plot(x, y);

        for (int x = 5; x <= 10; x++)
        {
            Plot(x, 13);
            Plot(x, 14);
        }
        for (int y = 11; y <= 14; y++)
        {
            Plot(5, y);
            Plot(10, y);
        }
        for (int x = 6; x <= 9; x++)
            Plot(x, 8);

        tex.SetPixels(px);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer || _pickedUp)
            return;

        if (Time.time >= _despawnAt)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
        }
    }
}

using Unity.Netcode;
using UnityEngine;

public class LootDrop : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float lifetimeSeconds = 60f;
    [SerializeField] private float pickupRadius = 1.5f;
    [SerializeField] private float pickupCheckInterval = 0.12f;

    private readonly NetworkVariable<int> _itemId = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float _despawnAt;
    private float _nextPickupCheck;
    private bool _pickedUp;

    public ushort ItemId => (ushort)Mathf.Max(0, _itemId.Value);

    public static LootDrop Spawn(ushort itemId, Vector3 worldPosition)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return null;

        GameObject prefab = FindLootPrefab();
        if (prefab == null)
        {
            Debug.LogError("[LootDrop] Prefab 'LootDrop' not found in NetworkManager NetworkPrefabs list. Add Assets/Prefabs/LootDrop.");
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
            Debug.LogError("[LootDrop] Prefab missing LootDrop or NetworkObject.");
            return null;
        }

        net.Spawn(true);
        drop.ServerInitialize(itemId);
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

    public void ServerInitialize(ushort itemId)
    {
        if (!IsServer || !IsSpawned)
            return;

        _itemId.Value = itemId;
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

        _itemId.OnValueChanged += HandleItemChanged;
        ApplyVisual((ushort)Mathf.Max(0, _itemId.Value));

        if (IsServer)
            _despawnAt = Time.time + lifetimeSeconds;
    }

    public override void OnNetworkDespawn()
    {
        _itemId.OnValueChanged -= HandleItemChanged;
    }

    private void HandleItemChanged(int previous, int current)
    {
        ApplyVisual((ushort)Mathf.Max(0, current));
    }

    private void ApplyVisual(ushort itemId)
    {
        if (spriteRenderer == null)
            return;

        if (spriteRenderer.sprite == null)
            spriteRenderer.sprite = CreateFallbackSprite();

        if (ItemCatalog.TryGet(itemId, out ItemDefinition def))
            spriteRenderer.color = def.IconColor;
        else
            spriteRenderer.color = new Color(1f, 0.85f, 0.2f, 1f);
    }

    private static Sprite _fallbackSprite;

    private static Sprite CreateFallbackSprite()
    {
        if (_fallbackSprite != null)
            return _fallbackSprite;

        const int size = 8;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 8f);
        return _fallbackSprite;
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer || _pickedUp)
            return;

        if (Time.time >= _despawnAt)
        {
            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
            return;
        }

        if (Time.time < _nextPickupCheck)
            return;
        _nextPickupCheck = Time.time + pickupCheckInterval;

        TryAutoPickup();
    }

    private void TryAutoPickup()
    {
        if (_itemId.Value <= 0)
            return;

        if (NetworkManager.Singleton == null)
            return;

        float r2 = pickupRadius * pickupRadius;
        Vector2 pos = transform.position;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client?.PlayerObject == null)
                continue;

            Vector2 p = client.PlayerObject.transform.position;
            if ((p - pos).sqrMagnitude > r2)
                continue;

            PlayerInventory inv = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inv == null || !inv.ServerHasBagSpace())
                continue;

            ushort id = (ushort)_itemId.Value;
            if (!inv.ServerTryAddItem(id))
                continue;

            _pickedUp = true;
            _itemId.Value = 0;
            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
            return;
        }
    }
}

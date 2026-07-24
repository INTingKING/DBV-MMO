using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

public class EnemySpawner : MonoBehaviour
{
    public const string SpawnTilemapTag = "EnemySpawn";

    public static EnemySpawner Instance { get; private set; }

    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private string spawnTilemapTag = SpawnTilemapTag;
    [Tooltip("Hard population cap and target alive count. System keeps this many slots filled.")]
    [SerializeField] private int maxSlots = 30;
    [SerializeField] private float minSlotSpacing = 1.5f;
    [SerializeField] private float respawnDelay = 15f;
    [SerializeField] private float playerBlockRadius = 0f;

    private readonly List<SpawnSlot> _slots = new List<SpawnSlot>();
    private bool _initialized;
    private Tilemap _spawnTilemap;

    private sealed class SpawnSlot
    {
        public Vector3 HomePosition;
        public NetworkObject AliveEnemy;
        public float RespawnReadyTime;
        public bool OnCooldown;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<EnemySpawner>() != null)
            return;

        GameObject go = new GameObject("EnemySpawner");
        go.AddComponent<EnemySpawner>();
    }

    private void Awake()
    {
        Instance = this;
        if (enemyPrefab == null)
            enemyPrefab = Resources.Load<GameObject>("Enemy");

    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
            if (NetworkManager.Singleton.IsServer)
                HandleServerStarted();
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
    }

    private void Update()
    {
        if (!_initialized && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            HandleServerStarted();

        if (!_initialized || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        TickRespawns();
    }

    private void HandleServerStarted()
    {
        if (_initialized || enemyPrefab == null)
            return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        BuildSlotsFromTilemap();

        if (_slots.Count == 0)
        {
            Debug.LogError(
                $"[EnemySpawner] No spawn slots. Create a Tilemap, paint cells, tag it '{spawnTilemapTag}'.");
            _initialized = true;
            return;
        }

        for (int i = 0; i < _slots.Count; i++)
            TrySpawnInSlot(i);

        _initialized = true;
        Debug.Log($"[EnemySpawner] Ready with {_slots.Count} fixed slots (cap={maxSlots}, spacing={minSlotSpacing}).");
    }

    private void BuildSlotsFromTilemap()
    {
        _slots.Clear();

        GameObject go = GameObject.FindWithTag(spawnTilemapTag);
        if (go != null)
            _spawnTilemap = go.GetComponent<Tilemap>();

        if (_spawnTilemap == null)
        {
            Debug.LogError($"[EnemySpawner] No Tilemap with tag '{spawnTilemapTag}'.");
            return;
        }

        List<Vector3> candidates = new List<Vector3>();
        BoundsInt bounds = _spawnTilemap.cellBounds;

        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (!_spawnTilemap.HasTile(cell))
                continue;

            Vector3 world = _spawnTilemap.GetCellCenterWorld(cell);
            world.z = -10f;
            candidates.Add(world);
        }

        if (candidates.Count == 0)
        {
            Debug.LogError("[EnemySpawner] EnemySpawn tilemap has no painted tiles.");
            return;
        }

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int limit = Mathf.Max(30, maxSlots);
        maxSlots = limit;

        float spacing = Mathf.Max(0.25f, minSlotSpacing);
        for (int attempt = 0; attempt < 8 && _slots.Count < limit; attempt++)
        {
            _slots.Clear();
            float minDistSq = spacing * spacing;

            foreach (Vector3 pos in candidates)
            {
                if (_slots.Count >= limit)
                    break;

                bool ok = true;
                for (int s = 0; s < _slots.Count; s++)
                {
                    if (((Vector2)_slots[s].HomePosition - (Vector2)pos).sqrMagnitude < minDistSq)
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok)
                    continue;

                _slots.Add(new SpawnSlot
                {
                    HomePosition = pos,
                    AliveEnemy = null,
                    RespawnReadyTime = 0f,
                    OnCooldown = false
                });
            }

            if (_slots.Count >= limit || _slots.Count >= candidates.Count)
                break;

            spacing *= 0.7f;
        }

        if (_slots.Count < 30)
        {
            Debug.LogWarning(
                $"[EnemySpawner] Only {_slots.Count} slots from {candidates.Count} cells " +
                $"(need ≥30). Paint more EnemySpawn tiles.");
        }
        else
        {
            Debug.Log($"[EnemySpawner] Built {_slots.Count} slots from {candidates.Count} painted cells (spacing≈{spacing:0.00}).");
        }
    }

    private void TickRespawns()
    {
        float now = Time.time;
        for (int i = 0; i < _slots.Count; i++)
        {
            SpawnSlot slot = _slots[i];
            if (slot.AliveEnemy != null)
            {

                if (!slot.AliveEnemy.IsSpawned)
                {
                    slot.AliveEnemy = null;
                    slot.OnCooldown = true;
                    slot.RespawnReadyTime = now + respawnDelay;
                }
                continue;
            }

            if (!slot.OnCooldown)
                continue;

            if (now < slot.RespawnReadyTime)
                continue;

            if (playerBlockRadius > 0f && IsPlayerNear(slot.HomePosition, playerBlockRadius))
            {

                slot.RespawnReadyTime = now + 2f;
                continue;
            }

            TrySpawnInSlot(i);
        }
    }

    private static bool IsPlayerNear(Vector3 home, float radius)
    {
        if (NetworkManager.Singleton == null)
            return false;

        float r2 = radius * radius;
        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client?.PlayerObject == null)
                continue;
            if (((Vector2)client.PlayerObject.transform.position - (Vector2)home).sqrMagnitude <= r2)
                return true;
        }
        return false;
    }

    private void TrySpawnInSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count || enemyPrefab == null)
            return;

        SpawnSlot slot = _slots[slotIndex];
        if (slot.AliveEnemy != null && slot.AliveEnemy.IsSpawned)
            return;

        GameObject instance = Instantiate(enemyPrefab, slot.HomePosition, Quaternion.identity);
        NetworkObject netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[EnemySpawner] Enemy prefab missing NetworkObject.");
            Destroy(instance);
            return;
        }

        EnemyAI ai = instance.GetComponent<EnemyAI>();
        if (ai != null)
            ai.BindSpawnSlot(slotIndex);

        netObj.Spawn(true);
        slot.AliveEnemy = netObj;
        slot.OnCooldown = false;
        slot.RespawnReadyTime = 0f;
    }

    public void NotifySlotDeath(int slotIndex)
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
            return;

        if (slotIndex < 0 || slotIndex >= _slots.Count)
            return;

        SpawnSlot slot = _slots[slotIndex];
        slot.AliveEnemy = null;
        slot.OnCooldown = true;
        slot.RespawnReadyTime = Time.time + respawnDelay;
    }

}

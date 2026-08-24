using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BossSpawner : MonoBehaviour
{
    public const string SpawnTilemapTag = "BossSpawn";

    public static BossSpawner Instance { get; private set; }

    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private string spawnTilemapTag = SpawnTilemapTag;
    [SerializeField] private float respawnDelay = 90f;

    private Vector3 _home;
    private bool _hasHome;
    private NetworkObject _aliveBoss;
    private bool _onCooldown;
    private float _respawnReadyTime;
    private bool _initialized;
    private bool _wasServer;
    private float _nextAttemptTime;
    private string _lastError;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!NetworkBootstrap.IsGameSceneLoaded())
            return;

        if (Instance != null || FindFirstObjectByType<BossSpawner>() != null)
            return;

        RuntimeSingleton.Ensure<BossSpawner>("BossSpawner");
    }

    public static void EnsureExists()
    {
        if (!NetworkBootstrap.IsGameSceneLoaded())
            return;
        if (Instance != null || FindFirstObjectByType<BossSpawner>() != null)
            return;
        RuntimeSingleton.Ensure<BossSpawner>("BossSpawner");
    }

    private void Awake()
    {
        Instance = this;
        EnsureBossPrefab();
    }

    private void EnsureBossPrefab()
    {
        if (bossPrefab != null)
            return;

        bossPrefab = NetworkPrefabUtil.FindByName("Boss");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        NetworkManager nm = NetworkManager.Singleton;
        bool isServer = nm != null && nm.IsServer;

        if (_wasServer && !isServer)
            ResetForNextSession();

        _wasServer = isServer;

        if (!isServer)
            return;

        EnsureBossPrefab();

        if (!_initialized)
            TryInitialize();

        if (_initialized)
            TickRespawn();
    }

    private void ResetForNextSession()
    {
        _initialized = false;
        _aliveBoss = null;
        _onCooldown = false;
        _respawnReadyTime = 0f;
        _hasHome = false;
        _nextAttemptTime = 0f;
        _lastError = null;
    }

    private void TryInitialize()
    {
        if (Time.time < _nextAttemptTime)
            return;

        _nextAttemptTime = Time.time + 1f;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        EnsureBossPrefab();
        if (bossPrefab == null)
        {
            LogOnce("Boss prefab not in Network Prefabs. Add Assets/Prefabs/Boss.prefab to DefaultNetworkPrefabs.");
            return;
        }

        if (!TryReadHomeFromTilemap())
            return;

        if (!TrySpawn())
            return;

        _initialized = true;
        _lastError = null;
        Debug.Log($"[BossSpawner] Spawned Hollowhide at {_home}.");
    }

    private bool TryReadHomeFromTilemap()
    {
        _hasHome = false;

        GameObject go = FindSpawnMap();
        if (go == null)
        {
            LogOnce($"No GameObject tagged '{spawnTilemapTag}' (or named BossSpawn).");
            return false;
        }

        Tilemap tilemap = go.GetComponent<Tilemap>();
        if (tilemap == null)
        {
            LogOnce($"'{go.name}' has no Tilemap.");
            return false;
        }

        List<Vector3> cells = new List<Vector3>();
        BoundsInt bounds = tilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cell))
                continue;

            Vector3 world = tilemap.GetCellCenterWorld(cell);
            world.z = -10f;
            cells.Add(world);
        }

        if (cells.Count == 0)
        {
            LogOnce("BossSpawn tilemap has no painted tiles.");
            return false;
        }

        _home = PickClusterHome(cells);
        _home.z = -10f;
        _hasHome = true;
        return true;
    }

    private GameObject FindSpawnMap()
    {
        try
        {
            GameObject tagged = GameObject.FindWithTag(spawnTilemapTag);
            if (tagged != null)
                return tagged;
        }
        catch (UnityException)
        {
            LogOnce($"Tag '{spawnTilemapTag}' is not defined in Tag Manager.");
        }

        return GameObject.Find("BossSpawn");
    }

    private static Vector3 PickClusterHome(List<Vector3> cells)
    {
        bool[] used = new bool[cells.Count];
        List<Vector3> best = null;
        float bestDist = -1f;

        const float link = 1.75f;
        float linkSq = link * link;

        for (int i = 0; i < cells.Count; i++)
        {
            if (used[i])
                continue;

            List<Vector3> cluster = new List<Vector3> { cells[i] };
            used[i] = true;

            bool grew;
            do
            {
                grew = false;
                for (int c = 0; c < cluster.Count; c++)
                {
                    for (int j = 0; j < cells.Count; j++)
                    {
                        if (used[j])
                            continue;
                        if (((Vector2)cells[j] - (Vector2)cluster[c]).sqrMagnitude > linkSq)
                            continue;

                        used[j] = true;
                        cluster.Add(cells[j]);
                        grew = true;
                    }
                }
            } while (grew);

            Vector3 centroid = Centroid(cluster);
            float dist = ((Vector2)centroid).sqrMagnitude;
            if (best == null ||
                cluster.Count > best.Count ||
                (cluster.Count == best.Count && dist > bestDist))
            {
                best = cluster;
                bestDist = dist;
            }
        }

        return Centroid(best ?? cells);
    }

    private static Vector3 Centroid(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < points.Count; i++)
            sum += points[i];
        return sum / Mathf.Max(1, points.Count);
    }

    private void TickRespawn()
    {
        if (_aliveBoss != null)
        {
            if (!_aliveBoss || !_aliveBoss.IsSpawned)
            {
                _aliveBoss = null;
                _onCooldown = true;
                _respawnReadyTime = Time.time + respawnDelay;
            }
            return;
        }

        if (!_onCooldown)
            return;

        if (Time.time < _respawnReadyTime)
            return;

        TrySpawn();
    }

    private bool TrySpawn()
    {
        if (!_hasHome || bossPrefab == null)
            return false;

        if (_aliveBoss != null && _aliveBoss && _aliveBoss.IsSpawned)
            return true;

        NetworkPrefabUtil.TryAdd(bossPrefab);

        GameObject instance = Instantiate(bossPrefab, _home, Quaternion.identity);
        NetworkObject netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            LogOnce("Boss prefab missing NetworkObject.");
            Destroy(instance);
            return false;
        }

        EnemyAI ai = instance.GetComponent<EnemyAI>();
        if (ai != null)
            ai.BindHome(_home);

        try
        {
            netObj.Spawn(true);
        }
        catch (System.Exception ex)
        {
            LogOnce($"NetworkObject.Spawn failed: {ex.Message}");
            Destroy(instance);
            return false;
        }

        _aliveBoss = netObj;
        _onCooldown = false;
        _respawnReadyTime = 0f;
        return netObj.IsSpawned;
    }

    public void NotifyDeath()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        _aliveBoss = null;
        _onCooldown = true;
        _respawnReadyTime = Time.time + respawnDelay;
    }

    private void LogOnce(string message)
    {
        if (_lastError == message)
            return;

        _lastError = message;
        Debug.LogError($"[BossSpawner] {message}");
    }
}

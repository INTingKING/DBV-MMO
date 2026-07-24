using UnityEngine;
using UnityEngine.Tilemaps;

public class HubArea : MonoBehaviour
{
    public const string SafeZoneTag = "SafeZone";

    public static HubArea Instance { get; private set; }

    [SerializeField] private string safeZoneTag = SafeZoneTag;
    [SerializeField] private Tilemap safeZoneTilemap;

    private void Awake()
    {
        Instance = this;
        CacheTilemap();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void CacheTilemap()
    {
        if (safeZoneTilemap != null)
            return;

        GameObject go = GameObject.FindWithTag(safeZoneTag);
        if (go != null)
            safeZoneTilemap = go.GetComponent<Tilemap>();

        if (safeZoneTilemap == null)
            Debug.LogWarning($"[HubArea] No Tilemap found with tag '{safeZoneTag}'. Paint a SafeZone tilemap and tag it.");
    }

    public bool Contains(Vector3 worldPosition)
    {
        if (safeZoneTilemap == null)
            CacheTilemap();

        if (safeZoneTilemap == null)
            return false;

        Vector3Int cell = safeZoneTilemap.WorldToCell(worldPosition);
        return safeZoneTilemap.GetTile(cell) != null;
    }

    public bool Contains(Transform t)
    {
        return t != null && Contains(t.position);
    }
}

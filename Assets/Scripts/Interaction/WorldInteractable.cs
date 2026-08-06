using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WorldInteractable : MonoBehaviour
{
    private static readonly Dictionary<string, WorldInteractable> Registry =
        new Dictionary<string, WorldInteractable>();

    private static readonly HashSet<string> MissingTagWarned = new HashSet<string>();

    [SerializeField] private string id = "interactable";
    [SerializeField] private string prompt = "[E] Interact";
    [SerializeField] private string triggerTilemapTag = "";
    [SerializeField] private Tilemap triggerTilemap;
    private bool _tilemapLookupDone;

    public string Id => id;
    public string Prompt => prompt;
    public string TriggerTilemapTag => triggerTilemapTag;

    public virtual string GetPromptFor(Player player) => prompt;

    protected virtual void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        Registry[id] = this;
        _tilemapLookupDone = false;
        CacheTriggerTilemap();
    }

    protected virtual void OnDisable()
    {
        if (Registry.TryGetValue(id, out WorldInteractable existing) && existing == this)
            Registry.Remove(id);
    }

    public static bool TryGet(string interactableId, out WorldInteractable interactable)
    {
        return Registry.TryGetValue(interactableId, out interactable) && interactable != null;
    }

    public static void ClearRegistry()
    {
        Registry.Clear();
        MissingTagWarned.Clear();
    }

    public static WorldInteractable FindAtPosition(Vector3 worldPosition)
    {
        // Drop destroyed entries left after scene unload.
        List<string> stale = null;
        WorldInteractable found = null;

        foreach (KeyValuePair<string, WorldInteractable> kvp in Registry)
        {
            WorldInteractable candidate = kvp.Value;
            if (candidate == null)
            {
                stale ??= new List<string>();
                stale.Add(kvp.Key);
                continue;
            }

            if (!candidate.isActiveAndEnabled)
                continue;

            if (found == null && candidate.IsInRange(worldPosition))
                found = candidate;
        }

        if (stale != null)
        {
            for (int i = 0; i < stale.Count; i++)
                Registry.Remove(stale[i]);
        }

        return found;
    }

    public void CacheTriggerTilemap()
    {
        if (triggerTilemap != null || _tilemapLookupDone)
            return;

        if (string.IsNullOrWhiteSpace(triggerTilemapTag))
        {
            _tilemapLookupDone = true;
            return;
        }

        GameObject go = GameObject.FindWithTag(triggerTilemapTag);
        if (go != null)
            triggerTilemap = go.GetComponent<Tilemap>();

        _tilemapLookupDone = true;

        if (triggerTilemap == null && MissingTagWarned.Add(triggerTilemapTag))
            Debug.LogWarning($"[Interactable:{id}] No Tilemap with tag '{triggerTilemapTag}'. Paint trigger tiles and tag the tilemap.");
    }

    public bool IsInRange(Vector3 worldPosition)
    {
        if (triggerTilemap == null && !_tilemapLookupDone)
            CacheTriggerTilemap();

        if (triggerTilemap == null)
            return false;

        Vector3Int cell = triggerTilemap.WorldToCell(worldPosition);
        return triggerTilemap.GetTile(cell) != null;
    }

    public virtual bool ServerExecute(Player player)
    {
        return true;
    }

    public virtual void ClientOnSuccess(Player player)
    {
    }

    protected void Configure(string newId, string newPrompt, string newTriggerTag)
    {
        if (!string.IsNullOrEmpty(id) && Registry.TryGetValue(id, out WorldInteractable existing) && existing == this)
            Registry.Remove(id);

        id = newId;
        prompt = newPrompt;
        triggerTilemapTag = newTriggerTag;
        triggerTilemap = null;
        _tilemapLookupDone = false;

        if (isActiveAndEnabled && !string.IsNullOrWhiteSpace(id))
            Registry[id] = this;

        CacheTriggerTilemap();
    }
}

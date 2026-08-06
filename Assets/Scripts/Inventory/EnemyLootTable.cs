using UnityEngine;

public static class EnemyLootTable
{

    public static float DropChance = 0.50f;

    public static void TrySpawnDrop(Vector3 worldPosition)
    {
        if (NetworkManagerMissingOrNotServer())
            return;

        if (Random.value > Mathf.Clamp01(DropChance))
            return;

        ushort itemId = ItemCatalog.RollWeightedItemId();
        if (itemId == ItemCatalog.Empty)
            return;

        Vector3 scatter = worldPosition + new Vector3(
            Random.Range(-0.35f, 0.35f),
            Random.Range(-0.35f, 0.35f),
            0f);

        LootDrop drop = LootDrop.Spawn(itemId, scatter);
        if (drop == null)
            Debug.LogWarning($"[Loot] Failed to spawn {ItemCatalog.GetName(itemId)}.");
    }

    private static bool NetworkManagerMissingOrNotServer()
    {
        return Unity.Netcode.NetworkManager.Singleton == null ||
               !Unity.Netcode.NetworkManager.Singleton.IsServer;
    }
}

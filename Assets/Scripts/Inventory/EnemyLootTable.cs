using Unity.Netcode;
using UnityEngine;

public static class EnemyLootTable
{
    public static float DropChance = 0.50f;
    public static float PersonalLootRadius = 25f;

    public static void TrySpawnDropsForNearbyPlayers(Vector3 worldPosition)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client?.PlayerObject == null)
                continue;

            NetworkHealth health = client.PlayerObject.GetComponent<NetworkHealth>();
            if (health != null && health.IsDead)
                continue;

            float dist = Vector2.Distance(worldPosition, client.PlayerObject.transform.position);
            if (dist > PersonalLootRadius)
                continue;

            if (Random.value > Mathf.Clamp01(DropChance))
                continue;

            ushort itemId = ItemCatalog.RollWeightedItemId();
            if (itemId == ItemCatalog.Empty)
                continue;

            Vector3 scatter = worldPosition + new Vector3(
                Random.Range(-0.45f, 0.45f),
                Random.Range(-0.45f, 0.45f),
                0f);

            LootDrop drop = LootDrop.Spawn(itemId, scatter, client.ClientId);
            if (drop == null)
                Debug.LogWarning($"[Loot] Failed to spawn personal loot for client {client.ClientId}.");
        }
    }
}

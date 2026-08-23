using Unity.Netcode;
using UnityEngine;

public static class NetworkPlayers
{
    public static Transform FindTransform(ulong clientId)
    {
        NetworkObject playerObject = FindObject(clientId);
        return playerObject != null ? playerObject.transform : null;
    }

    public static NetworkObject FindObject(ulong clientId)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null)
            return null;

        if (nm.ConnectedClients != null &&
            nm.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
            client?.PlayerObject != null)
        {
            return client.PlayerObject;
        }

        if (nm.SpawnManager?.SpawnedObjects == null)
            return null;

        foreach (var kvp in nm.SpawnManager.SpawnedObjects)
        {
            NetworkObject obj = kvp.Value;
            if (obj != null && obj.IsPlayerObject && obj.OwnerClientId == clientId)
                return obj;
        }

        return null;
    }
}

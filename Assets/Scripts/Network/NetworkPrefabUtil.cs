using Unity.Netcode;
using UnityEngine;

public static class NetworkPrefabUtil
{
    public static GameObject FindByName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return null;

        foreach (NetworkPrefab entry in EnumeratePrefabs())
        {
            if (entry.Prefab.name == prefabName)
                return entry.Prefab;
        }

        return null;
    }

    public static GameObject Find<T>() where T : Component
    {
        foreach (NetworkPrefab entry in EnumeratePrefabs())
        {
            if (entry.Prefab.GetComponent<T>() != null)
                return entry.Prefab;
        }

        return null;
    }

    public static bool TryAdd(GameObject prefab)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || prefab == null || nm.NetworkConfig?.Prefabs == null)
            return false;

        try
        {
            if (!nm.NetworkConfig.Prefabs.Contains(prefab))
                nm.AddNetworkPrefab(prefab);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NetworkPrefab] AddNetworkPrefab: {ex.Message}");
            return false;
        }
    }

    private static System.Collections.Generic.IEnumerable<NetworkPrefab> EnumeratePrefabs()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm?.NetworkConfig?.Prefabs?.Prefabs == null)
            yield break;

        foreach (NetworkPrefab entry in nm.NetworkConfig.Prefabs.Prefabs)
        {
            if (entry?.Prefab != null)
                yield return entry;
        }
    }
}

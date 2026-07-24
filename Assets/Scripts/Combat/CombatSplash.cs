using System.Collections.Generic;
using UnityEngine;

public static class CombatSplash
{
    public static void CollectExtraTargets(
        NetworkHealth primary,
        float radius,
        int maxExtra,
        List<NetworkHealth> results)
    {
        results.Clear();
        if (primary == null || maxExtra <= 0 || radius <= 0f)
            return;

        Vector2 origin = primary.transform.position;
        List<(NetworkHealth health, float dist)> candidates = new List<(NetworkHealth, float)>();

        foreach (NetworkHealth health in Object.FindObjectsByType<NetworkHealth>(FindObjectsSortMode.None))
        {
            if (health == null || health == primary || health.IsDead)
                continue;
            if (health.GetComponent<EnemyAI>() == null)
                continue;

            float dist = Vector2.Distance(origin, health.transform.position);
            if (dist <= radius)
                candidates.Add((health, dist));
        }

        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

        for (int i = 0; i < candidates.Count && results.Count < maxExtra; i++)
            results.Add(candidates[i].health);
    }
}

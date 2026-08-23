using System.Collections.Generic;
using UnityEngine;

public static class CombatSplash
{
    public static void CollectExtraTargets(
        Vector2 origin,
        NetworkHealth primary,
        float radius,
        int maxExtra,
        List<NetworkHealth> results)
    {
        results.Clear();
        if (maxExtra <= 0 || radius <= 0f)
            return;

        List<(NetworkHealth health, float dist)> candidates = new List<(NetworkHealth, float)>();
        IReadOnlyList<EnemyAI> enemies = EnemyRegistry.Alive;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null)
                continue;

            NetworkHealth health = enemy.Health;
            if (health == null || health == primary || health.IsDead)
                continue;

            float dist = Vector2.Distance(origin, enemy.transform.position);
            if (dist <= radius)
                candidates.Add((health, dist));
        }

        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
        for (int i = 0; i < candidates.Count && results.Count < maxExtra; i++)
            results.Add(candidates[i].health);
    }
}

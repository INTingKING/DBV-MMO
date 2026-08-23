using System.Collections.Generic;
using UnityEngine;

public static class EnemyRegistry
{
    private static readonly List<EnemyAI> AliveEnemies = new List<EnemyAI>(32);

    public static IReadOnlyList<EnemyAI> Alive => AliveEnemies;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AliveEnemies.Clear();
    }

    public static void Register(EnemyAI enemy)
    {
        if (enemy == null || AliveEnemies.Contains(enemy))
            return;
        AliveEnemies.Add(enemy);
    }

    public static void Unregister(EnemyAI enemy)
    {
        if (enemy == null)
            return;
        AliveEnemies.Remove(enemy);
    }

    public static void Clear()
    {
        AliveEnemies.Clear();
    }
}

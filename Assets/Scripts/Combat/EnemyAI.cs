using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EnemyAI : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float aggroRange = 8f;
    [SerializeField] private float meleeRange = 1.5f;
    [SerializeField] private float swingTime = 1.8f;
    [SerializeField] private int autoAttackDamage = 4;
    [SerializeField] private float separationRadius = 1.1f;
    [SerializeField] private float separationStrength = 2.5f;

    private NetworkHealth _health;
    private EnemyAnimation _animation;
    private float _swingTimer;
    private int _spawnSlotIndex = -1;

    public NetworkHealth Health => _health;
    public float AggroRange => aggroRange;
    public float MoveSpeed => moveSpeed;
    public Transform CurrentTarget { get; private set; }

    public void BindSpawnSlot(int slotIndex)
    {
        _spawnSlotIndex = slotIndex;
    }

    public override void OnNetworkSpawn()
    {
        _health = GetComponent<NetworkHealth>();
        _animation = GetComponent<EnemyAnimation>();
        if (_health != null)
            _health.Died += HandleDeath;

        _swingTimer = 0f;
        EnemyRegistry.Register(this);
    }

    public override void OnNetworkDespawn()
    {
        if (_health != null && _health.IsDead)
            GameSfx.PlayEnemyDeath();

        EnemyRegistry.Unregister(this);
        if (_health != null)
            _health.Died -= HandleDeath;
    }

    public override void OnDestroy()
    {
        EnemyRegistry.Unregister(this);
        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned)
            return;

        if (_health != null && _health.IsDead)
            return;

        Transform player = CurrentTarget = FindNearestLivingPlayer();
        Vector2 separation = ComputeSeparation();

        if (player == null)
        {

            ApplyMovement(separation * moveSpeed * Time.deltaTime);
            return;
        }

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        float dist = toPlayer.magnitude;

        if (dist > aggroRange)
        {
            ApplyMovement(separation * moveSpeed * Time.deltaTime);
            return;
        }

        if (dist > meleeRange * 0.85f)
        {
            Vector2 chase = toPlayer.normalized * moveSpeed;

            Vector2 velocity = chase + separation * separationStrength;
            if (velocity.sqrMagnitude > 0.0001f)
                velocity = velocity.normalized * moveSpeed;

            ApplyMovement(velocity * Time.deltaTime);
            return;
        }

        if (separation.sqrMagnitude > 0.0001f)
            ApplyMovement(separation.normalized * (moveSpeed * 0.5f * Time.deltaTime));

        _swingTimer -= Time.deltaTime;
        if (_swingTimer > 0f)
            return;

        NetworkHealth playerHealth = player.GetComponent<NetworkHealth>();
        if (playerHealth == null || playerHealth.IsDead)
            return;

        _swingTimer = swingTime;

        if (_animation == null)
            _animation = GetComponent<EnemyAnimation>();
        _animation?.ServerPlayAutoAttack();

        playerHealth.ApplyDamage(autoAttackDamage, _health);
    }

    private void ApplyMovement(Vector2 delta)
    {
        if (delta.sqrMagnitude < 0.0000001f)
            return;

        transform.position += new Vector3(delta.x, delta.y, 0f);
    }

    private Vector2 ComputeSeparation()
    {
        Vector2 push = Vector2.zero;
        int count = 0;

        IReadOnlyList<EnemyAI> others = EnemyRegistry.Alive;
        for (int i = 0; i < others.Count; i++)
        {
            EnemyAI other = others[i];
            if (other == null || other == this || !other.IsSpawned)
                continue;

            NetworkHealth otherHealth = other.Health;
            if (otherHealth != null && otherHealth.IsDead)
                continue;

            Vector2 away = (Vector2)transform.position - (Vector2)other.transform.position;
            float dist = away.magnitude;
            if (dist >= separationRadius || dist < 0.0001f)
                continue;

            float weight = 1f - (dist / separationRadius);
            push += away.normalized * weight;
            count++;
        }

        if (count == 0)
            return Vector2.zero;

        push /= count;
        return push;
    }

    public Transform FindNearestLivingPlayer()
    {
        if (NetworkManager == null || NetworkManager.ConnectedClientsList == null)
            return null;

        Transform best = null;
        int bestPriority = int.MaxValue;
        float bestDist = float.MaxValue;

        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (client?.PlayerObject == null)
                continue;

            NetworkHealth health = client.PlayerObject.GetComponent<NetworkHealth>();
            if (health == null || health.IsDead)
                continue;

            PlayerCombat combat = client.PlayerObject.GetComponent<PlayerCombat>();
            if (combat != null && combat.IsRespawning)
                continue;

            if (HubArea.Instance != null && HubArea.Instance.Contains(client.PlayerObject.transform))
                continue;

            float dist = Vector2.Distance(transform.position, client.PlayerObject.transform.position);
            if (dist > aggroRange)
                continue;

            int priority = GetThreatPriority(client.PlayerObject);
            if (priority < bestPriority || (priority == bestPriority && dist < bestDist))
            {
                bestPriority = priority;
                bestDist = dist;
                best = client.PlayerObject.transform;
            }
        }

        return best;
    }

    private static int GetThreatPriority(NetworkObject playerObject)
    {
        PlayerClass pc = playerObject.GetComponent<PlayerClass>();
        if (pc == null || !pc.HasSelectedClass)
            return 2;

        if (pc.CurrentClass == PlayerClassType.Warrior)
            return 0;

        if (pc.CurrentClass == PlayerClassType.Mage)
            return 1;

        return 2;
    }

    private void HandleDeath(NetworkHealth _)
    {
        if (!IsServer || !IsSpawned)
            return;

        EnemyLootTable.TrySpawnDropsForNearbyPlayers(transform.position);

        if (_spawnSlotIndex >= 0 && EnemySpawner.Instance != null)
            EnemySpawner.Instance.NotifySlotDeath(_spawnSlotIndex);

        NetworkObject.Despawn(true);
    }
}

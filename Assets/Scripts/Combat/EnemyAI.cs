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

    [SerializeField] private bool isBoss;
    [SerializeField] private string displayName = "Hollowhide";
    [SerializeField] private float leashRadius = 18f;
    [SerializeField] private float slamCooldown = 8f;
    [SerializeField] private float slamCastTime = 1.2f;
    [SerializeField] private float slamRadius = 2.4f;
    [SerializeField] private int slamDamage = 20;
    [SerializeField] private float enrageHealthFraction = 0.5f;
    [SerializeField] private float enragedSwingTime = 1.2f;
    [SerializeField] private float enragedSlamCooldown = 5f;
    [SerializeField] private float firstSlamDelay = 3f;
    [SerializeField] private float resetRegenPerSecond = 350f;

    private NetworkHealth _health;
    private EnemyAnimation _animation;
    private float _swingTimer;
    private int _spawnSlotIndex = -1;
    private Vector3 _home;
    private bool _homeBound;
    private bool _resetting;
    private bool _slamCasting;
    private float _slamCastRemaining;
    private float _slamReadyTime;
    private float _resetHealCarry;
    private SpriteRenderer _slamRing;
    private float _slamRingUntil;

    public NetworkHealth Health => _health;
    public float AggroRange => aggroRange;
    public float MoveSpeed => moveSpeed;
    public bool IsBoss => isBoss;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? "Hollowhide" : displayName;
    public Transform CurrentTarget { get; private set; }

    public void BindSpawnSlot(int slotIndex)
    {
        _spawnSlotIndex = slotIndex;
    }

    public void BindHome(Vector3 home)
    {
        home.z = -10f;
        _home = home;
        _homeBound = true;
    }

    public override void OnNetworkSpawn()
    {
        _health = GetComponent<NetworkHealth>();
        _animation = GetComponent<EnemyAnimation>();
        if (_health != null)
            _health.Died += HandleDeath;

        if (!_homeBound)
            BindHome(transform.position);

        _swingTimer = 0f;
        _resetting = false;
        _slamCasting = false;
        _resetHealCarry = 0f;
        _slamReadyTime = Time.time + (isBoss ? firstSlamDelay : 0f);
        HideSlamRing();
        EnemyRegistry.Register(this);
    }

    public override void OnNetworkDespawn()
    {
        if (_health != null && _health.IsDead)
            GameSfx.PlayEnemyDeath();

        EnemyRegistry.Unregister(this);
        if (_health != null)
            _health.Died -= HandleDeath;

        HideSlamRing();
    }

    public override void OnDestroy()
    {
        EnemyRegistry.Unregister(this);
        HideSlamRing();
        base.OnDestroy();
    }

    private void Update()
    {
        if (isBoss)
            PulseSlamRing();

        if (!IsServer || !IsSpawned)
            return;

        if (_health != null && _health.IsDead)
            return;

        if (isBoss && TickBossResetAndSlam())
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

        if (isBoss && TryBeginSlam())
            return;

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

        _swingTimer = CurrentSwingTime();

        if (_animation == null)
            _animation = GetComponent<EnemyAnimation>();
        _animation?.ServerPlayAutoAttack();

        playerHealth.ApplyDamage(autoAttackDamage, _health);
    }

    private bool TickBossResetAndSlam()
    {
        if (_resetting)
        {
            TickReturnHome();
            return true;
        }

        if (ShouldReset())
        {
            BeginReset();
            return true;
        }

        if (!_slamCasting)
            return false;

        _slamCastRemaining -= Time.deltaTime;
        if (_slamCastRemaining > 0f)
            return true;

        ResolveSlam();
        return true;
    }

    private bool ShouldReset()
    {
        if (!isBoss || !_homeBound)
            return false;

        float bossFromHome = Vector2.Distance(transform.position, _home);
        if (bossFromHome > leashRadius)
            return true;

        Transform target = CurrentTarget;
        if (target != null && Vector2.Distance(target.position, _home) > leashRadius)
            return true;

        bool away = bossFromHome > 0.35f;
        bool hurt = _health != null && _health.CurrentHealth < _health.MaxHealth;
        if (!away && !hurt)
            return false;

        return !HasValidCombatPlayer();
    }

    private bool HasValidCombatPlayer()
    {
        if (NetworkManager == null || NetworkManager.ConnectedClientsList == null)
            return false;

        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (!IsValidCombatPlayer(client?.PlayerObject))
                continue;

            if (Vector2.Distance(client.PlayerObject.transform.position, transform.position) > aggroRange)
                continue;

            return true;
        }

        return false;
    }

    private bool IsValidCombatPlayer(NetworkObject playerObject)
    {
        if (playerObject == null)
            return false;

        NetworkHealth health = playerObject.GetComponent<NetworkHealth>();
        if (health == null || health.IsDead)
            return false;

        PlayerCombat combat = playerObject.GetComponent<PlayerCombat>();
        if (combat != null && combat.IsRespawning)
            return false;

        if (HubArea.Instance != null && HubArea.Instance.Contains(playerObject.transform))
            return false;

        if (isBoss && _homeBound && Vector2.Distance(playerObject.transform.position, _home) > leashRadius)
            return false;

        return true;
    }

    private void BeginReset()
    {
        CurrentTarget = null;
        _resetting = true;
        _slamCasting = false;
        _slamCastRemaining = 0f;
        _resetHealCarry = 0f;
        HideSlamTelegraphClientRpc();
    }

    private void TickReturnHome()
    {
        TickResetRegen();

        Vector2 toHome = (Vector2)_home - (Vector2)transform.position;
        if (toHome.sqrMagnitude > 0.04f)
        {
            Vector2 step = toHome.normalized * moveSpeed * Time.deltaTime;
            if (step.sqrMagnitude > toHome.sqrMagnitude)
                transform.position = _home;
            else
                ApplyMovement(step);
            return;
        }

        transform.position = _home;

        if (_health == null || _health.CurrentHealth >= _health.MaxHealth)
            FinishReset();
    }

    private void TickResetRegen()
    {
        if (_health == null || _health.IsDead)
            return;
        if (_health.CurrentHealth >= _health.MaxHealth)
            return;

        float rate = Mathf.Max(1f, resetRegenPerSecond);
        _resetHealCarry += rate * Time.deltaTime;
        int heal = Mathf.FloorToInt(_resetHealCarry);
        if (heal <= 0)
            return;

        _resetHealCarry -= heal;
        _health.ApplyHeal(heal);
    }

    private void FinishReset()
    {
        _resetting = false;
        _resetHealCarry = 0f;
        _slamReadyTime = Time.time + firstSlamDelay;
        _swingTimer = 0f;
        if (_health != null && _health.CurrentHealth < _health.MaxHealth)
            _health.FullHeal();
    }

    private bool TryBeginSlam()
    {
        if (!isBoss || _slamCasting || Time.time < _slamReadyTime)
            return false;

        Transform target = CurrentTarget;
        if (target == null)
            return false;

        _slamCasting = true;
        _slamCastRemaining = Mathf.Max(0.15f, slamCastTime);
        if (_animation == null)
            _animation = GetComponent<EnemyAnimation>();
        _animation?.ServerPlaySlam();
        ShowSlamTelegraphClientRpc(slamRadius);
        return true;
    }

    private void ResolveSlam()
    {
        _slamCasting = false;
        _slamReadyTime = Time.time + CurrentSlamCooldown();
        HideSlamTelegraphClientRpc();
        DealSlamDamage();
    }

    private void DealSlamDamage()
    {
        if (NetworkManager == null)
            return;

        Vector2 origin = transform.position;
        float r2 = slamRadius * slamRadius;

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

            Vector2 toPlayer = (Vector2)client.PlayerObject.transform.position - origin;
            if (toPlayer.sqrMagnitude > r2)
                continue;

            health.ApplyDamage(slamDamage, _health);
        }
    }

    private float CurrentSwingTime()
    {
        return IsEnraged() ? enragedSwingTime : swingTime;
    }

    private float CurrentSlamCooldown()
    {
        return IsEnraged() ? enragedSlamCooldown : slamCooldown;
    }

    private bool IsEnraged()
    {
        if (!isBoss || _health == null)
            return false;

        int max = _health.MaxHealth;
        if (max <= 0)
            return false;

        return _health.CurrentHealth / (float)max <= enrageHealthFraction;
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
        if (isBoss && _resetting)
            return null;

        if (NetworkManager == null || NetworkManager.ConnectedClientsList == null)
            return null;

        Transform best = null;
        int bestPriority = int.MaxValue;
        float bestDist = float.MaxValue;

        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            if (!IsValidCombatPlayer(client?.PlayerObject))
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

        if (isBoss)
            EnemyLootTable.SpawnGuaranteedDropsForNearbyPlayers(transform.position);
        else
            EnemyLootTable.TrySpawnDropsForNearbyPlayers(transform.position);

        if (isBoss && BossSpawner.Instance != null)
            BossSpawner.Instance.NotifyDeath();
        else if (_spawnSlotIndex >= 0 && EnemySpawner.Instance != null)
            EnemySpawner.Instance.NotifySlotDeath(_spawnSlotIndex);

        HideSlamTelegraphClientRpc();
        NetworkObject.Despawn(true);
    }

    [ClientRpc]
    private void ShowSlamTelegraphClientRpc(float radius)
    {
        EnsureSlamRing(Mathf.Max(0.5f, radius));
        _slamRingUntil = Time.time + Mathf.Max(0.15f, slamCastTime);
        if (_slamRing != null)
            _slamRing.enabled = true;
    }

    [ClientRpc]
    private void HideSlamTelegraphClientRpc()
    {
        HideSlamRing();
    }

    private void PulseSlamRing()
    {
        if (_slamRing == null || !_slamRing.enabled)
            return;

        if (Time.time > _slamRingUntil)
        {
            HideSlamRing();
            return;
        }

        float pulse = 0.88f + 0.12f * Mathf.Sin(Time.time * 11f);
        float diameter = slamRadius * 2f * pulse;
        _slamRing.transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    private void EnsureSlamRing(float radius)
    {
        if (_slamRing == null)
        {
            GameObject go = new GameObject("SlamTelegraph");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.15f, 0.02f);
            _slamRing = go.AddComponent<SpriteRenderer>();
            _slamRing.sprite = CreateRingSprite();
            _slamRing.color = new Color(0.95f, 0.18f, 0.12f, 0.85f);
            _slamRing.sortingOrder = 8;
        }

        float diameter = radius * 2f;
        _slamRing.transform.localScale = new Vector3(diameter, diameter, 1f);
        _slamRing.enabled = true;
    }

    private void HideSlamRing()
    {
        _slamRingUntil = 0f;
        if (_slamRing != null)
            _slamRing.enabled = false;
    }

    private static Sprite _ringSprite;

    private static Sprite CreateRingSprite()
    {
        if (_ringSprite != null)
            return _ringSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        float outer = center;
        float inner = center * 0.72f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 0f;
                if (d <= outer && d >= inner)
                {
                    float edge = Mathf.Min(outer - d, d - inner);
                    a = Mathf.Clamp01(edge / 2.5f);
                }
                else if (d < inner)
                    a = 0.12f;

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return _ringSprite;
    }
}

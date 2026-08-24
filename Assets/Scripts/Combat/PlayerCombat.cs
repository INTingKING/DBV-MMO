using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : NetworkBehaviour
{
    [SerializeField] private float meleeRange = 1.75f;
    [SerializeField] private float swingTime = 1.5f;
    [SerializeField] private int autoAttackDamage = 5;
    [SerializeField] private float autoAttackCastTime;
    [SerializeField] private string autoAttackName = "Auto Attack";
    [SerializeField] private int splashExtraTargets;
    [SerializeField] private float splashRadius;
    [SerializeField] private float clickPickRadius = 2.1f;
    [SerializeField] private float tabTargetRange = 16f;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private Vector3 respawnPosition = new Vector3(0f, 0f, -10f);
    [SerializeField] private float castMoveInterruptDistance = 0.2f;

    private readonly NetworkVariable<ulong> _targetNetworkObjectId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkHealth _health;
    private PlayerClass _playerClass;
    private PlayerGearStats _gearStats;
    private PlayerClassAnimation _classAnimation;
    private Player _player;
    private float _swingTimer;
    private bool _autoAttackActive;
    private bool _respawning;

    private bool _isCasting;
    private float _castRemaining;
    private float _castDuration;
    private string _castSpellName;
    private Vector3 _castStartPosition;
    private float _castRange;
    private bool _castIsSkill;

    private float _reflectEndsAtServer;
    private float _damageAmpEndsAtServer;
    private float _damageAmpMultiplier = 1f;

    private SpriteRenderer _localTargetHighlight;
    private Color _localTargetOriginalColor;
    private ulong _highlightedTargetId;

    private readonly List<NetworkHealth> _splashBuffer = new List<NetworkHealth>(4);
    private readonly List<(NetworkHealth health, float dist)> _tabBuffer = new List<(NetworkHealth, float)>(16);

    public NetworkHealth Health => _health;
    public bool IsRespawning => _respawning;
    public bool HasTarget => _targetNetworkObjectId.Value != 0;
    public float MeleeRange => meleeRange;
    public bool IsCasting => _isCasting;
    public int SplashExtraTargets => splashExtraTargets;
    public float SplashRadius => splashRadius;

    public void ApplyClassCombatStats(ClassDefinition.Data data)
    {
        meleeRange = data.AutoAttackRange;
        swingTime = data.AutoAttackSwingTime;
        autoAttackDamage = data.AutoAttackDamage;
        autoAttackCastTime = data.AutoAttackCastTime;
        autoAttackName = string.IsNullOrEmpty(data.AutoAttackName) ? "Auto Attack" : data.AutoAttackName;
        splashExtraTargets = data.SplashExtraTargets;
        splashRadius = data.SplashRadius;
    }

    public int ApplyDamageWithSplash(NetworkHealth primary, int damage)
    {
        if (!IsServer || primary == null || damage <= 0)
            return 0;

        Vector2 splashOrigin = primary.transform.position;
        NetworkHealth primaryRef = primary;

        int total = DamageAndMaybeCreditKill(primary, damage);

        CombatSplash.CollectExtraTargets(splashOrigin, primaryRef, splashRadius, splashExtraTargets, _splashBuffer);
        for (int i = 0; i < _splashBuffer.Count; i++)
            total += DamageAndMaybeCreditKill(_splashBuffer[i], damage);

        return total;
    }

    private int DamageAndMaybeCreditKill(NetworkHealth target, int damage)
    {
        if (target == null || target.IsDead || damage <= 0)
            return 0;

        EnemyAI enemy = target.GetComponent<EnemyAI>();
        bool isEnemy = enemy != null;
        int hpBefore = target.CurrentHealth;
        int dealt = target.ApplyDamage(damage);

        if (isEnemy && dealt > 0 && hpBefore - dealt <= 0)
            ServerCreditKill(enemy.IsBoss);

        return dealt;
    }

    public void InterruptCastServer()
    {
        if (!IsServer || !_isCasting)
            return;

        _isCasting = false;
        _castRemaining = 0f;
        _castIsSkill = false;
        CastInterruptedClientRpc();
    }

    public override void OnNetworkSpawn()
    {
        _health = GetComponent<NetworkHealth>();
        _playerClass = GetComponent<PlayerClass>();
        _gearStats = GetComponent<PlayerGearStats>();
        _classAnimation = GetComponent<PlayerClassAnimation>();
        _player = GetComponent<Player>();
        if (_health != null)
            _health.Died += HandleDeath;

        _targetNetworkObjectId.OnValueChanged += HandleTargetIdChanged;

        _swingTimer = 0f;
        _autoAttackActive = false;
        _respawning = false;
        _isCasting = false;
        _castIsSkill = false;

        if (IsOwner)
            CombatCastBarUI.EnsureExists();

        if (_targetNetworkObjectId.Value != 0)
            RefreshLocalTargetHighlight(_targetNetworkObjectId.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (_health != null)
            _health.Died -= HandleDeath;

        _targetNetworkObjectId.OnValueChanged -= HandleTargetIdChanged;
        ClearLocalTargetHighlight();

        if (IsOwner && CombatCastBarUI.Instance != null)
            CombatCastBarUI.Instance.Hide();
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (IsOwner)
            HandleOwnerInput();

        if (IsServer)
            ServerCombatTick();

        if (IsOwner && _targetNetworkObjectId.Value != 0)
            EnsureHighlightColor();
    }

    private void HandleOwnerInput()
    {
        if (!GameplayInput.CanOwnerAct(true, true, _playerClass, this, _health))
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
        {
            TryTabTarget();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            ClearTargetServerRpc();
            return;
        }

        if (!mouse.leftButton.wasPressedThisFrame)
            return;

        if (Camera.main == null)
            return;

        Vector3 screen = mouse.position.ReadValue();
        Vector3 world = Camera.main.ScreenToWorldPoint(screen);
        world.z = 0f;

        NetworkHealth best = FindClickTarget(world);
        if (best == null || best.NetworkObject == null)
            return;

        if (best.GetComponent<EnemyAI>() == null)
            return;

        SelectEnemyTarget(best);
    }

    private void TryTabTarget()
    {
        NetworkHealth next = FindNextTabTarget();
        if (next == null)
            return;

        SelectEnemyTarget(next);
    }

    private void SelectEnemyTarget(NetworkHealth enemy)
    {
        if (enemy == null || enemy.NetworkObject == null)
            return;

        RefreshLocalTargetHighlight(enemy.NetworkObject.NetworkObjectId);
        SetTargetServerRpc(new NetworkObjectReference(enemy.NetworkObject));
    }

    private NetworkHealth FindNextTabTarget()
    {
        _tabBuffer.Clear();
        Vector2 origin = transform.position;
        float range = Mathf.Max(tabTargetRange, meleeRange);
        IReadOnlyList<EnemyAI> enemies = EnemyRegistry.Alive;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null || !enemy.IsSpawned)
                continue;

            NetworkHealth health = enemy.Health;
            if (health == null || health.IsDead || health.NetworkObject == null)
                continue;
            if (health.NetworkObject == NetworkObject)
                continue;

            float dist = Vector2.Distance(origin, enemy.transform.position);
            if (dist > range)
                continue;

            _tabBuffer.Add((health, dist));
        }

        if (_tabBuffer.Count == 0)
            return null;

        _tabBuffer.Sort((a, b) => a.dist.CompareTo(b.dist));

        ulong currentId = _targetNetworkObjectId.Value;
        int currentIndex = -1;
        if (currentId != 0)
        {
            for (int i = 0; i < _tabBuffer.Count; i++)
            {
                if (_tabBuffer[i].health.NetworkObject.NetworkObjectId == currentId)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % _tabBuffer.Count;
        return _tabBuffer[nextIndex].health;
    }

    private NetworkHealth FindClickTarget(Vector3 worldPoint)
    {
        NetworkHealth best = null;
        float bestDist = clickPickRadius;

        IReadOnlyList<EnemyAI> enemies = EnemyRegistry.Alive;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyAI enemy = enemies[i];
            if (enemy == null || !enemy.IsSpawned)
                continue;

            NetworkHealth health = enemy.Health;
            if (health == null || health.IsDead || health.NetworkObject == null)
                continue;
            if (health.NetworkObject == NetworkObject)
                continue;

            float dist = Vector2.Distance(worldPoint, enemy.transform.position);
            if (dist <= bestDist)
            {
                bestDist = dist;
                best = health;
            }
        }

        return best;
    }

    private void ServerCombatTick()
    {
        if (_respawning || _health == null || _health.IsDead)
        {
            InterruptCastServer();
            return;
        }

        if (_playerClass != null && !_playerClass.HasSelectedClass)
            return;

        if (_isCasting)
        {
            ServerUpdateCast();
            return;
        }

        if (!_autoAttackActive || _targetNetworkObjectId.Value == 0)
            return;

        if (!TryGetCurrentTarget(out NetworkObject targetObject, out NetworkHealth targetHealth))
        {
            ClearTargetServer();
            return;
        }

        if (targetHealth.IsDead)
        {
            ClearTargetServer();
            return;
        }

        float dist = Vector2.Distance(transform.position, targetObject.transform.position);
        if (dist > meleeRange)
            return;

        _swingTimer -= Time.deltaTime;
        if (_swingTimer > 0f)
            return;

        ResolveAutoAttackHit(targetHealth);
    }

    public void ServerActivateReflect(float durationSeconds = WarriorClass.ReflectDuration)
    {
        if (!IsServer)
            return;
        _reflectEndsAtServer = Time.time + Mathf.Max(0.1f, durationSeconds);
        AbilityEffectClientRpc("Reflect!", durationSeconds);
    }

    public void ServerActivateDamageAmp(float durationSeconds = MageClass.DamageAmpDuration, float multiplier = MageClass.DamageAmpMultiplier)
    {
        if (!IsServer)
            return;
        _damageAmpEndsAtServer = Time.time + Mathf.Max(0.1f, durationSeconds);
        _damageAmpMultiplier = Mathf.Max(1f, multiplier);
        AbilityEffectClientRpc("Triple Damage!", durationSeconds);
    }

    public bool ServerBeginSkillCast(string skillName, float castTime, float range)
    {
        if (!IsServer || !IsSpawned)
            return false;
        if (_respawning || _health == null || _health.IsDead)
            return false;

        InterruptCastServer();

        _isCasting = true;
        _castIsSkill = true;
        _castDuration = Mathf.Max(0.05f, castTime);
        _castRemaining = _castDuration;
        _castSpellName = skillName;
        _castRange = range;
        _castStartPosition = transform.position;
        CastStartedClientRpc(_castSpellName, _castDuration);
        return true;
    }

    public bool HasActiveReflect => IsServer && Time.time < _reflectEndsAtServer;

    public void ServerTryReflectDamage(int damageTaken, NetworkHealth attacker)
    {
        if (!IsServer || !HasActiveReflect || attacker == null || attacker.IsDead || damageTaken <= 0)
            return;
        if (attacker == _health)
            return;

        EnemyAI enemy = attacker.GetComponent<EnemyAI>();
        bool isEnemy = enemy != null;
        int hpBefore = attacker.CurrentHealth;
        int dealt = attacker.ApplyDamage(damageTaken, _health, isReflected: true);
        if (isEnemy && dealt > 0 && hpBefore - dealt <= 0)
            ServerCreditKill(enemy.IsBoss);
    }

    private float GetAutoAttackSwingTime()
    {
        return Mathf.Max(0.05f, swingTime);
    }

    private float GetDamageMultiplier()
    {
        if (IsServer && Time.time < _damageAmpEndsAtServer)
            return Mathf.Max(1f, _damageAmpMultiplier);
        return 1f;
    }

    public int ScaleDamage(int damage)
    {
        if (damage <= 0)
            return 0;
        float mult = GetDamageMultiplier();
        if (mult <= 1.001f)
            return damage;
        return Mathf.Max(1, Mathf.RoundToInt(damage * mult));
    }

    private void ServerUpdateCast()
    {
        if (IsMovingEnoughToBlockCast() ||
            Vector2.Distance(transform.position, _castStartPosition) > castMoveInterruptDistance)
        {
            InterruptCastServer();
            _swingTimer = 0.15f;
            return;
        }

        if (!TryGetCurrentTarget(out NetworkObject targetObject, out NetworkHealth targetHealth) || targetHealth.IsDead)
        {
            InterruptCastServer();
            return;
        }

        float maxRange = _castIsSkill ? _castRange : meleeRange;
        float dist = Vector2.Distance(transform.position, targetObject.transform.position);
        if (dist > maxRange)
        {
            InterruptCastServer();
            return;
        }

        _castRemaining -= Time.deltaTime;
        if (_castRemaining > 0f)
            return;

        bool skill = _castIsSkill;
        _isCasting = false;
        _castIsSkill = false;
        CastFinishedClientRpc();

        if (skill)
        {
            PlayerSkills skills = GetComponent<PlayerSkills>();
            skills?.ServerCompleteSkillCast();
            return;
        }

        ResolveAutoAttackHit(targetHealth);
    }

    private bool IsMovingEnoughToBlockCast()
    {
        if (_player == null)
            _player = GetComponent<Player>();

        if (_player != null && _player.IsTryingToMove)
            return true;

        return false;
    }

    private void ResolveAutoAttackHit(NetworkHealth targetHealth)
    {
        _swingTimer = GetAutoAttackSwingTime();

        PlayAutoAttackAnimationClientRpc();

        int aaDamage = autoAttackDamage;
        if (_gearStats == null)
            _gearStats = GetComponent<PlayerGearStats>();
        if (_gearStats != null)
            aaDamage += _gearStats.BonusAutoAttackDamage;

        aaDamage = ScaleDamage(aaDamage);
        int dealt = ApplyDamageWithSplash(targetHealth, aaDamage);
        TryApplyLifeSteal(dealt);

        if (!TryGetCurrentTarget(out _, out NetworkHealth th) || th == null || th.IsDead)
            ClearTargetServer();
    }

    [ClientRpc]
    private void PlayAutoAttackAnimationClientRpc()
    {
        if (_classAnimation == null)
            _classAnimation = GetComponent<PlayerClassAnimation>();
        _classAnimation?.PlayAutoAttack();

        PlayerClassType type = _playerClass != null ? _playerClass.CurrentClass : PlayerClassType.None;
        GameSfx.PlayPlayerAutoAttack(type);
    }

    private void ServerCreditKill(bool bossKill)
    {
        if (!IsServer)
            return;
        PlayerQuest quest = GetComponent<PlayerQuest>();
        if (quest == null)
            return;

        quest.ServerNotifyEnemyKill();
        if (bossKill)
            quest.ServerNotifyBossKill();
    }

    [ClientRpc]
    private void AbilityEffectClientRpc(string label, float duration)
    {
        if (!IsOwner)
            return;
        FloatingChatText.Show(transform, label, Mathf.Min(2f, duration));
        ChatUI.AddSystem($"{label} ({duration:0}s)");
    }

    private void TryApplyLifeSteal(int damageDealt)
    {
        if (!IsServer || damageDealt <= 0 || _health == null || _health.IsDead)
            return;

        if (_playerClass == null || !_playerClass.HasSelectedClass)
            return;

        if (!ClassDefinition.TryGet(_playerClass.CurrentClass, out ClassDefinition.Data data))
            return;

        if (data.LifeStealPercent <= 0f)
            return;

        int heal = Mathf.Max(1, Mathf.RoundToInt(damageDealt * data.LifeStealPercent));
        _health.ApplyHeal(heal);
    }

    public bool TryGetCurrentTarget(out NetworkObject targetObject, out NetworkHealth targetHealth)
    {
        targetObject = null;
        targetHealth = null;

        ulong id = _targetNetworkObjectId.Value;
        if (id == 0 || NetworkManager == null || NetworkManager.SpawnManager == null)
            return false;

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out targetObject) || targetObject == null)
            return false;

        targetHealth = targetObject.GetComponent<NetworkHealth>();
        return targetHealth != null;
    }

    [ServerRpc]
    private void SetTargetServerRpc(NetworkObjectReference targetRef)
    {
        if (_playerClass != null && !_playerClass.HasSelectedClass)
            return;

        if (_respawning || _health == null || _health.IsDead)
            return;

        if (!targetRef.TryGet(out NetworkObject targetObject) || targetObject == null)
            return;

        if (targetObject == NetworkObject)
            return;

        NetworkHealth targetHealth = targetObject.GetComponent<NetworkHealth>();
        if (targetHealth == null || targetHealth.IsDead)
            return;

        if (targetObject.GetComponent<EnemyAI>() == null)
            return;

        if (_isCasting)
            InterruptCastServer();

        _targetNetworkObjectId.Value = targetObject.NetworkObjectId;
        _autoAttackActive = true;
        _swingTimer = 0f;
    }

    [ServerRpc]
    private void ClearTargetServerRpc()
    {
        ClearTargetServer();
    }

    private void ClearTargetServer()
    {
        if (!IsServer)
            return;

        if (_isCasting)
            InterruptCastServer();

        _targetNetworkObjectId.Value = 0;
        _autoAttackActive = false;
        _swingTimer = 0f;
    }

    [ClientRpc]
    private void CastStartedClientRpc(string spellName, float duration)
    {
        if (!IsOwner)
            return;

        CombatCastBarUI.EnsureExists().BeginCast(spellName, duration);
    }

    [ClientRpc]
    private void CastFinishedClientRpc()
    {
        if (!IsOwner)
            return;

        CombatCastBarUI.EnsureExists().Complete();
    }

    [ClientRpc]
    private void CastInterruptedClientRpc()
    {
        if (!IsOwner)
            return;

        CombatCastBarUI.EnsureExists().Interrupt();
    }

    private void HandleTargetIdChanged(ulong previous, ulong current)
    {
        if (current == 0)
            ClearLocalTargetHighlight();
        else
            RefreshLocalTargetHighlight(current);
    }

    private void HandleDeath(NetworkHealth _)
    {
        if (!IsServer || _respawning)
            return;

        ClearTargetServer();
        StartCoroutine(ServerRespawnRoutine());
    }

    private IEnumerator ServerRespawnRoutine()
    {
        _respawning = true;
        NotifyDeathClientRpc();

        yield return new WaitForSeconds(respawnDelay);

        if (!IsSpawned)
            yield break;

        transform.position = respawnPosition;
        _health.FullHeal();
        _respawning = false;
        NotifyRespawnClientRpc();
    }

    [ClientRpc]
    private void NotifyDeathClientRpc()
    {
        _respawning = true;
        ClearLocalTargetHighlight();
        GameSfx.PlayPlayerDeath();
        if (IsOwner)
        {
            if (CombatCastBarUI.Instance != null)
                CombatCastBarUI.Instance.Hide();
            ChatUI.AddSystem("You died. Respawning...");
        }
    }

    [ClientRpc]
    private void NotifyRespawnClientRpc()
    {
        _respawning = false;
        if (IsOwner)
            ChatUI.AddSystem("You respawned.");
    }

    private void RefreshLocalTargetHighlight(ulong targetId)
    {
        ClearLocalTargetHighlight();

        if (targetId == 0 || NetworkManager == null || NetworkManager.SpawnManager == null)
            return;

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject target) || target == null)
            return;

        _highlightedTargetId = targetId;
        _localTargetHighlight = target.GetComponentInChildren<SpriteRenderer>();
        if (_localTargetHighlight == null)
            return;

        _localTargetOriginalColor = _localTargetHighlight.color;
        _localTargetHighlight.color = new Color(1f, 0.92f, 0.2f, 1f);
    }

    private void EnsureHighlightColor()
    {
        if (_localTargetHighlight == null || _highlightedTargetId == 0)
            return;

        Color c = _localTargetHighlight.color;
        if (c.r < 0.95f || c.g < 0.85f)
            _localTargetHighlight.color = new Color(1f, 0.92f, 0.2f, 1f);
    }

    private void ClearLocalTargetHighlight()
    {
        if (_localTargetHighlight != null)
        {
            NetworkHealth health = _localTargetHighlight.GetComponentInParent<NetworkHealth>();
            if (health != null && health.IsSpawned)
                _localTargetHighlight.color = _localTargetOriginalColor;
        }

        _localTargetHighlight = null;
        _highlightedTargetId = 0;
    }
}

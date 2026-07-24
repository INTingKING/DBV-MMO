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
    [SerializeField] private float clickPickRadius = 1.5f;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private Vector3 respawnPosition = new Vector3(0f, 0f, -10f);
    [SerializeField] private float castMoveInterruptDistance = 0.05f;

    private readonly NetworkVariable<ulong> _targetNetworkObjectId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkHealth _health;
    private PlayerClass _playerClass;
    private float _swingTimer;
    private bool _autoAttackActive;
    private bool _respawning;

    private bool _isCasting;
    private float _castRemaining;
    private float _castDuration;
    private string _castSpellName;
    private Vector3 _castStartPosition;

    private float _reflectEndsAtServer;
    private float _castHasteEndsAtServer;
    private const float AbilityEffectDuration = 2f;
    private const float MageCastHasteFactor = 5f;

    private SpriteRenderer _localTargetHighlight;
    private Color _localTargetOriginalColor;
    private ulong _highlightedTargetId;

    private readonly List<NetworkHealth> _splashBuffer = new List<NetworkHealth>(4);

    public NetworkHealth Health => _health;
    public bool IsRespawning => _respawning;
    public bool HasTarget => _targetNetworkObjectId.Value != 0;
    public float MeleeRange => meleeRange;
    public bool IsCasting => _isCasting;
    public int SplashExtraTargets => splashExtraTargets;
    public float SplashRadius => splashRadius;

    public void ApplyClassCombatStats(
        float range,
        float swing,
        int damage,
        float castTime,
        string attackName,
        int splashExtra,
        float splashRad)
    {
        meleeRange = range;
        swingTime = swing;
        autoAttackDamage = damage;
        autoAttackCastTime = castTime;
        autoAttackName = string.IsNullOrEmpty(attackName) ? "Auto Attack" : attackName;
        splashExtraTargets = splashExtra;
        splashRadius = splashRad;
    }

    public bool TryGetCurrentTarget(out NetworkObject targetObject, out NetworkHealth targetHealth)
    {
        return TryGetTarget(out targetObject, out targetHealth);
    }

    public int ApplyDamageWithSplash(NetworkHealth primary, int damage)
    {
        if (!IsServer || primary == null || damage <= 0)
            return 0;

        Vector2 splashOrigin = primary.transform.position;
        NetworkHealth primaryRef = primary;

        int total = DamageAndMaybeCreditKill(primary, damage);

        CollectSplashTargetsAt(splashOrigin, primaryRef, splashRadius, splashExtraTargets, _splashBuffer);
        for (int i = 0; i < _splashBuffer.Count; i++)
            total += DamageAndMaybeCreditKill(_splashBuffer[i], damage);

        return total;
    }

    private int DamageAndMaybeCreditKill(NetworkHealth target, int damage)
    {
        if (target == null || target.IsDead || damage <= 0)
            return 0;

        bool isEnemy = target.GetComponent<EnemyAI>() != null;
        int hpBefore = target.CurrentHealth;
        int dealt = target.ApplyDamage(damage);

        if (isEnemy && dealt > 0 && hpBefore - dealt <= 0)
            ServerCreditKill();

        return dealt;
    }

    private static void CollectSplashTargetsAt(
        Vector2 origin,
        NetworkHealth primary,
        float radius,
        int maxExtra,
        List<NetworkHealth> results)
    {
        results.Clear();
        if (maxExtra <= 0 || radius <= 0f)
            return;

        var candidates = new List<(NetworkHealth health, float dist)>();

        foreach (NetworkHealth health in FindObjectsByType<NetworkHealth>(FindObjectsSortMode.None))
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

    public void InterruptCastServer(string reason = null)
    {
        if (!IsServer || !_isCasting)
            return;

        _isCasting = false;
        _castRemaining = 0f;
        CastInterruptedClientRpc();
    }

    public override void OnNetworkSpawn()
    {
        _health = GetComponent<NetworkHealth>();
        _playerClass = GetComponent<PlayerClass>();
        if (_health != null)
            _health.Died += HandleDeath;

        _targetNetworkObjectId.OnValueChanged += HandleTargetIdChanged;

        _swingTimer = 0f;
        _autoAttackActive = false;
        _respawning = false;
        _isCasting = false;

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
        if (ChatUI.Instance != null && ChatUI.Instance.IsOpen)
            return;

        if (_playerClass != null && !_playerClass.HasSelectedClass)
            return;

        if (_respawning || (_health != null && _health.IsDead))
            return;

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

        RefreshLocalTargetHighlight(best.NetworkObject.NetworkObjectId);
        SetTargetServerRpc(new NetworkObjectReference(best.NetworkObject));
    }

    private NetworkHealth FindClickTarget(Vector3 worldPoint)
    {
        NetworkHealth best = null;
        float bestDist = clickPickRadius;

        foreach (NetworkHealth health in FindObjectsByType<NetworkHealth>(FindObjectsSortMode.None))
        {
            if (health == null || health.IsDead || health.NetworkObject == null)
                continue;
            if (health.NetworkObject == NetworkObject)
                continue;
            if (health.GetComponent<EnemyAI>() == null)
                continue;

            float dist = Vector2.Distance(worldPoint, health.transform.position);
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

        if (!TryGetTarget(out NetworkObject targetObject, out NetworkHealth targetHealth))
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

        if (GetAutoAttackCastTime() > 0.01f)
            BeginAutoAttackCast();
        else
            ResolveAutoAttackHit(targetHealth);
    }

    public void ServerActivateReflect(float durationSeconds = AbilityEffectDuration)
    {
        if (!IsServer)
            return;
        _reflectEndsAtServer = Time.time + Mathf.Max(0.1f, durationSeconds);
        AbilityEffectClientRpc("Reflect!", durationSeconds);
    }

    public void ServerActivateCastHaste(float durationSeconds = AbilityEffectDuration, float factor = MageCastHasteFactor)
    {
        if (!IsServer)
            return;
        _castHasteEndsAtServer = Time.time + Mathf.Max(0.1f, durationSeconds);
        AbilityEffectClientRpc("Arcane Haste!", durationSeconds);
    }

    public bool HasActiveReflect => IsServer && Time.time < _reflectEndsAtServer;

    public void ServerTryReflectDamage(int damageTaken, NetworkHealth attacker)
    {
        if (!IsServer || !HasActiveReflect || attacker == null || attacker.IsDead || damageTaken <= 0)
            return;
        if (attacker == _health)
            return;

        bool isEnemy = attacker.GetComponent<EnemyAI>() != null;
        int hpBefore = attacker.CurrentHealth;
        int dealt = attacker.ApplyDamage(damageTaken, _health, isReflected: true);
        if (isEnemy && dealt > 0 && hpBefore - dealt <= 0)
            ServerCreditKill();
    }

    private float GetAutoAttackCastTime()
    {
        float cast = autoAttackCastTime;
        if (cast <= 0.01f)
            return 0f;

        if (IsServer && Time.time < _castHasteEndsAtServer)
            cast /= MageCastHasteFactor;

        return Mathf.Max(0.05f, cast);
    }

    private float GetAutoAttackSwingTime()
    {
        float s = swingTime;
        if (IsServer && Time.time < _castHasteEndsAtServer)
            s /= MageCastHasteFactor;
        return Mathf.Max(0.05f, s);
    }

    private void BeginAutoAttackCast()
    {
        float cast = GetAutoAttackCastTime();
        _isCasting = true;
        _castDuration = cast;
        _castRemaining = cast;
        _castSpellName = autoAttackName;
        _castStartPosition = transform.position;
        CastStartedClientRpc(_castSpellName, _castDuration);
    }

    private void ServerUpdateCast()
    {

        if (Vector2.Distance(transform.position, _castStartPosition) > castMoveInterruptDistance)
        {
            InterruptCastServer();
            _swingTimer = 0.25f;
            return;
        }

        if (!TryGetTarget(out NetworkObject targetObject, out NetworkHealth targetHealth) || targetHealth.IsDead)
        {
            InterruptCastServer();
            return;
        }

        float dist = Vector2.Distance(transform.position, targetObject.transform.position);
        if (dist > meleeRange)
        {
            InterruptCastServer();
            return;
        }

        _castRemaining -= Time.deltaTime;
        if (_castRemaining > 0f)
            return;

        _isCasting = false;
        CastFinishedClientRpc();
        ResolveAutoAttackHit(targetHealth);
    }

    private void ResolveAutoAttackHit(NetworkHealth targetHealth)
    {
        _swingTimer = GetAutoAttackSwingTime();
        int dealt = ApplyDamageWithSplash(targetHealth, autoAttackDamage);
        TryApplyLifeSteal(dealt);

        if (!TryGetTarget(out _, out NetworkHealth th) || th == null || th.IsDead)
            ClearTargetServer();
    }

    private void ServerCreditKill()
    {
        if (!IsServer)
            return;
        PlayerQuest quest = GetComponent<PlayerQuest>();
        quest?.ServerNotifyEnemyKill();
    }

    [ClientRpc]
    private void AbilityEffectClientRpc(string label, float duration)
    {
        if (!IsOwner)
            return;
        FloatingChatText.Show(transform, label, Mathf.Min(2f, duration));
        if (ChatUI.Instance != null)
            ChatUI.Instance.AddMessage($"System: {label} ({duration:0}s)");
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

    private bool TryGetTarget(out NetworkObject targetObject, out NetworkHealth targetHealth)
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
        if (IsOwner)
        {
            if (CombatCastBarUI.Instance != null)
                CombatCastBarUI.Instance.Hide();
            if (ChatUI.Instance != null)
                ChatUI.Instance.AddMessage("System: You died. Respawning...");
        }
    }

    [ClientRpc]
    private void NotifyRespawnClientRpc()
    {
        _respawning = false;
        if (IsOwner && ChatUI.Instance != null)
            ChatUI.Instance.AddMessage("System: You respawned.");
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

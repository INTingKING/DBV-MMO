using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSkills : NetworkBehaviour
{
    private PlayerClass _playerClass;
    private PlayerCombat _combat;
    private NetworkHealth _health;

    private float _cooldownRemaining;
    private float _cooldownDuration;

    public float CooldownRemaining => _cooldownRemaining;
    public float CooldownDuration => _cooldownDuration;
    public bool IsOnCooldown => _cooldownRemaining > 0f;

    public string CurrentSkillName
    {
        get
        {
            if (_playerClass == null || !_playerClass.HasSelectedClass)
                return "—";
            return ClassDefinition.TryGet(_playerClass.CurrentClass, out ClassDefinition.Data data)
                ? data.SkillName
                : "—";
        }
    }

    public override void OnNetworkSpawn()
    {
        _playerClass = GetComponent<PlayerClass>();
        _combat = GetComponent<PlayerCombat>();
        _health = GetComponent<NetworkHealth>();
        _cooldownRemaining = 0f;
        _cooldownDuration = 0f;
    }

    private void Update()
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.deltaTime);

        if (!IsSpawned || !IsOwner)
            return;

        if (_playerClass == null || !_playerClass.HasSelectedClass)
            return;

        if (ChatUI.Instance != null && ChatUI.Instance.IsOpen)
            return;

        if (_combat != null && _combat.IsRespawning)
            return;

        if (_health != null && _health.IsDead)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            TryCastSkill();
    }

    private void TryCastSkill()
    {
        if (IsOnCooldown)
        {
            NotifyOwner($"System: {CurrentSkillName} on cooldown ({_cooldownRemaining:0.0}s)");
            return;
        }

        CastSkillServerRpc();
    }

    [ServerRpc]
    private void CastSkillServerRpc()
    {
        if (_playerClass == null || !_playerClass.HasSelectedClass)
            return;

        if (_combat != null && _combat.IsRespawning)
            return;

        if (_health != null && _health.IsDead)
            return;

        if (!ClassDefinition.TryGet(_playerClass.CurrentClass, out ClassDefinition.Data data))
            return;

        if (_cooldownRemaining > 0f)
            return;

        if (_combat == null || !_combat.TryGetCurrentTarget(out NetworkObject targetObject, out NetworkHealth targetHealth))
        {
            NotifyOwnerClientRpc("System: No target.");
            return;
        }

        if (targetHealth.IsDead || targetObject.GetComponent<EnemyAI>() == null)
        {
            NotifyOwnerClientRpc("System: Invalid target.");
            return;
        }

        float dist = Vector2.Distance(transform.position, targetObject.transform.position);
        if (dist > data.SkillRange)
        {
            NotifyOwnerClientRpc("System: Out of range.");
            return;
        }

        _combat.InterruptCastServer();

        int dealt = _combat.ApplyDamageWithSplash(targetHealth, data.SkillDamage);
        if (data.LifeStealPercent > 0f && _health != null && !_health.IsDead && dealt > 0)
        {
            int heal = Mathf.Max(1, Mathf.RoundToInt(dealt * data.LifeStealPercent));
            _health.ApplyHeal(heal);
        }

        PlayerQuest quest = GetComponent<PlayerQuest>();
        if (quest != null && quest.HasAbilityUpgrade)
        {
            if (_playerClass.CurrentClass == PlayerClassType.Warrior)
                _combat.ServerActivateReflect(2f);
            else if (_playerClass.CurrentClass == PlayerClassType.Mage)
                _combat.ServerActivateCastHaste(2f, 5f);
        }

        _cooldownRemaining = data.SkillCooldown;
        _cooldownDuration = data.SkillCooldown;

        SkillCastClientRpc(data.SkillName, OwnerClientId);
        BeginCooldownClientRpc(data.SkillCooldown);
    }

    [ClientRpc]
    private void BeginCooldownClientRpc(float duration)
    {
        _cooldownDuration = duration;
        _cooldownRemaining = duration;
    }

    [ClientRpc]
    private void SkillCastClientRpc(string skillName, ulong casterClientId)
    {
        foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (player != null && player.IsSpawned && player.OwnerClientId == casterClientId)
            {
                FloatingChatText.Show(player.transform, skillName + "!", 1.4f);
                break;
            }
        }
    }

    [ClientRpc]
    private void NotifyOwnerClientRpc(string message)
    {
        if (!IsOwner)
            return;
        NotifyOwner(message);
    }

    private static void NotifyOwner(string message)
    {
        if (ChatUI.Instance != null)
            ChatUI.Instance.AddMessage(message);
    }
}

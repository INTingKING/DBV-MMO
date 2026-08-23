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

        if (!GameplayInput.CanOwnerAct(IsSpawned, IsOwner, _playerClass, _combat, _health))
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (_combat != null && _combat.IsCasting)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            TryCastSkill();
    }

    private void TryCastSkill()
    {
        if (IsOnCooldown)
        {
            NotifyOwner($"{CurrentSkillName} on cooldown ({_cooldownRemaining:0.0}s)");
            return;
        }

        CastSkillServerRpc();
    }

    [ServerRpc]
    private void CastSkillServerRpc()
    {
        if (!GameplayInput.CanAct(_playerClass, _combat, _health))
            return;

        if (!ClassDefinition.TryGet(_playerClass.CurrentClass, out ClassDefinition.Data data))
            return;

        if (_cooldownRemaining > 0f)
            return;

        if (_combat != null && _combat.IsCasting)
            return;

        if (!TryGetSkillTarget(data.SkillRange, out NetworkHealth targetHealth, out string fail))
        {
            NotifyOwnerClientRpc(fail);
            return;
        }

        if (data.SkillCastTime > 0.01f)
        {
            if (_combat == null)
                return;
            _combat.ServerBeginSkillCast(data.SkillName, data.SkillCastTime, data.SkillRange);
            return;
        }

        ResolveSkill(data, targetHealth);
    }

    public void ServerCompleteSkillCast()
    {
        if (!IsServer || !IsSpawned)
            return;
        if (!GameplayInput.CanAct(_playerClass, _combat, _health))
            return;
        if (!ClassDefinition.TryGet(_playerClass.CurrentClass, out ClassDefinition.Data data))
            return;
        if (!TryGetSkillTarget(data.SkillRange, out NetworkHealth targetHealth, out _))
            return;

        ResolveSkill(data, targetHealth);
    }

    private bool TryGetSkillTarget(float skillRange, out NetworkHealth targetHealth, out string fail)
    {
        targetHealth = null;
        fail = "No target.";

        if (_combat == null || !_combat.TryGetCurrentTarget(out NetworkObject targetObject, out targetHealth))
            return false;

        if (targetHealth.IsDead || targetObject.GetComponent<EnemyAI>() == null)
        {
            fail = "Invalid target.";
            return false;
        }

        float dist = Vector2.Distance(transform.position, targetObject.transform.position);
        if (dist > skillRange)
        {
            fail = "Out of range.";
            return false;
        }

        return true;
    }

    private void ResolveSkill(ClassDefinition.Data data, NetworkHealth targetHealth)
    {
        int skillDamage = data.SkillDamage;
        PlayerGearStats gear = GetComponent<PlayerGearStats>();
        if (gear != null)
            skillDamage += gear.BonusSkillDamage;

        if (_combat == null)
            return;

        skillDamage = _combat.ScaleDamage(skillDamage);
        int dealt = _combat.ApplyDamageWithSplash(targetHealth, skillDamage);

        PlayerQuest quest = GetComponent<PlayerQuest>();
        bool hasUpgrade = quest != null && quest.HasAbilityUpgrade;
        ClassDefinition.ApplySkillEffects(_playerClass.CurrentClass, _combat, _health, dealt, hasUpgrade);

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
        Transform caster = NetworkPlayers.FindTransform(casterClientId);
        if (caster == null)
            return;

        FloatingChatText.Show(caster, skillName + "!", 1.4f);
        caster.GetComponent<PlayerClassAnimation>()?.PlaySkill();

        PlayerClass playerClass = caster.GetComponent<PlayerClass>();
        GameSfx.PlayPlayerSkill(playerClass != null ? playerClass.CurrentClass : PlayerClassType.None);
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
        ChatUI.AddSystem(message);
    }
}

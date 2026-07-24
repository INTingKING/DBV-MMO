using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerClass : NetworkBehaviour
{
    private readonly NetworkVariable<byte> _classType = new NetworkVariable<byte>(
        (byte)PlayerClassType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkHealth _health;
    private PlayerCombat _combat;
    private SpriteRenderer _bodyRenderer;

    public PlayerClassType CurrentClass => (PlayerClassType)_classType.Value;
    public bool HasSelectedClass => CurrentClass != PlayerClassType.None;

    public event Action<PlayerClassType> ClassChanged;

    public override void OnNetworkSpawn()
    {
        _health = GetComponent<NetworkHealth>();
        _combat = GetComponent<PlayerCombat>();
        _bodyRenderer = GetComponentInChildren<SpriteRenderer>();

        _classType.OnValueChanged += HandleClassChanged;

        if (HasSelectedClass)
            ApplyLocalPresentation(CurrentClass);

        if (IsOwner)
            ClassSelectUI.EnsureExists().Bind(this);
    }

    public override void OnNetworkDespawn()
    {
        _classType.OnValueChanged -= HandleClassChanged;

        if (IsOwner)
            ClassSelectUI.EnsureExists().Unbind(this);
    }

    public void RequestSelectClass(PlayerClassType type)
    {
        if (!IsOwner || !IsSpawned)
            return;

        if (type == PlayerClassType.None)
            return;

        if (HasSelectedClass)
            return;

        SelectClassServerRpc((byte)type);
    }

    [ServerRpc]
    private void SelectClassServerRpc(byte classByte)
    {
        if (HasSelectedClass)
            return;

        PlayerClassType type = (PlayerClassType)classByte;
        if (!ClassDefinition.TryGet(type, out ClassDefinition.Data data))
            return;

        _classType.Value = classByte;
        ApplyServerStats(data);
    }

    private void ApplyServerStats(ClassDefinition.Data data)
    {
        if (!IsServer)
            return;

        if (_health != null)
        {
            _health.SetBaseColor(data.BodyColor);
            _health.SetMaxHealth(data.MaxHealth, healToFull: true);
        }

        if (_combat != null)
            ApplyCombatStats(data);
    }

    private void HandleClassChanged(byte previous, byte current)
    {
        PlayerClassType type = (PlayerClassType)current;
        ApplyLocalPresentation(type);
        ClassChanged?.Invoke(type);

        if (IsOwner && type != PlayerClassType.None && ChatUI.Instance != null &&
            ClassDefinition.TryGet(type, out ClassDefinition.Data data))
        {
            string extra = data.AutoAttackCastTime > 0f
                ? $" AA casts ({data.AutoAttackCastTime:0.0}s)."
                : ".";
            ChatUI.Instance.AddMessage(
                $"System: You are a {data.DisplayName}. Press 1 for instant {data.SkillName}.{extra}");
        }
    }

    private void ApplyLocalPresentation(PlayerClassType type)
    {
        if (!ClassDefinition.TryGet(type, out ClassDefinition.Data data))
            return;

        if (_bodyRenderer == null)
            _bodyRenderer = GetComponentInChildren<SpriteRenderer>();

        if (_health != null)
            _health.SetBaseColor(data.BodyColor);
        else if (_bodyRenderer != null)
            _bodyRenderer.color = data.BodyColor;

        if (_combat != null)
            ApplyCombatStats(data);
    }

    private void ApplyCombatStats(ClassDefinition.Data data)
    {
        _combat.ApplyClassCombatStats(
            data.AutoAttackRange,
            data.AutoAttackSwingTime,
            data.AutoAttackDamage,
            data.AutoAttackCastTime,
            data.AutoAttackName,
            data.SplashExtraTargets,
            data.SplashRadius);
    }
}

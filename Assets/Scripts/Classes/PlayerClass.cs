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
    private PlayerClassAnimation _classAnimation;

    public PlayerClassType CurrentClass => (PlayerClassType)_classType.Value;
    public bool HasSelectedClass => CurrentClass != PlayerClassType.None;

    public event Action<PlayerClassType> ClassChanged;

    public override void OnNetworkSpawn()
    {
        _health = GetComponent<NetworkHealth>();
        _combat = GetComponent<PlayerCombat>();
        _bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        _classAnimation = GetComponent<PlayerClassAnimation>();

        _classType.OnValueChanged += HandleClassChanged;

        if (HasSelectedClass)
            ApplyLocalPresentation(CurrentClass);

        // Attach class picker directly on the local player's GameObject.
        if (IsOwnedByLocalClient())
            ClassSelectUI.EnsureOnPlayer(this);
    }

    public override void OnNetworkDespawn()
    {
        _classType.OnValueChanged -= HandleClassChanged;

        ClassSelectUI ui = GetComponent<ClassSelectUI>();
        if (ui != null)
            ui.Unbind(this);
    }

    private bool IsOwnedByLocalClient()
    {
        if (IsOwner)
            return true;
        if (NetworkManager == null)
            return false;
        return OwnerClientId == NetworkManager.LocalClientId;
    }

    public void RequestSelectClass(PlayerClassType type)
    {
        if (!IsSpawned)
        {
            Debug.LogWarning("[PlayerClass] RequestSelectClass ignored — not spawned.");
            return;
        }

        if (type == PlayerClassType.None || HasSelectedClass)
            return;

        if (!IsOwnedByLocalClient() && !IsServer)
        {
            Debug.LogWarning("[PlayerClass] RequestSelectClass ignored — not local player.");
            return;
        }

        if (IsServer)
        {
            ApplyClassOnServer(type);
            return;
        }

        SelectClassServerRpc((byte)type);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SelectClassServerRpc(byte classByte, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        ApplyClassOnServer((PlayerClassType)classByte);
    }

    private void ApplyClassOnServer(PlayerClassType type)
    {
        if (!IsServer || !IsSpawned)
            return;

        if (HasSelectedClass)
            return;

        if (type == PlayerClassType.None)
            return;

        if (!ClassDefinition.TryGet(type, out ClassDefinition.Data data))
        {
            Debug.LogError($"[PlayerClass] Unknown class type {type}");
            return;
        }

        _classType.Value = (byte)type;
        ApplyServerStats(data);

        // Ensure host UI updates even if NV callback is weird this frame.
        ApplyLocalPresentation(type);
        ClassChanged?.Invoke(type);

        Debug.Log($"[PlayerClass] Class set to {type} for OwnerClientId={OwnerClientId}");
    }

    private void ApplyServerStats(ClassDefinition.Data data)
    {
        if (!IsServer)
            return;

        if (_health != null)
        {
            _health.SetBaseColor(Color.white);
            int bonusHp = 0;
            PlayerGearStats gear = GetComponent<PlayerGearStats>();
            if (gear != null)
                bonusHp = gear.BonusMaxHp;
            _health.SetMaxHealth(data.MaxHealth + bonusHp, healToFull: true);
        }

        if (_combat != null)
            ApplyCombatStats(data);
    }

    private void HandleClassChanged(byte previous, byte current)
    {
        PlayerClassType type = (PlayerClassType)current;
        ApplyLocalPresentation(type);
        ClassChanged?.Invoke(type);

        if (IsOwnedByLocalClient() && type != PlayerClassType.None && ChatUI.Instance != null &&
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

        if (_classAnimation == null)
            _classAnimation = GetComponent<PlayerClassAnimation>();

        if (_health != null)
            _health.SetBaseColor(Color.white);
        if (_bodyRenderer != null)
            _bodyRenderer.color = Color.white;

        if (_classAnimation != null)
            _classAnimation.ApplyForClass(type);

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

using Unity.Netcode;
using UnityEngine;

public class PlayerQuest : NetworkBehaviour
{
    public enum QuestState : byte
    {
        None = 0,
        Active = 1,
        ReadyToTurnIn = 2,
        Completed = 3
    }

    public const int RequiredKills = 10;
    public const string NpcName = "Captain Renn";

    private readonly NetworkVariable<byte> _state = new NetworkVariable<byte>(
        (byte)QuestState.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _killCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> _upgradeUnlocked = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public QuestState State => (QuestState)_state.Value;
    public int KillCount => _killCount.Value;
    public bool UpgradeUnlocked => _upgradeUnlocked.Value;
    public bool HasAbilityUpgrade => _upgradeUnlocked.Value;

    public string GetNpcPrompt()
    {
        switch (State)
        {
            case QuestState.None:
                return $"[E] Talk to {NpcName}";
            case QuestState.Active:
                return $"[E] Talk to {NpcName} ({KillCount}/{RequiredKills})";
            case QuestState.ReadyToTurnIn:
                return $"[E] Report to {NpcName}";
            case QuestState.Completed:
                return $"[E] Talk to {NpcName}";
            default:
                return $"[E] Talk to {NpcName}";
        }
    }

    public void GetDialogue(out string body, out string primaryButton, out bool canAccept, out bool canTurnIn)
    {
        canAccept = false;
        canTurnIn = false;

        switch (State)
        {
            case QuestState.None:
                body =
                    $"Greetings, adventurer.\n\n" +
                    $"The wilds press hard against our hub. Prove your steel:\n" +
                    $"slay {RequiredKills} enemies beyond the safe ground, then return to me.\n\n" +
                    $"Do this, and I will teach you a deeper use of your class power.";
                primaryButton = "Accept quest";
                canAccept = true;
                break;

            case QuestState.Active:
                body =
                    $"The work is not yet done.\n\n" +
                    $"Progress: {KillCount} / {RequiredKills} enemies slain.\n\n" +
                    $"Return when the count is complete.";
                primaryButton = "Understood";
                break;

            case QuestState.ReadyToTurnIn:
                body =
                    $"You return bloodied and proven.\n\n" +
                    $"You have slain {KillCount} foes. As promised, I unlock a greater form of your ability.";
                primaryButton = "Claim upgrade";
                canTurnIn = true;
                break;

            case QuestState.Completed:
            default:
                body =
                    $"You already carry my blessing, warrior of the hub.\n\n" +
                    $"Use your enhanced ability wisely.";
                primaryButton = "Farewell";
                break;
        }
    }

    [ServerRpc]
    public void AcceptQuestServerRpc()
    {

        if (!IsSpawned)
            return;
        if (State != QuestState.None)
            return;

        PlayerClass pc = GetComponent<PlayerClass>();
        if (pc == null || !pc.HasSelectedClass)
        {
            QuestMessageClientRpc("Choose a class before accepting the quest.");
            return;
        }

        _state.Value = (byte)QuestState.Active;
        _killCount.Value = 0;
        QuestMessageClientRpc($"Quest accepted: Defeat {RequiredKills} enemies, then return to {NpcName}.");
    }

    [ServerRpc]
    public void TurnInQuestServerRpc()
    {
        if (!IsSpawned)
            return;
        if (State != QuestState.ReadyToTurnIn)
            return;

        _state.Value = (byte)QuestState.Completed;
        _upgradeUnlocked.Value = true;
        QuestMessageClientRpc(GetUpgradeUnlockMessage());
        FloatingUpgradeClientRpc();
    }

    public void ServerNotifyEnemyKill()
    {
        if (!IsServer || !IsSpawned)
            return;

        if (State != QuestState.Active)
        {

            return;
        }

        int next = Mathf.Min(RequiredKills, _killCount.Value + 1);
        _killCount.Value = next;
        QuestMessageClientRpc($"Quest: {next}/{RequiredKills} kills.");

        if (next >= RequiredKills)
        {
            _state.Value = (byte)QuestState.ReadyToTurnIn;
            QuestMessageClientRpc($"Objective complete! Report back to {NpcName}.");
        }
    }

    private string GetUpgradeUnlockMessage()
    {
        PlayerClass pc = GetComponent<PlayerClass>();
        PlayerClassType type = pc != null ? pc.CurrentClass : PlayerClassType.None;
        return ClassDefinition.GetUpgradeUnlockMessage(type);
    }

    [ClientRpc]
    private void QuestMessageClientRpc(string message)
    {
        if (!IsOwner)
            return;
        ChatUI.AddSystem(message);
    }

    [ClientRpc]
    private void FloatingUpgradeClientRpc()
    {
        if (!IsOwner)
            return;
        FloatingChatText.Show(transform, "Upgrade!", 2.5f);
    }
}

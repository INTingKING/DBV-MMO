using UnityEngine;

public class QuestNpcInteractable : WorldInteractable
{
    public const string TilemapTag = "QuestNpc";

    public void Setup()
    {
        Configure("quest_npc", $"[E] Talk to {PlayerQuest.NpcName}", TilemapTag);
    }

    public override string GetPromptFor(Player player)
    {
        if (player == null)
            return Prompt;

        PlayerQuest quest = player.GetComponent<PlayerQuest>();
        if (quest == null)
            return Prompt;

        return quest.GetNpcPrompt();
    }

    public override bool ServerExecute(Player player)
    {

        if (player == null || !player.IsSpawned)
            return false;

        PlayerClass pc = player.GetComponent<PlayerClass>();
        if (pc == null || !pc.HasSelectedClass)
            return false;

        return player.GetComponent<PlayerQuest>() != null;
    }

    public override void ClientOnSuccess(Player player)
    {
        if (player == null)
            return;

        PlayerQuest quest = player.GetComponent<PlayerQuest>();
        if (quest == null)
            return;

        NpcDialogueUI.EnsureExists().Open(quest);
    }
}

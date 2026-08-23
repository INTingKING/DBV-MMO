public static class GameplayInput
{
    public static bool IsUiBlocking =>
        GameOptionsUI.IsOpen || ChatUI.IsChatOpen;

    public static bool CanAct(PlayerClass playerClass, PlayerCombat combat, NetworkHealth health)
    {
        if (playerClass == null || !playerClass.HasSelectedClass)
            return false;
        if (combat != null && combat.IsRespawning)
            return false;
        if (health != null && health.IsDead)
            return false;
        return true;
    }

    public static bool CanOwnerAct(
        bool isSpawned,
        bool isOwner,
        PlayerClass playerClass,
        PlayerCombat combat,
        NetworkHealth health)
    {
        return isSpawned && isOwner && !IsUiBlocking && CanAct(playerClass, combat, health);
    }
}

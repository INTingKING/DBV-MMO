using UnityEngine;

public static class WarriorClass
{
    public const PlayerClassType Type = PlayerClassType.Warrior;
    public const string DisplayName = "Warrior";
    public const string SkillName = "Slam";
    public const string AutoAttackName = "Auto Attack";
    public const string Blurb = "Lifesteal. Quest unlocks Slam Reflect";
    public const string UpgradeUnlockMessage = "Upgrade unlocked: Slam now grants 2s Reflect!";

    public const int MaxHealth = 70;
    public const int AutoAttackDamage = 6;
    public const float AutoAttackSwingTime = 1.5f;
    public const float AutoAttackRange = 1.75f;
    public const float AutoAttackCastTime = 0f;
    public const int SkillDamage = 18;
    public const float SkillRange = 1.9f;
    public const float SkillCooldown = 6f;
    public const int SplashExtraTargets = 0;
    public const float SplashRadius = 0f;
    public const float LifeStealPercent = 0.30f;
    public const float ReflectDuration = 2f;

    public static Color BodyColor => new Color(0.15f, 0.35f, 0.95f, 1f);

    public static ClassDefinition.Data GetData()
    {
        return new ClassDefinition.Data(
            displayName: DisplayName,
            skillName: SkillName,
            autoAttackName: AutoAttackName,
            maxHealth: MaxHealth,
            autoAttackDamage: AutoAttackDamage,
            autoAttackSwingTime: AutoAttackSwingTime,
            autoAttackRange: AutoAttackRange,
            autoAttackCastTime: AutoAttackCastTime,
            skillDamage: SkillDamage,
            skillRange: SkillRange,
            skillCooldown: SkillCooldown,
            splashExtraTargets: SplashExtraTargets,
            splashRadius: SplashRadius,
            lifeStealPercent: LifeStealPercent,
            bodyColor: BodyColor,
            blurb: Blurb);
    }

    public static void ApplySkillEffects(
        PlayerCombat combat,
        NetworkHealth self,
        int damageDealt,
        bool hasAbilityUpgrade)
    {
        if (combat == null)
            return;

        if (LifeStealPercent > 0f && self != null && !self.IsDead && damageDealt > 0)
        {
            int heal = Mathf.Max(1, Mathf.RoundToInt(damageDealt * LifeStealPercent));
            self.ApplyHeal(heal);
        }

        if (hasAbilityUpgrade)
            combat.ServerActivateReflect(ReflectDuration);
    }
}

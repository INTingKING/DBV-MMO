using UnityEngine;

public static class MageClass
{
    public const PlayerClassType Type = PlayerClassType.Mage;
    public const string DisplayName = "Mage";
    public const string SkillName = "Firebolt";
    public const string AutoAttackName = "Arcane Bolt";
    public const string Blurb = "Splash AA. Quest unlocks Firebolt triple damage";
    public const string UpgradeUnlockMessage = "Upgrade unlocked: Firebolt now grants 2s of 3× damage!";

    public const int MaxHealth = 45;
    public const int AutoAttackDamage = 7;
    public const float AutoAttackSwingTime = 0.33f;
    public const float AutoAttackRange = 5.5f;
    public const float AutoAttackCastTime = 0f;
    public const int SkillDamage = 22;
    public const float SkillRange = 7f;
    public const float SkillCastTime = 0.35f;
    public const float SkillCooldown = 3.3f;
    public const int SplashExtraTargets = 2;
    public const float SplashRadius = 2.5f;
    public const float LifeStealPercent = 0f;
    public const float DamageAmpDuration = 2f;
    public const float DamageAmpMultiplier = 3f;

    public static Color BodyColor => new Color(0.65f, 0.25f, 0.9f, 1f);

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
            skillCastTime: SkillCastTime,
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

        if (hasAbilityUpgrade)
            combat.ServerActivateDamageAmp(DamageAmpDuration, DamageAmpMultiplier);
    }
}

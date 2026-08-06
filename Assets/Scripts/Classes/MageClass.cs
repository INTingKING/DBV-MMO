using UnityEngine;

public static class MageClass
{
    public const PlayerClassType Type = PlayerClassType.Mage;
    public const string DisplayName = "Mage";
    public const string SkillName = "Firebolt";
    public const string AutoAttackName = "Arcane Bolt";
    public const string Blurb = "Splash AA. Quest unlocks Firebolt haste";
    public const string UpgradeUnlockMessage = "Upgrade unlocked: Firebolt now grants 2s of 5× cast haste!";

    public const int MaxHealth = 45;
    public const int AutoAttackDamage = 4;
    public const float AutoAttackSwingTime = 0.5f;
    public const float AutoAttackRange = 5.5f;
    public const float AutoAttackCastTime = 0.85f;
    public const int SkillDamage = 16;
    public const float SkillRange = 7f;
    public const float SkillCooldown = 5f;
    public const int SplashExtraTargets = 2;
    public const float SplashRadius = 2.5f;
    public const float LifeStealPercent = 0f;
    public const float CastHasteDuration = 2f;
    public const float CastHasteFactor = 5f;

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
            combat.ServerActivateCastHaste(CastHasteDuration, CastHasteFactor);
    }
}

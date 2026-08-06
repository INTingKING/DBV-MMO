using UnityEngine;

public static class ClassDefinition
{
    public readonly struct Data
    {
        public readonly string DisplayName;
        public readonly string SkillName;
        public readonly string AutoAttackName;
        public readonly int MaxHealth;
        public readonly int AutoAttackDamage;
        public readonly float AutoAttackSwingTime;
        public readonly float AutoAttackRange;
        public readonly float AutoAttackCastTime;
        public readonly int SkillDamage;
        public readonly float SkillRange;
        public readonly float SkillCooldown;
        public readonly int SplashExtraTargets;
        public readonly float SplashRadius;
        public readonly float LifeStealPercent;
        public readonly Color BodyColor;
        public readonly string Blurb;

        public Data(
            string displayName,
            string skillName,
            string autoAttackName,
            int maxHealth,
            int autoAttackDamage,
            float autoAttackSwingTime,
            float autoAttackRange,
            float autoAttackCastTime,
            int skillDamage,
            float skillRange,
            float skillCooldown,
            int splashExtraTargets,
            float splashRadius,
            float lifeStealPercent,
            Color bodyColor,
            string blurb)
        {
            DisplayName = displayName;
            SkillName = skillName;
            AutoAttackName = autoAttackName;
            MaxHealth = maxHealth;
            AutoAttackDamage = autoAttackDamage;
            AutoAttackSwingTime = autoAttackSwingTime;
            AutoAttackRange = autoAttackRange;
            AutoAttackCastTime = autoAttackCastTime;
            SkillDamage = skillDamage;
            SkillRange = skillRange;
            SkillCooldown = skillCooldown;
            SplashExtraTargets = splashExtraTargets;
            SplashRadius = splashRadius;
            LifeStealPercent = lifeStealPercent;
            BodyColor = bodyColor;
            Blurb = blurb;
        }
    }

    public static bool TryGet(PlayerClassType type, out Data data)
    {
        switch (type)
        {
            case PlayerClassType.Warrior:
                data = WarriorClass.GetData();
                return true;
            case PlayerClassType.Mage:
                data = MageClass.GetData();
                return true;
            default:
                data = default;
                return false;
        }
    }

    public static void ApplySkillEffects(
        PlayerClassType type,
        PlayerCombat combat,
        NetworkHealth self,
        int damageDealt,
        bool hasAbilityUpgrade)
    {
        switch (type)
        {
            case PlayerClassType.Warrior:
                WarriorClass.ApplySkillEffects(combat, self, damageDealt, hasAbilityUpgrade);
                break;
            case PlayerClassType.Mage:
                MageClass.ApplySkillEffects(combat, self, damageDealt, hasAbilityUpgrade);
                break;
        }
    }

    public static string GetUpgradeUnlockMessage(PlayerClassType type)
    {
        switch (type)
        {
            case PlayerClassType.Warrior:
                return WarriorClass.UpgradeUnlockMessage;
            case PlayerClassType.Mage:
                return MageClass.UpgradeUnlockMessage;
            default:
                return "Upgrade unlocked: your class ability is enhanced!";
        }
    }
}

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
                data = new Data(
                    displayName: "Warrior",
                    skillName: "Slam",
                    autoAttackName: "Auto Attack",
                    maxHealth: 70,
                    autoAttackDamage: 6,
                    autoAttackSwingTime: 1.5f,
                    autoAttackRange: 1.75f,
                    autoAttackCastTime: 0f,
                    skillDamage: 18,
                    skillRange: 1.9f,
                    skillCooldown: 6f,
                    splashExtraTargets: 0,
                    splashRadius: 0f,
                    lifeStealPercent: 0.30f,
                    bodyColor: new Color(0.15f, 0.35f, 0.95f, 1f),
                    blurb: "Lifesteal. Quest unlocks Slam Reflect");
                return true;

            case PlayerClassType.Mage:
                data = new Data(
                    displayName: "Mage",
                    skillName: "Firebolt",
                    autoAttackName: "Arcane Bolt",
                    maxHealth: 45,
                    autoAttackDamage: 4,
                    autoAttackSwingTime: 0.5f,
                    autoAttackRange: 5.5f,
                    autoAttackCastTime: 0.85f,
                    skillDamage: 16,
                    skillRange: 7f,
                    skillCooldown: 5f,
                    splashExtraTargets: 2,
                    splashRadius: 2.5f,
                    lifeStealPercent: 0f,
                    bodyColor: new Color(0.65f, 0.25f, 0.9f, 1f),
                    blurb: "Splash AA. Quest unlocks Firebolt haste");
                return true;

            default:
                data = default;
                return false;
        }
    }
}

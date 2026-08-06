using Unity.Netcode;
using UnityEngine;

public class PlayerGearStats : NetworkBehaviour
{
    private readonly NetworkVariable<int> _bonusMaxHp = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _bonusAutoAttack = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> _bonusSkill = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> _bonusArmor = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int BonusMaxHp => _bonusMaxHp.Value;
    public int BonusAutoAttackDamage => _bonusAutoAttack.Value;
    public int BonusSkillDamage => _bonusSkill.Value;
    public float BonusArmorPercent => Mathf.Clamp01(_bonusArmor.Value);

    public void ServerSetBonuses(int maxHp, int aa, int skill, float armor)
    {
        if (!IsServer)
            return;

        _bonusMaxHp.Value = Mathf.Max(0, maxHp);
        _bonusAutoAttack.Value = Mathf.Max(0, aa);
        _bonusSkill.Value = Mathf.Max(0, skill);
        _bonusArmor.Value = Mathf.Clamp(armor, 0f, 0.5f);
    }

    public void ServerClear()
    {
        ServerSetBonuses(0, 0, 0, 0f);
    }
}

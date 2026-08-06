using System;
using UnityEngine;

[Serializable]
public class ClassAnimationSet
{
    [Tooltip("Idle frames — multi-select sprites (e.g. Knight_Idle / Wizard_Idle) and drag here.")]
    public Sprite[] idleSprites;

    [Tooltip("Walk frames — multi-select sprites (e.g. Knight_Walk / Wizard_Walk) and drag here.")]
    public Sprite[] moveSprites;

    [Tooltip("Auto-attack frames — multi-select sprites (e.g. Knight_Attack) and drag here.")]
    public Sprite[] attackSprites;

    [Tooltip("Ability [1] frames — multi-select sprites for Slam / Firebolt and drag here.")]
    public Sprite[] skillSprites;

    [Min(0.1f)]
    public float idleFramesPerSecond = 6f;

    [Min(0.1f)]
    public float walkFramesPerSecond = 8f;

    [Min(0.1f)]
    public float attackFramesPerSecond = 12f;

    [Min(0.1f)]
    public float skillFramesPerSecond = 12f;

    public bool HasAnyVisual =>
        (idleSprites != null && idleSprites.Length > 0) ||
        (moveSprites != null && moveSprites.Length > 0) ||
        (attackSprites != null && attackSprites.Length > 0) ||
        (skillSprites != null && skillSprites.Length > 0);
}

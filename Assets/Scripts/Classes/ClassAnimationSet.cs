using System;
using UnityEngine;

[Serializable]
public class ClassAnimationSet
{
    public Texture2D idleSheet;
    public Texture2D walkSheet;
    public Texture2D attackSheet;
    public Texture2D skillSheet;

    [HideInInspector] public Sprite[] idleSprites;
    [HideInInspector] public Sprite[] moveSprites;
    [HideInInspector] public Sprite[] attackSprites;
    [HideInInspector] public Sprite[] skillSprites;

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

    public void FillFromSheets()
    {
        Sprite[] idle = SpriteSheetFrames.LoadSorted(idleSheet);
        if (idle != null)
            idleSprites = idle;

        Sprite[] walk = SpriteSheetFrames.LoadSorted(walkSheet);
        if (walk != null)
            moveSprites = walk;

        Sprite[] attack = SpriteSheetFrames.LoadSorted(attackSheet);
        if (attack != null)
            attackSprites = attack;

        Sprite[] skill = SpriteSheetFrames.LoadSorted(skillSheet);
        if (skill != null)
            skillSprites = skill;
    }
}

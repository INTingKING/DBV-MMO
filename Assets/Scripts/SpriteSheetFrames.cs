using UnityEngine;

public static class SpriteSheetFrames
{
    public static Sprite[] LoadSorted(Object sheet)
    {
#if UNITY_EDITOR
        if (sheet == null)
            return null;

        string path = UnityEditor.AssetDatabase.GetAssetPath(sheet);
        if (string.IsNullOrEmpty(path))
            return null;

        Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets == null || assets.Length == 0)
            return null;

        var sprites = new System.Collections.Generic.List<Sprite>(assets.Length);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                sprites.Add(sprite);
        }

        if (sprites.Count == 0)
            return null;

        sprites.Sort(CompareSprites);
        return sprites.ToArray();
#else
        return null;
#endif
    }

    private static int CompareSprites(Sprite a, Sprite b)
    {
        int na = TrailingNumber(a.name);
        int nb = TrailingNumber(b.name);
        int byNumber = na.CompareTo(nb);
        if (byNumber != 0)
            return byNumber;

        int byY = b.rect.y.CompareTo(a.rect.y);
        if (byY != 0)
            return byY;

        return a.rect.x.CompareTo(b.rect.x);
    }

    private static int TrailingNumber(string name)
    {
        if (string.IsNullOrEmpty(name))
            return 0;

        int end = name.Length - 1;
        if (end < 0 || name[end] < '0' || name[end] > '9')
            return 0;

        int start = end;
        while (start > 0 && name[start - 1] >= '0' && name[start - 1] <= '9')
            start--;

        int value = 0;
        for (int i = start; i <= end; i++)
            value = value * 10 + (name[i] - '0');
        return value;
    }
}

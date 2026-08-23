using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameSettings
{
    private const string PrefMasterVolume = "dbv.settings.masterVolume";
    private const string PrefMusicVolume = "dbv.settings.musicVolume";
    private const string PrefSfxVolume = "dbv.settings.sfxVolume";
    private const string PrefFullscreen = "dbv.settings.fullscreen";
    private const string PrefWidth = "dbv.settings.width";
    private const string PrefHeight = "dbv.settings.height";
    private const string PrefRefresh = "dbv.settings.refresh";

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 1f;
    public static float SfxVolume { get; private set; } = 1f;
    public static bool Fullscreen { get; private set; } = true;
    public static int ResolutionWidth { get; private set; }
    public static int ResolutionHeight { get; private set; }
    public static int ResolutionRefresh { get; private set; }

    public static string ControlsHelpText =>
        "WASD — Move\n" +
        "Left Mouse — Auto-attack (sticky target)\n" +
        "Tab — Target nearest / next enemy\n" +
        "Right Mouse — Clear target / drop bag item\n" +
        "1 — Class skill (mage casts Firebolt)\n" +
        "E — Interact / pick up loot\n" +
        "I / B / C — Inventory & equipment\n" +
        "Enter — Chat\n" +
        "Esc — Options / close panels";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Load();
        Apply(save: false);
    }

    public static void Load()
    {
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefMasterVolume, 1f));
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefMusicVolume, 1f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefSfxVolume, 1f));
        Fullscreen = PlayerPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) != 0;

        ResolutionWidth = PlayerPrefs.GetInt(PrefWidth, Screen.currentResolution.width);
        ResolutionHeight = PlayerPrefs.GetInt(PrefHeight, Screen.currentResolution.height);
        ResolutionRefresh = PlayerPrefs.GetInt(PrefRefresh, (int)Screen.currentResolution.refreshRateRatio.numerator);

        if (ResolutionWidth <= 0 || ResolutionHeight <= 0)
        {
            ResolutionWidth = Screen.currentResolution.width;
            ResolutionHeight = Screen.currentResolution.height;
            ResolutionRefresh = (int)Screen.currentResolution.refreshRateRatio.numerator;
        }
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(PrefMasterVolume, MasterVolume);
        PlayerPrefs.SetFloat(PrefMusicVolume, MusicVolume);
        PlayerPrefs.SetFloat(PrefSfxVolume, SfxVolume);
        PlayerPrefs.SetInt(PrefFullscreen, Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(PrefWidth, ResolutionWidth);
        PlayerPrefs.SetInt(PrefHeight, ResolutionHeight);
        PlayerPrefs.SetInt(PrefRefresh, ResolutionRefresh);
        PlayerPrefs.Save();
    }

    public static void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        AudioListener.volume = MasterVolume;
    }

    public static void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
    }

    public static void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
    }

    public static void SetFullscreen(bool value)
    {
        Fullscreen = value;
    }

    public static void SetResolution(int width, int height, int refreshRate)
    {
        if (width <= 0 || height <= 0)
            return;

        ResolutionWidth = width;
        ResolutionHeight = height;
        ResolutionRefresh = Mathf.Max(0, refreshRate);
    }

    public static void Apply(bool save = true)
    {
        AudioListener.volume = MasterVolume;

        RefreshRate rate = ResolutionRefresh > 0
            ? new RefreshRate { numerator = (uint)ResolutionRefresh, denominator = 1u }
            : Screen.currentResolution.refreshRateRatio;

        Screen.SetResolution(ResolutionWidth, ResolutionHeight, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed, rate);

        if (save)
            Save();
    }

    public static List<ResolutionOption> GetUniqueResolutions()
    {
        Resolution[] all = Screen.resolutions;
        var list = new List<ResolutionOption>();
        var seen = new HashSet<string>();

        for (int i = all.Length - 1; i >= 0; i--)
        {
            Resolution r = all[i];
            int refresh = (int)r.refreshRateRatio.numerator;
            string key = $"{r.width}x{r.height}@{refresh}";
            if (!seen.Add(key))
                continue;

            list.Add(new ResolutionOption(r.width, r.height, refresh));
        }

        if (list.Count == 0)
            list.Add(new ResolutionOption(Screen.width, Screen.height, (int)Screen.currentResolution.refreshRateRatio.numerator));

        return list;
    }

    public static int FindResolutionIndex(List<ResolutionOption> options)
    {
        if (options == null || options.Count == 0)
            return 0;

        for (int i = 0; i < options.Count; i++)
        {
            ResolutionOption o = options[i];
            if (o.Width == ResolutionWidth && o.Height == ResolutionHeight &&
                (ResolutionRefresh <= 0 || o.RefreshRate == ResolutionRefresh))
                return i;
        }

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].Width == ResolutionWidth && options[i].Height == ResolutionHeight)
                return i;
        }

        return 0;
    }

    public readonly struct ResolutionOption
    {
        public readonly int Width;
        public readonly int Height;
        public readonly int RefreshRate;

        public ResolutionOption(int width, int height, int refreshRate)
        {
            Width = width;
            Height = height;
            RefreshRate = refreshRate;
        }

        public string Label => RefreshRate > 0
            ? $"{Width} x {Height} ({RefreshRate} Hz)"
            : $"{Width} x {Height}";
    }
}

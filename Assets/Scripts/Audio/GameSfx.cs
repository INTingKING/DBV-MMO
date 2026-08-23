using System.Collections.Generic;
using UnityEngine;

public static class GameSfx
{
    public const float BaseVolume = 0.48f;

    private const int SampleRate = 22050;

    private static AudioSource[] _voices;
    private static int _voiceIndex;
    private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

    public static void PlayPlayerAutoAttack(PlayerClassType type)
    {
        if (type == PlayerClassType.Mage)
            Play("mage_aa", 0.96f, 1.06f);
        else
            Play("warrior_aa", 0.92f, 1.08f);
    }

    public static void PlayPlayerSkill(PlayerClassType type)
    {
        if (type == PlayerClassType.Mage)
            Play("mage_skill", 0.97f, 1.04f);
        else
            Play("warrior_skill", 0.94f, 1.05f);
    }

    public static void PlayEnemyAttack()
    {
        Play("enemy_aa", 0.90f, 1.10f);
    }

    public static void PlayPlayerDeath()
    {
        Play("player_death", 0.97f, 1.03f);
    }

    public static void PlayEnemyDeath()
    {
        Play("enemy_death", 0.92f, 1.08f);
    }

    public static void PlayLootDrop()
    {
        Play("loot_drop", 0.95f, 1.06f);
    }

    public static void PlayLootPickup()
    {
        Play("loot_pickup", 0.97f, 1.04f);
    }

    private static void Play(string id, float pitchMin, float pitchMax)
    {
        if (Application.isBatchMode)
            return;

        AudioClip clip = GetClip(id);
        AudioSource source = NextVoice();
        if (clip == null || source == null)
            return;

        source.pitch = Random.Range(pitchMin, pitchMax);
        source.PlayOneShot(clip, BaseVolume * GameSettings.SfxVolume);
    }

    private static AudioClip GetClip(string id)
    {
        if (Clips.TryGetValue(id, out AudioClip cached) && cached != null)
            return cached;

        AudioClip clip = id switch
        {
            "warrior_aa" => BuildWarriorAutoAttack(),
            "mage_aa" => BuildMageAutoAttack(),
            "warrior_skill" => BuildWarriorSlam(),
            "mage_skill" => BuildMageFirebolt(),
            "enemy_aa" => BuildEnemyAttack(),
            "player_death" => BuildPlayerDeath(),
            "enemy_death" => BuildEnemyDeath(),
            "loot_drop" => BuildLootDrop(),
            "loot_pickup" => BuildLootPickup(),
            _ => null
        };

        if (clip != null)
            Clips[id] = clip;
        return clip;
    }

    private static AudioSource NextVoice()
    {
        EnsureVoices();
        if (_voices == null || _voices.Length == 0)
            return null;

        _voiceIndex = (_voiceIndex + 1) % _voices.Length;
        return _voices[_voiceIndex];
    }

    private static void EnsureVoices()
    {
        if (_voices != null)
        {
            bool alive = true;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (_voices[i] == null)
                {
                    alive = false;
                    break;
                }
            }
            if (alive)
                return;
        }

        GameObject host = GameObject.Find("GameSfx");
        if (host == null)
        {
            host = new GameObject("GameSfx");
            Object.DontDestroyOnLoad(host);
        }

        _voices = new AudioSource[6];
        AudioSource[] existing = host.GetComponents<AudioSource>();
        for (int i = 0; i < _voices.Length; i++)
        {
            _voices[i] = i < existing.Length ? existing[i] : host.AddComponent<AudioSource>();
            _voices[i].playOnAwake = false;
            _voices[i].loop = false;
            _voices[i].spatialBlend = 0f;
            _voices[i].priority = 64;
        }
    }

    private static AudioClip BuildWarriorAutoAttack()
    {
        int n = Samples(0.11f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = Decay(t, 0.09f);
            float hz = Mathf.Lerp(240f, 130f, t / 0.11f);
            float slash = Pulse(hz, t, 0.28f) * 0.72f;
            float grit = Noise() * Decay(t, 0.045f) * 0.35f;
            data[i] = (slash + grit) * env;
        }
        return Clip("warrior_aa", data);
    }

    private static AudioClip BuildMageAutoAttack()
    {
        int n = Samples(0.16f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = Decay(t, 0.14f);
            float a = Sine(Mathf.Lerp(740f, 1180f, t / 0.16f), t);
            float b = Sine(Mathf.Lerp(1180f, 880f, t / 0.16f), t) * 0.45f;
            data[i] = (a + b) * env * 0.7f;
        }
        return Clip("mage_aa", data);
    }

    private static AudioClip BuildWarriorSlam()
    {
        int n = Samples(0.22f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = Decay(t, 0.18f);
            float thud = Triangle(Mathf.Lerp(92f, 46f, t / 0.22f), t) * 0.85f;
            float crack = Noise() * Decay(t, 0.06f) * 0.4f;
            float click = Pulse(180f, t, 0.2f) * Decay(t, 0.04f) * 0.25f;
            data[i] = (thud + crack + click) * env;
        }
        return Clip("warrior_slam", data);
    }

    private static AudioClip BuildMageFirebolt()
    {
        int n = Samples(0.20f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = Decay(t, 0.17f);
            float whoosh = Noise() * Decay(t, 0.07f) * 0.28f;
            float bolt = Pulse(Mathf.Lerp(620f, 260f, t / 0.20f), t, 0.22f) * 0.55f;
            float spark = Sine(Mathf.Lerp(1400f, 700f, t / 0.20f), t) * 0.35f;
            data[i] = (whoosh + bolt + spark) * env;
        }
        return Clip("mage_firebolt", data);
    }

    private static AudioClip BuildPlayerDeath()
    {
        int n = Samples(0.38f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = Decay(t, 0.34f);
            float fall = Pulse(Mathf.Lerp(220f, 70f, t / 0.38f), t, 0.32f) * 0.55f;
            float low = Triangle(Mathf.Lerp(110f, 40f, t / 0.38f), t) * 0.5f;
            float air = Noise() * Decay(t, 0.12f) * 0.22f;
            data[i] = (fall + low + air) * env;
        }
        return Clip("player_death", data);
    }

    private static AudioClip BuildEnemyDeath()
    {
        int n = Samples(0.18f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = Decay(t, 0.15f);
            float crumple = Pulse(Mathf.Lerp(150f, 75f, t / 0.18f), t, 0.4f) * 0.5f;
            float dust = Noise() * Decay(t, 0.08f) * 0.4f;
            data[i] = (crumple + dust) * env;
        }
        return Clip("enemy_death", data);
    }

    private static AudioClip BuildLootDrop()
    {
        int n = Samples(0.16f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float plop = Triangle(Mathf.Lerp(420f, 180f, t / 0.08f), t) * Decay(t, 0.09f) * 0.55f;
            float clink = Sine(880f, t) * Decay(Mathf.Max(0f, t - 0.05f), 0.07f) * 0.4f;
            data[i] = plop + clink;
        }
        return Clip("loot_drop", data);
    }

    private static AudioClip BuildLootPickup()
    {
        int n = Samples(0.18f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float a = Sine(660f, t) * Decay(t, 0.07f);
            float b = Sine(880f, t) * Decay(Mathf.Max(0f, t - 0.04f), 0.08f);
            float c = Sine(1320f, t) * Decay(Mathf.Max(0f, t - 0.08f), 0.09f);
            data[i] = (a * 0.45f + b * 0.4f + c * 0.35f);
        }
        return Clip("loot_pickup", data);
    }

    private static AudioClip BuildEnemyAttack()
    {
        int n = Samples(0.10f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = Decay(t, 0.08f);
            float hit = Pulse(Mathf.Lerp(170f, 95f, t / 0.10f), t, 0.4f) * 0.55f;
            float body = Noise() * Decay(t, 0.05f) * 0.38f;
            data[i] = (hit + body) * env;
        }
        return Clip("enemy_aa", data);
    }

    private static AudioClip Clip(string name, float[] data)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++)
        {
            float a = Mathf.Abs(data[i]);
            if (a > peak)
                peak = a;
        }

        float scale = 0.85f / peak;
        for (int i = 0; i < data.Length; i++)
            data[i] *= scale;

        AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static int Samples(float seconds)
    {
        return Mathf.Max(8, Mathf.RoundToInt(seconds * SampleRate));
    }

    private static float Decay(float t, float life)
    {
        if (t <= 0f)
            return 1f;
        if (t >= life)
            return 0f;
        float x = 1f - t / life;
        return x * x;
    }

    private static float Sine(float hz, float t)
    {
        return Mathf.Sin(2f * Mathf.PI * hz * t);
    }

    private static float Pulse(float hz, float t, float duty)
    {
        float p = Frac(hz * t);
        return p < duty ? 1f : -1f;
    }

    private static float Triangle(float hz, float t)
    {
        float p = Frac(hz * t);
        return p < 0.5f ? p * 4f - 1f : 3f - p * 4f;
    }

    private static float Noise()
    {
        return Random.Range(-1f, 1f);
    }

    private static float Frac(float x)
    {
        return x - Mathf.Floor(x);
    }
}

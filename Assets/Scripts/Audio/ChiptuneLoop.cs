using System;
using UnityEngine;

public static class ChiptuneLoop
{
    private const int SampleRate = 44100;
    private const int BeatsPerBar = 4;

    public static AudioClip BuildOverworld()
    {
        return BuildSong(
            "OverworldLoop",
            120f,
            Repeat(OverworldChordRoot),
            Repeat(OverworldChordThird),
            Repeat(OverworldChordFifth),
            Repeat(OverworldBass),
            RepeatMelody(OverworldMelody, 16),
            padScale: 0.32f,
            leadGain: 0.20f,
            arpGain: 0.055f);
    }

    public static AudioClip BuildTitle()
    {
        return BuildSong(
            "TitleLoop",
            102f,
            Repeat(TitleChordRoot),
            Repeat(TitleChordThird),
            Repeat(TitleChordFifth),
            Repeat(TitleBass),
            RepeatMelody(TitleMelody, 16),
            padScale: 0.40f,
            leadGain: 0.18f,
            arpGain: 0.07f);
    }

    public static AudioClip BuildCombat()
    {
        return BuildSong(
            "CombatLoop",
            156f,
            Repeat(CombatChordRoot, 3),
            Repeat(CombatChordThird, 3),
            Repeat(CombatChordFifth, 3),
            Repeat(CombatBass, 3),
            RepeatMelody(CombatMelody, 8, 3),
            padScale: 0.22f,
            leadGain: 0.20f,
            arpGain: 0.06f);
    }

    public static AudioClip BuildRickroll()
    {
        return BuildSong(
            "RickrollChorus",
            120f,
            Repeat(RickChordRoot),
            Repeat(RickChordThird),
            Repeat(RickChordFifth),
            Repeat(RickBass),
            RepeatMelody(RickMelody, 16),
            padScale: 0.32f,
            leadGain: 0.22f,
            arpGain: 0.055f);
    }

    private static AudioClip BuildSong(
        string name,
        float bpm,
        int[] chordRoot,
        int[] chordThird,
        int[] chordFifth,
        int[] bassRoot,
        float[] melody,
        float padScale,
        float leadGain,
        float arpGain)
    {
        int bars = chordRoot.Length;
        int samplesPerBeat = Mathf.RoundToInt(SampleRate * 60f / bpm);
        int length = samplesPerBeat * bars * BeatsPerBar;

        float[] left = new float[length];
        float[] right = new float[length];

        RenderPad(left, right, samplesPerBeat, bars, chordRoot, chordThird, chordFifth, padScale);
        RenderWalkingBass(left, right, samplesPerBeat, bars, bassRoot);
        RenderArpeggio(left, right, samplesPerBeat, bars, chordRoot, chordThird, chordFifth, arpGain);
        RenderMelody(left, right, samplesPerBeat, melody, bpm, leadGain);
        Normalize(left, right, 0.72f);
        CrossfadeLoop(left, right, SampleRate / 40);

        float[] interleaved = new float[length * 2];
        for (int i = 0; i < length; i++)
        {
            interleaved[i * 2] = left[i];
            interleaved[i * 2 + 1] = right[i];
        }

        AudioClip clip = AudioClip.Create(name, length, 2, SampleRate, false);
        clip.SetData(interleaved, 0);
        return clip;
    }

    private static int[] Repeat(int[] source, int times = 2)
    {
        int[] result = new int[source.Length * times];
        for (int t = 0; t < times; t++)
            Array.Copy(source, 0, result, t * source.Length, source.Length);
        return result;
    }

    private static float[] RepeatMelody(float[] source, int bars, int times = 2)
    {
        float[] result = new float[source.Length * times];
        float beatSpan = bars * BeatsPerBar;
        for (int t = 0; t < times; t++)
        {
            int offset = t * source.Length;
            float beatAdd = t * beatSpan;
            for (int i = 0; i < source.Length; i += 3)
            {
                result[offset + i] = source[i];
                result[offset + i + 1] = source[i + 1] + beatAdd;
                result[offset + i + 2] = source[i + 2];
            }
        }
        return result;
    }

    // Terraria-day bounce: G G C D | G C D G | G Em Am D | C G D G
    private static readonly int[] OverworldChordRoot = { 55, 55, 48, 50, 55, 48, 50, 55, 55, 52, 45, 50, 48, 55, 50, 55 };
    private static readonly int[] OverworldChordThird = { 59, 59, 52, 54, 59, 52, 54, 59, 59, 55, 48, 54, 52, 59, 54, 59 };
    private static readonly int[] OverworldChordFifth = { 62, 62, 55, 57, 62, 55, 57, 62, 62, 59, 52, 57, 55, 62, 57, 62 };
    private static readonly int[] OverworldBass = { 43, 43, 36, 38, 43, 36, 38, 43, 43, 40, 33, 38, 36, 43, 38, 43 };

    private static readonly float[] OverworldMelody =
    {
        62, 0, 0.5f, 64, 0.5f, 0.5f, 66, 1, 1, 62, 2, 1, 64, 3, 1,
        66, 4, 1, 69, 5, 1, 66, 6, 0.5f, 64, 6.5f, 0.5f, 62, 7, 1,
        60, 8, 1, 64, 9, 1, 67, 10, 1, 64, 11, 1,
        66, 12, 2, 62, 14, 2,
        62, 16, 0.5f, 64, 16.5f, 0.5f, 66, 17, 1, 69, 18, 1, 66, 19, 1,
        64, 20, 1, 62, 21, 1, 60, 22, 2,
        62, 24, 1, 66, 25, 1, 64, 26, 1, 62, 27, 1,
        59, 28, 2, 62, 30, 2,
        62, 32, 1, 66, 33, 0.5f, 69, 33.5f, 0.5f, 66, 34, 1, 62, 35, 1,
        64, 36, 2, 59, 38, 2,
        57, 40, 1, 60, 41, 1, 64, 42, 1, 60, 43, 1,
        62, 44, 2, 66, 46, 2,
        64, 48, 1, 60, 49, 1, 62, 50, 2,
        66, 52, 1, 64, 53, 1, 62, 54, 1, 59, 55, 1,
        62, 56, 2, 66, 58, 1, 64, 59, 1,
        62, 60, 4
    };

    // Title: C Am F G bounce, mid-low lead
    private static readonly int[] TitleChordRoot = { 48, 48, 45, 41, 43, 43, 45, 47, 48, 45, 41, 43, 41, 43, 48, 48 };
    private static readonly int[] TitleChordThird = { 52, 52, 48, 45, 47, 47, 48, 50, 52, 48, 45, 47, 45, 47, 52, 52 };
    private static readonly int[] TitleChordFifth = { 55, 55, 52, 48, 50, 50, 52, 54, 55, 52, 48, 50, 48, 50, 55, 55 };
    private static readonly int[] TitleBass = { 36, 36, 33, 29, 31, 31, 33, 35, 36, 33, 29, 31, 29, 31, 36, 36 };

    private static readonly float[] TitleMelody =
    {
        48, 0, 1, 52, 1, 1, 55, 2, 1, 52, 3, 1,
        55, 4, 2, 52, 6, 1, 48, 7, 1,
        45, 8, 1, 48, 9, 1, 52, 10, 2,
        53, 12, 2, 50, 14, 2,
        55, 16, 1, 52, 17, 1, 50, 18, 1, 47, 19, 1,
        43, 20, 2, 47, 22, 2,
        45, 24, 1, 48, 25, 1, 52, 26, 2,
        50, 28, 1, 47, 29, 1, 43, 30, 2,
        48, 32, 1, 52, 33, 0.5f, 55, 33.5f, 0.5f, 52, 34, 2,
        48, 36, 2, 45, 38, 2,
        41, 40, 2, 45, 42, 1, 48, 43, 1,
        47, 44, 2, 50, 46, 2,
        48, 48, 1, 45, 49, 1, 41, 50, 2,
        43, 52, 2, 47, 54, 2,
        48, 56, 2, 52, 58, 1, 50, 59, 1,
        48, 60, 4
    };

    // Chorus in A, matching the vocal: give you up is A B D B | F# F# | E.
    // Short lines are 2 bars; "run around" / "hurt you" are 4 bars.
    private static readonly int[] RickChordRoot =
    {
        50, 52, 54, 52,
        50, 52, 57, 52,
        50, 52, 54, 52,
        50, 52, 57, 52
    };
    private static readonly int[] RickChordThird =
    {
        54, 56, 57, 56,
        54, 56, 61, 56,
        54, 56, 57, 56,
        54, 56, 61, 56
    };
    private static readonly int[] RickChordFifth =
    {
        57, 59, 61, 59,
        57, 59, 64, 59,
        57, 59, 61, 59,
        57, 59, 64, 59
    };
    private static readonly int[] RickBass =
    {
        38, 40, 42, 40,
        38, 40, 45, 40,
        38, 40, 42, 40,
        38, 40, 45, 40
    };

    private static readonly float[] RickMelody =
    {
        // Never gonna give you up     A B D B | F# F# | E
        69, 0, 0.5f, 71, 0.5f, 0.5f, 74, 1, 0.5f, 71, 1.5f, 0.5f,
        78, 2, 1f, 78, 3, 1f,
        76, 4, 3.5f,

        // Never gonna let you down    A B D B | E E | D C# B
        69, 8, 0.5f, 71, 8.5f, 0.5f, 74, 9, 0.5f, 71, 9.5f, 0.5f,
        76, 10, 1f, 76, 11, 1f,
        74, 12, 1f, 73, 13, 0.5f, 71, 13.5f, 0.5f,

        // Never gonna run around and desert you (4 bars)
        69, 16, 0.5f, 71, 16.5f, 0.5f, 74, 17, 0.5f, 71, 17.5f, 0.5f,
        74, 18, 0.5f, 76, 18.5f, 0.5f, 73, 19, 0.5f, 69, 19.5f, 0.5f,
        69, 20, 0.5f, 76, 21, 1.5f, 74, 22.5f, 3.5f,

        // Never gonna make you cry    A B D B | F# F# | E
        69, 32, 0.5f, 71, 32.5f, 0.5f, 74, 33, 0.5f, 71, 33.5f, 0.5f,
        78, 34, 1f, 78, 35, 1f,
        76, 36, 3.5f,

        // Never gonna say goodbye     A B D B | A5 | C# D C# B
        69, 40, 0.5f, 71, 40.5f, 0.5f, 74, 41, 0.5f, 71, 41.5f, 0.5f,
        81, 42, 2f,
        73, 44, 0.5f, 74, 44.5f, 0.5f, 73, 45, 0.5f, 71, 45.5f, 2f,

        // Never gonna tell a lie and hurt you (4 bars)
        69, 48, 0.5f, 71, 48.5f, 0.5f, 74, 49, 0.5f, 71, 49.5f, 0.5f,
        74, 50, 0.5f, 76, 50.5f, 0.5f, 73, 51, 0.5f, 69, 51.5f, 0.5f,
        69, 52, 0.5f, 76, 53, 1.5f, 74, 54.5f, 3.5f
    };

    // Boss-lite: Em drive
    private static readonly int[] CombatChordRoot = { 52, 52, 48, 50, 52, 47, 45, 50 };
    private static readonly int[] CombatChordThird = { 55, 55, 52, 54, 55, 51, 48, 54 };
    private static readonly int[] CombatChordFifth = { 59, 59, 55, 57, 59, 54, 52, 57 };
    private static readonly int[] CombatBass = { 40, 40, 36, 38, 40, 35, 33, 38 };

    private static readonly float[] CombatMelody =
    {
        52, 0, 0.5f, 55, 0.5f, 0.5f, 59, 1, 0.5f, 55, 1.5f, 0.5f, 52, 2, 1, 55, 3, 1,
        52, 4, 0.5f, 59, 4.5f, 0.5f, 55, 5, 1, 52, 6, 2,
        48, 8, 0.5f, 52, 8.5f, 0.5f, 55, 9, 1, 52, 10, 1, 48, 11, 1,
        50, 12, 1, 54, 13, 1, 57, 14, 2,
        52, 16, 0.5f, 55, 16.5f, 0.5f, 59, 17, 1, 62, 18, 1, 59, 19, 1,
        54, 20, 1, 47, 21, 1, 51, 22, 2,
        45, 24, 1, 48, 25, 0.5f, 52, 25.5f, 0.5f, 48, 26, 2,
        50, 28, 0.5f, 54, 28.5f, 0.5f, 57, 29, 1, 54, 30, 1, 50, 31, 1
    };

    private static void RenderPad(
        float[] left,
        float[] right,
        int samplesPerBeat,
        int bars,
        int[] chordRoot,
        int[] chordThird,
        int[] chordFifth,
        float padScale)
    {
        for (int bar = 0; bar < bars; bar++)
        {
            float start = bar * BeatsPerBar;
            AddSine(left, right, chordRoot[bar], start, BeatsPerBar, samplesPerBeat, 0.04f * padScale, 0.08f, 0.25f, -0.35f, -5f);
            AddSine(left, right, chordThird[bar], start, BeatsPerBar, samplesPerBeat, 0.034f * padScale, 0.08f, 0.25f, 0.15f, 4f);
            AddSine(left, right, chordFifth[bar], start, BeatsPerBar, samplesPerBeat, 0.028f * padScale, 0.08f, 0.25f, 0.40f, 7f);
        }
    }

    private static void RenderWalkingBass(float[] left, float[] right, int samplesPerBeat, int bars, int[] bassRoot)
    {
        for (int bar = 0; bar < bars; bar++)
        {
            int root = bassRoot[bar];
            int fifth = root + 7;
            int oct = root + 12;
            float start = bar * BeatsPerBar;
            AddPluck(left, right, root, start, 0.92f, samplesPerBeat, 0.13f, 0f, bass: true);
            AddPluck(left, right, fifth, start + 1f, 0.92f, samplesPerBeat, 0.11f, 0f, bass: true);
            AddPluck(left, right, oct, start + 2f, 0.92f, samplesPerBeat, 0.12f, 0f, bass: true);
            AddPluck(left, right, fifth, start + 3f, 0.92f, samplesPerBeat, 0.11f, 0f, bass: true);
        }
    }

    private static void RenderArpeggio(
        float[] left,
        float[] right,
        int samplesPerBeat,
        int bars,
        int[] chordRoot,
        int[] chordThird,
        int[] chordFifth,
        float gain)
    {
        int[] pattern = { 0, 1, 2, 1, 2, 0, 1, 2 };
        for (int bar = 0; bar < bars; bar++)
        {
            int[] tones = { chordRoot[bar] + 12, chordThird[bar] + 12, chordFifth[bar] + 12 };
            float barStart = bar * BeatsPerBar;
            for (int i = 0; i < 8; i++)
            {
                int midi = tones[pattern[i] % tones.Length];
                AddPluck(left, right, midi, barStart + i * 0.5f, 0.42f, samplesPerBeat, gain, i % 2 == 0 ? -0.2f : 0.25f, bass: false);
            }
        }
    }

    private static void RenderMelody(
        float[] left,
        float[] right,
        int samplesPerBeat,
        float[] melody,
        float bpm,
        float leadGain)
    {
        float[] leadL = new float[left.Length];
        float[] leadR = new float[right.Length];

        for (int i = 0; i < melody.Length; i += 3)
        {
            int midi = (int)melody[i];
            float start = melody[i + 1];
            float dur = melody[i + 2];
            AddLead(leadL, leadR, midi, start, dur, samplesPerBeat, bpm, leadGain);
        }

        int echo = Mathf.RoundToInt(SampleRate * 0.22f);
        for (int s = 0; s < left.Length; s++)
        {
            float delayL = s >= echo ? leadL[s - echo] * 0.14f : 0f;
            float delayR = s >= echo ? leadR[s - echo] * 0.10f : 0f;
            left[s] += leadL[s] + delayL;
            right[s] += leadR[s] + delayR;
        }
    }

    private static void AddLead(
        float[] left,
        float[] right,
        int midi,
        float startBeat,
        float lengthBeats,
        int samplesPerBeat,
        float bpm,
        float gain)
    {
        int start = Mathf.FloorToInt(startBeat * samplesPerBeat);
        int count = Mathf.FloorToInt(lengthBeats * samplesPerBeat);
        if (start >= left.Length || count <= 0)
            return;

        int end = Mathf.Min(left.Length, start + count);
        float hz = MidiToHz(midi);
        float release = Mathf.Min(0.16f, lengthBeats * 60f / bpm * 0.4f);

        for (int s = start; s < end; s++)
        {
            float t = (s - start) / (float)SampleRate;
            float dur = (end - start) / (float)SampleRate;
            float env = Envelope(t, dur, 0.008f, release);
            env *= Mathf.Exp(-t * 2.4f);
            float sine = Mathf.Sin(2f * Mathf.PI * hz * t);
            float wave = sine * 0.72f + TriangleWave(hz, t) * 0.28f;

            float sample = wave * env * gain;
            left[s] += sample * 0.85f;
            right[s] += sample;
        }
    }

    private static void AddPluck(
        float[] left,
        float[] right,
        int midi,
        float startBeat,
        float lengthBeats,
        int samplesPerBeat,
        float gain,
        float pan,
        bool bass)
    {
        int start = Mathf.FloorToInt(startBeat * samplesPerBeat);
        int count = Mathf.FloorToInt(lengthBeats * samplesPerBeat);
        if (start >= left.Length || count <= 0)
            return;

        int end = Mathf.Min(left.Length, start + count);
        float hz = MidiToHz(midi);
        PanGains(pan, out float gL, out float gR);
        float damp = bass ? 3.2f : 7.5f;

        for (int s = start; s < end; s++)
        {
            float t = (s - start) / (float)SampleRate;
            float env = t < 0.004f ? t / 0.004f : Mathf.Exp(-t * damp);
            float wave = bass ? TriangleWave(hz, t) : (Mathf.Sin(2f * Mathf.PI * hz * t) * 0.65f + TriangleWave(hz, t) * 0.35f);
            float sample = wave * env * gain;
            left[s] += sample * gL;
            right[s] += sample * gR;
        }
    }

    private static void AddSine(
        float[] left,
        float[] right,
        int midi,
        float startBeat,
        float lengthBeats,
        int samplesPerBeat,
        float gain,
        float attack,
        float release,
        float pan,
        float cents)
    {
        int start = Mathf.FloorToInt(startBeat * samplesPerBeat);
        int count = Mathf.FloorToInt(lengthBeats * samplesPerBeat);
        if (start >= left.Length || count <= 0)
            return;

        int end = Mathf.Min(left.Length, start + count);
        float hz = MidiToHz(midi) * Mathf.Pow(2f, cents / 1200f);
        PanGains(pan, out float gL, out float gR);

        for (int s = start; s < end; s++)
        {
            float t = (s - start) / (float)SampleRate;
            float dur = (end - start) / (float)SampleRate;
            float env = Envelope(t, dur, attack, release);
            float sample = Mathf.Sin(2f * Mathf.PI * hz * t) * env * gain;
            left[s] += sample * gL;
            right[s] += sample * gR;
        }
    }

    private static float TriangleWave(float hz, float t)
    {
        float p = Frac(hz * t);
        return p < 0.5f ? p * 4f - 1f : 3f - p * 4f;
    }

    private static float Envelope(float t, float dur, float attack, float release)
    {
        if (dur <= 0.0001f)
            return 0f;
        if (t < 0f || t > dur)
            return 0f;

        float env = 1f;
        if (attack > 0f && t < attack)
            env = t / attack;

        if (release > 0f && t > dur - release)
            env *= Mathf.Max(0f, (dur - t) / release);

        return env;
    }

    private static void Normalize(float[] left, float[] right, float peak)
    {
        float max = 0.0001f;
        for (int i = 0; i < left.Length; i++)
        {
            float a = Mathf.Abs(left[i]);
            float b = Mathf.Abs(right[i]);
            if (a > max) max = a;
            if (b > max) max = b;
        }

        float scale = peak / max;
        for (int i = 0; i < left.Length; i++)
        {
            left[i] *= scale;
            right[i] *= scale;
        }
    }

    private static void CrossfadeLoop(float[] left, float[] right, int samples)
    {
        samples = Mathf.Clamp(samples, 2, left.Length / 8);
        int n = left.Length;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            int tail = n - samples + i;
            float l = left[tail] * (1f - t) + left[i] * t;
            float r = right[tail] * (1f - t) + right[i] * t;
            left[i] = l;
            right[i] = r;
            left[tail] = l;
            right[tail] = r;
        }
    }

    private static void PanGains(float pan, out float left, out float right)
    {
        float p = Mathf.Clamp(pan, -1f, 1f);
        left = Mathf.Sqrt((1f - p) * 0.5f) * 1.414f;
        right = Mathf.Sqrt((1f + p) * 0.5f) * 1.414f;
    }

    private static float MidiToHz(int midi)
    {
        return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }

    private static float Frac(float x)
    {
        return x - Mathf.Floor(x);
    }
}

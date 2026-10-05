using UnityEngine;

namespace StarTrek.Audio
{
    /// <summary>
    /// Placeholder sound effects synthesised at runtime, so the graybox has original audio before
    /// the real sound design exists. Each clip is generated once, on first use.
    /// </summary>
    public static class ProceduralSfx
    {
        const int Rate = 44100;

        static AudioClip doorWhoosh, chirp, denied, klaxon, transporter, disruptor, handPhaser, swing, scan;

        public static AudioClip DoorWhoosh => doorWhoosh ? doorWhoosh : doorWhoosh = BuildWhoosh();
        public static AudioClip Chirp => chirp ? chirp : chirp = BuildTones("sfx_chirp", new[] { 1400f, 1900f }, 0.06f, 0.0f);
        public static AudioClip Denied => denied ? denied : denied = BuildTones("sfx_denied", new[] { 520f, 380f }, 0.13f, 0.04f);
        /// <summary>One rising whoop plus a pause; loop it for a continuous alert.</summary>
        public static AudioClip Klaxon => klaxon ? klaxon : klaxon = BuildKlaxon();
        /// <summary>A shimmering chord that swells and fades (our own transporter sound).</summary>
        public static AudioClip Transporter => transporter ? transporter : transporter = BuildShimmer();
        public static AudioClip Disruptor => disruptor ? disruptor : disruptor = BuildZap("sfx_disruptor", 900f, 160f, 0.32f, 0.55f);
        public static AudioClip HandPhaser => handPhaser ? handPhaser : handPhaser = BuildZap("sfx_hand_phaser", 1700f, 1100f, 0.42f, 0.15f);
        public static AudioClip Swing => swing ? swing : swing = BuildWhoosh("sfx_swing", 0.3f, 0.35f);
        public static AudioClip Scan => scan ? scan : scan = BuildTones("sfx_scan", new[] { 2200f, 2600f, 2200f, 3000f }, 0.03f, 0.02f);

        static AudioClip BuildShimmer()
        {
            const float duration = 2.6f;
            var data = new float[(int)(Rate * duration)];
            float[] freqs = { 1046.5f, 1318.5f, 1568f, 2093f, 2637f };
            var rng = new System.Random(4);
            float hp = 0f, prev = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)data.Length;
                float time = i / (float)Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f));
                float s = 0f;
                for (int k = 0; k < freqs.Length; k++)
                    s += Mathf.Sin(2f * Mathf.PI * freqs[k] * time * (1f + 0.003f * Mathf.Sin(time * (5f + k)))) * (0.5f + 0.5f * Mathf.Sin(time * (9f + 3.7f * k) + k));
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                hp = 0.92f * (hp + noise - prev);
                prev = noise;
                data[i] = (s * 0.18f + hp * 0.25f) * env;
            }
            return Clip("sfx_transporter", data, 0.6f);
        }

        /// <summary>A pitch sweep with buzz: phasers high and clean, disruptors low and rough.</summary>
        static AudioClip BuildZap(string name, float fromHz, float toHz, float seconds, float grit)
        {
            var data = new float[(int)(Rate * seconds)];
            float phase = 0f;
            var rng = new System.Random(name.Length);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)data.Length;
                phase += 2f * Mathf.PI * Mathf.Lerp(fromHz, toHz, Mathf.Sqrt(t)) / Rate;
                float env = Mathf.Min(1f, t * 60f) * Mathf.Pow(1f - t, 1.6f);
                float tone = Mathf.Sin(phase) + 0.4f * Mathf.Sin(2.01f * phase);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                data[i] = Tanh(2f * (tone + grit * noise)) * env;
            }
            return Clip(name, data, 0.6f);
        }

        static AudioClip BuildWhoosh(string name, float duration, float peak)
        {
            var data = new float[(int)(Rate * duration)];
            var rng = new System.Random(77);
            float lp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)data.Length;
                float env = Mathf.Sin(Mathf.PI * t);
                lp += Mathf.Lerp(0.05f, 0.35f, env) * ((float)(rng.NextDouble() * 2.0 - 1.0) - lp);
                data[i] = lp * env;
            }
            return Clip(name, data, peak);
        }

        static AudioClip BuildWhoosh()
        {
            const float duration = 0.55f;
            var data = new float[(int)(Rate * duration)];
            var rng = new System.Random(1701);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)data.Length;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.55f));
                float cutoff = Mathf.Lerp(0.015f, 0.22f, Mathf.Sin(Mathf.PI * t));
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += cutoff * (noise - lp1);
                lp2 += cutoff * (lp1 - lp2);
                data[i] = lp2 * env * env;
            }
            return Clip("sfx_door_whoosh", data, 0.7f);
        }

        static AudioClip BuildTones(string name, float[] freqs, float toneSeconds, float gapSeconds)
        {
            int tone = (int)(Rate * toneSeconds);
            int gap = (int)(Rate * gapSeconds);
            var data = new float[freqs.Length * (tone + gap)];
            for (int f = 0; f < freqs.Length; f++)
            {
                int start = f * (tone + gap);
                for (int i = 0; i < tone; i++)
                {
                    float t = i / (float)tone;
                    float env = Mathf.Min(1f, t * 20f) * Mathf.Min(1f, (1f - t) * 8f);
                    data[start + i] = Mathf.Sin(2f * Mathf.PI * freqs[f] * i / Rate) * env;
                }
            }
            return Clip(name, data, 0.5f);
        }

        static AudioClip BuildKlaxon()
        {
            const float whoop = 0.62f, pause = 0.28f;
            int whoopSamples = (int)(Rate * whoop);
            var data = new float[(int)(Rate * (whoop + pause))];
            float phase = 0f;
            for (int i = 0; i < whoopSamples; i++)
            {
                float t = i / (float)whoopSamples;
                float freq = Mathf.Lerp(330f, 820f, t * t * (3f - 2f * t));
                phase += 2f * Mathf.PI * freq / Rate;
                float env = Mathf.Min(1f, t * 30f) * Mathf.Min(1f, (1f - t) * 12f);
                float s = Tanh(2.5f * Mathf.Sin(phase)) * 0.8f + 0.2f * Mathf.Sin(2f * phase);
                data[i] = s * env;
            }
            return Clip("sfx_klaxon", data, 0.6f);
        }

        static float Tanh(float x)
        {
            float e = Mathf.Exp(2f * x);
            return (e - 1f) / (e + 1f);
        }

        static AudioClip Clip(string name, float[] data, float peak)
        {
            float max = 1e-6f;
            for (int i = 0; i < data.Length; i++)
                max = Mathf.Max(max, Mathf.Abs(data[i]));
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;

            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

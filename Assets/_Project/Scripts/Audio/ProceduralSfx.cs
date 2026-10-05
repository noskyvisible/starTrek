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

        static AudioClip doorWhoosh, chirp, denied, klaxon;

        public static AudioClip DoorWhoosh => doorWhoosh ? doorWhoosh : doorWhoosh = BuildWhoosh();
        public static AudioClip Chirp => chirp ? chirp : chirp = BuildTones("sfx_chirp", new[] { 1400f, 1900f }, 0.06f, 0.0f);
        public static AudioClip Denied => denied ? denied : denied = BuildTones("sfx_denied", new[] { 520f, 380f }, 0.13f, 0.04f);
        /// <summary>One rising whoop plus a pause; loop it for a continuous alert.</summary>
        public static AudioClip Klaxon => klaxon ? klaxon : klaxon = BuildKlaxon();

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

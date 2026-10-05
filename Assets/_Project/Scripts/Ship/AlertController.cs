using System.Collections.Generic;
using StarTrek.Audio;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.Ship
{
    /// <summary>
    /// Shows the ship's alert state in the room. Red Alert tints and pulses the interior lights and
    /// emissive light panels and loops the klaxon; Normal (and Yellow, for now) restores everything.
    /// On the bridge the level comes from the ship simulation; in test scenes it can be toggled directly.
    /// </summary>
    public class AlertController : MonoBehaviour
    {
        [SerializeField] Light[] lights;
        [Tooltip("Light-panel materials whose emission pulses red during Red Alert.")]
        [SerializeField] Material[] emissiveMaterials;
        [SerializeField] Color redColor = new Color(1f, 0.07f, 0.04f);
        [SerializeField] float pulseHz = 0.75f;
        [Tooltip("Room lights are dimmed to this fraction of normal at the pulse low point...")]
        [SerializeField, Range(0f, 1f)] float lightDimLow = 0.3f;
        [Tooltip("...and to this fraction at the pulse peak.")]
        [SerializeField, Range(0f, 1f)] float lightDimHigh = 0.55f;
        [Tooltip("How far the room lights shift toward red at the pulse peak (0 = stay white).")]
        [SerializeField, Range(0f, 1f)] float lightRedMix = 0.55f;
        [SerializeField, Range(0f, 1f)] float pulseLow = 0.15f;
        [SerializeField] float emissionPeak = 4f;
        [SerializeField] AudioSource klaxonSource;

        static readonly int[] EmissionIds =
        {
            Shader.PropertyToID("_EmissionColor"),
            Shader.PropertyToID("emissiveFactor")
        };

        struct LightState
        {
            public Light Light;
            public Color Color;
            public float Intensity;
        }

        struct EmissiveSlot
        {
            public Renderer Renderer;
            public int Index;
            public int PropertyId;
        }

        LightState[] lightStates;
        EmissiveSlot[] slots;
        MaterialPropertyBlock block;

        public AlertLevel Level { get; private set; }
        public event System.Action<AlertLevel> LevelChanged;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            lightStates = new LightState[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                lightStates[i] = new LightState { Light = lights[i], Color = lights[i].color, Intensity = lights[i].intensity };
            slots = FindEmissiveSlots();

            if (klaxonSource)
            {
                klaxonSource.clip = ProceduralSfx.Klaxon;
                klaxonSource.loop = true;
            }
        }

        public void ToggleRedAlert() => SetLevel(Level == AlertLevel.Red ? AlertLevel.Normal : AlertLevel.Red);

        public void SetLevel(AlertLevel level)
        {
            if (level == Level || houseLights)
                return;
            Level = level;

            if (level == AlertLevel.Red)
            {
                if (klaxonSource)
                    klaxonSource.Play();
            }
            else
            {
                if (klaxonSource)
                    klaxonSource.Stop();
                foreach (var s in lightStates)
                {
                    s.Light.color = s.Color;
                    s.Light.intensity = s.Intensity;
                }
                foreach (var slot in slots)
                    slot.Renderer.SetPropertyBlock(null, slot.Index);
            }

            LevelChanged?.Invoke(level);
        }

        /// <summary>
        /// The simulation is over: alerts stop and every light goes to flat, bright work light, like
        /// the house lights coming up on a stage set.
        /// </summary>
        public void HouseLights(float brightness = 1.8f)
        {
            SetLevel(AlertLevel.Normal);
            for (int i = 0; i < lightStates.Length; i++)
            {
                lightStates[i].Color = new Color(0.95f, 0.97f, 1f);
                lightStates[i].Intensity *= brightness;
                lightStates[i].Light.color = lightStates[i].Color;
                lightStates[i].Light.intensity = lightStates[i].Intensity;
            }
            houseLights = true;
        }

        bool houseLights;
        float flickerUntil, flickerStrength;
        bool flickering;

        /// <summary>Make the lights stutter for a moment (a hit on the ship). strength 0..1.</summary>
        public void Flicker(float strength)
        {
            flickerUntil = Time.time + Mathf.Lerp(0.25f, 0.7f, strength);
            flickerStrength = Mathf.Max(flickerStrength, Mathf.Clamp01(strength));
        }

        void Update()
        {
            bool flicker = Time.time < flickerUntil;
            if (!flicker)
                flickerStrength = 0f;
            if (Level != AlertLevel.Red)
            {
                if (flicker)
                    foreach (var s in lightStates)
                        s.Light.intensity = s.Intensity * FlickerFactor();
                else if (flickering)
                    foreach (var s in lightStates)
                        s.Light.intensity = s.Intensity;
                flickering = flicker;
                return;
            }
            flickering = flicker;

            float pulse = 0.5f + 0.5f * Mathf.Cos(Time.time * 2f * Mathf.PI * pulseHz);
            // Film-style: the room stays dimly lit and only tints red as the panels flash.
            foreach (var s in lightStates)
            {
                s.Light.color = Color.Lerp(s.Color, redColor, lightRedMix * pulse);
                s.Light.intensity = s.Intensity * Mathf.Lerp(lightDimLow, lightDimHigh, pulse) * (flicker ? FlickerFactor() : 1f);
            }

            Color emission = redColor * (emissionPeak * Mathf.Lerp(pulseLow, 1f, pulse));
            foreach (var slot in slots)
            {
                slot.Renderer.GetPropertyBlock(block, slot.Index);
                block.SetColor(slot.PropertyId, emission);
                slot.Renderer.SetPropertyBlock(block, slot.Index);
            }
        }

        float FlickerFactor() => Random.value < 0.35f ? Mathf.Lerp(1f, 0.08f, flickerStrength) : Random.Range(0.75f, 1f);

        EmissiveSlot[] FindEmissiveSlots()
        {
            var found = new List<EmissiveSlot>();
            if (emissiveMaterials == null || emissiveMaterials.Length == 0)
                return found.ToArray();

            var targets = new HashSet<Material>(emissiveMaterials);
            foreach (var r in FindObjectsByType<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !targets.Contains(mats[i]))
                        continue;
                    foreach (int id in EmissionIds)
                    {
                        if (!mats[i].HasProperty(id))
                            continue;
                        found.Add(new EmissiveSlot { Renderer = r, Index = i, PropertyId = id });
                        break;
                    }
                }
            }
            return found.ToArray();
        }
    }
}

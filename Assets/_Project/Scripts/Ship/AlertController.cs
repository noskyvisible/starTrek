using System.Collections.Generic;
using StarTrek.Audio;
using UnityEngine;

namespace StarTrek.Ship
{
    public enum AlertLevel
    {
        Normal,
        Red
    }

    /// <summary>
    /// Ship-wide alert state. Red Alert tints and pulses the interior lights and emissive light
    /// panels and loops the klaxon; Normal restores everything.
    /// </summary>
    public class AlertController : MonoBehaviour
    {
        [SerializeField] Light[] lights;
        [Tooltip("Light-panel materials whose emission pulses red during Red Alert.")]
        [SerializeField] Material[] emissiveMaterials;
        [SerializeField] Color redColor = new Color(1f, 0.07f, 0.04f);
        [SerializeField] float pulseHz = 0.75f;
        [SerializeField, Range(0f, 1f)] float pulseLow = 0.25f;
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
            if (level == Level)
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

        void Update()
        {
            if (Level != AlertLevel.Red)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Cos(Time.time * 2f * Mathf.PI * pulseHz);
            float k = Mathf.Lerp(pulseLow, 1f, pulse);
            foreach (var s in lightStates)
            {
                s.Light.color = redColor;
                s.Light.intensity = s.Intensity * k;
            }

            Color emission = redColor * (emissionPeak * k);
            foreach (var slot in slots)
            {
                slot.Renderer.GetPropertyBlock(block, slot.Index);
                block.SetColor(slot.PropertyId, emission);
                slot.Renderer.SetPropertyBlock(block, slot.Index);
            }
        }

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

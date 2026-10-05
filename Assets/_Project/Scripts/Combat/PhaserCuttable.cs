using UnityEngine;
using UnityEngine.Events;

namespace StarTrek.Combat
{
    /// <summary>A weld or seized lock that a phaser on kill can cut through, a few shots at a time.</summary>
    public class PhaserCuttable : MonoBehaviour
    {
        [SerializeField] int hitsNeeded = 4;
        [SerializeField] Renderer glow;
        [SerializeField] UnityEvent onCut = new UnityEvent();

        int hits;
        float heat;
        Material glowMaterial;
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public bool Cut => hits >= hitsNeeded;
        public float Progress => Mathf.Clamp01(hits / (float)hitsNeeded);
        public UnityEvent OnCut => onCut;

        void Awake()
        {
            if (glow != null)
            {
                glowMaterial = glow.material;
                glowMaterial.EnableKeyword("_EMISSION");
            }
        }

        public void Hit(Vector3 point)
        {
            if (Cut)
                return;
            hits++;
            heat = 1f;
            Vfx.SparkBurst(point, -transform.forward, 30, 5f);
            if (Cut)
            {
                Vfx.Flash(point, new Color(1f, 0.6f, 0.3f), 4f, 4f, 0.4f);
                onCut.Invoke();
            }
        }

        void Update()
        {
            if (glowMaterial == null)
                return;
            heat = Mathf.MoveTowards(heat, Cut ? 0.25f : 0f, Time.deltaTime * 0.5f);
            float level = Progress * 0.5f + heat * 2.5f;
            glowMaterial.SetColor(EmissionColor, new Color(4f, 1.2f, 0.3f) * level);
        }
    }
}

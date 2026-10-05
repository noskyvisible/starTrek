using System.Collections.Generic;
using UnityEngine;

namespace StarTrek.Combat
{
    /// <summary>
    /// Neutronic radiation around the leaking fuel: hurts anyone inside, strongest at the centre, and
    /// shows on the tricorder from a distance. Turned off when the leak is sealed.
    /// </summary>
    public class RadiationZone : MonoBehaviour
    {
        [SerializeField] float radius = 7f;
        [SerializeField] float damagePerSecond = 9f;
        [Tooltip("The tricorder picks it up this far beyond the radius.")]
        [SerializeField] float detectRange = 12f;

        static readonly List<RadiationZone> All = new List<RadiationZone>();
        bool sealedOff;

        public float Radius { get => radius; set => radius = value; }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Seal() => sealedOff = true;

        /// <summary>0..1 radiation at a point (1 = at the source), from every active zone.</summary>
        public static float LevelAt(Vector3 p, bool includeDetectRange)
        {
            float level = 0f;
            foreach (var z in All)
            {
                if (z.sealedOff)
                    continue;
                float d = Vector3.Distance(p, z.transform.position);
                float reach = z.radius + (includeDetectRange ? z.detectRange : 0f);
                if (d < reach)
                    level = Mathf.Max(level, 1f - d / reach);
            }
            return level;
        }

        void Update()
        {
            if (sealedOff)
                return;
            var player = StarTrek.Core.PlayerLocator.Player;
            if (player == null)
                return;
            float d = Vector3.Distance(player.position + Vector3.up, transform.position);
            if (d > radius)
                return;
            var health = player.GetComponent<Health>();
            if (health != null)
                health.TakeDamage(damagePerSecond * (1.2f - d / radius) * Time.deltaTime, DamageKind.Radiation, transform.position);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

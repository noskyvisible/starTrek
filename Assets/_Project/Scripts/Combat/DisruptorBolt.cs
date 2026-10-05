using StarTrek.Audio;
using UnityEngine;

namespace StarTrek.Combat
{
    /// <summary>A Klingon hand-disruptor bolt: a fast glowing streak that hurts the first thing it hits.</summary>
    public class DisruptorBolt : MonoBehaviour
    {
        const float Speed = 32f;
        const float Length = 0.7f;
        const float Lifetime = 2.5f;

        float damage;
        GameObject owner;
        float age;
        LineRenderer line;

        public static void Fire(Vector3 from, Vector3 direction, float damage, GameObject owner)
        {
            var go = new GameObject("FX_DisruptorBolt");
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(direction);
            var bolt = go.AddComponent<DisruptorBolt>();
            bolt.damage = damage;
            bolt.owner = owner;
            bolt.line = go.AddComponent<LineRenderer>();
            bolt.line.useWorldSpace = true;
            bolt.line.positionCount = 2;
            bolt.line.widthMultiplier = 0.045f;
            bolt.line.numCapVertices = 3;
            bolt.line.sharedMaterial = Vfx.Get(Vfx.Disruptor, new Color(6f, 1.2f, 0.5f));
            bolt.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.35f, 0.2f);
            light.range = 2.5f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
            var audio = go.AddComponent<AudioSource>();
            audio.spatialBlend = 1f;
            audio.minDistance = 2f;
            audio.maxDistance = 30f;
            audio.PlayOneShot(ProceduralSfx.Disruptor, 0.6f);
            Vfx.Flash(from, new Color(1f, 0.4f, 0.2f), 2f, 3f, 0.1f);
            bolt.UpdateLine();
        }

        void Update()
        {
            float step = Speed * Time.deltaTime;
            age += Time.deltaTime;
            if (age > Lifetime)
            {
                Destroy(gameObject);
                return;
            }
            var hits = Physics.RaycastAll(transform.position, transform.forward, step, Physics.DefaultRaycastLayers | (1 << 2), QueryTriggerInteraction.Ignore);
            RaycastHit? nearest = null;
            foreach (var h in hits)
            {
                if (owner != null && h.collider.transform.IsChildOf(owner.transform))
                    continue;
                if (nearest == null || h.distance < nearest.Value.distance)
                    nearest = h;
            }
            if (nearest.HasValue)
            {
                var hit = nearest.Value;
                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                    health.TakeDamage(damage, DamageKind.Disruptor, transform.position - transform.forward * 5f);
                else
                    Vfx.SparkBurst(hit.point, hit.normal, 14, 3f);
                Vfx.Flash(hit.point, new Color(1f, 0.4f, 0.2f), 2.5f, 3f, 0.15f);
                Destroy(gameObject);
                return;
            }
            transform.position += transform.forward * step;
            UpdateLine();
        }

        void UpdateLine()
        {
            line.SetPosition(0, transform.position - transform.forward * Mathf.Min(Length, age * Speed));
            line.SetPosition(1, transform.position);
        }
    }
}

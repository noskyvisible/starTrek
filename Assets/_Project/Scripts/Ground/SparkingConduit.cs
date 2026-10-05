using StarTrek.Combat;
using UnityEngine;

namespace StarTrek.Ground
{
    /// <summary>A broken power conduit: random spark showers and a stuttering light.</summary>
    public class SparkingConduit : MonoBehaviour
    {
        [SerializeField] Vector2 interval = new Vector2(1.5f, 5f);
        [SerializeField] Light flickerLight;
        [SerializeField] AudioSource audioSource;
        [Tooltip("Hurts the player standing right under it.")]
        [SerializeField] float shockRadius = 0.9f;
        [SerializeField] float shockDamage = 8f;

        float next;
        float baseIntensity;

        void Start()
        {
            next = Time.time + Random.Range(interval.x, interval.y);
            if (flickerLight != null)
                baseIntensity = flickerLight.intensity;
        }

        void Update()
        {
            if (flickerLight != null)
                flickerLight.intensity = baseIntensity * (Random.value < 0.08f ? Random.Range(0f, 0.4f) : Random.Range(0.85f, 1f));
            if (Time.time < next)
                return;
            next = Time.time + Random.Range(interval.x, interval.y);
            Vfx.SparkBurst(transform.position, -transform.up + Random.insideUnitSphere * 0.3f, Random.Range(12, 30), 4f);
            Vfx.Flash(transform.position, new Color(0.7f, 0.8f, 1f), 3f, 4f, 0.12f);
            if (audioSource != null)
                audioSource.PlayOneShot(StarTrek.Audio.ProceduralSfx.Disruptor, 0.25f);
            var player = StarTrek.Core.PlayerLocator.Player;
            if (player != null && Vector3.Distance(player.position + Vector3.up * 1.2f, transform.position) < shockRadius + 1.2f)
            {
                var h = player.GetComponent<Health>();
                if (h != null)
                    h.TakeDamage(shockDamage, DamageKind.Electrical, transform.position);
            }
        }
    }
}

using System;
using StarTrek.Audio;
using UnityEngine;

namespace StarTrek.Combat
{
    public enum PhaserSetting { Stun, Kill }

    /// <summary>
    /// The cadet's hand phaser: a hitscan beam from the centre of the view. Stun knocks people out,
    /// kill does real damage and cuts through welded hatches (<see cref="PhaserCuttable"/>).
    /// Fired by <see cref="PlayerEquipment"/>.
    /// </summary>
    public class HandPhaser : MonoBehaviour
    {
        [SerializeField] Transform view;
        [SerializeField] Transform muzzle;
        [SerializeField] AudioSource audioSource;
        [SerializeField] float range = 45f;
        [SerializeField] float cooldown = 0.45f;
        [SerializeField] float stunAmount = 55f;
        [SerializeField] float killAmount = 38f;
        [SerializeField] LayerMask mask = Physics.DefaultRaycastLayers;

        float sinceFired = 99f;

        public PhaserSetting Setting { get; set; } = PhaserSetting.Stun;
        public bool Ready => sinceFired >= cooldown;
        public event Action<RaycastHit?> Fired;

        void Update() => sinceFired += Time.deltaTime;

        public void ToggleSetting()
        {
            Setting = Setting == PhaserSetting.Stun ? PhaserSetting.Kill : PhaserSetting.Stun;
            if (audioSource != null)
                audioSource.PlayOneShot(ProceduralSfx.Chirp, 0.5f);
        }

        public bool Fire()
        {
            if (!Ready || view == null)
                return false;
            sinceFired = 0f;
            bool stun = Setting == PhaserSetting.Stun;
            Vector3 from = muzzle != null ? muzzle.position : view.position;
            Vector3 end = view.position + view.forward * range;
            RaycastHit? result = null;
            if (Physics.Raycast(view.position, view.forward, out RaycastHit hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                result = hit;
                end = hit.point;
                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null && health.gameObject != gameObject)
                    health.TakeDamage(stun ? stunAmount : killAmount, stun ? DamageKind.PhaserStun : DamageKind.PhaserKill, view.position);
                var cuttable = hit.collider.GetComponentInParent<PhaserCuttable>();
                if (cuttable != null && !stun)
                    cuttable.Hit(hit.point);
                if (health == null)
                    Vfx.SparkBurst(hit.point, hit.normal, stun ? 8 : 16, stun ? 2f : 3.5f);
            }
            var material = stun ? Vfx.Get(Vfx.PhaserStun, new Color(2.2f, 3.2f, 6f)) : Vfx.Get(Vfx.PhaserKill, new Color(6f, 1.4f, 0.4f));
            Vfx.Beam(from, end, material, stun ? 0.02f : 0.028f, 0.16f);
            Vfx.Flash(from, stun ? new Color(0.6f, 0.75f, 1f) : new Color(1f, 0.5f, 0.3f), 1.2f, 3f, 0.08f);
            Vfx.Flash(end, stun ? new Color(0.6f, 0.75f, 1f) : new Color(1f, 0.5f, 0.3f), 2f, 2.5f, 0.15f);
            if (audioSource != null)
                audioSource.PlayOneShot(ProceduralSfx.HandPhaser, 0.6f);
            Fired?.Invoke(result);
            return true;
        }
    }
}

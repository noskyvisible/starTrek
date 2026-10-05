using System;
using UnityEngine;

namespace StarTrek.Combat
{
    public enum DamageKind { PhaserStun, PhaserKill, Disruptor, Blade, Radiation, Electrical }

    /// <summary>
    /// Hit points for the player, crew, Klingons and survivors. Phasers on stun fill a separate stun
    /// meter that knocks the target out instead of killing it.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;
        [Tooltip("Hit points regained per second after a pause without damage (the player).")]
        [SerializeField] float regenPerSecond;
        [SerializeField] float regenDelay = 5f;
        [Tooltip("Stun damage that knocks this character out.")]
        [SerializeField] float stunThreshold = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public float StunLevel { get; private set; }
        public bool Dead { get; private set; }
        public bool Stunned { get; private set; }
        public bool IsDown => Dead || Stunned;
        public float SinceDamaged { get; private set; } = 99f;
        /// <summary>Ignore all damage (cutscenes, the simulation ending).</summary>
        public bool Invulnerable { get; set; }

        /// <summary>amount, kind, where it came from.</summary>
        public event Action<float, DamageKind, Vector3> Damaged;
        /// <summary>True if killed, false if stunned.</summary>
        public event Action<bool> Downed;

        void Awake() => Current = maxHealth;

        public void Configure(float max, float regen, float stunAt)
        {
            maxHealth = max;
            regenPerSecond = regen;
            stunThreshold = stunAt;
            Current = max;
        }

        public void TakeDamage(float amount, DamageKind kind, Vector3 from)
        {
            if (IsDown || Invulnerable || amount <= 0f)
                return;
            SinceDamaged = 0f;
            if (kind == DamageKind.PhaserStun)
            {
                StunLevel += amount;
                Damaged?.Invoke(amount, kind, from);
                if (StunLevel >= stunThreshold)
                {
                    Stunned = true;
                    Downed?.Invoke(false);
                }
                return;
            }
            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(amount, kind, from);
            if (Current <= 0f)
            {
                Dead = true;
                Downed?.Invoke(true);
            }
        }

        /// <summary>Back on their feet at full health (the simulation reset, the bridge sickbay).</summary>
        public void Revive()
        {
            Dead = Stunned = false;
            Current = maxHealth;
            StunLevel = 0f;
            SinceDamaged = 99f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            SinceDamaged += dt;
            if (IsDown)
                return;
            StunLevel = Mathf.Max(0f, StunLevel - stunThreshold * 0.08f * dt);
            if (regenPerSecond > 0f && SinceDamaged > regenDelay)
                Current = Mathf.Min(maxHealth, Current + regenPerSecond * dt);
        }
    }
}

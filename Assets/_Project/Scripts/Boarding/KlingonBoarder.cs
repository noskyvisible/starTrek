using System;
using System.Collections.Generic;
using StarTrek.Audio;
using StarTrek.Characters;
using StarTrek.Combat;
using StarTrek.Core;
using StarTrek.Crew;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace StarTrek.Boarding
{
    /// <summary>
    /// A Klingon boarder on the bridge. Disruptor troops close to a clear shot, then hold, sidestep and
    /// fire; bat'leth warriors charge and swing. They go for the captain first and the crew after.
    /// Phasers on stun drop them unconscious; on kill, dead.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(CharacterAnimator))]
    public class KlingonBoarder : MonoBehaviour
    {
        public enum Arms { Disruptor, Batleth }

        [SerializeField] Arms weapon = Arms.Disruptor;
        [SerializeField] float disruptorRange = 16f;
        [SerializeField] float disruptorDamage = 8f;
        [SerializeField] float fireInterval = 2.7f;
        [SerializeField] float meleeRange = 1.6f;
        [SerializeField] float meleeDamage = 18f;
        [SerializeField] float walkSpeed = 1.7f;
        [SerializeField] float chargeSpeed = 3.2f;
        [Tooltip("Chance of going after the captain rather than the nearest crew member.")]
        [SerializeField, Range(0f, 1f)] float captainPreference = 0.5f;

        public static readonly List<KlingonBoarder> Active = new List<KlingonBoarder>();

        /// <summary>The boarder, and whether they were killed (false = stunned).</summary>
        public event Action<KlingonBoarder, bool> Downed;

        NavMeshAgent agent;
        Health health;
        CharacterAnimator anim;
        Collider body;
        AudioSource voice;
        Transform muzzle;
        Health target;
        bool fighting, swingPending, frozen;
        float retarget, fireTimer, swingTimer, staggerTimer, strafeTimer, swingHitAt;

        public Arms Weapon { get => weapon; set => weapon = value; }
        public Health Health => health;
        public bool IsDown => health != null && health.IsDown;
        public bool Fighting => fighting;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            anim = GetComponent<CharacterAnimator>();
            body = GetComponent<Collider>();
            voice = GetComponent<AudioSource>();
            agent.enabled = false;
            health.Damaged += OnDamaged;
            health.Downed += OnDowned;
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        /// <summary>Materialise where it stands, then join the fight.</summary>
        public void BeamIn()
        {
            anim.DriveLocomotion = false;
            anim.Hold(weapon == Arms.Batleth ? CharacterProp.Batleth : CharacterProp.Disruptor);
            anim.Play(weapon == Arms.Batleth ? CharacterAnimator.Idle : CharacterAnimator.AimPistol, 0f);
            muzzle = FindMuzzle();
            fireTimer = 2.6f + Random.value;   // a moment to react after they materialise
            TransporterEffect.Play(gameObject, true, TransporterEffect.DefaultSeconds, () =>
            {
                if (this == null || health.IsDown)
                    return;
                if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
                {
                    agent.enabled = true;
                    agent.Warp(hit.position);
                }
                fighting = true;
            });
        }

        Transform FindMuzzle()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith("PROP_Disruptor"))
                    return t;
            return null;
        }

        /// <summary>The simulation stopped: hold still.</summary>
        public void Freeze()
        {
            frozen = true;
            fighting = false;
            if (agent.enabled)
                agent.isStopped = true;
            anim.Freeze(true);
        }

        /// <summary>The test is over; the "Klingons" are cadets in costume. Up they get.</summary>
        public void StandDown()
        {
            frozen = true;
            fighting = false;
            if (agent.enabled)
                agent.isStopped = true;
            anim.Freeze(false);
            anim.Hold(CharacterProp.None);
            anim.Play(CharacterAnimator.Idle, 0.6f);
        }

        void Update()
        {
            if (!fighting || frozen || health.IsDown || !agent.enabled || !agent.isOnNavMesh)
                return;
            float dt = Time.deltaTime;
            retarget -= dt;
            fireTimer -= dt;
            swingTimer -= dt;
            staggerTimer -= dt;
            strafeTimer -= dt;

            if (target == null || target.IsDown || retarget <= 0f)
                PickTarget();
            if (target == null)
            {
                agent.ResetPath();
                Animate(false);
                return;
            }

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            agent.isStopped = staggerTimer > 0f;
            if (staggerTimer > 0f)
                return;

            if (weapon == Arms.Batleth || dist < 2.2f)
                Melee(dist, to);
            else
                Ranged(dist, to);

            if (swingPending && Time.time >= swingHitAt)
            {
                swingPending = false;
                if (target != null && !target.IsDown && Vector3.Distance(target.transform.position, transform.position) <= meleeRange + 0.5f)
                    target.TakeDamage(meleeDamage, DamageKind.Blade, transform.position);
            }
        }

        void Melee(float dist, Vector3 to)
        {
            if (dist > meleeRange)
            {
                agent.updateRotation = true;
                agent.speed = weapon == Arms.Batleth ? chargeSpeed : walkSpeed;
                agent.stoppingDistance = meleeRange * 0.8f;
                agent.SetDestination(target.transform.position);
                Animate(true);
                return;
            }
            agent.ResetPath();
            Face(to);
            if (swingTimer <= 0f)
            {
                swingTimer = 1.9f;
                if (weapon != Arms.Batleth)
                    anim.Hold(CharacterProp.Disruptor);
                anim.PlayOnce(CharacterAnimator.MeleeSwing, 0.85f, CharacterAnimator.Idle);
                swingPending = true;
                swingHitAt = Time.time + 0.45f;
                if (voice != null)
                    voice.PlayOneShot(ProceduralSfx.Swing, 0.8f);
            }
            else if (!anim.InOneShot)
                anim.Play(CharacterAnimator.Idle);
        }

        void Ranged(float dist, Vector3 to)
        {
            bool clear = LineOfSight(target);
            if (!clear || dist > disruptorRange)
            {
                agent.updateRotation = true;
                agent.speed = walkSpeed;
                agent.stoppingDistance = 1f;
                agent.SetDestination(target.transform.position);
                Animate(true);
                return;
            }
            // A clear shot: hold, sidestep now and then, keep firing.
            if (strafeTimer <= 0f)
            {
                strafeTimer = Random.Range(2.5f, 4.5f);
                Vector3 side = Vector3.Cross(Vector3.up, to.normalized) * (Random.value < 0.5f ? -1f : 1f) * Random.Range(1.2f, 2.5f);
                if (NavMesh.SamplePosition(transform.position + side, out var hit, 1f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
                agent.speed = walkSpeed * 0.8f;
                agent.stoppingDistance = 0.1f;
            }
            agent.updateRotation = false;
            Face(to);
            bool moving = agent.velocity.sqrMagnitude > 0.05f;
            if (!anim.InOneShot)
                anim.Play(moving ? CharacterAnimator.Walk : CharacterAnimator.AimPistol);
            if (fireTimer <= 0f && !moving)
            {
                fireTimer = fireInterval * Random.Range(0.8f, 1.25f);
                Shoot(dist);
            }
        }

        void Shoot(float dist)
        {
            anim.PlayOnce(CharacterAnimator.FirePistol, 0.38f, CharacterAnimator.AimPistol);
            Vector3 from = muzzle != null ? muzzle.position + transform.forward * 0.12f : transform.position + Vector3.up * 1.4f + transform.forward * 0.5f;
            Vector3 aim = AimPoint(target);
            float spread = Mathf.Lerp(0.035f, 0.11f, dist / disruptorRange);
            Vector3 dir = ((aim - from).normalized + Random.insideUnitSphere * spread).normalized;
            DisruptorBolt.Fire(from, dir, disruptorDamage, gameObject);
        }

        void Animate(bool moving)
        {
            if (anim.InOneShot)
                return;
            float speed = agent.velocity.magnitude;
            anim.Play(!moving || speed < 0.2f ? (weapon == Arms.Batleth ? CharacterAnimator.Idle : CharacterAnimator.AimPistol)
                : speed > 2.6f ? CharacterAnimator.Run : CharacterAnimator.Walk);
        }

        void Face(Vector3 to)
        {
            if (to.sqrMagnitude < 0.001f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * Time.deltaTime);
        }

        static Vector3 AimPoint(Health h)
        {
            bool player = PlayerLocator.Player != null && h.transform == PlayerLocator.Player;
            var officer = h.GetComponent<CrewOfficer>();
            float height = player ? 1.35f : officer != null && officer.Seated ? 0.95f : 1.2f;
            return h.transform.position + Vector3.up * height;
        }

        bool LineOfSight(Health h)
        {
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 aim = AimPoint(h);
            Vector3 d = aim - eye;
            foreach (var hit in Physics.RaycastAll(eye, d.normalized, d.magnitude, Physics.DefaultRaycastLayers | (1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform))
                    continue;
                if (!hit.collider.transform.IsChildOf(h.transform))
                    return false;
            }
            return true;
        }

        void PickTarget()
        {
            retarget = Random.Range(2.5f, 4f);
            Health captain = PlayerLocator.Player != null ? PlayerLocator.Player.GetComponent<Health>() : null;
            if (captain != null && captain.IsDown)
                captain = null;
            Health nearestCrew = null;
            float best = float.MaxValue;
            foreach (var o in CrewOfficer.All)
            {
                if (!o.Targetable)
                    continue;
                float d = Vector3.Distance(o.transform.position, transform.position);
                if (d < best)
                {
                    best = d;
                    nearestCrew = o.Health;
                }
            }
            if (captain != null && (nearestCrew == null || Random.value < captainPreference))
                target = captain;
            else
                target = nearestCrew != null ? nearestCrew : captain;
        }

        void OnDamaged(float amount, DamageKind kind, Vector3 from)
        {
            if (health.IsDown)
                return;
            staggerTimer = 0.3f;
            anim.PlayOnce(CharacterAnimator.HitReact, 0.4f, weapon == Arms.Batleth ? CharacterAnimator.Idle : CharacterAnimator.AimPistol);
            // Turn on whoever shot us.
            if (PlayerLocator.Player != null && Vector3.Distance(from, PlayerLocator.Player.position + Vector3.up * 1.68f) < 2f)
            {
                var captain = PlayerLocator.Player.GetComponent<Health>();
                if (captain != null && !captain.IsDown)
                    target = captain;
            }
        }

        void OnDowned(bool killed)
        {
            fighting = false;
            swingPending = false;
            if (agent.enabled)
                agent.enabled = false;
            if (body != null)
                body.enabled = false;
            anim.Hold(CharacterProp.None);
            if (killed)
                anim.Play(CharacterAnimator.Death, 0.1f);
            else
                anim.PlayOnce(CharacterAnimator.Death, 1.25f, CharacterAnimator.InjuredLie);
            Downed?.Invoke(this, killed);
        }
    }
}

using System.Collections.Generic;
using StarTrek.Boarding;
using StarTrek.Characters;
using StarTrek.Combat;
using StarTrek.Core;
using StarTrek.Interaction;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.Crew
{
    /// <summary>
    /// A cadet officer crewing a bridge station: works the console from the chair, steps aside when the
    /// captain takes the seat, can be shot when the bridge is boarded (the station then goes
    /// unanswered), and gets back up when the simulation ends. Security stands and returns fire.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(CharacterAnimator))]
    public class CrewOfficer : MonoBehaviour
    {
        enum State { Seated, StandingAside, Fighting, Down, Dismissed }

        [SerializeField] StationRole role;
        [SerializeField] Seat seat;
        [Tooltip("Where the body sits: feet on the floor at the chair, facing the console.")]
        [SerializeField] Transform seatPose;
        [Tooltip("Beside the station: where the officer stands aside, fights from, or falls.")]
        [SerializeField] Transform standPoint;
        [SerializeField] bool fightsBack;
        [SerializeField] float returnFireInterval = 2.4f;
        [SerializeField, Range(0f, 1f)] float hitChance = 0.45f;

        public static readonly List<CrewOfficer> All = new List<CrewOfficer>();

        Health health;
        CharacterAnimator anim;
        CapsuleCollider capsule;
        AudioSource audioSource;
        State state;
        float fireTimer;

        public StationRole Role => role;
        public Health Health => health;
        public bool Seated => state == State.Seated;
        public bool Targetable => health != null && !health.IsDown && state != State.Dismissed;

        /// <summary>The station's officer is down, so orders to it go unanswered.</summary>
        public static bool IsDown(StationRole role)
        {
            foreach (var o in All)
                if (o.role == role)
                    return o.health.IsDown;
            return false;
        }

        public void Configure(StationRole stationRole, Seat stationSeat, Transform pose, Transform stand, bool armed)
        {
            role = stationRole;
            seat = stationSeat;
            seatPose = pose;
            standPoint = stand;
            fightsBack = armed;
        }

        void Awake()
        {
            health = GetComponent<Health>();
            anim = GetComponent<CharacterAnimator>();
            capsule = GetComponent<CapsuleCollider>();
            audioSource = GetComponent<AudioSource>();
            health.Downed += OnDowned;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            anim.Hold(CharacterProp.None);
            SitDown();
        }

        void Update()
        {
            if (state == State.Down || state == State.Dismissed)
                return;
            bool captainHere = seat != null && seat.IsOccupied;
            bool boarders = AnyBoarders();
            switch (state)
            {
                case State.Seated:
                    if (captainHere)
                        StandAside();
                    else if (fightsBack && boarders)
                        StartFighting();
                    break;
                case State.StandingAside:
                    if (!captainHere && !(fightsBack && boarders))
                        SitDown();
                    else if (fightsBack && boarders)
                        StartFighting();
                    break;
                case State.Fighting:
                    if (!boarders)
                    {
                        anim.Hold(CharacterProp.None);
                        if (captainHere) StandAside();
                        else SitDown();
                    }
                    else
                        ReturnFire();
                    break;
            }
        }

        void SitDown()
        {
            state = State.Seated;
            if (seatPose != null)
                transform.SetPositionAndRotation(seatPose.position, seatPose.rotation);
            anim.Play(role == StationRole.Helm || role == StationRole.Navigation || Random.value < 0.6f
                ? CharacterAnimator.SitConsole : CharacterAnimator.SitIdle, 0.3f);
            SetCapsule(seated: true);
        }

        void StandAside()
        {
            state = State.StandingAside;
            if (standPoint != null)
                transform.SetPositionAndRotation(standPoint.position, standPoint.rotation);
            anim.Play(CharacterAnimator.Idle, 0.3f);
            SetCapsule(seated: false);
        }

        void StartFighting()
        {
            state = State.Fighting;
            if (standPoint != null)
                transform.SetPositionAndRotation(standPoint.position, standPoint.rotation);
            anim.Hold(CharacterProp.HandPhaser);
            anim.Play(CharacterAnimator.AimPistol, 0.2f);
            SetCapsule(seated: false);
            fireTimer = 1.5f;
        }

        void ReturnFire()
        {
            KlingonBoarder nearest = null;
            float best = float.MaxValue;
            foreach (var k in KlingonBoarder.Active)
            {
                if (!k.Fighting || k.IsDown)
                    continue;
                float d = Vector3.Distance(k.transform.position, transform.position);
                if (d < best)
                {
                    best = d;
                    nearest = k;
                }
            }
            if (nearest == null)
                return;
            Vector3 to = nearest.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 240f * Time.deltaTime);
            fireTimer -= Time.deltaTime;
            if (fireTimer > 0f)
                return;
            fireTimer = returnFireInterval * Random.Range(0.8f, 1.3f);
            anim.PlayOnce(CharacterAnimator.FirePistol, 0.38f, CharacterAnimator.AimPistol);
            Vector3 from = transform.position + Vector3.up * 1.4f + transform.forward * 0.45f;
            Vector3 aim = nearest.transform.position + Vector3.up * 1.2f;
            bool hit = Random.value < hitChance;
            if (!hit)
                aim += Vector3.Cross(Vector3.up, (aim - from).normalized) * Random.Range(0.6f, 1.2f) * (Random.value < 0.5f ? -1f : 1f);
            Vfx.Beam(from, aim, Vfx.Get(Vfx.PhaserStun, new Color(2.2f, 3.2f, 6f)), 0.02f, 0.16f);
            Vfx.Flash(aim, new Color(0.6f, 0.75f, 1f), 1.5f, 2.5f, 0.12f);
            if (audioSource != null)
                audioSource.PlayOneShot(StarTrek.Audio.ProceduralSfx.HandPhaser, 0.5f);
            if (hit)
                nearest.Health.TakeDamage(55f, DamageKind.PhaserStun, from);
        }

        void OnDowned(bool killed)
        {
            state = State.Down;
            if (standPoint != null)
                transform.SetPositionAndRotation(standPoint.position, standPoint.rotation);
            anim.Hold(CharacterProp.None);
            anim.PlayOnce(CharacterAnimator.Death, 1.25f, CharacterAnimator.InjuredLie);
            if (capsule != null)
                capsule.enabled = false;
            if (GameSession.Exists && GameSession.Instance.Running)
                GameSession.Instance.Mission.ReportCrewDown();
        }

        /// <summary>The simulation stopped: hold still.</summary>
        public void Freeze() => anim.Freeze(true);

        /// <summary>The test is over. Everyone gets up, the "dead" included.</summary>
        public void StandDown()
        {
            anim.Freeze(false);
            health.Revive();
            health.Invulnerable = true;
            state = State.Dismissed;
            anim.Hold(CharacterProp.None);
            if (standPoint != null)
                transform.SetPositionAndRotation(standPoint.position, standPoint.rotation);
            anim.Play(CharacterAnimator.Idle, 0.8f);
            SetCapsule(seated: false);
        }

        void SetCapsule(bool seated)
        {
            if (capsule == null)
                return;
            capsule.enabled = true;
            capsule.height = seated ? 1.35f : 1.8f;
            capsule.center = new Vector3(0f, seated ? 0.68f : 0.9f, seated ? 0.12f : 0f);
        }

        static bool AnyBoarders()
        {
            foreach (var k in KlingonBoarder.Active)
                if (k.Fighting && !k.IsDown)
                    return true;
            return false;
        }
    }
}

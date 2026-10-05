using UnityEngine;

namespace StarTrek.Characters
{
    public enum CharacterProp { None, HandPhaser, Tricorder, Disruptor, Batleth }

    /// <summary>
    /// Drives a character's Animator by state name (Idle, Walk, Run, SitIdle, SitConsole, AimPistol,
    /// FirePistol, MeleeSwing, HitReact, Death, InjuredLie) and shows/hides the hand props.
    /// With <see cref="driveLocomotion"/> on, Idle/Walk/Run follow how fast the character moves.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimator : MonoBehaviour
    {
        public const string Idle = "Idle", Walk = "Walk", Run = "Run", SitIdle = "SitIdle", SitConsole = "SitConsole",
            AimPistol = "AimPistol", FirePistol = "FirePistol", MeleeSwing = "MeleeSwing", HitReact = "HitReact",
            Death = "Death", InjuredLie = "InjuredLie";

        [SerializeField] bool driveLocomotion;
        [SerializeField] float walkSpeed = 0.4f;
        [SerializeField] float runSpeed = 3.2f;
        [SerializeField] string startState = Idle;

        Animator animator;
        string current;
        Vector3 lastPosition;
        float oneShotUntil;
        string afterOneShot;
        GameObject phaser, tricorder, disruptor, batleth;

        public string Current => current;
        public bool DriveLocomotion { get => driveLocomotion; set => driveLocomotion = value; }

        void Awake()
        {
            animator = GetComponent<Animator>();
            phaser = FindProp("PROP_HandPhaser");
            tricorder = FindProp("PROP_Tricorder");
            disruptor = FindProp("PROP_Disruptor");
            batleth = FindProp("PROP_Batleth");
            Hold(CharacterProp.None);
            lastPosition = transform.position;
        }

        void Start() => Play(startState, 0f);

        GameObject FindProp(string suffix)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(suffix))
                    return t.gameObject;
            return null;
        }

        /// <summary>Show one hand prop (or none).</summary>
        public void Hold(CharacterProp prop)
        {
            if (phaser) phaser.SetActive(prop == CharacterProp.HandPhaser);
            if (tricorder) tricorder.SetActive(prop == CharacterProp.Tricorder);
            if (disruptor) disruptor.SetActive(prop == CharacterProp.Disruptor);
            if (batleth) batleth.SetActive(prop == CharacterProp.Batleth);
        }

        /// <summary>Cross-fade to a looping state.</summary>
        public void Play(string state, float fade = 0.2f)
        {
            if (state == current || animator == null)
                return;
            current = state;
            oneShotUntil = 0f;
            if (fade <= 0f)
                animator.Play(state, 0, 0f);
            else
                animator.CrossFadeInFixedTime(state, fade);
        }

        /// <summary>Play a one-shot (FirePistol, MeleeSwing, HitReact) then return to <paramref name="then"/>.</summary>
        public void PlayOnce(string state, float seconds, string then)
        {
            if (animator == null)
                return;
            animator.CrossFadeInFixedTime(state, 0.05f, 0, 0f);
            current = state;
            oneShotUntil = Time.time + seconds;
            afterOneShot = then;
        }

        void Update()
        {
            if (oneShotUntil > 0f && Time.time >= oneShotUntil)
            {
                oneShotUntil = 0f;
                current = null;
                Play(afterOneShot ?? Idle, 0.15f);
            }

            if (!driveLocomotion || oneShotUntil > 0f || Time.deltaTime <= 0f)
            {
                lastPosition = transform.position;
                return;
            }
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            float speed = delta.magnitude / Time.deltaTime;
            lastPosition = transform.position;
            if (current == Death || current == InjuredLie || current == SitIdle || current == SitConsole || current == AimPistol)
                return;
            Play(speed > runSpeed ? Run : speed > walkSpeed ? Walk : Idle, 0.2f);
        }
    }
}

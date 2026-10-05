using System.Collections.Generic;
using StarTrek.Audio;
using StarTrek.Bridge;
using StarTrek.Characters;
using StarTrek.Combat;
using StarTrek.Core;
using StarTrek.Interaction;
using UnityEngine;

namespace StarTrek.Ground
{
    /// <summary>
    /// A group of trapped people aboard the Kobayashi Maru, shown by one injured or huddled figure.
    /// The tricorder finds them; the away team tags them with a transponder so the transporter can
    /// pull them out once the radiation is down.
    /// </summary>
    [RequireComponent(typeof(ScanTarget), typeof(CharacterAnimator))]
    public class Survivor : MonoBehaviour, IInteractable
    {
        [SerializeField] int people = 12;
        [Tooltip("Lying injured on the deck, or sitting huddled.")]
        [SerializeField] bool injured = true;
        [SerializeField] string groupName = "Passengers";

        public static readonly List<Survivor> All = new List<Survivor>();

        ScanTarget scan;
        CharacterAnimator anim;
        AudioSource audioSource;
        GameObject beacon;

        public bool Tagged { get; private set; }
        public int People => people;

        public string Prompt => $"Place a transponder ({groupName}, {people} people)";
        public bool CanInteract => !Tagged;

        public void Configure(int count, bool lying, string name)
        {
            people = count;
            injured = lying;
            groupName = name;
        }

        void Awake()
        {
            scan = GetComponent<ScanTarget>();
            anim = GetComponent<CharacterAnimator>();
            audioSource = GetComponent<AudioSource>();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            anim.Hold(CharacterProp.None);
            anim.Play(injured ? CharacterAnimator.InjuredLie : CharacterAnimator.SitIdle, 0f);
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null && injured)
            {
                // Lying on their back, head away from where they face.
                capsule.direction = 2;
                capsule.radius = 0.28f;
                capsule.height = 1.8f;
                capsule.center = new Vector3(0f, 0.22f, -0.65f);
            }
            else if (capsule != null)
            {
                capsule.height = 1.2f;
                capsule.center = new Vector3(0f, 0.6f, 0f);
            }
            scan.Configure(groupName,
                $"{people} life signs, weak. {(injured ? "Radiation burns and smoke inhalation; they can't walk." : "Shock and mild radiation exposure.")} " +
                "Place a transponder so the transporter can find them.", people);
        }

        public void Interact(Interactor interactor)
        {
            if (Tagged)
                return;
            Tagged = true;
            if (GameSession.Exists && GameSession.Instance.Running)
                GameSession.Instance.Mission.ReportSurvivorsTagged(people);
            scan.Configure(groupName, $"{people} life signs. Transponder in place: the transporter can lock on to them once the radiation is down.", people);
            if (audioSource != null)
                audioSource.PlayOneShot(ProceduralSfx.Chirp, 0.7f);
            BridgeMessageLog.Post("Away team", $"Transponder placed. {people} {groupName.ToLowerInvariant()} ready for transport.");
            AddBeacon();
        }

        void AddBeacon()
        {
            beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "Transponder";
            Destroy(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(transform, false);
            beacon.transform.localPosition = injured ? new Vector3(0.25f, 0.3f, -0.4f) : new Vector3(0.25f, 0.75f, 0.1f);
            beacon.transform.localScale = Vector3.one * 0.05f;
            beacon.GetComponent<Renderer>().sharedMaterial = Vfx.Get(Vfx.PhaserStun, new Color(2.2f, 3.2f, 6.5f));
            var light = beacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.5f, 0.75f, 1f);
            light.range = 2f;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
        }

        void Update()
        {
            if (beacon != null)
                beacon.SetActive(Mathf.Repeat(Time.time, 1.2f) < 0.6f);
        }
    }
}

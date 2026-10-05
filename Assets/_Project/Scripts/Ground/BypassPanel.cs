using StarTrek.Audio;
using StarTrek.Combat;
using StarTrek.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace StarTrek.Ground
{
    /// <summary>
    /// A seized door lock. Scan it with the tricorder to find the bypass, then use it to open the
    /// door; or cut the weld with a phaser on kill instead (a <see cref="PhaserCuttable"/> nearby).
    /// </summary>
    [RequireComponent(typeof(ScanTarget))]
    public class BypassPanel : MonoBehaviour, IInteractable
    {
        [SerializeField] SlidingDoor door;
        [SerializeField] AudioSource audioSource;
        [SerializeField] UnityEvent onOpened = new UnityEvent();

        ScanTarget scan;
        bool open;

        public string Prompt => !scan.Scanned
            ? "Lock seized. Scan it with the tricorder [T], or cut the weld (phaser on kill)"
            : "Bypass the lock";
        public bool CanInteract => !open;
        public UnityEvent OnOpened => onOpened;

        public void Configure(SlidingDoor target) => door = target;

        void Awake()
        {
            scan = GetComponent<ScanTarget>();
            scan.Configure("Hatch lock", "Magnetic lock fused by a power surge. The secondary circuit is intact: a bypass will open it.", 0);
        }

        public void Interact(Interactor interactor)
        {
            if (open)
                return;
            if (!scan.Scanned)
            {
                if (audioSource != null)
                    audioSource.PlayOneShot(SfxBank.ButtonDenied);
                return;
            }
            Open();
        }

        /// <summary>Also called when the weld is cut.</summary>
        public void Open()
        {
            if (open)
                return;
            open = true;
            if (door != null)
                door.Unlock();
            if (audioSource != null)
                audioSource.PlayOneShot(ProceduralSfx.Chirp, 0.7f);
            onOpened.Invoke();
        }
    }
}

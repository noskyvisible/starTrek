using StarTrek.Audio;
using StarTrek.Core;
using UnityEngine;

namespace StarTrek.Interaction
{
    /// <summary>
    /// Starfleet split door: both leaves slide sideways when the player comes near and close again
    /// after they leave. A locked door stays shut and answers a press with a denied tone.
    /// </summary>
    public class SlidingDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform leftLeaf;
        [SerializeField] Transform rightLeaf;
        [SerializeField] float slideDistance = 0.78f;
        [SerializeField] float openSeconds = 0.35f;
        [SerializeField] float openRadius = 2.6f;
        [SerializeField] float holdOpenSeconds = 0.8f;
        [SerializeField] bool locked;
        [SerializeField] string lockedPrompt = "Door locked";
        [SerializeField] AudioSource audioSource;

        Vector3 leftClosed, rightClosed, leftDir, rightDir, centre;
        float openAmount, lastNearTime = float.NegativeInfinity;
        bool opening;

        public bool Locked
        {
            get => locked;
            set => locked = value;
        }

        public bool IsOpen => openAmount > 0.99f;

        /// <summary>Free a locked or jammed door (wired to a cut weld or a bypassed lock).</summary>
        public void Unlock() => locked = false;

        public string Prompt => lockedPrompt;
        public bool CanInteract => locked;

        void Awake()
        {
            leftClosed = leftLeaf.localPosition;
            rightClosed = rightLeaf.localPosition;
            // Slide each leaf outward, away from the doorway centre, whatever the import axes are.
            Vector3 across = (rightClosed - leftClosed).normalized;
            leftDir = -across;
            rightDir = across;
            centre = (leftLeaf.position + rightLeaf.position) * 0.5f;
        }

        void Update()
        {
            Transform player = PlayerLocator.Player;
            if (!locked && player != null)
            {
                Vector3 d = player.position - centre;
                d.y = 0f;
                if (d.sqrMagnitude < openRadius * openRadius)
                    lastNearTime = Time.time;
            }

            bool shouldOpen = !locked && Time.time - lastNearTime < holdOpenSeconds;
            if (shouldOpen != opening)
            {
                opening = shouldOpen;
                if (audioSource)
                    audioSource.PlayOneShot(opening ? SfxBank.DoorOpen : SfxBank.DoorClose);
            }

            float target = opening ? 1f : 0f;
            if (Mathf.Approximately(openAmount, target))
                return;

            openAmount = Mathf.MoveTowards(openAmount, target, Time.deltaTime / openSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, openAmount) * slideDistance;
            leftLeaf.localPosition = leftClosed + leftDir * eased;
            rightLeaf.localPosition = rightClosed + rightDir * eased;
        }

        public void Interact(Interactor interactor)
        {
            if (audioSource)
                audioSource.PlayOneShot(SfxBank.ButtonDenied);
        }
    }
}

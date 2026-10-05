using UnityEngine;
using UnityEngine.InputSystem;

namespace StarTrek.Interaction
{
    /// <summary>Looks along the camera for an <see cref="IInteractable"/> and uses it on the Interact action.</summary>
    public class Interactor : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;
        [SerializeField] Transform view;
        [SerializeField] float range = 2.4f;
        [Tooltip("Excludes Ignore Raycast, where the player's own capsule lives (the camera sits inside it).")]
        [SerializeField] LayerMask mask = Physics.DefaultRaycastLayers;

        InputAction interact;
        Collider lastCollider;
        IInteractable target;

        /// <summary>The interactable under the crosshair that can be used right now, or null.</summary>
        public IInteractable Current => target != null && target.CanInteract ? target : null;

        /// <summary>Display name of the first Interact binding, e.g. "E".</summary>
        public string InteractKeyLabel { get; private set; }

        void Awake()
        {
            interact = actions.FindActionMap("Player", true).FindAction("Interact", true);
            InteractKeyLabel = interact.GetBindingDisplayString(0);
        }

        void Update()
        {
            if (Physics.Raycast(view.position, view.forward, out RaycastHit hit, range, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != lastCollider)
                {
                    lastCollider = hit.collider;
                    target = hit.collider.GetComponentInParent<IInteractable>();
                }
            }
            else
            {
                lastCollider = null;
                target = null;
            }

            if (interact.WasPressedThisFrame())
                Current?.Interact(this);
        }
    }
}

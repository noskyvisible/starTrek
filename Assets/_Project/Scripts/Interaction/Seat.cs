using StarTrek.Core;
using StarTrek.Player;
using UnityEngine;

namespace StarTrek.Interaction
{
    /// <summary>
    /// A chair the player can take: the captain's chair, helm, or a bridge station.
    /// The eye point sets the seated view (its forward is the seat's facing); the exit point is where
    /// the player stands up.
    /// </summary>
    public class Seat : MonoBehaviour, IInteractable
    {
        [SerializeField] string prompt = "Sit";
        [SerializeField] Transform eyePoint;
        [SerializeField] Transform exitPoint;

        FirstPersonController occupant;
        FirstPersonController localPlayer;

        public Transform EyePoint => eyePoint;
        public Transform ExitPoint => exitPoint;
        public bool IsOccupied => occupant != null;

        public string Prompt => prompt;

        public bool CanInteract
        {
            get
            {
                if (occupant != null)
                    return false;
                if (localPlayer == null && PlayerLocator.Player != null)
                    localPlayer = PlayerLocator.Player.GetComponent<FirstPersonController>();
                return localPlayer == null || !localPlayer.IsSeated;
            }
        }

        public void Interact(Interactor interactor)
        {
            var player = interactor.GetComponent<FirstPersonController>();
            if (player == null || player.IsSeated || occupant != null)
                return;
            occupant = player;
            player.SitAt(this);
        }

        /// <summary>Called by the occupant when they stand up.</summary>
        public void Vacate() => occupant = null;
    }
}

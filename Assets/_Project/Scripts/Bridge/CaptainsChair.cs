using StarTrek.Core;
using StarTrek.Interaction;
using UnityEngine;

namespace StarTrek.Bridge
{
    /// <summary>Tells the mission when the cadet first takes the captain's chair.</summary>
    [RequireComponent(typeof(Seat))]
    public class CaptainsChair : MonoBehaviour
    {
        Seat seat;
        bool reported;

        void Awake() => seat = GetComponent<Seat>();

        void Update()
        {
            if (reported || !seat.IsOccupied || !GameSession.Exists || GameSession.Instance.Mission == null)
                return;
            reported = true;
            GameSession.Instance.Mission.CaptainSeated();
        }
    }
}

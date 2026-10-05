using UnityEngine;

namespace StarTrek.Ship
{
    /// <summary>Slowly turns the viewscreen's space camera so the stars drift, as if the ship is under way.</summary>
    public class SpaceDrift : MonoBehaviour
    {
        [SerializeField] Vector3 degreesPerSecond = new Vector3(0f, 0.8f, 0.15f);

        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}

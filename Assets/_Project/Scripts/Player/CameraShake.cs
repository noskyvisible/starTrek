using UnityEngine;

namespace StarTrek.Player
{
    /// <summary>
    /// Trauma-based camera shake for hits on the ship. Lives on the "Eyes" object under the camera
    /// pivot, so it never fights the look controls or the interaction ray (which use the pivot).
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] float maxOffset = 0.06f;      // metres
        [SerializeField] float maxAngle = 3.5f;        // degrees
        [SerializeField] float recoveryPerSecond = 1.4f;

        float trauma;

        /// <summary>Add 0..1 of shake; it decays on its own.</summary>
        public void Add(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        void LateUpdate()
        {
            if (trauma <= 0f)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }
            float s = trauma * trauma;
            float t = Time.time * 22f;
            transform.localPosition = new Vector3(Noise(t, 0f), Noise(t, 10f), 0f) * (maxOffset * s);
            transform.localRotation = Quaternion.Euler(Noise(t, 20f) * maxAngle * s, Noise(t, 30f) * maxAngle * s, Noise(t, 40f) * maxAngle * 1.5f * s);
            trauma = Mathf.Max(0f, trauma - recoveryPerSecond * Time.deltaTime);
        }

        static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;
    }
}

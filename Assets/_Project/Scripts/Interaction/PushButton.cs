using StarTrek.Audio;
using UnityEngine;
using UnityEngine.Events;

namespace StarTrek.Interaction
{
    /// <summary>A physical console button: dips when pressed, chirps, and fires <see cref="OnPressed"/>.</summary>
    public class PushButton : MonoBehaviour, IInteractable
    {
        [SerializeField] string prompt = "Press";
        [SerializeField] UnityEvent onPressed = new UnityEvent();
        [SerializeField] float pressDepth = 0.012f;
        [SerializeField] float pressSeconds = 0.25f;
        [SerializeField] float cooldown = 0.5f;
        [SerializeField] AudioSource audioSource;

        Vector3 restPosition;
        float pressedAt = float.NegativeInfinity;

        public UnityEvent OnPressed => onPressed;
        public string Prompt => prompt;
        public bool CanInteract => Time.time - pressedAt > cooldown;

        void Awake() => restPosition = transform.localPosition;

        public void Interact(Interactor interactor)
        {
            pressedAt = Time.time;
            if (audioSource)
                audioSource.PlayOneShot(ProceduralSfx.Chirp);
            onPressed.Invoke();
        }

        void Update()
        {
            float k = (Time.time - pressedAt) / pressSeconds;
            if (k > 1.5f)
                return;
            float depth = pressDepth * (1f - Mathf.Clamp01(k));
            transform.localPosition = restPosition - transform.localRotation * Vector3.up * depth;
        }
    }
}

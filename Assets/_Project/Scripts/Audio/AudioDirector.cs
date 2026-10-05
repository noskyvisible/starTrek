using UnityEngine;

namespace StarTrek.Audio
{
    /// <summary>Makes the scene's <see cref="SfxBank"/> current and plays the room ambience loop.</summary>
    public class AudioDirector : MonoBehaviour
    {
        [SerializeField] SfxBank bank;
        [SerializeField] AudioSource ambience;

        void Awake()
        {
            SfxBank.Current = bank;
            if (ambience != null && bank != null && bank.bridgeAmbience != null)
            {
                ambience.clip = bank.bridgeAmbience;
                ambience.loop = true;
                ambience.Play();
            }
        }

        void OnDestroy()
        {
            if (SfxBank.Current == bank)
                SfxBank.Current = null;
        }
    }
}

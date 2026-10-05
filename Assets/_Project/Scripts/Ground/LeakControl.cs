using System.Collections;
using StarTrek.Audio;
using StarTrek.Bridge;
using StarTrek.Combat;
using StarTrek.Core;
using StarTrek.Interaction;
using UnityEngine;

namespace StarTrek.Ground
{
    /// <summary>
    /// The freighter's fuel-control console. Sealing the neutronic fuel leak takes a few seconds in
    /// the radiation; once it is done the transporter can lock on to the away team and the tagged
    /// survivors.
    /// </summary>
    public class LeakControl : MonoBehaviour, IInteractable
    {
        [SerializeField] RadiationZone[] radiation;
        [SerializeField] float sealSeconds = 4f;
        [SerializeField] Light warningLight;
        [SerializeField] AudioSource audioSource;

        bool sealing, done;

        public string Prompt => sealing ? "Sealing the fuel lines..." : "Seal the neutronic fuel leak";
        public bool CanInteract => !sealing && !done;
        public bool Sealed => done;

        public void Interact(Interactor interactor)
        {
            if (sealing || done)
                return;
            StartCoroutine(Seal());
        }

        IEnumerator Seal()
        {
            sealing = true;
            BridgeMessageLog.Post("Away team", "Closing the fuel valves and re-routing to the reserve tanks...");
            for (float t = 0f; t < sealSeconds; t += 0.5f)
            {
                if (audioSource != null)
                    audioSource.PlayOneShot(ProceduralSfx.Scan, 0.4f);
                Vfx.SparkBurst(transform.position + Vector3.up * 1.1f, transform.forward, 10, 2.5f);
                yield return new WaitForSeconds(0.5f);
            }
            sealing = false;
            done = true;
            foreach (var z in radiation)
                if (z != null)
                    z.Seal();
            if (warningLight != null)
                warningLight.color = new Color(0.3f, 1f, 0.45f);
            if (audioSource != null)
                audioSource.PlayOneShot(ProceduralSfx.Chirp, 0.7f);
            BridgeMessageLog.Post("Away team", "Leak sealed. Radiation's falling off.");
            if (GameSession.Exists && GameSession.Instance.Running)
                GameSession.Instance.Mission.ReportLeakSealed();
        }
    }
}

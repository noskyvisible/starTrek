using StarTrek.Audio;
using StarTrek.Bridge;
using StarTrek.Combat;
using StarTrek.Core;
using StarTrek.Interaction;
using UnityEngine;

namespace StarTrek.Academy
{
    /// <summary>
    /// The easter egg (GAME_PROMPT §8, beat 8): an old maintenance terminal for Simulator Room 4.
    /// Restricted, until a tricorder scan shows its access protocol can be spoofed; then the cadet can
    /// upload a subroutine that makes the Kobayashi Maru winnable. The evaluation will notice.
    /// </summary>
    [RequireComponent(typeof(ScanTarget))]
    public class MaintenanceTerminal : MonoBehaviour, IInteractable
    {
        [SerializeField] Renderer screen;
        [SerializeField] AudioSource audioSource;

        ScanTarget scan;

        public string Prompt => GameSession.SimulationReprogrammed ? "Simulator 4: subroutine installed"
            : scan.Scanned ? "Spoof the clearance and upload a subroutine to Simulator 4"
            : "Simulator maintenance terminal (restricted)";
        public bool CanInteract => !GameSession.SimulationReprogrammed;

        void Awake()
        {
            scan = GetComponent<ScanTarget>();
            scan.Configure("Simulator control relay, Room 4",
                "Scenario controls for the command simulator. It still runs a twenty-year-old access protocol: a tricorder could mimic an instructor's clearance code.", 0);
        }

        void Start()
        {
            if (GameSession.SimulationReprogrammed)
                ShowInstalled();
        }

        public void Interact(Interactor interactor)
        {
            if (GameSession.SimulationReprogrammed)
                return;
            if (!scan.Scanned)
            {
                if (audioSource != null)
                    audioSource.PlayOneShot(SfxBank.ButtonDenied);
                BridgeMessageLog.Post("Terminal", "ACCESS RESTRICTED. Instructor clearance required.");
                return;
            }
            GameSession.SimulationReprogrammed = true;
            if (audioSource != null)
            {
                audioSource.PlayOneShot(ProceduralSfx.Scan, 0.7f);
                audioSource.PlayOneShot(ProceduralSfx.Chirp, 0.6f);
            }
            BridgeMessageLog.Post("Terminal", "Clearance accepted: H-O-L-L-I-S. Scenario parameters modified. Subroutine installed in Simulator 4.");
            ShowInstalled();
        }

        void ShowInstalled()
        {
            scan.Configure("Simulator control relay, Room 4", "Scenario parameters: MODIFIED. Hostile shield generators: offline. Nobody will notice. Probably.", 0);
            if (screen != null)
            {
                var m = screen.material;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.3f, 1.4f, 0.5f));
                m.SetColor("_BaseColor", new Color(0.1f, 0.5f, 0.2f));
            }
        }
    }
}

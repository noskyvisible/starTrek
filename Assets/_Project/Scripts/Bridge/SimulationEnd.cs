using System.Collections;
using StarTrek.Audio;
using StarTrek.Boarding;
using StarTrek.Combat;
using StarTrek.Core;
using StarTrek.Crew;
using StarTrek.Player;
using StarTrek.Ship;
using StarTrek.Simulation;
using StarTrek.UI;
using UnityEngine;

namespace StarTrek.Bridge
{
    /// <summary>
    /// The end of the Kobayashi Maru (GAME_PROMPT §8, beat 7): the last moment plays out (a white-out
    /// if the ship is lost), the screens freeze, the house lights come up, the "dead" crew and the
    /// "Klingons" get to their feet, the instructor speaks, then the evaluation.
    /// </summary>
    public class SimulationEnd : MonoBehaviour
    {
        [SerializeField] AlertController lights;
        [SerializeField] Renderer viewscreen;
        [SerializeField] SpaceView spaceView;
        [SerializeField] Camera viewscreenCamera;
        [SerializeField] BoardingDirector boarding;
        [SerializeField] EvaluationScreen evaluation;
        [SerializeField] AudioSource audioSource;

        Mission mission;
        float whiteout;

        void Start()
        {
            if (!GameSession.Exists || GameSession.Instance.Mission == null)
                return;
            mission = GameSession.Instance.Mission;
            mission.Ended += OnEnded;
        }

        void OnDestroy()
        {
            if (mission != null)
                mission.Ended -= OnEnded;
        }

        void OnEnded(MissionOutcome outcome) => StartCoroutine(Sequence(outcome));

        IEnumerator Sequence(MissionOutcome outcome)
        {
            var player = PlayerLocator.Player;
            var health = player != null ? player.GetComponent<Health>() : null;
            if (health != null)
                health.Invulnerable = true;

            bool lost = outcome == MissionOutcome.ShipDestroyed || outcome == MissionOutcome.SelfDestructed;
            if (lost)
            {
                if (audioSource != null)
                    audioSource.PlayOneShot(SfxBank.Explosion, 1f);
                var shake = player != null ? player.GetComponentInChildren<CameraShake>() : null;
                if (shake != null)
                    shake.Add(1f);
                if (lights != null)
                    lights.Flicker(1f);
                for (float t = 0f; t < 0.5f; t += Time.deltaTime)
                {
                    whiteout = t / 0.5f;
                    yield return null;
                }
                whiteout = 1f;
                yield return new WaitForSeconds(0.6f);
            }
            else
                yield return new WaitForSeconds(outcome == MissionOutcome.Retreated ? 2.5f : 1.5f);

            // Freeze: the simulation has stopped mid-frame.
            if (spaceView != null)
                spaceView.enabled = false;
            if (boarding != null)
                boarding.FreezeAll();
            foreach (var o in CrewOfficer.All)
                o.Freeze();
            if (lights != null)
                lights.SetLevel(AlertLevel.Normal);
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                whiteout = Mathf.Max(0f, whiteout - Time.deltaTime / 1.4f);
                yield return null;
            }
            BridgeMessageLog.Post("Simulator", "Simulation ended.");
            yield return new WaitForSeconds(1.2f);

            // House lights.
            if (lights != null)
                lights.HouseLights();
            if (viewscreenCamera != null)
                viewscreenCamera.enabled = false;
            if (viewscreen != null)
                ShowTestPattern(viewscreen);
            if (audioSource != null)
                audioSource.PlayOneShot(ProceduralSfx.DoorWhoosh, 0.5f);
            yield return new WaitForSeconds(1.5f);

            // Everyone gets up, the cadet captain too.
            if (boarding != null)
                boarding.StandDownAll();
            foreach (var o in CrewOfficer.All)
                o.StandDown();
            if (health != null && health.IsDown)
            {
                health.Revive();
                var body = player.GetComponent<FirstPersonController>();
                if (body != null)
                    body.Locked = false;
            }
            yield return new WaitForSeconds(1.2f);

            foreach (string line in InstructorLines(outcome))
            {
                BridgeMessageLog.Post(CrewRoster.Instructor + " (instructor)", line);
                yield return new WaitForSeconds(3.6f);
            }
            yield return new WaitForSeconds(1f);
            if (evaluation != null)
                evaluation.Show(Evaluation.Build(mission.Record));
        }

        static string[] InstructorLines(MissionOutcome outcome)
        {
            string verdict;
            switch (outcome)
            {
                case MissionOutcome.DeclinedRescue: verdict = "You never crossed the line, Cadet. Some captains would call that wisdom."; break;
                case MissionOutcome.ShipDestroyed: verdict = "You went down fighting, Cadet. All hands lost."; break;
                case MissionOutcome.CaptainKilled: verdict = "Captain killed in action. It happens, even to the best of them."; break;
                case MissionOutcome.Surrendered: verdict = "You chose to save your crew's lives, as prisoners. That takes a kind of courage too."; break;
                case MissionOutcome.SelfDestructed: verdict = "You took one of them with you. That's a choice some captains have made."; break;
                case MissionOutcome.Retreated: verdict = "You got your ship out. The Maru wasn't so lucky."; break;
                case MissionOutcome.MaruSaved: verdict = "Well. That has never happened before."; break;
                default: verdict = "That's enough for today."; break;
            }
            return new[]
            {
                "That's the end of the simulation. Stand easy, everyone. You can get up now.",
                verdict,
                "Report for evaluation. The board will review how you faced it.",
            };
        }

        /// <summary>The viewscreen drops to a flat calibration grid: the window was never a window.</summary>
        static void ShowTestPattern(Renderer screen)
        {
            const int w = 256, h = 144;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false) { name = "TX_TestPattern", filterMode = FilterMode.Point };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool line = x % 32 == 0 || y % 32 == 0 || x == w - 1 || y == h - 1;
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(w / 2f, h / 2f));
                    bool ring = Mathf.Abs(d - 50f) < 0.8f;
                    tex.SetPixel(x, y, line || ring ? new Color(0.8f, 0.82f, 0.85f) : new Color(0.12f, 0.13f, 0.15f));
                }
            tex.Apply();
            var m = screen.material;   // an instance: the shared asset stays as it is
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", new Color(0.9f, 0.9f, 0.9f));
            m.EnableKeyword("_EMISSION");
        }

        void OnGUI()
        {
            if (whiteout <= 0.001f)
                return;
            GUI.depth = -15;
            GUI.color = new Color(1f, 0.97f, 0.9f, whiteout);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}

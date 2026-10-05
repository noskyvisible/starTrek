using System.Collections;
using StarTrek.Bridge;
using StarTrek.Characters;
using StarTrek.Core;
using StarTrek.Interaction;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.Academy
{
    /// <summary>
    /// Starfleet Academy before the test (GAME_PROMPT §8, beat 1): report to Simulator Room 4, the
    /// instructor's briefing, a cadet's rumour about the maintenance terminals, and the simulator door,
    /// which opens once you've been briefed and takes you onto the simulator bridge.
    /// </summary>
    public class AcademyDirector : MonoBehaviour
    {
        [SerializeField] Transform instructor;
        [SerializeField] Transform briefingPoint;
        [SerializeField] float briefingRadius = 4f;
        [SerializeField] SlidingDoor simulatorDoor;
        [Tooltip("Walking this close to the simulator doorway starts the test.")]
        [SerializeField] Transform simulatorEntrance;
        [SerializeField] Transform rumourCadet;

        string objective = "Report to Simulator Room 4, at the end of the corridor, for your Kobayashi Maru test.";
        bool briefed, briefing, rumourTold, entering;
        GUIStyle titleStyle, textStyle;
        int styledForHeight;

        static readonly string[] Briefing =
        {
            "Cadet. Commander Hollis, command training. You're next in the simulator.",
            "You'll have the conn of a starship on patrol near the Klingon Neutral Zone. Your crew are fellow cadets. Treat them as you would a real crew.",
            "Standing orders: no Starfleet vessel enters the Neutral Zone. The treaty is very clear on that.",
            "We don't grade whether you win. We grade how you decide, how you look after your people, and how you hold up under pressure.",
            "Simulator Four is through that door. Good luck, Cadet.",
        };

        void Start()
        {
            // Coming back from a finished test: it's over, the next visit to the bridge starts fresh.
            var session = GameSession.Instance;
            if (session.Mission != null && session.Mission.Beat == MissionBeat.Ended)
                session.ClearSimulation();
            if (simulatorDoor != null)
                simulatorDoor.Locked = true;
        }

        void Update()
        {
            var player = PlayerLocator.Player;
            if (player == null)
                return;

            if (!briefing && !briefed && briefingPoint != null && Flat(player.position, briefingPoint.position) < briefingRadius)
                StartCoroutine(Brief());

            if (!rumourTold && rumourCadet != null && Flat(player.position, rumourCadet.position) < 3f)
            {
                rumourTold = true;
                BridgeMessageLog.Post("Cadet Ito", "Simulator Four? Good luck. Nobody passes the Maru. Rumour is someone once reprogrammed the whole test... these old maintenance terminals never get updated.");
            }

            if (instructor != null && briefing)
            {
                Vector3 to = player.position - instructor.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                    instructor.rotation = Quaternion.RotateTowards(instructor.rotation, Quaternion.LookRotation(to), 120f * Time.deltaTime);
            }

            if (briefed && !entering && simulatorEntrance != null && Flat(player.position, simulatorEntrance.position) < 1.1f)
            {
                entering = true;
                GameSession.Instance.Load(GameSession.BridgeScene, null, Transition.Fade);
            }
        }

        IEnumerator Brief()
        {
            briefing = true;
            objective = "Listen to the briefing.";
            foreach (string line in Briefing)
            {
                BridgeMessageLog.Post(CrewRoster.Instructor, line);
                yield return new WaitForSeconds(Mathf.Clamp(line.Length * 0.055f, 3f, 7f));
            }
            briefed = true;
            briefing = false;
            if (simulatorDoor != null)
                simulatorDoor.Unlock();
            objective = "Enter Simulator Four.";
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        void OnGUI()
        {
            if (entering || (GameSession.Exists && GameSession.Instance.Cover > 0.5f))
                return;
            if (titleStyle == null || styledForHeight != Screen.height)
            {
                styledForHeight = Screen.height;
                int size = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.018f));
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold };
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true };
            }
            float w = Screen.width * 0.3f;
            var r = new Rect(Screen.width * 0.02f, Screen.height * 0.03f, w, textStyle.fontSize * 1.6f);
            titleStyle.normal.textColor = new Color(1f, 0.72f, 0.3f);
            GUI.Label(r, "STARFLEET ACADEMY", titleStyle);
            textStyle.normal.textColor = new Color(0.92f, 0.93f, 1f);
            GUI.Label(new Rect(r.x, r.yMax, w, textStyle.CalcHeight(new GUIContent(objective), w)), objective, textStyle);
        }
    }
}

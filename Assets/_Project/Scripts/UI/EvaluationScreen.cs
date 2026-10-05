using StarTrek.Core;
using StarTrek.Player;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.UI
{
    /// <summary>
    /// The Starfleet Academy evaluation after the Kobayashi Maru (GAME_PROMPT §8, beat 7): grades for
    /// decisions, crew care and composure, the final choice described, the instructor's remark, and
    /// the way back to the Academy.
    /// </summary>
    public class EvaluationScreen : MonoBehaviour
    {
        Evaluation shown;
        Vector2 scroll;
        float openedAt;
        GUIStyle title, subtitle, heading, gradeStyle, note, remark, button, warning;
        int styledForHeight;

        public bool Visible => shown != null;

        public void Show(Evaluation evaluation)
        {
            shown = evaluation;
            openedAt = Time.unscaledTime;
            var player = PlayerLocator.Player != null ? PlayerLocator.Player.GetComponent<FirstPersonController>() : null;
            if (player != null)
                player.Locked = true;
        }

        void OnGUI()
        {
            if (shown == null)
                return;
            if (title == null || styledForHeight != Screen.height)
                BuildStyles();
            GUI.depth = -20;
            float fade = Mathf.Clamp01((Time.unscaledTime - openedAt) / 1.2f);

            GUI.color = new Color(0.02f, 0.025f, 0.05f, 0.93f * fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, fade);

            float w = Mathf.Min(Screen.width * 0.72f, Screen.height * 1.25f);
            var area = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.05f, w, Screen.height * 0.9f);
            GUI.color = new Color(1f, 0.62f, 0.2f, fade);
            GUI.DrawTexture(new Rect(area.x, area.y, w, 4f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, fade);

            GUILayout.BeginArea(new Rect(area.x, area.y + 12f, w, area.height - 12f));
            GUILayout.Label("STARFLEET ACADEMY  ·  COMMAND TRAINING EVALUATION", subtitle);
            GUILayout.Label("Kobayashi Maru scenario", title);
            GUILayout.Space(6f);
            GUILayout.Label(shown.OutcomeTitle.ToUpperInvariant(), heading);
            if (!string.IsNullOrEmpty(shown.IntegrityNote))
                GUILayout.Label(shown.IntegrityNote, warning);
            GUILayout.Space(8f);

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(area.height * 0.6f));
            foreach (var c in shown.Categories)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(c.Title.ToUpperInvariant(), heading);
                GUILayout.FlexibleSpace();
                gradeStyle.normal.textColor = GradeColour(c.Grade);
                GUILayout.Label(c.GradeName, gradeStyle);
                GUILayout.EndHorizontal();
                foreach (var n in c.Notes)
                    GUILayout.Label("·  " + n, note);
                GUILayout.Space(10f);
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label("OVERALL", heading);
            GUILayout.FlexibleSpace();
            gradeStyle.normal.textColor = GradeColour(shown.OverallGrade);
            GUILayout.Label(shown.OverallGradeName, gradeStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label($"\"{shown.InstructorRemark}\"  ({CrewRoster.Instructor})", remark);
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (fade >= 1f && GUILayout.Button("Retake the test", button, GUILayout.Width(w * 0.3f)))
                Retake();
            GUILayout.Space(16f);
            if (fade >= 1f && GUILayout.Button("Quit", button, GUILayout.Width(w * 0.2f)))
                Application.Quit();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.EndArea();
            GUI.color = Color.white;
        }

        void Retake()
        {
            shown = null;
            var session = GameSession.Instance;
            session.ClearSimulation();
            GameSession.SimulationReprogrammed = false;
            var player = PlayerLocator.Player != null ? PlayerLocator.Player.GetComponent<FirstPersonController>() : null;
            if (player != null)
                player.Locked = false;
            string scene = Application.CanStreamedLevelBeLoaded(GameSession.AcademyScene) ? GameSession.AcademyScene : GameSession.BridgeScene;
            session.Load(scene, null, Transition.Fade);
        }

        static Color GradeColour(int grade)
        {
            switch (grade)
            {
                case 4: return new Color(0.5f, 1f, 0.6f);
                case 3: return new Color(0.7f, 0.95f, 0.5f);
                case 2: return new Color(1f, 0.85f, 0.45f);
                case 1: return new Color(1f, 0.6f, 0.35f);
                case 0: return new Color(1f, 0.4f, 0.35f);
                default: return new Color(0.7f, 0.75f, 0.85f);
            }
        }

        void BuildStyles()
        {
            styledForHeight = Screen.height;
            int s = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.019f));
            subtitle = new GUIStyle(GUI.skin.label) { fontSize = s - 1, fontStyle = FontStyle.Bold };
            subtitle.normal.textColor = new Color(1f, 0.65f, 0.25f);
            title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(s * 2f), fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(0.92f, 0.94f, 1f);
            heading = new GUIStyle(GUI.skin.label) { fontSize = s + 1, fontStyle = FontStyle.Bold };
            heading.normal.textColor = new Color(0.75f, 0.85f, 1f);
            gradeStyle = new GUIStyle(GUI.skin.label) { fontSize = s + 1, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            note = new GUIStyle(GUI.skin.label) { fontSize = s, wordWrap = true };
            note.normal.textColor = new Color(0.86f, 0.88f, 0.92f);
            remark = new GUIStyle(GUI.skin.label) { fontSize = s, wordWrap = true, fontStyle = FontStyle.Italic };
            remark.normal.textColor = new Color(1f, 0.85f, 0.6f);
            warning = new GUIStyle(GUI.skin.label) { fontSize = s, wordWrap = true, fontStyle = FontStyle.Bold };
            warning.normal.textColor = new Color(1f, 0.4f, 0.35f);
            button = new GUIStyle(GUI.skin.button) { fontSize = s + 1, fontStyle = FontStyle.Bold, fixedHeight = s * 2.6f };
        }
    }
}

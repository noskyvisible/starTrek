using StarTrek.Core;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.UI
{
    /// <summary>
    /// Persistent HUD layer on the <see cref="GameSession"/>: the current objective, the warp-core and
    /// auto-destruct countdowns, and the scene-change cover (a fade, or the transporter shimmer).
    /// </summary>
    public class MissionHud : MonoBehaviour
    {
        GameSession session;
        GUIStyle objectiveTitle, objectiveText, countdown, controlsHint;
        int styledForHeight;
        Texture2D white;
        readonly System.Random rng = new System.Random(1701);

        string toast;
        float toastUntil;

        void Awake() => session = GetComponent<GameSession>();

        bool showControls;
        float startedAt;
        GUIStyle controlsStyle;

        const string Controls =
            "<b>CONTROLS</b>\n" +
            "WASD  move     Mouse  look     Shift  run     C  crouch\n" +
            "E  use, sit, press console buttons     Space  stand up\n" +
            "Look at a bridge station, then 1-8  give that station an order\n" +
            "F  hand phaser     Click  fire     X  stun / kill\n" +
            "T  tricorder (hold it on something to scan)\n" +
            "V  communicator (away team: call for transport)\n" +
            "Esc  free the mouse     F10  graphics quality     F1  this panel";

        void Start() => startedAt = Time.unscaledTime;

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
                return;
            if (kb.f1Key.wasPressedThisFrame)
                showControls = !showControls;
            // F10 cycles the graphics tiers (Low / High / Ultra), for laptops and stronger machines alike.
            if (!kb.f10Key.wasPressedThisFrame)
                return;
            int next = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
            QualitySettings.SetQualityLevel(next, true);
            toast = "Graphics: " + QualitySettings.names[next];
            toastUntil = Time.unscaledTime + 2.5f;
        }

        void OnGUI()
        {
            if (session == null)
                return;
            if (objectiveTitle == null || styledForHeight != Screen.height)
                BuildStyles();
            GUI.depth = -10;

            if (session.Running && session.Cover < 0.5f)
            {
                DrawObjective(session.Mission);
                DrawCountdowns(session.Mission);
            }
            if (session.Cover > 0.001f)
                DrawCover(session.Cover, session.CoverStyle);
            if (showControls)
                DrawControls();
            else if (Time.unscaledTime - startedAt < 12f && session.Cover < 0.5f)
                Shadowed(new Rect(0f, Screen.height * 0.94f, Screen.width * 0.98f, objectiveText.fontSize * 1.6f), "F1  Controls", controlsHint, new Color(0.8f, 0.85f, 0.95f, 0.8f));
            if (toast != null && Time.unscaledTime < toastUntil)
                Shadowed(new Rect(0f, Screen.height * 0.12f, Screen.width, countdown.fontSize * 1.5f), toast, countdown, new Color(0.8f, 0.9f, 1f));
        }

        void DrawObjective(Mission mission)
        {
            if (string.IsNullOrEmpty(mission.Objective))
                return;
            float w = Screen.width * 0.3f;
            var r = new Rect(Screen.width * 0.02f, Screen.height * 0.03f, w, objectiveText.fontSize * 1.6f);
            Shadowed(r, "OBJECTIVE", objectiveTitle, new Color(1f, 0.72f, 0.3f));
            float h = objectiveText.CalcHeight(new GUIContent(mission.Objective), w);
            Shadowed(new Rect(r.x, r.yMax, w, h), mission.Objective, objectiveText, new Color(0.92f, 0.93f, 1f));
        }

        void DrawCountdowns(Mission mission)
        {
            string text = null;
            if (mission.AutoDestructRemaining >= 0f)
                text = $"AUTO-DESTRUCT  {Mathf.CeilToInt(mission.AutoDestructRemaining)}";
            else if (mission.BreachRemaining >= 0f)
            {
                int s = Mathf.CeilToInt(mission.BreachRemaining);
                text = $"WARP CORE BREACH  {s / 60}:{s % 60:00}";
            }
            if (text == null)
                return;
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
            var r = new Rect(0f, Screen.height * 0.06f, Screen.width, countdown.fontSize * 1.5f);
            Shadowed(r, text, countdown, new Color(1f, 0.25f, 0.2f, pulse));
        }

        void DrawCover(float amount, Transition style)
        {
            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            if (style == Transition.Fade)
            {
                GUI.color = new Color(0f, 0f, 0f, amount);
                GUI.DrawTexture(full, white);
                GUI.color = Color.white;
                return;
            }
            // Transporter: the view dissolves into sparkling light, then into white-blue.
            GUI.color = new Color(0.75f, 0.85f, 1f, Mathf.SmoothStep(0f, 1f, (amount - 0.35f) / 0.65f));
            GUI.DrawTexture(full, white);
            int sparks = (int)(900 * Mathf.Sin(Mathf.PI * Mathf.Min(1f, amount * 1.2f)) + 40);
            float size = Mathf.Max(2f, Screen.height * 0.004f);
            for (int i = 0; i < sparks; i++)
            {
                float x = (float)rng.NextDouble() * Screen.width;
                float y = (float)rng.NextDouble() * Screen.height;
                float b = (float)rng.NextDouble();
                GUI.color = new Color(0.85f + 0.15f * b, 0.9f + 0.1f * b, 1f, 0.5f + 0.5f * b);
                GUI.DrawTexture(new Rect(x, y, size, size * (1f + 3f * b)), white);
            }
            GUI.color = Color.white;
        }

        void DrawControls()
        {
            float w = Mathf.Min(Screen.width * 0.6f, Screen.height * 1.1f);
            float h = controlsStyle.CalcHeight(new GUIContent(Controls), w - 32f) + 28f;
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h);
            GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.88f);
            GUI.DrawTexture(r, white);
            GUI.color = new Color(1f, 0.62f, 0.2f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), white);
            GUI.color = Color.white;
            controlsStyle.normal.textColor = new Color(0.9f, 0.92f, 1f);
            GUI.Label(new Rect(r.x + 16f, r.y + 14f, w - 32f, h - 20f), Controls, controlsStyle);
        }

        void Shadowed(Rect r, string text, GUIStyle style, Color colour)
        {
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * colour.a);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
            style.normal.textColor = colour;
            GUI.Label(r, text, style);
        }

        void BuildStyles()
        {
            styledForHeight = Screen.height;
            int size = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.018f));
            objectiveTitle = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold };
            objectiveText = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true };
            countdown = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(size * 1.9f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            controlsStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(size * 1.1f), richText = true, wordWrap = true };
            controlsHint = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.MiddleRight };
            if (white == null)
                white = Texture2D.whiteTexture;
        }
    }
}

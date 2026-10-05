using System.Collections.Generic;
using StarTrek.Core;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.Bridge
{
    /// <summary>
    /// HUD log of what is said: the captain's orders, the crew's replies and reports, and voices from
    /// outside (the Maru, the Klingons, the instructor). On the bridge it follows the ship host; on
    /// the freighter it hears the bridge over the communicator. Placeholder for voice.
    /// </summary>
    public class BridgeMessageLog : MonoBehaviour
    {
        const int MaxLines = 6;
        const float LifeSeconds = 12f;

        static BridgeMessageLog current;

        struct Entry
        {
            public string Text;
            public Color Colour;
            public float Time;
        }

        readonly List<Entry> entries = new List<Entry>();
        ShipSimHost host;
        Starship ship;
        Mission mission;
        GUIStyle style;
        int styledForHeight;

        static readonly Color CaptainColour = new Color(0.85f, 0.9f, 1f);
        static readonly Color CrewColour = new Color(1f, 0.82f, 0.5f);
        static readonly Color RefusedColour = new Color(1f, 0.45f, 0.4f);
        static readonly Color VoiceColour = new Color(0.55f, 0.85f, 1f);

        /// <summary>Show a line from someone who isn't a station (the instructor, the simulator).</summary>
        public static void Post(string speaker, string text)
        {
            if (current != null)
                current.Add($"{speaker.ToUpperInvariant()}: {text}", VoiceColour);
        }

        void OnEnable() => current = this;

        void Start()
        {
            host = ShipSimHost.Instance;
            if (host != null)
            {
                host.Message += OnStationMessage;
                host.CaptainSpoke += OnCaptainSpoke;
            }
            else if (GameSession.Exists && GameSession.Instance.Ship != null)
            {
                // Away from the bridge: station reports arrive over the communicator.
                ship = GameSession.Instance.Ship;
                ship.EventRaised += OnShipEvent;
            }
            if (GameSession.Exists && GameSession.Instance.Mission != null)
            {
                mission = GameSession.Instance.Mission;
                mission.Spoke += OnVoice;
            }
        }

        void OnDestroy()
        {
            if (current == this)
                current = null;
            if (host != null)
            {
                host.Message -= OnStationMessage;
                host.CaptainSpoke -= OnCaptainSpoke;
            }
            if (ship != null)
                ship.EventRaised -= OnShipEvent;
            if (mission != null)
                mission.Spoke -= OnVoice;
        }

        void OnCaptainSpoke(string text) => Add("CAPTAIN: " + text, CaptainColour);

        void OnShipEvent(ShipEvent e) => Add($"BRIDGE, {Who(e.Station)}: {e.Text}", CrewColour);

        void OnStationMessage(StationRole station, string text, bool accepted)
            => Add($"{Who(station)}: {text}", accepted ? CrewColour : RefusedColour);

        void OnVoice(string speaker, string text) => Add($"{speaker.ToUpperInvariant()}: {text}", VoiceColour);

        static string Who(StationRole station) => $"{CrewRoster.Officer(station).ToUpperInvariant()} ({Names.Of(station)})";

        void Add(string text, Color colour)
        {
            entries.Add(new Entry { Text = text, Colour = colour, Time = Time.time });
            if (entries.Count > MaxLines)
                entries.RemoveAt(0);
        }

        void OnGUI()
        {
            if (entries.Count == 0)
                return;
            if (style == null || styledForHeight != Screen.height)
            {
                styledForHeight = Screen.height;
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.018f)), wordWrap = true };
            }
            float w = Screen.width * 0.42f;
            float y = Screen.height * 0.97f;
            // Newest at the bottom; each entry as tall as its wrapped text.
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                float age = Time.time - e.Time;
                if (age > LifeSeconds)
                    continue;
                float h = style.CalcHeight(new GUIContent(e.Text), w) + 2f;
                y -= h;
                float alpha = Mathf.Clamp01((LifeSeconds - age) / 2f);
                var r = new Rect(Screen.width * 0.02f, y, w, h);
                style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
                GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), e.Text, style);
                style.normal.textColor = new Color(e.Colour.r, e.Colour.g, e.Colour.b, alpha);
                GUI.Label(r, e.Text, style);
            }
        }
    }
}

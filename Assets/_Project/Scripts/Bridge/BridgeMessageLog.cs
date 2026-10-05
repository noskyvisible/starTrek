using System.Collections.Generic;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.Bridge
{
    /// <summary>HUD log of the captain's orders and the crew's replies and reports (placeholder for voice).</summary>
    public class BridgeMessageLog : MonoBehaviour
    {
        const int MaxLines = 6;
        const float LifeSeconds = 12f;

        struct Entry
        {
            public string Text;
            public Color Colour;
            public float Time;
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly Dictionary<StationRole, string> officers = new Dictionary<StationRole, string>();
        ShipSimHost host;
        GUIStyle style;
        int styledForHeight;

        void Start()
        {
            host = ShipSimHost.Instance;
            if (host == null)
                return;
            foreach (var console in FindObjectsByType<StationConsole>())
                officers[console.Role] = console.Officer;
            host.Message += OnMessage;
            host.CaptainSpoke += text => Add("CAPTAIN: " + text, new Color(0.85f, 0.9f, 1f));
        }

        void OnDestroy()
        {
            if (host != null)
                host.Message -= OnMessage;
        }

        void OnMessage(StationRole station, string text, bool accepted)
        {
            string who = officers.TryGetValue(station, out var name) ? $"{name.ToUpperInvariant()} ({Names.Of(station)})" : Names.Of(station).ToUpperInvariant();
            Add($"{who}: {text}", accepted ? new Color(1f, 0.82f, 0.5f) : new Color(1f, 0.45f, 0.4f));
        }

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
            float lineH = style.fontSize * 2.6f;
            float y = Screen.height * 0.97f - lineH * entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                float age = Time.time - e.Time;
                if (age > LifeSeconds)
                    continue;
                float alpha = Mathf.Clamp01((LifeSeconds - age) / 2f);
                var r = new Rect(Screen.width * 0.02f, y + i * lineH, w, lineH);
                style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
                GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), e.Text, style);
                style.normal.textColor = new Color(e.Colour.r, e.Colour.g, e.Colour.b, alpha);
                GUI.Label(r, e.Text, style);
            }
        }
    }
}

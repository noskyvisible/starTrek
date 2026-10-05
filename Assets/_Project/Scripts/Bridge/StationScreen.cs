using System.Collections.Generic;
using StarTrek.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace StarTrek.Bridge
{
    /// <summary>
    /// A live station screen: a world-space panel redrawn from the ship simulation a few times a
    /// second (headers, readouts, bars, flashing warnings). Graybox styling until the real
    /// Starfleet-style display design exists.
    /// </summary>
    public class StationScreen : MonoBehaviour
    {
        const float PixelsPerMetre = 1000f;
        const int MaxRows = 9;
        const float RefreshSeconds = 0.25f;

        static readonly Color Background = new Color(0.015f, 0.02f, 0.035f, 0.97f);
        static readonly Color LabelColour = new Color(0.72f, 0.8f, 0.92f);
        static readonly Color ValueColour = new Color(1f, 0.92f, 0.75f);
        static readonly Color WarningColour = new Color(1f, 0.2f, 0.15f);
        static readonly Color BarBack = new Color(0.1f, 0.12f, 0.16f);

        class Row
        {
            public GameObject Root;
            public Text Label, Value, Block;
            public Image BarBack, BarFill;
        }

        ShipSimHost host;
        StationRole role;
        int screen;
        Color accent;
        Text header;
        Vector2 px;
        float rowHeight;
        readonly List<Row> rows = new List<Row>();
        readonly List<ReadoutLine> lines = new List<ReadoutLine>();
        float nextRefresh;

        static Font font;
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static StationScreen Create(Transform anchor, Vector2 sizeMetres, ShipSimHost host, StationRole role, int screen, Color accent, string title)
        {
            var go = new GameObject($"Screen_{role}_{screen}", typeof(RectTransform));
            go.transform.SetParent(anchor, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeMetres * PixelsPerMetre;
            rt.localScale = Vector3.one / PixelsPerMetre;

            var s = go.AddComponent<StationScreen>();
            s.host = host;
            s.role = role;
            s.screen = screen;
            s.accent = accent;
            s.px = rt.sizeDelta;
            s.Build(title);
            return s;
        }

        void Build(string title)
        {
            Panel("Background", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Background);
            rowHeight = px.y / (MaxRows + 1.6f);

            var bar = Panel("Header", transform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -rowHeight * 1.3f), Vector2.zero, accent * 0.85f);
            header = Label(bar.transform, title, Mathf.RoundToInt(rowHeight * 0.62f), TextAnchor.MiddleLeft, new Color(0.05f, 0.04f, 0.03f));
            header.fontStyle = FontStyle.Bold;
            Inset((RectTransform)header.transform, px.x * 0.04f);

            for (int i = 0; i < MaxRows; i++)
                rows.Add(MakeRow(i));
        }

        Row MakeRow(int index)
        {
            float top = -rowHeight * (1.6f + index);
            var root = new GameObject("Row" + index, typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(px.x * 0.04f, top - rowHeight);
            rt.offsetMax = new Vector2(-px.x * 0.04f, top);

            int size = Mathf.RoundToInt(rowHeight * 0.56f);
            var row = new Row { Root = root };
            row.Label = Label(root.transform, "", size, TextAnchor.MiddleLeft, LabelColour);
            row.Value = Label(root.transform, "", size, TextAnchor.MiddleRight, ValueColour);
            row.BarBack = Panel("BarBack", root.transform, new Vector2(0.48f, 0.22f), new Vector2(1f, 0.78f), Vector2.zero, Vector2.zero, BarBack).GetComponent<Image>();
            row.BarFill = Panel("BarFill", row.BarBack.transform, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, accent).GetComponent<Image>();
            row.Value.transform.SetAsLastSibling();
            row.Block = Label(root.transform, "", size, TextAnchor.UpperLeft, ValueColour);
            row.Block.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.Block.verticalOverflow = VerticalWrapMode.Overflow;
            return row;
        }

        void Update()
        {
            if (Time.time < nextRefresh || host == null)
                return;
            nextRefresh = Time.time + RefreshSeconds;
            StationReadouts.Build(host.Ship, role, screen, lines);
            bool blinkOn = Mathf.Repeat(Time.time, 1f) < 0.65f;

            int r = 0;
            foreach (var line in lines)
            {
                if (line.Kind == ReadoutKind.Header)
                {
                    header.text = line.Label;
                    continue;
                }
                if (r >= rows.Count)
                    break;
                Show(rows[r++], line, blinkOn);
            }
            for (; r < rows.Count; r++)
                rows[r].Root.SetActive(false);
        }

        void Show(Row row, ReadoutLine line, bool blinkOn)
        {
            row.Root.SetActive(true);
            bool bar = line.Kind == ReadoutKind.Bar;
            bool block = line.Kind == ReadoutKind.Text && line.Label == null;
            row.BarBack.gameObject.SetActive(bar);
            row.Block.gameObject.SetActive(block);
            row.Label.gameObject.SetActive(!block);
            row.Value.gameObject.SetActive(!block);

            if (block)
            {
                row.Block.text = line.Value;
                return;
            }
            if (line.Kind == ReadoutKind.Warning)
            {
                row.Label.text = blinkOn ? line.Label : "";
                row.Label.color = WarningColour;
                row.Label.alignment = TextAnchor.MiddleCenter;
                row.Value.text = "";
                return;
            }
            row.Label.text = line.Label;
            row.Label.color = LabelColour;
            row.Label.alignment = TextAnchor.MiddleLeft;
            row.Value.text = line.Value;
            if (bar)
            {
                var fill = (RectTransform)row.BarFill.transform;
                fill.anchorMax = new Vector2(line.Fraction, 1f);
                row.BarFill.color = line.Fraction < 0.25f ? WarningColour : accent;
            }
        }

        static GameObject Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.GetComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
            return go;
        }

        internal static Text Label(Transform parent, string text, int size, TextAnchor align, Color colour)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = colour;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static void Inset(RectTransform rt, float x)
        {
            rt.offsetMin = new Vector2(x, 0f);
            rt.offsetMax = new Vector2(-x, 0f);
        }
    }

    /// <summary>Button labels printed on a station's control surface.</summary>
    public class StationLabels : MonoBehaviour
    {
        const float PixelsPerMetre = 1000f;
        RectTransform rect;

        public static StationLabels Create(Transform surface, Vector2 sizeMetres, float height)
        {
            var go = new GameObject("ButtonLabels", typeof(RectTransform));
            go.transform.SetParent(surface, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            // Lie flat on the surface, readable by the operator: canvas forward into the surface, text up = away from operator.
            go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeMetres * PixelsPerMetre;
            rt.localScale = Vector3.one / PixelsPerMetre;
            var labels = go.AddComponent<StationLabels>();
            labels.rect = rt;
            return labels;
        }

        /// <summary>Dark text printed on a button face of the given size (metres).</summary>
        public void Add(string text, Vector2 surfacePosition, Vector2 faceSize)
        {
            var t = StationScreen.Label(transform, text, 30, TextAnchor.MiddleCenter, new Color(0.06f, 0.04f, 0.03f));
            t.fontStyle = FontStyle.Bold;
            t.lineSpacing = 0.85f;
            var rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = faceSize * PixelsPerMetre;
            rt.anchoredPosition = surfacePosition * PixelsPerMetre;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
    }
}

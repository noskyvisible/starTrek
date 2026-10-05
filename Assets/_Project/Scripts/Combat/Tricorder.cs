using StarTrek.Audio;
using UnityEngine;

namespace StarTrek.Combat
{
    /// <summary>
    /// The cadet's tricorder: while it is open it points a life-sign finder and a radiation meter at
    /// the world, and reads whatever <see cref="ScanTarget"/> the cadet holds it on for a moment.
    /// Opened by <see cref="PlayerEquipment"/>.
    /// </summary>
    public class Tricorder : MonoBehaviour
    {
        [SerializeField] Transform view;
        [SerializeField] AudioSource audioSource;
        [SerializeField] float range = 14f;
        [SerializeField] float scanSeconds = 1.2f;
        [SerializeField] float coneDegrees = 14f;
        [SerializeField] float lifeSignRange = 60f;

        ScanTarget target, shown;
        float progress, chirp;
        GUIStyle header, body, small;
        int styledForHeight;

        public bool Open { get; set; }
        public ScanTarget Target => target;
        public float Progress => progress;

        void Update()
        {
            if (!Open || view == null)
            {
                target = null;
                progress = 0f;
                return;
            }
            var t = FindTarget();
            if (t != target)
            {
                target = t;
                progress = shown == t && t != null ? 1f : 0f;
            }
            if (target == null)
                return;
            if (progress < 1f)
            {
                progress += Time.deltaTime / scanSeconds;
                chirp -= Time.deltaTime;
                if (chirp <= 0f && audioSource != null)
                {
                    chirp = 0.3f;
                    audioSource.PlayOneShot(ProceduralSfx.Scan, 0.25f);
                }
                if (progress >= 1f)
                {
                    progress = 1f;
                    shown = target;
                    target.CompleteScan();
                    if (audioSource != null)
                        audioSource.PlayOneShot(ProceduralSfx.Chirp, 0.5f);
                }
            }
        }

        ScanTarget FindTarget()
        {
            ScanTarget best = null;
            float bestScore = float.MaxValue;
            foreach (var s in ScanTarget.Active)
            {
                Vector3 aim = AimPoint(s);
                Vector3 to = aim - view.position;
                float d = to.magnitude;
                if (d > range || d < 0.01f)
                    continue;
                float angle = Vector3.Angle(view.forward, to);
                if (angle > coneDegrees)
                    continue;
                if (Physics.Raycast(view.position, to / d, out RaycastHit hit, d - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && !hit.collider.transform.IsChildOf(s.transform) && !s.transform.IsChildOf(hit.collider.transform))
                    continue;
                float score = angle + d * 0.4f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            return best;
        }

        static Vector3 AimPoint(ScanTarget s)
        {
            var c = s.GetComponentInChildren<Collider>();
            return c != null ? c.bounds.center : s.transform.position;
        }

        void OnGUI()
        {
            if (!Open)
                return;
            if (header == null || styledForHeight != Screen.height)
            {
                styledForHeight = Screen.height;
                int size = Mathf.Max(11, Mathf.RoundToInt(Screen.height * 0.017f));
                header = new GUIStyle(GUI.skin.label) { fontSize = size + 1, fontStyle = FontStyle.Bold };
                body = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true, richText = true };
                small = new GUIStyle(body) { fontSize = size - 1 };
            }
            float w = Screen.width * 0.27f, h = Screen.height * 0.34f;
            var panel = new Rect(Screen.width - w - Screen.width * 0.02f, Screen.height * 0.6f - h * 0.5f, w, h);
            GUI.color = new Color(0.02f, 0.05f, 0.04f, 0.82f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 1f, 0.6f, 0.9f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float x = panel.x + 12f, y = panel.y + 8f, iw = w - 24f, line = body.fontSize * 1.5f;
            header.normal.textColor = new Color(0.45f, 1f, 0.65f);
            GUI.Label(new Rect(x, y, iw, line), "TRICORDER", header);
            y += line * 1.1f;

            body.normal.textColor = new Color(0.8f, 1f, 0.85f);
            GUI.Label(new Rect(x, y, iw, line), LifeSignLine(), body);
            y += line;
            float rad = RadiationZone.LevelAt(view.position, true);
            string radText = rad <= 0.01f ? "normal" : rad < 0.4f ? "<color=#ffd060>elevated</color>" : rad < 0.7f ? "<color=#ff9040>dangerous</color>" : "<color=#ff4030>LETHAL</color>";
            GUI.Label(new Rect(x, y, iw, line), $"RADIATION  {radText}", body);
            DrawBar(new Rect(x + iw * 0.55f, y + line * 0.3f, iw * 0.45f, line * 0.4f), rad, new Color(1f, 0.6f, 0.2f));
            y += line * 1.3f;

            if (target == null)
            {
                small.normal.textColor = new Color(0.6f, 0.85f, 0.7f);
                GUI.Label(new Rect(x, y, iw, line * 2f), "Point at something to scan it.", small);
                return;
            }
            header.normal.textColor = new Color(1f, 0.85f, 0.5f);
            GUI.Label(new Rect(x, y, iw, line), target.ScanName.ToUpperInvariant(), header);
            y += line;
            if (progress < 1f)
            {
                DrawBar(new Rect(x, y + line * 0.25f, iw, line * 0.45f), progress, new Color(0.45f, 1f, 0.65f));
                return;
            }
            small.normal.textColor = new Color(0.85f, 1f, 0.9f);
            GUI.Label(new Rect(x, y, iw, panel.yMax - y - 6f), target.Reading, small);
        }

        string LifeSignLine()
        {
            int total = 0;
            ScanTarget nearest = null;
            float nearestD = float.MaxValue;
            foreach (var s in ScanTarget.Active)
            {
                if (s.LifeSigns <= 0)
                    continue;
                float d = Vector3.Distance(view.position, s.transform.position);
                if (d > lifeSignRange)
                    continue;
                total += s.LifeSigns;
                if (d < nearestD)
                {
                    nearestD = d;
                    nearest = s;
                }
            }
            if (nearest == null)
                return "LIFE SIGNS  none in range";
            Vector3 to = nearest.transform.position - view.position;
            to.y = 0f;
            Vector3 fwd = view.forward;
            fwd.y = 0f;
            float bearing = Vector3.SignedAngle(fwd, to, Vector3.up);
            string dir = Mathf.Abs(bearing) < 15f ? "ahead" : bearing < 0f ? $"{-bearing:0}° left" : $"{bearing:0}° right";
            return $"LIFE SIGNS  {total}  ·  nearest {nearestD:0} m {dir}";
        }

        static void DrawBar(Rect r, float value, Color colour)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = colour;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value), r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}

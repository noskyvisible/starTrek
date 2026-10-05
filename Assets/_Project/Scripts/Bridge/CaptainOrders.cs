using System.Text;
using StarTrek.Interaction;
using StarTrek.Player;
using StarTrek.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarTrek.Bridge
{
    /// <summary>
    /// Command by looking: aim at any bridge station (from the chair or across the room) to see its
    /// available orders, then press 1-8 to give one. The station's officer carries it out.
    /// Close-up buttons and seats take priority over the order menu.
    /// </summary>
    public class CaptainOrders : MonoBehaviour
    {
        static readonly Key[] DigitKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };

        [SerializeField] Transform view;
        [SerializeField] Interactor interactor;
        [SerializeField] FirstPersonController body;
        [SerializeField] float range = 18f;

        GUIStyle titleStyle, lineStyle;
        int styledForHeight;
        StationConsole menuFor;
        string menuText;

        /// <summary>The station whose orders are showing, or null.</summary>
        public StationConsole Target { get; private set; }

        void Update()
        {
            Target = FindTarget();
            if (Target == null || ShipSimHost.Instance == null || Keyboard.current == null)
                return;
            var commands = CommandCatalog.For(Target.Role);
            for (int i = 0; i < commands.Count && i < DigitKeys.Length; i++)
                if (Keyboard.current[DigitKeys[i]].wasPressedThisFrame)
                    ShipSimHost.Instance.Order(commands[i].Id);
        }

        StationConsole FindTarget()
        {
            if (interactor != null && interactor.Current != null)
                return null;
            if (!Physics.Raycast(view.position, view.forward, out RaycastHit hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return null;
            var console = StationLink.Resolve(hit.collider);
            // At your own station you work the buttons yourself.
            if (console != null && body != null && body.IsSeated && StationLink.Resolve(body.CurrentSeat) == console)
                return null;
            return console;
        }

        void OnGUI()
        {
            if (Target == null)
                return;
            if (titleStyle == null || styledForHeight != Screen.height)
            {
                styledForHeight = Screen.height;
                int size = Mathf.Max(13, Mathf.RoundToInt(Screen.height * 0.02f));
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = size + 3, fontStyle = FontStyle.Bold, richText = true };
                lineStyle = new GUIStyle(GUI.skin.label) { fontSize = size, richText = true };
            }
            if (menuFor != Target)
            {
                menuFor = Target;
                var sb = new StringBuilder();
                var commands = CommandCatalog.For(Target.Role);
                for (int i = 0; i < commands.Count && i < DigitKeys.Length; i++)
                    sb.Append($"<color=#ffc66b>{i + 1}</color>  {commands[i].Order}\n");
                menuText = sb.ToString();
            }

            float w = Screen.width * 0.26f;
            float lineH = lineStyle.fontSize * 1.45f;
            float h = lineH * (CommandCatalog.For(Target.Role).Count + 3.2f);
            var box = new Rect(Screen.width - w - Screen.width * 0.02f, Screen.height * 0.18f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            string hex = ColorUtility.ToHtmlStringRGB(Target.Accent);
            GUI.Label(new Rect(box.x + 12, box.y + 6, w - 24, lineH * 1.3f), $"<color=#{hex}>{Target.Title}</color>", titleStyle);
            GUI.Label(new Rect(box.x + 12, box.y + 6 + lineH * 1.2f, w - 24, lineH), Target.Officer, lineStyle);
            GUI.Label(new Rect(box.x + 12, box.y + lineH * 2.5f, w - 24, h), menuText, lineStyle);
        }
    }
}

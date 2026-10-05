using System.Collections.Generic;
using StarTrek.Audio;
using StarTrek.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTrek.Bridge
{
    /// <summary>
    /// One bridge station: its role, its officer, a grid of physical buttons (one per command) on the
    /// control surface, and live screens. Buttons and screens are generated from the command catalogue
    /// when the scene starts.
    /// </summary>
    public class StationConsole : MonoBehaviour
    {
        const int Columns = 4;
        const int Rows = 2;
        public const float ButtonHeight = 0.014f;

        [SerializeField] StationRole role;
        [SerializeField] string officer = "Cadet";
        [Tooltip("Buttons are laid out in this transform's XZ plane: +Y is the surface normal, +Z points away from the operator.")]
        [SerializeField] Transform controlSurface;
        [SerializeField] Vector2 surfaceSize = new Vector2(1.3f, 0.42f);
        [Tooltip("Where the screens go: +Z points into the screen (the way the viewer looks).")]
        [SerializeField] Transform[] screenAnchors;
        [SerializeField] Vector2 screenSize = new Vector2(0.62f, 0.88f);
        [SerializeField] Color accent = new Color(1f, 0.62f, 0.2f);
        [SerializeField] AudioSource audioSource;

        static readonly Dictionary<Color, Material> ButtonMaterials = new Dictionary<Color, Material>();

        public StationRole Role => role;
        public string Officer => officer;
        public Color Accent => accent;
        public string Title => Names.Of(role).ToUpperInvariant();

        void Start()
        {
            var host = ShipSimHost.Instance;
            if (host == null)
            {
                Debug.LogWarning($"[StarTrek] {name}: no ShipSimHost in the scene; station is inert.");
                return;
            }
            if (controlSurface != null)
                BuildButtons(host);
            if (screenAnchors != null)
                for (int i = 0; i < screenAnchors.Length; i++)
                    StationScreen.Create(screenAnchors[i], screenSize, host, role, i, accent, Title);
        }

        void BuildButtons(ShipSimHost host)
        {
            var commands = CommandCatalog.For(role);
            float cellW = surfaceSize.x / Columns;
            float cellH = surfaceSize.y / Rows;
            var labels = StationLabels.Create(controlSurface, surfaceSize, ButtonHeight + 0.0015f);

            for (int i = 0; i < commands.Count && i < Columns * Rows; i++)
            {
                int col = i % Columns;
                int row = i / Columns;   // row 0 is the far row
                float x = -surfaceSize.x / 2f + cellW * (col + 0.5f);
                float z = surfaceSize.y / 2f - cellH * (row + 0.5f);

                var def = commands[i];
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "BTN_" + def.Id;
                go.transform.SetParent(controlSurface, false);
                go.transform.localPosition = new Vector3(x, ButtonHeight / 2f, z);
                go.transform.localScale = new Vector3(cellW * 0.86f, ButtonHeight, cellH * 0.72f);
                var renderer = go.GetComponent<MeshRenderer>();
                Color c = ColourFor(def.Style);
                renderer.sharedMaterial = MaterialFor(c);
                renderer.shadowCastingMode = ShadowCastingMode.Off;

                var button = go.AddComponent<StationButton>();
                button.Init(this, host, def, c);

                // Label printed on the button face.
                labels.Add(def.Button, new Vector2(x, z), new Vector2(cellW * 0.84f, cellH * 0.7f));
            }
        }

        Color ColourFor(CommandStyle style)
        {
            switch (style)
            {
                case CommandStyle.Weapons: return new Color(1f, 0.32f, 0.1f);
                case CommandStyle.Alert: return new Color(1f, 0.08f, 0.06f);
                default: return accent;
            }
        }

        static Material MaterialFor(Color c)
        {
            if (ButtonMaterials.TryGetValue(c, out var m) && m != null)
                return m;
            m = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial.shader) { name = "M_StationButton" };
            m.SetColor("_BaseColor", c * 0.35f);
            m.SetFloat("_Smoothness", 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 1.4f);
            ButtonMaterials[c] = m;
            return m;
        }

        public void PlayClick(bool accepted)
        {
            if (audioSource != null)
                audioSource.PlayOneShot(accepted ? SfxBank.ButtonPress : SfxBank.ButtonDenied);
        }
    }
}

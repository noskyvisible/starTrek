using StarTrek.Interaction;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.Bridge
{
    /// <summary>A physical station button for one command. Dips, flashes and chirps when pressed.</summary>
    public class StationButton : MonoBehaviour, IInteractable
    {
        const float PressSeconds = 0.22f;
        const float PressDepth = 0.005f;   // shallow, so the printed label stays on the face

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        StationConsole console;
        ShipSimHost host;
        CommandDefinition command;
        Color colour;
        Renderer body;
        MaterialPropertyBlock block;
        Vector3 rest;
        float pressedAt = float.NegativeInfinity;
        bool lastAccepted;

        public CommandDefinition Command => command;
        public string Prompt => command.Button;
        public bool CanInteract => Time.time - pressedAt > 0.3f;

        public void Init(StationConsole owner, ShipSimHost simHost, CommandDefinition def, Color c)
        {
            console = owner;
            host = simHost;
            command = def;
            colour = c;
            body = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            rest = transform.localPosition;
        }

        public void Interact(Interactor interactor)
        {
            pressedAt = Time.time;
            lastAccepted = host.Issue(command.Id).Accepted;
            console.PlayClick(lastAccepted);
        }

        void Update()
        {
            float k = (Time.time - pressedAt) / PressSeconds;
            if (k > 2f)
                return;
            transform.localPosition = rest - Vector3.up * (PressDepth * (1f - Mathf.Clamp01(k)));
            // Flash bright on success, red on refusal, then settle back to the normal glow.
            Color flash = lastAccepted ? colour * 4f : new Color(1f, 0.05f, 0.05f) * 3f;
            body.GetPropertyBlock(block);
            block.SetColor(EmissionId, Color.Lerp(flash, colour * 1.4f, Mathf.Clamp01(k * 0.5f)));
            body.SetPropertyBlock(block);
        }
    }
}

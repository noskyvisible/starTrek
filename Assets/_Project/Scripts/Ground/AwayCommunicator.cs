using StarTrek.Audio;
using StarTrek.Core;
using StarTrek.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarTrek.Ground
{
    /// <summary>The away team's communicator [V]: ask the ship to beam you (and the tagged survivors) back.</summary>
    public class AwayCommunicator : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;
        [SerializeField] AudioSource audioSource;

        InputAction call;
        FirstPersonController body;
        GUIStyle style;
        int styledForHeight;

        void Awake()
        {
            call = actions.FindActionMap("Player", true).FindAction("Communicator", true);
            body = GetComponent<FirstPersonController>();
        }

        void Update()
        {
            if (body != null && !body.InputEnabled)
                return;
            if (!call.WasPressedThisFrame() || !GameSession.Exists || !GameSession.Instance.Running)
                return;
            if (audioSource != null)
                audioSource.PlayOneShot(ProceduralSfx.Chirp, 0.7f);
            GameSession.Instance.Mission.RequestBeamBack();
        }

        void OnGUI()
        {
            if (!GameSession.Exists || !GameSession.Instance.Running || !GameSession.Instance.Mission.Record.LeakSealed)
                return;
            if (style == null || styledForHeight != Screen.height)
            {
                styledForHeight = Screen.height;
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.02f)), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            var r = new Rect(0f, Screen.height * 0.82f, Screen.width, style.fontSize * 2f);
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), "[V]  Communicator: \"Away team to ship. Energise.\"", style);
            style.normal.textColor = new Color(0.6f, 0.85f, 1f);
            GUI.Label(r, "[V]  Communicator: \"Away team to ship. Energise.\"", style);
        }
    }
}

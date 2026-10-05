using StarTrek.Interaction;
using UnityEngine;

namespace StarTrek.UI
{
    /// <summary>
    /// Temporary HUD for the graybox: a crosshair dot and the interaction prompt.
    /// To be replaced by the real Starfleet-style UI.
    /// </summary>
    public class GrayboxHud : MonoBehaviour
    {
        [SerializeField] Interactor interactor;
        [SerializeField] Color promptColor = new Color(1f, 0.78f, 0.42f);

        GUIStyle style;
        Texture2D dot;
        string shownPrompt;
        string text;
        int styledForHeight;

        void OnGUI()
        {
            if (style == null || styledForHeight != Screen.height)
                BuildStyle();

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), dot);
            GUI.color = Color.white;

            IInteractable current = interactor ? interactor.Current : null;
            if (current == null)
                return;

            string prompt = current.Prompt;
            if (!ReferenceEquals(prompt, shownPrompt))
            {
                shownPrompt = prompt;
                text = "[" + interactor.InteractKeyLabel + "]  " + prompt;
            }

            var rect = new Rect(0f, cy + Screen.height * 0.05f, Screen.width, style.fontSize * 2f);
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            style.normal.textColor = promptColor;
            GUI.Label(rect, text, style);
        }

        void BuildStyle()
        {
            styledForHeight = Screen.height;
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.026f)),
                fontStyle = FontStyle.Bold
            };
            if (dot == null)
            {
                dot = new Texture2D(1, 1);
                dot.SetPixel(0, 0, Color.white);
                dot.Apply();
            }
        }
    }
}

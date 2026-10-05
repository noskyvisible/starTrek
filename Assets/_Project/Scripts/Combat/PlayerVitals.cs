using StarTrek.Core;
using StarTrek.Player;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.Combat
{
    /// <summary>
    /// The cadet's health in play: a red edge on hits and low health, camera shake, and what going
    /// down means. On the freighter the bridge pulls you out; on the bridge it ends the test.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerVitals : MonoBehaviour
    {
        Health health;
        FirstPersonController body;
        CameraShake shake;
        float flash;

        public Health Health => health;

        void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponent<FirstPersonController>();
            shake = GetComponentInChildren<CameraShake>();
            health.Damaged += OnDamaged;
            health.Downed += OnDowned;
        }

        void OnDamaged(float amount, DamageKind kind, Vector3 from)
        {
            if (kind == DamageKind.Radiation)
            {
                flash = Mathf.Max(flash, 0.25f);
                return;
            }
            flash = 1f;
            if (shake != null)
                shake.Add(Mathf.Clamp01(amount / 40f) * 0.5f);
        }

        void OnDowned(bool killed)
        {
            if (body != null)
                body.Locked = true;
            var session = GameSession.Exists ? GameSession.Instance : null;
            if (session == null || !session.Running)
                return;
            if (session.Mission.Beat == MissionBeat.AwayMission)
                session.Mission.ReportAwayTeamInjured();
            else
                session.Mission.ReportCaptainDown();
        }

        void OnEnable()
        {
            if (GameSession.Exists)
                GameSession.Instance.Arrived += OnArrived;
        }

        void OnDisable()
        {
            if (GameSession.Exists)
                GameSession.Instance.Arrived -= OnArrived;
        }

        void OnArrived(string spawn)
        {
            if (!health.IsDown)
                return;
            health.Revive();
            if (body != null)
                body.Locked = false;
        }

        void Update() => flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 1.8f);

        void OnGUI()
        {
            float low = 1f - health.Current / health.Max;
            float a = Mathf.Max(flash * 0.45f, low * low * 0.6f);
            if (health.IsDown)
                a = 0.75f;
            if (a <= 0.01f)
                return;
            // Red edges, heavier toward the corners.
            float edge = Screen.height * 0.18f;
            GUI.color = new Color(0.7f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, edge), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - edge, Screen.width, edge), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, edge, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width - edge, 0, edge, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 0f, 0f, a * 0.35f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}

using StarTrek.Core;
using StarTrek.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarTrek.Dev
{
    /// <summary>
    /// Development only (Editor and development builds): Page Down jumps the running test to its next
    /// beat, so later beats can be tried without flying the whole mission.
    /// </summary>
    public class MissionDebugKeys : MonoBehaviour
    {
        void Awake()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
                Destroy(this);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.pageDownKey.wasPressedThisFrame || !GameSession.Exists || !GameSession.Instance.Running)
                return;
            var mission = GameSession.Instance.Mission;
            var next = mission.Beat == MissionBeat.Rescue ? MissionBeat.Ambush : mission.Beat + 1;
            if (next == MissionBeat.AwayMission)
                next = MissionBeat.Ambush;
            Debug.Log($"[StarTrek] Dev: skipping from {mission.Beat} to {next}.");
            mission.SkipTo(next);
        }
    }
}

using System.Collections;
using StarTrek.Bridge;
using StarTrek.Core;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.Ground
{
    /// <summary>
    /// The freighter scene's director: the away team's arrival briefing, and (when the scene is played
    /// on its own in the Editor) a test already at the away-mission beat.
    /// </summary>
    public class AwayMissionScene : MonoBehaviour
    {
        IEnumerator Start()
        {
            var session = GameSession.Instance;
            if (!session.Running)
            {
                session.StartSimulation();
                session.Mission.BeginAwayMissionHere();
            }
            yield return new WaitForSeconds(2.5f);
            BridgeMessageLog.Post("Bridge", "Away team, we read you aboard the Maru. Life signs are forward of your position. And watch the radiation: it's coming from their fuel control, off the main spine.");
            yield return new WaitForSeconds(5f);
            BridgeMessageLog.Post("Away team", "Tricorder's up. It's dark in here. Emergency power only.");
        }
    }
}

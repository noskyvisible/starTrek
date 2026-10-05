using System.Collections.Generic;
using System.Text;
using StarTrek.Interaction;
using StarTrek.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace StarTrek.EditorTools
{
    /// <summary>
    /// Bakes a temporary NavMesh for a player-sized agent from the open scene's colliders and checks
    /// that the player can walk from the spawn point to every seat's stand-up point and every door.
    /// Catches layouts where furniture leaves gaps narrower than the player.
    /// </summary>
    public static class WalkabilityCheck
    {
        const float AgentRadius = 0.3f;   // CharacterController radius
        const float AgentHeight = 1.8f;
        const float AgentClimb = 0.3f;    // CharacterController step offset

        [MenuItem("StarTrek/Check Walkability (open scene)")]
        static void RunFromMenu() => Debug.Log(Run());

        /// <returns>A report; the first line is "PASS" or "FAIL".</returns>
        public static string Run()
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            if (player == null)
                return "FAIL\nNo player in the open scene.";

            int ignore = LayerMask.NameToLayer("Ignore Raycast");
            var bounds = new Bounds(player.transform.position, new Vector3(200f, 40f, 200f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~(1 << ignore), NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);

            var settings = NavMesh.CreateSettings();
            settings.agentRadius = AgentRadius;
            settings.agentHeight = AgentHeight;
            settings.agentClimb = AgentClimb;
            settings.agentSlope = 45f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            var instance = NavMesh.AddNavMeshData(data);

            try
            {
                var report = new StringBuilder();
                bool allOk = true;
                if (!NavMesh.SamplePosition(player.transform.position, out var startHit, 1f, NavMesh.AllAreas))
                    return "FAIL\nSpawn point is not on walkable floor.";

                var targets = new List<(string name, Vector3 pos)>();
                foreach (var seat in Object.FindObjectsByType<Seat>())
                    targets.Add((seat.name, seat.ExitPoint.position));
                foreach (var door in Object.FindObjectsByType<SlidingDoor>())
                    targets.Add((door.name + " (front)", door.transform.position + door.transform.forward * -0.9f));

                var path = new NavMeshPath();
                foreach (var (name, pos) in targets)
                {
                    string result;
                    if (!NavMesh.SamplePosition(pos, out var hit, 0.6f, NavMesh.AllAreas))
                        result = "not on walkable floor";
                    else if (!NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path) ||
                             path.status != NavMeshPathStatus.PathComplete)
                        result = "NO PATH";
                    else
                        result = "ok, " + PathLength(path).ToString("F1") + " m";
                    bool ok = result.StartsWith("ok");
                    allOk &= ok;
                    report.Append(ok ? "  ok   " : "  FAIL ").Append(name).Append(": ").Append(result).Append('\n');
                }
                return (allOk ? "PASS" : "FAIL") + $" ({targets.Count} targets, agent r={AgentRadius} m)\n" + report;
            }
            finally
            {
                NavMesh.RemoveNavMeshData(instance);
            }
        }

        static float PathLength(NavMeshPath path)
        {
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return length;
        }
    }
}

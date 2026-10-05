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
            // Doors open as the player walks up (locked ones are puzzles the player solves), so bake them open.
            var opened = new List<Collider>();
            foreach (var door in Object.FindObjectsByType<SlidingDoor>())
                if (door != null)
                    foreach (var c in door.GetComponentsInChildren<Collider>())
                        if (c.enabled && c is BoxCollider)
                        {
                            c.enabled = false;
                            opened.Add(c);
                        }
            NavMeshBuilder.CollectSources(bounds, ~(1 << ignore), NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            foreach (var c in opened)
                c.enabled = true;

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

                // Each target is reachable if any of its candidate points is (a door from either side).
                var targets = new List<(string name, Vector3[] points)>();
                foreach (var seat in Object.FindObjectsByType<Seat>())
                    targets.Add((seat.name, new[] { seat.ExitPoint.position }));
                foreach (var s in Object.FindObjectsByType<StarTrek.Ground.Survivor>())
                    targets.Add((s.name, new[] { s.transform.position + s.transform.right * 0.7f, s.transform.position - s.transform.right * 0.7f, s.transform.position + s.transform.forward * 0.8f }));
                foreach (var c in Object.FindObjectsByType<StarTrek.Ground.LeakControl>())
                    targets.Add((c.name, new[] { c.transform.position - c.transform.right * 0.9f, c.transform.position + c.transform.right * 0.9f }));
                foreach (var door in Object.FindObjectsByType<SlidingDoor>())
                {
                    Vector3 centre = DoorwayCentre(door);
                    Vector3 normal = DoorNormal(door);
                    targets.Add((door.name + (door.Locked ? " (either side)" : " (doorway)"), door.Locked
                        ? new[] { centre + normal * 0.9f, centre - normal * 0.9f }
                        : new[] { centre, centre + normal * 0.9f, centre - normal * 0.9f }));
                }

                var path = new NavMeshPath();
                foreach (var (name, points) in targets)
                {
                    string result = "not on walkable floor";
                    foreach (var pos in points)
                    {
                        if (!NavMesh.SamplePosition(pos, out var hit, 0.6f, NavMesh.AllAreas))
                            continue;
                        if (!NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path) ||
                            path.status != NavMeshPathStatus.PathComplete)
                        {
                            result = "NO PATH";
                            continue;
                        }
                        result = "ok, " + PathLength(path).ToString("F1") + " m";
                        break;
                    }
                    bool ok = result.StartsWith("ok");
                    allOk &= ok;
                    report.Append(ok ? "  ok   " : "  FAIL ").Append(name).Append(": ").Append(result).Append('\n');
                }
                return (allOk ? "PASS" : "FAIL") + $" ({targets.Count} targets, agent r={AgentRadius} m)\n" + report;
            }
            finally
            {
                NavMesh.RemoveNavMeshData(instance);
                // CreateSettings registers a new agent type in the project settings; don't leave it there.
                NavMesh.RemoveSettings(settings.agentTypeID);
            }
        }

        /// <summary>Floor point in the middle of the doorway, between the two leaves.</summary>
        static Vector3 DoorwayCentre(SlidingDoor door)
        {
            var (l, r) = Leaves(door);
            if (l == null || r == null)
                return door.transform.position;
            Vector3 c = (l.position + r.position) * 0.5f;
            c.y = door.transform.position.y;
            return c;
        }

        /// <summary>Horizontal direction through the doorway (perpendicular to the leaves).</summary>
        static Vector3 DoorNormal(SlidingDoor door)
        {
            var (l, r) = Leaves(door);
            if (l == null || r == null)
                return door.transform.forward;
            Vector3 across = r.position - l.position;
            across.y = 0f;
            return Vector3.Cross(Vector3.up, across.normalized);
        }

        static (Transform, Transform) Leaves(SlidingDoor door)
        {
            var so = new SerializedObject(door);
            return ((Transform)so.FindProperty("leftLeaf").objectReferenceValue, (Transform)so.FindProperty("rightLeaf").objectReferenceValue);
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

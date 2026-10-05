using System.Collections.Generic;
using StarTrek.Player;
using UnityEngine;
using UnityEngine.AI;

namespace StarTrek.Dev
{
    /// <summary>
    /// Test helper: walks the player to a target with the real controller, steering along a NavMesh
    /// path with scripted forward input. Proves a route is walkable in practice (steps, gaps), not
    /// just that a path exists. Scripted input keeps working when the Editor loses focus.
    /// </summary>
    public class AutoWalker : MonoBehaviour
    {
        public enum State { Walking, Arrived, Failed }

        const float CornerReach = 0.35f;
        const float TimeoutSeconds = 40f;

        FirstPersonController player;
        Vector3[] corners;
        int next;
        float startTime;
        NavMeshDataInstance navMesh;

        public State Status { get; private set; } = State.Walking;
        public string Detail { get; private set; } = "";

        /// <summary>Start walking <paramref name="playerObject"/> to <paramref name="target"/>.</summary>
        public static AutoWalker Begin(GameObject playerObject, Vector3 target)
        {
            var walker = playerObject.AddComponent<AutoWalker>();
            walker.Plan(target);
            return walker;
        }

        void Plan(Vector3 target)
        {
            player = GetComponent<FirstPersonController>();
            startTime = Time.time;

            var bounds = new Bounds(transform.position, new Vector3(200f, 40f, 200f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~(1 << gameObject.layer), NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            var settings = NavMesh.CreateSettings();
            settings.agentRadius = 0.3f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.3f;
            navMesh = NavMesh.AddNavMeshData(NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity));

            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(transform.position, out var a, 1f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(target, out var b, 1f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                Finish(State.Failed, "no complete path");
                return;
            }
            corners = path.corners;
            next = 1;
            player.SetScriptedMove(Vector2.up);
        }

        void Update()
        {
            if (Status != State.Walking)
                return;
            if (Time.time - startTime > TimeoutSeconds)
            {
                Finish(State.Failed, $"timed out at {transform.position:F2}, corner {next}/{corners.Length - 1}");
                return;
            }

            Vector3 to = corners[next] - transform.position;
            to.y = 0f;
            if (to.magnitude < CornerReach)
            {
                if (++next >= corners.Length)
                {
                    Finish(State.Arrived, $"arrived in {Time.time - startTime:F1} s");
                    return;
                }
                to = corners[next] - transform.position;
                to.y = 0f;
            }
            player.SetLook(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 10f);
        }

        void Finish(State state, string detail)
        {
            Status = state;
            Detail = detail;
            if (player != null)
                player.SetScriptedMove(null);
            if (navMesh.valid)
                NavMesh.RemoveNavMeshData(navMesh);
        }

        void OnDestroy()
        {
            if (navMesh.valid)
                NavMesh.RemoveNavMeshData(navMesh);
        }
    }
}

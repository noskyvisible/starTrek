using System.Collections.Generic;
using UnityEngine;

namespace StarTrek.Core
{
    /// <summary>A named place the player arrives at when a scene loads (forward = facing).</summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] string id = "Default";

        static readonly List<SpawnPoint> All = new List<SpawnPoint>();

        public string Id => id;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static SpawnPoint Find(string id)
        {
            foreach (var s in All)
                if (s.id == id)
                    return s;
            return null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.3f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.up * 0.1f + transform.forward * 0.8f);
        }
    }
}

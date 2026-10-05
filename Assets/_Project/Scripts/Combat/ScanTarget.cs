using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace StarTrek.Combat
{
    /// <summary>Something the tricorder can read: survivors, hazards, systems, locks, the Klingons.</summary>
    public class ScanTarget : MonoBehaviour
    {
        [SerializeField] string scanName = "Unknown";
        [TextArea(2, 6)]
        [SerializeField] string reading = "";
        [Tooltip("Counts toward the tricorder's life-sign finder.")]
        [SerializeField] int lifeSigns;
        [SerializeField] UnityEvent onScanned = new UnityEvent();

        static readonly List<ScanTarget> All = new List<ScanTarget>();

        public string ScanName { get => scanName; set => scanName = value; }
        public string Reading { get => reading; set => reading = value; }
        public int LifeSigns { get => lifeSigns; set => lifeSigns = value; }
        public bool Scanned { get; private set; }
        public UnityEvent OnScanned => onScanned;

        public static IReadOnlyList<ScanTarget> Active => All;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Configure(string name, string text, int lives)
        {
            scanName = name;
            reading = text;
            lifeSigns = lives;
        }

        /// <summary>Called by the tricorder once a scan of this target completes.</summary>
        public void CompleteScan()
        {
            bool first = !Scanned;
            Scanned = true;
            if (first)
                onScanned.Invoke();
        }
    }
}

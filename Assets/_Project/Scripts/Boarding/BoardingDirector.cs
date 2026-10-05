using System.Collections;
using System.Collections.Generic;
using StarTrek.Core;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.Boarding
{
    /// <summary>Beams Klingon boarders onto the bridge when the mission says so, and reports them down.</summary>
    public class BoardingDirector : MonoBehaviour
    {
        [SerializeField] GameObject boarderPrefab;
        [SerializeField] Transform[] spawnPoints;
        [Tooltip("Which boarders (by order) carry a bat'leth instead of a disruptor.")]
        [SerializeField] int[] batlethIndices = { 1 };

        readonly List<KlingonBoarder> spawned = new List<KlingonBoarder>();
        Mission mission;

        public IReadOnlyList<KlingonBoarder> Spawned => spawned;

        void Start()
        {
            if (!GameSession.Exists || GameSession.Instance.Mission == null)
                return;
            mission = GameSession.Instance.Mission;
            mission.BoardersBeamIn += OnBoardersBeamIn;
        }

        void OnDestroy()
        {
            if (mission != null)
                mission.BoardersBeamIn -= OnBoardersBeamIn;
        }

        void OnBoardersBeamIn(int count) => StartCoroutine(BeamIn(count));

        IEnumerator BeamIn(int count)
        {
            for (int i = 0; i < count && spawnPoints.Length > 0; i++)
            {
                var point = spawnPoints[i % spawnPoints.Length];
                var go = Instantiate(boarderPrefab, point.position, point.rotation);
                go.name = $"Klingon_Boarder_{i + 1}";
                var boarder = go.GetComponent<KlingonBoarder>();
                boarder.Weapon = System.Array.IndexOf(batlethIndices, i) >= 0 ? KlingonBoarder.Arms.Batleth : KlingonBoarder.Arms.Disruptor;
                boarder.Downed += (b, killed) => { if (mission != null) mission.ReportBoarderDown(killed); };
                spawned.Add(boarder);
                boarder.BeamIn();
                yield return new WaitForSeconds(0.7f);
            }
        }

        public void FreezeAll()
        {
            foreach (var b in spawned)
                if (b != null)
                    b.Freeze();
        }

        public void StandDownAll()
        {
            foreach (var b in spawned)
                if (b != null && !b.IsDown)
                    b.StandDown();
        }
    }
}

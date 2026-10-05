using System;
using System.Collections;
using StarTrek.Player;
using StarTrek.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarTrek.Core
{
    public enum Transition { Fade, Transporter }

    /// <summary>
    /// Lives across scene loads. Owns the running Kobayashi Maru test (the ship and the mission) and
    /// ticks it whichever scene the cadet is in, moves the player between the Academy, the simulator
    /// bridge and the freighter (with a fade or a transporter shimmer), and remembers the hallway
    /// easter egg.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public const string AcademyScene = "Academy_Simulator";
        public const string BridgeScene = "Deck01_Bridge";
        public const string FreighterScene = "KM_Freighter";
        public const string ArrivalSpawn = "AwayTeamArrival";
        public const string ReturnSpawn = "AwayTeamReturn";

        static GameSession instance;

        /// <summary>The session, created on first use.</summary>
        public static GameSession Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("GameSession");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<GameSession>();
                    go.AddComponent<StarTrek.UI.MissionHud>();
                    go.AddComponent<StarTrek.Dev.MissionDebugKeys>();
                }
                return instance;
            }
        }

        public static bool Exists => instance != null;

        /// <summary>Set by the maintenance terminal in the Academy hallway, before the test starts.</summary>
        public static bool SimulationReprogrammed { get; set; }

        public Starship Ship { get; private set; }
        public Mission Mission { get; private set; }
        public bool Running => Mission != null && Mission.Beat != MissionBeat.Ended;

        /// <summary>0 = clear, 1 = covered; drawn by the HUD.</summary>
        public float Cover { get; private set; }
        public Transition CoverStyle { get; private set; }
        public bool Transitioning { get; private set; }

        public event Action SimulationStarted;
        /// <summary>A scene finished loading and the player is at the spawn.</summary>
        public event Action<string> Arrived;

        AudioSource sfx;

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
        }

        /// <summary>Start a fresh test (ship, mission, the easter egg if it was found).</summary>
        public void StartSimulation()
        {
            Ship = Starship.KobayashiMaruTest();
            Mission = new Mission(Ship, SimulationReprogrammed);
            Mission.AwayTeamBeamOut += () => Load(FreighterScene, ArrivalSpawn, Transition.Transporter, 1.4f);
            Mission.AwayTeamBeamBack += () => Load(BridgeScene, ReturnSpawn, Transition.Transporter, 0.6f);
            SimulationStarted?.Invoke();
        }

        /// <summary>The test is over: drop it, so the next bridge visit starts a new one.</summary>
        public void ClearSimulation()
        {
            Ship = null;
            Mission = null;
        }

        void Update()
        {
            if (!Running)
                return;
            float dt = Time.deltaTime;
            Ship.Tick(dt);
            Mission.Tick(dt);
        }

        // ------------------------------------------------------------------ scene flow

        public void Load(string scene, string spawnId, Transition style, float delay = 0f)
        {
            if (Transitioning)
                return;
            StartCoroutine(LoadRoutine(scene, spawnId, style, delay));
        }

        IEnumerator LoadRoutine(string scene, string spawnId, Transition style, float delay)
        {
            Transitioning = true;
            CoverStyle = style;
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float outSeconds = style == Transition.Transporter ? 1.6f : 0.6f;
            if (style == Transition.Transporter)
                sfx.PlayOneShot(StarTrek.Audio.ProceduralSfx.Transporter, 0.8f);
            yield return Animate(0f, 1f, outSeconds);

            var load = SceneManager.LoadSceneAsync(scene);
            while (!load.isDone)
                yield return null;
            yield return null;   // let Awake/Start run

            PlacePlayer(spawnId);
            if (scene == BridgeScene && spawnId == ReturnSpawn && Mission != null)
                Mission.AwayTeamArrivedBack();
            Arrived?.Invoke(spawnId);

            if (style == Transition.Transporter)
                sfx.PlayOneShot(StarTrek.Audio.ProceduralSfx.Transporter, 0.6f);
            yield return Animate(1f, 0f, style == Transition.Transporter ? 1.4f : 0.6f);
            Transitioning = false;
        }

        IEnumerator Animate(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                Cover = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            Cover = to;
        }

        static void PlacePlayer(string spawnId)
        {
            if (string.IsNullOrEmpty(spawnId))
                return;
            var spawn = SpawnPoint.Find(spawnId);
            var player = PlayerLocator.Player != null ? PlayerLocator.Player.GetComponent<FirstPersonController>() : null;
            if (spawn != null && player != null)
                player.Teleport(spawn.transform.position, spawn.transform.eulerAngles.y);
        }
    }
}

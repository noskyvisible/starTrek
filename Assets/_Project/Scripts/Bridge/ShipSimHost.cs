using System;
using StarTrek.Core;
using StarTrek.Crew;
using StarTrek.Ship;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.Bridge
{
    /// <summary>
    /// The bridge's link to the running test (owned by the <see cref="GameSession"/>, so it survives
    /// the away mission) and the one place commands enter it, whether from a station button, a
    /// captain's order or a scripted event. Every reply and report is raised as a
    /// <see cref="Message"/> for the HUD log (and, later, voice).
    /// </summary>
    public class ShipSimHost : MonoBehaviour
    {
        [SerializeField] AlertController alertDisplay;

        public static ShipSimHost Instance { get; private set; }
        public Starship Ship { get; private set; }
        public Mission Mission { get; private set; }

        /// <summary>station, text, accepted (false for refusals).</summary>
        public event Action<StationRole, string, bool> Message;
        /// <summary>The captain's spoken order, for the log.</summary>
        public event Action<string> CaptainSpoke;

        void Awake()
        {
            Instance = this;
            var session = GameSession.Instance;
            if (!session.Running)
                session.StartSimulation();
            Ship = session.Ship;
            Mission = session.Mission;
            Ship.EventRaised += OnShipEvent;
            Ship.AlertChanged += OnAlertChanged;
        }

        void Start()
        {
            // Back from the away mission: show the alert state the ship is already in.
            if (alertDisplay != null)
                alertDisplay.SetLevel(Ship.Alert);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (Ship != null)
            {
                Ship.EventRaised -= OnShipEvent;
                Ship.AlertChanged -= OnAlertChanged;
            }
        }

        void OnShipEvent(ShipEvent e) => Message?.Invoke(e.Station, e.Text, true);

        void OnAlertChanged(AlertLevel level)
        {
            if (alertDisplay != null)
                alertDisplay.SetLevel(level);
        }

        bool Over => Mission != null && Mission.Beat == MissionBeat.Ended;

        /// <summary>Carry out a command as if the station's own controls were used.</summary>
        public CommandResult Issue(CommandId id)
        {
            var station = CommandCatalog.Get(id).Station;
            if (Over)
                return Refuse(station, "The simulation has ended.");
            var result = Ship.Execute(id);
            Message?.Invoke(result.Station, result.Reply, result.Accepted);
            return result;
        }

        /// <summary>The captain gives an order out loud; the station's officer carries it out.</summary>
        public CommandResult Order(CommandId id)
        {
            var station = CommandCatalog.Get(id).Station;
            CaptainSpoke?.Invoke(CommandCatalog.Get(id).Order + ".");
            if (!Over && CrewOfficer.IsDown(station))
                return Refuse(station, $"No answer from {Names.Of(station)}! {CrewRoster.Officer(station)} is down. Someone has to take that station.");
            return Issue(id);
        }

        CommandResult Refuse(StationRole station, string text)
        {
            var result = new CommandResult(false, station, text);
            Message?.Invoke(station, text, false);
            return result;
        }

        /// <summary>For buttons wired in the Editor (e.g. the captain's armrest).</summary>
        public void ToggleRedAlert() => Order(CommandId.RedAlert);
    }
}

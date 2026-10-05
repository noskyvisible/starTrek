using System;
using StarTrek.Ship;
using StarTrek.Simulation;
using UnityEngine;

namespace StarTrek.Bridge
{
    /// <summary>
    /// Runs the ship simulation in the scene and is the one place commands enter it, whether from a
    /// station button, a captain's order or a scripted event. Every reply and report is raised as a
    /// <see cref="Message"/> for the HUD log (and, later, voice).
    /// </summary>
    public class ShipSimHost : MonoBehaviour
    {
        [SerializeField] AlertController alertDisplay;

        public static ShipSimHost Instance { get; private set; }
        public Starship Ship { get; private set; }

        /// <summary>station, text, accepted (false for refusals).</summary>
        public event Action<StationRole, string, bool> Message;
        /// <summary>The captain's spoken order, for the log.</summary>
        public event Action<string> CaptainSpoke;

        void Awake()
        {
            Instance = this;
            Ship = Starship.KobayashiMaruTest();
            Ship.EventRaised += e => Message?.Invoke(e.Station, e.Text, true);
            Ship.AlertChanged += level =>
            {
                if (alertDisplay != null)
                    alertDisplay.SetLevel(level);
            };
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update() => Ship.Tick(Time.deltaTime);

        /// <summary>Carry out a command as if the station's own controls were used.</summary>
        public CommandResult Issue(CommandId id)
        {
            var result = Ship.Execute(id);
            Message?.Invoke(result.Station, result.Reply, result.Accepted);
            return result;
        }

        /// <summary>The captain gives an order out loud; the station's officer carries it out.</summary>
        public CommandResult Order(CommandId id)
        {
            CaptainSpoke?.Invoke(CommandCatalog.Get(id).Order + ".");
            return Issue(id);
        }

        /// <summary>For buttons wired in the Editor (e.g. the captain's armrest).</summary>
        public void ToggleRedAlert() => Order(CommandId.RedAlert);
    }
}

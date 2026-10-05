using System.Collections.Generic;

namespace StarTrek.Simulation
{
    /// <summary>The Kobayashi Maru simulator crew: fellow cadets at every station (GAME_PROMPT §8).</summary>
    public static class CrewRoster
    {
        public const string Instructor = "Commander Hollis";

        static readonly Dictionary<StationRole, string> Officers = new Dictionary<StationRole, string>
        {
            { StationRole.Helm, "Cadet Rourke" }, { StationRole.Navigation, "Cadet Okafor" },
            { StationRole.Tactical, "Cadet Haines" }, { StationRole.Science, "Cadet Varela" },
            { StationRole.Communications, "Cadet Mbeki" }, { StationRole.Engineering, "Cadet Duffy" },
            { StationRole.Environmental, "Cadet Lindqvist" }, { StationRole.Security, "Cadet Takeda" },
            { StationRole.DamageControl, "Cadet Petrov" },
        };

        public static string Officer(StationRole role) => Officers.TryGetValue(role, out var name) ? name : Names.Of(role);
    }
}

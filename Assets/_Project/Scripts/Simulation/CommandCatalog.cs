using System.Collections.Generic;

namespace StarTrek.Simulation
{
    /// <summary>A bridge position. Each has an officer, screens and its own set of commands.</summary>
    public enum StationRole
    {
        Helm,
        Navigation,
        Tactical,
        Science,
        Communications,
        Engineering,
        Environmental,
        Security,
        DamageControl
    }

    /// <summary>Every order the ship understands. Station buttons and captain's orders both issue these.</summary>
    public enum CommandId
    {
        // Helm
        AllStop, ImpulseQuarter, ImpulseHalf, ImpulseFull, ComeAbout, EvasivePattern, AttackPattern, SteadyAsSheGoes,
        // Navigation
        PlotCourseDistressCall, PlotEscapeVector, EngageWarp, WarpUp, WarpDown, DropToImpulse, ReportPosition,
        // Tactical
        RedAlert, RaiseShields, LowerShields, ArmPhasers, FirePhasers, LoadTorpedoes, FireTorpedo, CycleTargetSystem,
        // Science
        ScanContact, NextContact, LongRangeScan, ScanLifeSigns, CloakSweep, AnalyseShields, MineScan,
        // Communications
        HailContact, AnswerDistressCall, JamComms, CallStarfleet, ShipwideIntercom, CloseChannel,
        // Engineering
        PowerToShields, PowerToWeapons, PowerToEngines, BalancePower, EmergencyPower, CoolWarpCore, EngineeringReport,
        // Environmental
        BoostLifeSupport, BoostInertialDampers, FireSuppression, EmergencyLighting, EnvironmentReport,
        // Security
        IntruderAlert, ForceFieldsBridge, ForceFieldsEngineering, SecurityToBridge, SecurityToEngineering, LockTurbolifts,
        // Damage control
        RepairShields, RepairWeapons, RepairEngines, RepairHull, SealBulkheads, StructuralIntegrityBoost
    }

    /// <summary>How a station button is coloured: routine, weapons, or alert/emergency.</summary>
    public enum CommandStyle
    {
        Routine,
        Weapons,
        Alert
    }

    public sealed class CommandDefinition
    {
        public CommandId Id { get; }
        public StationRole Station { get; }
        /// <summary>Short label printed on the station button.</summary>
        public string Button { get; }
        /// <summary>The captain's wording in the order menu.</summary>
        public string Order { get; }
        public CommandStyle Style { get; }

        public CommandDefinition(CommandId id, StationRole station, string button, string order, CommandStyle style = CommandStyle.Routine)
        {
            Id = id;
            Station = station;
            Button = button;
            Order = order;
            Style = style;
        }
    }

    /// <summary>The fixed list of commands per station, in button order (at most 8 per station).</summary>
    public static class CommandCatalog
    {
        public const int MaxPerStation = 8;

        static readonly Dictionary<StationRole, CommandDefinition[]> ByStation = new Dictionary<StationRole, CommandDefinition[]>();
        static readonly Dictionary<CommandId, CommandDefinition> ById = new Dictionary<CommandId, CommandDefinition>();

        static CommandCatalog()
        {
            Add(StationRole.Helm,
                D(CommandId.AllStop, "ALL STOP", "All stop"),
                D(CommandId.ImpulseQuarter, "1/4 IMPULSE", "One-quarter impulse"),
                D(CommandId.ImpulseHalf, "1/2 IMPULSE", "Half impulse"),
                D(CommandId.ImpulseFull, "FULL IMPULSE", "Full impulse"),
                D(CommandId.ComeAbout, "COME ABOUT", "Bring us about"),
                D(CommandId.EvasivePattern, "EVASIVE", "Evasive manoeuvres", CommandStyle.Alert),
                D(CommandId.AttackPattern, "ATTACK PATTERN", "Attack pattern on the target", CommandStyle.Weapons),
                D(CommandId.SteadyAsSheGoes, "STEADY", "Steady as she goes"));
            Add(StationRole.Navigation,
                D(CommandId.PlotCourseDistressCall, "COURSE: DISTRESS", "Plot a course to the distress call"),
                D(CommandId.PlotEscapeVector, "ESCAPE VECTOR", "Plot an escape vector", CommandStyle.Alert),
                D(CommandId.EngageWarp, "ENGAGE WARP", "Engage"),
                D(CommandId.WarpUp, "WARP +1", "Increase warp factor"),
                D(CommandId.WarpDown, "WARP -1", "Reduce warp factor"),
                D(CommandId.DropToImpulse, "DROP TO IMPULSE", "Drop to impulse"),
                D(CommandId.ReportPosition, "POSITION", "Report our position"));
            Add(StationRole.Tactical,
                D(CommandId.RedAlert, "RED ALERT", "Red alert", CommandStyle.Alert),
                D(CommandId.RaiseShields, "SHIELDS UP", "Raise shields"),
                D(CommandId.LowerShields, "SHIELDS DOWN", "Lower shields"),
                D(CommandId.ArmPhasers, "ARM PHASERS", "Arm phasers", CommandStyle.Weapons),
                D(CommandId.FirePhasers, "FIRE PHASERS", "Fire phasers", CommandStyle.Weapons),
                D(CommandId.LoadTorpedoes, "LOAD TORPEDO", "Load photon torpedoes", CommandStyle.Weapons),
                D(CommandId.FireTorpedo, "FIRE TORPEDO", "Fire torpedo", CommandStyle.Weapons),
                D(CommandId.CycleTargetSystem, "TARGET SYSTEM", "Change target system"));
            Add(StationRole.Science,
                D(CommandId.ScanContact, "SCAN CONTACT", "Scan the contact"),
                D(CommandId.NextContact, "NEXT CONTACT", "Next contact"),
                D(CommandId.LongRangeScan, "LONG RANGE", "Long-range sensor sweep"),
                D(CommandId.ScanLifeSigns, "LIFE SIGNS", "Scan for life signs"),
                D(CommandId.CloakSweep, "CLOAK SWEEP", "Sweep for cloaked ships", CommandStyle.Alert),
                D(CommandId.AnalyseShields, "SHIELD ANALYSIS", "Analyse their shields"),
                D(CommandId.MineScan, "MINE SCAN", "Scan for mines"));
            Add(StationRole.Communications,
                D(CommandId.HailContact, "HAIL", "Open hailing frequencies"),
                D(CommandId.AnswerDistressCall, "ANSWER DISTRESS", "Answer the distress call"),
                D(CommandId.JamComms, "JAM COMMS", "Jam their communications", CommandStyle.Alert),
                D(CommandId.CallStarfleet, "CALL STARFLEET", "Contact Starfleet Command"),
                D(CommandId.ShipwideIntercom, "SHIP-WIDE", "Open a ship-wide channel"),
                D(CommandId.CloseChannel, "CLOSE CHANNEL", "Close the channel"));
            Add(StationRole.Engineering,
                D(CommandId.PowerToShields, "POWER: SHIELDS", "More power to the shields"),
                D(CommandId.PowerToWeapons, "POWER: WEAPONS", "Divert power to weapons", CommandStyle.Weapons),
                D(CommandId.PowerToEngines, "POWER: ENGINES", "Divert power to the engines"),
                D(CommandId.BalancePower, "BALANCE POWER", "Balance the power distribution"),
                D(CommandId.EmergencyPower, "EMERGENCY POWER", "Emergency power", CommandStyle.Alert),
                D(CommandId.CoolWarpCore, "VENT CORE HEAT", "Bring the core temperature down"),
                D(CommandId.EngineeringReport, "STATUS", "Engineering, report"));
            Add(StationRole.Environmental,
                D(CommandId.BoostLifeSupport, "LIFE SUPPORT+", "Boost life support"),
                D(CommandId.BoostInertialDampers, "DAMPERS+", "Reinforce the inertial dampers"),
                D(CommandId.FireSuppression, "FIRE SUPPRESS", "Fire suppression", CommandStyle.Alert),
                D(CommandId.EmergencyLighting, "EMERG. LIGHTS", "Emergency lighting"),
                D(CommandId.EnvironmentReport, "STATUS", "Environmental report"));
            Add(StationRole.Security,
                D(CommandId.IntruderAlert, "INTRUDER ALERT", "Intruder alert", CommandStyle.Alert),
                D(CommandId.ForceFieldsBridge, "FIELDS: BRIDGE", "Force fields on the bridge"),
                D(CommandId.ForceFieldsEngineering, "FIELDS: ENGRG", "Force fields in engineering"),
                D(CommandId.SecurityToBridge, "TEAM TO BRIDGE", "Security to the bridge"),
                D(CommandId.SecurityToEngineering, "TEAM TO ENGRG", "Security to engineering"),
                D(CommandId.LockTurbolifts, "LOCK TURBOLIFTS", "Lock out the turbolifts"));
            Add(StationRole.DamageControl,
                D(CommandId.RepairShields, "REPAIR SHIELDS", "Repair crews to the shield generators"),
                D(CommandId.RepairWeapons, "REPAIR WEAPONS", "Repair crews to weapons"),
                D(CommandId.RepairEngines, "REPAIR ENGINES", "Repair crews to the engines"),
                D(CommandId.RepairHull, "REPAIR HULL", "Repair crews to the hull breaches"),
                D(CommandId.SealBulkheads, "SEAL BULKHEADS", "Seal the bulkheads", CommandStyle.Alert),
                D(CommandId.StructuralIntegrityBoost, "SIF BOOST", "Reinforce structural integrity"));
        }

        static CommandDefinition D(CommandId id, string button, string order, CommandStyle style = CommandStyle.Routine)
            => new CommandDefinition(id, default, button, order, style);

        static void Add(StationRole station, params CommandDefinition[] defs)
        {
            var list = new CommandDefinition[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var d = new CommandDefinition(defs[i].Id, station, defs[i].Button, defs[i].Order, defs[i].Style);
                list[i] = d;
                ById.Add(d.Id, d);
            }
            ByStation.Add(station, list);
        }

        public static IReadOnlyList<CommandDefinition> For(StationRole station) => ByStation[station];

        public static CommandDefinition Get(CommandId id) => ById[id];
    }

    /// <summary>What happened when a command was given: whether it was carried out and what the officer said.</summary>
    public readonly struct CommandResult
    {
        public readonly bool Accepted;
        public readonly StationRole Station;
        public readonly string Reply;

        public CommandResult(bool accepted, StationRole station, string reply)
        {
            Accepted = accepted;
            Station = station;
            Reply = reply;
        }
    }
}

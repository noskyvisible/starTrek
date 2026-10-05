using System.Collections.Generic;

namespace StarTrek.Simulation
{
    public enum ReadoutKind { Header, Text, Bar, Warning }

    /// <summary>One line on a station screen: a header, a label/value pair, a bar (0..1) or a warning.</summary>
    public readonly struct ReadoutLine
    {
        public readonly ReadoutKind Kind;
        public readonly string Label;
        public readonly string Value;
        public readonly float Fraction;

        public ReadoutLine(ReadoutKind kind, string label, string value = null, float fraction = 0f)
        {
            Kind = kind;
            Label = label;
            Value = value;
            Fraction = fraction;
        }
    }

    /// <summary>What each station shows on its two screens, built fresh from the ship's state.</summary>
    public static class StationReadouts
    {
        public const int ScreensPerStation = 2;

        public static void Build(Starship s, StationRole role, int screen, List<ReadoutLine> o)
        {
            o.Clear();
            switch (role)
            {
                case StationRole.Helm: if (screen == 0) Helm(s, o); else Course(s, o); break;
                case StationRole.Navigation: if (screen == 0) Navigation(s, o); else Chart(s, o); break;
                case StationRole.Tactical: if (screen == 0) ShieldScreen(s, o); else WeaponScreen(s, o); break;
                case StationRole.Science: if (screen == 0) SensorScreen(s, o); else ScanScreen(s, o); break;
                case StationRole.Communications: if (screen == 0) CommsScreen(s, o); else MessageScreen(s, o); break;
                case StationRole.Engineering: if (screen == 0) CoreScreen(s, o); else PowerScreen(s, o); break;
                case StationRole.Environmental: if (screen == 0) LifeSupportScreen(s, o); else SafetyScreen(s, o); break;
                case StationRole.Security: if (screen == 0) SecurityScreen(s, o); else InternalScreen(s, o); break;
                case StationRole.DamageControl: if (screen == 0) SystemsScreen(s, o); else HullScreen(s, o); break;
            }
        }

        static ReadoutLine H(string t) => new ReadoutLine(ReadoutKind.Header, t);
        static ReadoutLine T(string label, string value) => new ReadoutLine(ReadoutKind.Text, label, value);
        static ReadoutLine B(string label, float f, string value = null) => new ReadoutLine(ReadoutKind.Bar, label, value ?? Pct(f), Clamp01(f));
        static ReadoutLine W(string t) => new ReadoutLine(ReadoutKind.Warning, t);
        static string Pct(float f) => (f * 100f).ToString("0") + "%";
        static float Clamp01(float f) => f < 0f ? 0f : f > 1f ? 1f : f;
        static string OnOff(bool b) => b ? "ON" : "OFF";

        static string SpeedText(Flight f)
        {
            if (f.AtWarp)
                return "WARP " + f.WarpFactor;
            if (f.SpeedKmS < 1)
                return "ALL STOP";
            return (f.SpeedKmS / Flight.FullImpulseKmS).ToString("0.00") + " IMPULSE";
        }

        static void Helm(Starship s, List<ReadoutLine> o)
        {
            var f = s.Flight;
            o.Add(H("HELM"));
            o.Add(T("SPEED", SpeedText(f)));
            o.Add(T("VELOCITY", f.SpeedKmS.ToString("N0") + " KM/S"));
            o.Add(T("HEADING", f.Heading.ToString("000")));
            o.Add(T("MANOEUVRE", f.Manoeuvre == Manoeuvre.None ? "NONE" : f.Manoeuvre.ToString().ToUpperInvariant()));
            o.Add(B("IMPULSE SET", f.ImpulseSetting));
            o.Add(B("ENGINE POWER", s.Power.Factor(PowerTarget.Engines) / 1.5f, Pct(s.Power.Factor(PowerTarget.Engines))));
            if (s.Damage.Health(ShipSystem.Engines) < 0.5f)
                o.Add(W("ENGINES DAMAGED"));
        }

        static void Course(Starship s, List<ReadoutLine> o)
        {
            var f = s.Flight;
            o.Add(H("COURSE"));
            o.Add(T("DESTINATION", f.CourseName?.ToUpperInvariant() ?? "NONE"));
            if (f.CourseTarget != null)
            {
                o.Add(T("RANGE", f.DistanceTo(f.CourseTarget).ToString("N0") + " KM"));
                o.Add(T("BEARING", f.BearingTo(f.CourseTarget).ToString("000")));
                double eta = f.EtaSeconds;
                o.Add(T("ETA", eta < 0 ? "--" : eta < 1 ? "ARRIVING" : eta.ToString("0") + " S"));
            }
            else if (f.CourseName != null)
                o.Add(T("BEARING", f.TargetHeading.ToString("000")));
            if (s.InNeutralZone)
                o.Add(W("INSIDE NEUTRAL ZONE"));
        }

        static void Navigation(Starship s, List<ReadoutLine> o)
        {
            var f = s.Flight;
            o.Add(H("NAVIGATION"));
            o.Add(T("SECTOR", "GAMMA HYDRA"));
            o.Add(T("WARP FACTOR", f.WarpFactor.ToString()));
            o.Add(T("DRIVE", f.AtWarp ? "WARP ENGAGED" : "IMPULSE"));
            o.Add(T("NEUTRAL ZONE", s.InNeutralZone ? "INSIDE" : s.DistanceToNeutralZone.ToString("N0") + " KM"));
            if (s.InNeutralZone)
                o.Add(W("TREATY VIOLATION"));
        }

        static void Chart(Starship s, List<ReadoutLine> o)
        {
            o.Add(H("SENSOR CHART"));
            var visible = s.Sensors.Visible;
            if (visible.Count == 0)
                o.Add(T("CONTACTS", "NONE"));
            foreach (var c in visible)
                o.Add(T(c.Name.ToUpperInvariant(), s.Flight.DistanceTo(c).ToString("N0") + " KM  " + s.Flight.BearingTo(c).ToString("000")));
        }

        static void ShieldScreen(Starship s, List<ReadoutLine> o)
        {
            var sh = s.Shields;
            o.Add(H("DEFLECTOR SHIELDS"));
            o.Add(T("STATUS", sh.State.ToString().ToUpperInvariant()));
            foreach (ShieldFacing facing in new[] { ShieldFacing.Fore, ShieldFacing.Aft, ShieldFacing.Port, ShieldFacing.Starboard })
                o.Add(B(facing.ToString().ToUpperInvariant(), sh.Strength(facing) / Shields.MaxStrength));
            if (s.Alert == AlertLevel.Red)
                o.Add(W("RED ALERT"));
        }

        static void WeaponScreen(Starship s, List<ReadoutLine> o)
        {
            var w = s.Weapons;
            o.Add(H("WEAPONS"));
            o.Add(T("PHASERS", w.PhasersArmed ? (w.PhasersReady ? "READY" : "CHARGING") : "SAFE"));
            o.Add(B("PHASER CHARGE", w.PhaserCharge));
            o.Add(T("TORPEDOES", w.Torpedoes.ToString()));
            o.Add(B("TUBE", w.TorpedoLoadProgress, w.TorpedoLoaded ? "LOADED" : w.TorpedoLoading ? "LOADING" : "EMPTY"));
            var c = s.Sensors.Selected;
            o.Add(T("TARGET", c == null ? "NONE" : c.Name.ToUpperInvariant()));
            o.Add(T("AIM POINT", w.TargetSystem.ToString().ToUpperInvariant()));
            if (c != null && c.Kind == ContactKind.Civilian)
                o.Add(W("TARGET IS CIVILIAN"));
        }

        static void SensorScreen(Starship s, List<ReadoutLine> o)
        {
            var c = s.Sensors.Selected;
            o.Add(H("SENSORS"));
            if (c == null)
            {
                o.Add(T("CONTACT", "NONE"));
                return;
            }
            o.Add(T("CONTACT", c.Name.ToUpperInvariant()));
            o.Add(T("CLASS", c.Description.ToUpperInvariant()));
            o.Add(T("RANGE", s.Flight.DistanceTo(c).ToString("N0") + " KM"));
            o.Add(T("BEARING", s.Flight.BearingTo(c).ToString("000")));
            o.Add(T("TYPE", c.Kind.ToString().ToUpperInvariant()));
            if (s.Sensors.Scanning)
                o.Add(B(s.Sensors.ActiveScan.ToUpperInvariant(), s.Sensors.ScanProgress));
        }

        static void ScanScreen(Starship s, List<ReadoutLine> o)
        {
            o.Add(H("SCAN RESULTS"));
            o.Add(T(null, s.Sensors.LastResult));
        }

        static void CommsScreen(Starship s, List<ReadoutLine> o)
        {
            var c = s.Comms;
            o.Add(H("COMMUNICATIONS"));
            o.Add(T("CHANNEL", c.OpenChannel?.ToUpperInvariant() ?? "CLOSED"));
            o.Add(T("JAMMING", OnOff(c.Jamming)));
            o.Add(T("SHIP-WIDE", OnOff(c.ShipwideOpen)));
            o.Add(T("STARFLEET", c.StarfleetNotified ? "NOTIFIED" : "NOT CONTACTED"));
            if (c.DistressSignal != null)
                o.Add(W("DISTRESS SIGNAL"));
        }

        static void MessageScreen(Starship s, List<ReadoutLine> o)
        {
            o.Add(H("MESSAGES"));
            if (s.Comms.DistressSignal != null)
                o.Add(T(null, s.Comms.DistressSignal));
            o.Add(T("LAST", s.Comms.LastMessage));
        }

        static void CoreScreen(Starship s, List<ReadoutLine> o)
        {
            var p = s.Power;
            o.Add(H("WARP CORE"));
            o.Add(T("OUTPUT", Pct(p.Output)));
            o.Add(B("TEMPERATURE", p.CoreHeat / 1.3f, Pct(p.CoreHeat)));
            o.Add(T("EMERGENCY", OnOff(p.Emergency)));
            o.Add(T("VENTING", OnOff(p.Venting)));
            if (p.Critical)
                o.Add(W("CORE TEMPERATURE CRITICAL"));
            else if (p.CoreHeat >= 0.85f)
                o.Add(W("CORE TEMPERATURE HIGH"));
        }

        static void PowerScreen(Starship s, List<ReadoutLine> o)
        {
            var p = s.Power;
            o.Add(H("POWER DISTRIBUTION"));
            o.Add(B("SHIELDS", p.Share(PowerTarget.Shields) * 2f, Pct(p.Share(PowerTarget.Shields))));
            o.Add(B("WEAPONS", p.Share(PowerTarget.Weapons) * 2f, Pct(p.Share(PowerTarget.Weapons))));
            o.Add(B("ENGINES", p.Share(PowerTarget.Engines) * 2f, Pct(p.Share(PowerTarget.Engines))));
            o.Add(B("LIFE SUPPORT", p.Share(PowerTarget.LifeSupport) * 2f, Pct(p.Share(PowerTarget.LifeSupport))));
        }

        static void LifeSupportScreen(Starship s, List<ReadoutLine> o)
        {
            var e = s.Environment;
            o.Add(H("LIFE SUPPORT"));
            o.Add(B("LIFE SUPPORT", e.LifeSupport(s.Power, s.Damage.Health(ShipSystem.LifeSupport)) / 1.2f,
                Pct(e.LifeSupport(s.Power, s.Damage.Health(ShipSystem.LifeSupport)))));
            o.Add(B("INERTIAL DAMPERS", e.Dampers(s.Damage.Health(ShipSystem.Engines)) / 1.25f, Pct(e.Dampers(s.Damage.Health(ShipSystem.Engines)))));
            o.Add(T("ATMOSPHERE", "NOMINAL"));
            o.Add(T("GRAVITY", "1.00 G"));
            o.Add(T("LIGHTING", e.EmergencyLighting ? "EMERGENCY" : "NORMAL"));
        }

        static void SafetyScreen(Starship s, List<ReadoutLine> o)
        {
            var e = s.Environment;
            o.Add(H("FIRE & SAFETY"));
            o.Add(T("FIRES", e.Fires.ToString()));
            o.Add(T("SUPPRESSION", e.FireSuppressionSeconds > 0f ? "ACTIVE" : "STANDBY"));
            o.Add(T("BULKHEADS", s.Damage.BulkheadsSealed ? "SEALED" : "OPEN"));
            if (e.Fires > 0)
                o.Add(W("FIRE REPORTED"));
        }

        static void SecurityScreen(Starship s, List<ReadoutLine> o)
        {
            var sec = s.Security;
            o.Add(H("SECURITY"));
            o.Add(T("INTRUDER ALERT", OnOff(sec.IntruderAlert)));
            o.Add(T("INTRUDERS", sec.IntrudersDetected.ToString()));
            o.Add(T("TEAM", sec.TeamLocation.ToUpperInvariant()));
            if (sec.IntruderAlert)
                o.Add(W("INTRUDER ALERT"));
        }

        static void InternalScreen(Starship s, List<ReadoutLine> o)
        {
            var sec = s.Security;
            o.Add(H("INTERNAL"));
            o.Add(T("FIELDS: BRIDGE", OnOff(sec.FieldsBridge)));
            o.Add(T("FIELDS: ENGRG", OnOff(sec.FieldsEngineering)));
            o.Add(T("TURBOLIFTS", sec.TurboliftsLocked ? "LOCKED" : "ON-LINE"));
        }

        static void SystemsScreen(Starship s, List<ReadoutLine> o)
        {
            o.Add(H("SYSTEM STATUS"));
            foreach (ShipSystem sys in new[] { ShipSystem.Shields, ShipSystem.Weapons, ShipSystem.Engines, ShipSystem.Sensors, ShipSystem.LifeSupport })
                o.Add(B(Names.Of(sys).ToUpperInvariant(), s.Damage.Health(sys)));
            if (s.Damage.RepairingSystem.HasValue)
                o.Add(T("REPAIRING", Names.Of(s.Damage.RepairingSystem.Value).ToUpperInvariant()));
        }

        static void HullScreen(Starship s, List<ReadoutLine> o)
        {
            o.Add(H("HULL INTEGRITY"));
            foreach (HullSection h in new[] { HullSection.Saucer, HullSection.EngineeringHull, HullSection.PortNacelle, HullSection.StarboardNacelle })
                o.Add(B(Names.Of(h).ToUpperInvariant(), s.Damage.Hull(h)));
            o.Add(T("SIF", s.Damage.StructuralBoost ? "REINFORCED" : "NORMAL"));
            if (s.Damage.RepairingHull)
                o.Add(T("REPAIRING", "HULL"));
        }
    }
}

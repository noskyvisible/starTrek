using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// Tests can set internal state (e.g. phaser charge) directly.
[assembly: InternalsVisibleTo("StarTrek.Tests.EditMode")]

namespace StarTrek.Simulation
{
    public enum PowerTarget { Shields, Weapons, Engines, LifeSupport }

    /// <summary>Warp core output and how it is shared between ship systems.</summary>
    public sealed class PowerGrid
    {
        public const float BalancedShare = 0.3f;
        public const float LifeSupportShare = 0.1f;
        public const float NominalHeat = 0.35f;
        public const float CriticalHeat = 1.0f;

        readonly float[] share = { BalancedShare, BalancedShare, BalancedShare, LifeSupportShare };

        public bool Emergency { get; internal set; }
        public bool Venting { get; internal set; }
        /// <summary>Warp core temperature, 0..1.3; above <see cref="CriticalHeat"/> is a breach risk.</summary>
        public float CoreHeat { get; internal set; } = NominalHeat;
        public float Output => Emergency ? 1.25f : 1f;
        public bool Critical => CoreHeat >= CriticalHeat;

        public float Share(PowerTarget t) => share[(int)t];

        /// <summary>Multiplier for a system's performance: 1 with balanced power, higher when boosted.</summary>
        public float Factor(PowerTarget t)
            => share[(int)t] * Output / (t == PowerTarget.LifeSupport ? LifeSupportShare : BalancedShare);

        internal void Focus(PowerTarget t)
        {
            share[0] = share[1] = share[2] = 0.2f;
            share[(int)t] = 0.5f;
            share[3] = LifeSupportShare;
        }

        internal void Balance()
        {
            share[0] = share[1] = share[2] = BalancedShare;
            share[3] = LifeSupportShare;
        }

        internal void Tick(float dt)
        {
            if (Venting)
            {
                CoreHeat = Math.Max(NominalHeat, CoreHeat - 0.2f * dt);
                if (CoreHeat <= NominalHeat + 1e-3f)
                    Venting = false;
            }
            else if (Emergency)
                CoreHeat = Math.Min(1.3f, CoreHeat + 0.035f * dt);
            else
                CoreHeat = Math.Max(NominalHeat, CoreHeat - 0.01f * dt);
        }
    }

    public enum ShieldFacing { Fore, Aft, Port, Starboard }
    public enum ShieldState { Down, Raising, Up }

    /// <summary>Deflector shields with four facings, each with its own strength.</summary>
    public sealed class Shields
    {
        public const float MaxStrength = 100f;
        public const float RaiseSeconds = 2f;
        public const float RegenPerSecond = 3f;

        readonly float[] strength = new float[4];
        float raiseTimer;

        public ShieldState State { get; private set; }
        public bool IsUp => State == ShieldState.Up;
        public float Strength(ShieldFacing f) => strength[(int)f];

        /// <summary>Strength each facing can reach with the given power factor and generator health.</summary>
        public static float Capacity(float powerFactor, float health) => MaxStrength * Math.Min(1.5f, powerFactor) * health;

        internal void Raise()
        {
            if (State != ShieldState.Down)
                return;
            State = ShieldState.Raising;
            raiseTimer = RaiseSeconds;
        }

        internal void Lower()
        {
            State = ShieldState.Down;
            Array.Clear(strength, 0, strength.Length);
        }

        /// <returns>True on the tick the shields finish coming up.</returns>
        internal bool Tick(float dt, float powerFactor, float health)
        {
            if (State == ShieldState.Raising)
            {
                raiseTimer -= dt;
                if (raiseTimer > 0f)
                    return false;
                State = ShieldState.Up;
                float start = Capacity(powerFactor, health) * 0.6f;
                for (int i = 0; i < 4; i++)
                    strength[i] = start;
                return true;
            }
            if (State == ShieldState.Up)
            {
                float cap = Capacity(powerFactor, health);
                for (int i = 0; i < 4; i++)
                    strength[i] = strength[i] < cap
                        ? Math.Min(cap, strength[i] + RegenPerSecond * powerFactor * health * dt)
                        : Math.Max(cap, strength[i] - RegenPerSecond * dt);   // drains down to a lowered cap
            }
            return false;
        }

        /// <summary>Apply damage to a facing. Returns what gets through to the hull.</summary>
        internal float Absorb(ShieldFacing facing, float damage)
        {
            if (State != ShieldState.Up)
                return damage;
            int i = (int)facing;
            float absorbed = Math.Min(strength[i], damage);
            strength[i] -= absorbed;
            return damage - absorbed;
        }
    }

    public enum TargetSystem { Weapons, Engines, Shields, Hull }

    /// <summary>Phaser banks and the photon torpedo launcher.</summary>
    public sealed class Weapons
    {
        public const float PhaserChargePerSecond = 0.25f;
        public const float TorpedoLoadSeconds = 3f;
        public const int TorpedoMagazine = 12;

        float loadTimer;

        public bool PhasersArmed { get; internal set; }
        public float PhaserCharge { get; internal set; }
        public bool PhasersReady => PhasersArmed && PhaserCharge >= 1f;
        public int Torpedoes { get; internal set; } = TorpedoMagazine;
        public bool TorpedoLoaded { get; internal set; }
        public bool TorpedoLoading { get; private set; }
        public float TorpedoLoadProgress => TorpedoLoading ? 1f - loadTimer / TorpedoLoadSeconds : TorpedoLoaded ? 1f : 0f;
        public TargetSystem TargetSystem { get; internal set; } = TargetSystem.Weapons;

        internal void StartLoading()
        {
            TorpedoLoading = true;
            loadTimer = TorpedoLoadSeconds;
        }

        /// <returns>True on the tick a torpedo finishes loading.</returns>
        internal bool Tick(float dt, float powerFactor, float health)
        {
            if (PhasersArmed)
                PhaserCharge = Math.Min(1f, PhaserCharge + PhaserChargePerSecond * powerFactor * health * dt);
            if (!TorpedoLoading)
                return false;
            loadTimer -= dt * Math.Max(0.1f, health);
            if (loadTimer > 0f)
                return false;
            TorpedoLoading = false;
            TorpedoLoaded = true;
            return true;
        }
    }

    public enum Manoeuvre { None, ComeAbout, Evasive, AttackPattern }

    /// <summary>Helm and navigation: position (km, 2D chart), heading, impulse and warp.</summary>
    public sealed class Flight
    {
        public const double LightSpeedKmS = 299792.458;
        /// <summary>Tactical impulse scale for gameplay (fast enough to cross a system, slow enough to fight).</summary>
        public const double FullImpulseKmS = LightSpeedKmS * 0.05;
        public const float TurnDegreesPerSecond = 12f;
        public const int MaxWarp = 6;
        /// <summary>Drop out of warp this far short of the destination.</summary>
        public const double ArrivalStandoffKm = 60000;

        public double X { get; internal set; }
        public double Y { get; internal set; }
        /// <summary>Bearing in degrees, 0 = chart +Y, clockwise.</summary>
        public float Heading { get; internal set; }
        public float ImpulseSetting { get; internal set; }
        public double SpeedKmS { get; internal set; }
        public Manoeuvre Manoeuvre { get; internal set; }
        public bool AtWarp { get; internal set; }
        public int WarpFactor { get; internal set; } = 2;
        public Contact CourseTarget { get; internal set; }
        public string CourseName { get; internal set; }

        internal float TargetHeading;
        internal float EvasiveClock;
        internal float EvasiveBase;

        public static double WarpSpeedKmS(int factor) => factor * factor * factor * LightSpeedKmS;

        public double DistanceTo(Contact c) => Math.Sqrt((c.X - X) * (c.X - X) + (c.Y - Y) * (c.Y - Y));

        public float BearingTo(Contact c) => Normalise((float)(Math.Atan2(c.X - X, c.Y - Y) * 180.0 / Math.PI));

        /// <summary>Seconds to reach the course target at the current speed, or -1.</summary>
        public double EtaSeconds => CourseTarget == null || SpeedKmS < 1 ? -1 : Math.Max(0, DistanceTo(CourseTarget) - ArrivalStandoffKm) / SpeedKmS;

        public static float Normalise(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }

        public static float DeltaAngle(float from, float to)
        {
            float d = Normalise(to - from);
            return d > 180f ? d - 360f : d;
        }
    }

    public enum ContactKind { Civilian, Hostile, Debris, Unknown }

    /// <summary>Something on sensors.</summary>
    public sealed class Contact
    {
        public string Name;
        public string Description;
        public ContactKind Kind;
        public double X, Y;
        public bool Cloaked;
        public int LifeSigns;
        public float ShieldPercent;
        public string ScanReport;
        /// <summary>Combat state for ships that can fight; null for everything else.</summary>
        public HostileState Combat;
    }

    /// <summary>Sensor contacts and timed scans.</summary>
    public sealed class Sensors
    {
        public readonly List<Contact> Contacts = new List<Contact>();
        public int SelectedIndex { get; internal set; }
        public string ActiveScan { get; private set; }
        public float ScanProgress { get; private set; }
        public string LastResult { get; private set; } = "No scans run.";

        float scanDuration;
        Func<string> pendingResult;

        public Contact Selected
        {
            get
            {
                var visible = Visible;
                if (visible.Count == 0)
                    return null;
                return visible[Math.Min(SelectedIndex, visible.Count - 1)];
            }
        }

        public List<Contact> Visible
        {
            get
            {
                var list = new List<Contact>();
                foreach (var c in Contacts)
                    if (!c.Cloaked)
                        list.Add(c);
                return list;
            }
        }

        public bool Scanning => ActiveScan != null;

        internal void StartScan(string name, float seconds, Func<string> result)
        {
            ActiveScan = name;
            ScanProgress = 0f;
            scanDuration = seconds;
            pendingResult = result;
        }

        /// <returns>The finished scan's result on the tick it completes, else null.</returns>
        internal string Tick(float dt, float health)
        {
            if (ActiveScan == null)
                return null;
            ScanProgress = Math.Min(1f, ScanProgress + dt * Math.Max(0.1f, health) / scanDuration);
            if (ScanProgress < 1f)
                return null;
            LastResult = pendingResult();
            ActiveScan = null;
            pendingResult = null;
            return LastResult;
        }
    }

    /// <summary>Communications: open channel, jamming, Starfleet contact.</summary>
    public sealed class Comms
    {
        public string OpenChannel { get; internal set; }
        public bool Jamming { get; internal set; }
        public bool StarfleetNotified { get; internal set; }
        public bool ShipwideOpen { get; internal set; }
        public string DistressSignal { get; internal set; }
        public string LastMessage { get; internal set; } = "No traffic.";
    }

    /// <summary>Life support, inertial dampers, fire suppression, lighting.</summary>
    public sealed class EnvironmentalSystem
    {
        public bool LifeSupportBoost { get; internal set; }
        public bool DampersBoost { get; internal set; }
        public bool EmergencyLighting { get; internal set; }
        public float FireSuppressionSeconds { get; internal set; }
        public int Fires { get; internal set; }
        public float LifeSupport(PowerGrid power, float health) => Math.Min(1.2f, (LifeSupportBoost ? 1.15f : 1f) * Math.Min(1f, power.Factor(PowerTarget.LifeSupport)) * health);
        public float Dampers(float health) => (DampersBoost ? 1.25f : 1f) * health;
    }

    /// <summary>Internal security: alerts, force fields, security teams, turbolift lockout.</summary>
    public sealed class Security
    {
        public bool IntruderAlert { get; internal set; }
        public bool FieldsBridge { get; internal set; }
        public bool FieldsEngineering { get; internal set; }
        public bool TurboliftsLocked { get; internal set; }
        public string TeamLocation { get; internal set; } = "Standing by, deck 11";
        public int IntrudersDetected { get; internal set; }
    }

    public enum ShipSystem { Shields, Weapons, Engines, Sensors, LifeSupport }
    public enum HullSection { Saucer, EngineeringHull, PortNacelle, StarboardNacelle }

    /// <summary>System health, hull sections, repair crews and bulkheads.</summary>
    public sealed class DamageControl
    {
        public const float RepairPerSecond = 0.04f;

        readonly float[] system = { 1f, 1f, 1f, 1f, 1f };
        readonly float[] hull = { 1f, 1f, 1f, 1f };

        public bool BulkheadsSealed { get; internal set; }
        public bool StructuralBoost { get; internal set; }
        /// <summary>System the repair crews are working on, or null.</summary>
        public ShipSystem? RepairingSystem { get; internal set; }
        public bool RepairingHull { get; internal set; }

        public float Health(ShipSystem s) => system[(int)s];
        public float Hull(HullSection h) => hull[(int)h];

        public float HullAverage
        {
            get
            {
                float sum = 0f;
                foreach (float h in hull)
                    sum += h;
                return sum / hull.Length;
            }
        }

        internal void Damage(ShipSystem s, float amount) => system[(int)s] = Math.Max(0f, system[(int)s] - amount);
        internal void DamageHull(HullSection h, float amount) => hull[(int)h] = Math.Max(0f, hull[(int)h] - amount * (StructuralBoost ? 0.6f : 1f));

        /// <returns>A finished-repair message on the tick a repair completes, else null.</returns>
        internal string Tick(float dt)
        {
            if (RepairingSystem.HasValue)
            {
                int i = (int)RepairingSystem.Value;
                system[i] = Math.Min(1f, system[i] + RepairPerSecond * dt);
                if (system[i] >= 1f)
                {
                    var done = RepairingSystem.Value;
                    RepairingSystem = null;
                    return $"Repairs to {Names.Of(done)} complete.";
                }
            }
            if (RepairingHull)
            {
                bool all = true;
                for (int i = 0; i < hull.Length; i++)
                {
                    hull[i] = Math.Min(1f, hull[i] + RepairPerSecond * 0.5f * dt);
                    all &= hull[i] >= 1f;
                }
                if (all)
                {
                    RepairingHull = false;
                    return "Hull breaches sealed and repaired.";
                }
            }
            return null;
        }
    }

    public static class Names
    {
        public static string Of(ShipSystem s)
        {
            switch (s)
            {
                case ShipSystem.Shields: return "shield generators";
                case ShipSystem.Weapons: return "weapons";
                case ShipSystem.Engines: return "impulse engines";
                case ShipSystem.Sensors: return "sensors";
                default: return "life support";
            }
        }

        public static string Of(HullSection h)
        {
            switch (h)
            {
                case HullSection.Saucer: return "Saucer";
                case HullSection.EngineeringHull: return "Engineering hull";
                case HullSection.PortNacelle: return "Port nacelle";
                default: return "Starboard nacelle";
            }
        }

        public static string Of(StationRole r)
        {
            switch (r)
            {
                case StationRole.DamageControl: return "Damage Control";
                default: return r.ToString();
            }
        }
    }
}

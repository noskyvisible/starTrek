using System;
using System.Text;

namespace StarTrek.Simulation
{
    public enum AlertLevel { Normal, Yellow, Red }

    /// <summary>Something a station reports on its own (a scan finishing, shields coming up).</summary>
    public readonly struct ShipEvent
    {
        public readonly StationRole Station;
        public readonly string Text;

        public ShipEvent(StationRole station, string text)
        {
            Station = station;
            Text = text;
        }
    }

    /// <summary>
    /// The simulated starship: every system, every command, one Tick. Plain C# with no Unity types,
    /// so it can be unit-tested and shared by the player ship and (later) enemy ships.
    /// </summary>
    public sealed partial class Starship
    {
        public string Name { get; }
        public string Registry { get; }

        public readonly PowerGrid Power = new PowerGrid();
        public readonly Shields Shields = new Shields();
        public readonly Weapons Weapons = new Weapons();
        public readonly Flight Flight = new Flight();
        public readonly Sensors Sensors = new Sensors();
        public readonly Comms Comms = new Comms();
        public readonly EnvironmentalSystem Environment = new EnvironmentalSystem();
        public readonly Security Security = new Security();
        public readonly DamageControl Damage = new DamageControl();

        public AlertLevel Alert { get; private set; }
        public double Time { get; private set; }

        /// <summary>Chart Y (km) where the Klingon Neutral Zone begins; the zone is everything beyond it.</summary>
        public double NeutralZoneY { get; set; } = double.PositiveInfinity;
        public bool InNeutralZone => Flight.Y >= NeutralZoneY;
        public double DistanceToNeutralZone => Math.Max(0, NeutralZoneY - Flight.Y);

        /// <summary>The ship in distress, if the scenario has one.</summary>
        public Contact DistressShip { get; set; }

        public event Action<ShipEvent> EventRaised;
        public event Action<AlertLevel> AlertChanged;
        /// <summary>Every command after it was carried out or refused, whoever gave it.</summary>
        public event Action<CommandId, CommandResult> CommandExecuted;

        /// <summary>
        /// The running mission's handler for its own orders (away team, surrender, auto-destruct).
        /// Returns null to let the ship handle the command as usual.
        /// </summary>
        public Func<CommandId, CommandResult?> MissionOrders { get; set; }

        bool warnedHeat, warnedCritical;

        public Starship(string name, string registry)
        {
            Name = name;
            Registry = registry;
        }

        /// <summary>A station reports something on its own (used by the mission director).</summary>
        public void Report(StationRole station, string text) => Say(station, text);

        // ------------------------------------------------------------------ commands

        public CommandResult Execute(CommandId id)
        {
            var result = MissionOrders?.Invoke(id) ?? ExecuteShip(id);
            CommandExecuted?.Invoke(id, result);
            return result;
        }

        CommandResult ExecuteShip(CommandId id)
        {
            var station = CommandCatalog.Get(id).Station;
            switch (id)
            {
                // Helm
                case CommandId.AllStop:
                    Flight.ImpulseSetting = 0f;
                    Flight.AtWarp = false;
                    Flight.Manoeuvre = Manoeuvre.None;
                    return Ok(station, "All stop, aye.");
                case CommandId.ImpulseQuarter: return SetImpulse(station, 0.25f, "One-quarter impulse.");
                case CommandId.ImpulseHalf: return SetImpulse(station, 0.5f, "Half impulse.");
                case CommandId.ImpulseFull: return SetImpulse(station, 1f, "Full impulse, aye.");
                case CommandId.ComeAbout:
                    Flight.CourseTarget = null;
                    Flight.CourseName = null;
                    Flight.TargetHeading = Flight.Normalise(Flight.Heading + 180f);
                    Flight.Manoeuvre = Manoeuvre.ComeAbout;
                    return Ok(station, $"Coming about to {Flight.TargetHeading:000}.");
                case CommandId.EvasivePattern:
                    Flight.Manoeuvre = Manoeuvre.Evasive;
                    Flight.EvasiveBase = Flight.Heading;
                    Flight.EvasiveClock = 0f;
                    return Ok(station, "Evasive manoeuvres, aye.");
                case CommandId.AttackPattern:
                    if (SelectedHostile == null)
                        return No(station, "No hostile target, Captain.");
                    Flight.Manoeuvre = Manoeuvre.AttackPattern;
                    return Ok(station, $"Attack pattern, closing on the {SelectedHostile.Name}.");
                case CommandId.SteadyAsSheGoes:
                    Flight.Manoeuvre = Manoeuvre.None;
                    Flight.TargetHeading = Flight.Heading;
                    return Ok(station, "Steady as she goes.");

                // Navigation
                case CommandId.PlotCourseDistressCall:
                    if (DistressShip == null)
                        return No(station, "There's no distress signal to plot to, Captain.");
                    Flight.CourseTarget = DistressShip;
                    Flight.CourseName = DistressShip.Name;
                    Flight.Manoeuvre = Manoeuvre.None;
                    string nz = !InNeutralZone && DistressShip.Y >= NeutralZoneY ? " Captain, that course takes us into the Neutral Zone." : "";
                    return Ok(station, $"Course laid in for the {DistressShip.Name}, bearing {Flight.BearingTo(DistressShip):000}.{nz}");
                case CommandId.PlotEscapeVector:
                {
                    float away = DistressShip != null ? Flight.BearingTo(DistressShip) + 180f : Flight.Heading + 180f;
                    Flight.CourseTarget = null;
                    Flight.CourseName = "Escape vector";
                    Flight.TargetHeading = Flight.Normalise(away);
                    Flight.Manoeuvre = Manoeuvre.None;
                    return Ok(station, $"Escape vector plotted, bearing {Flight.TargetHeading:000}.");
                }
                case CommandId.EngageWarp:
                    if (Flight.CourseName == null)
                        return No(station, "No course laid in, Captain.");
                    if (Damage.Health(ShipSystem.Engines) < 0.3f)
                        return No(station, "Warp drive is off-line.");
                    Flight.AtWarp = true;
                    return Ok(station, $"Warp {Flight.WarpFactor}. Engaging.");
                case CommandId.WarpUp:
                    if (Flight.WarpFactor >= Flight.MaxWarp)
                        return No(station, $"Warp {Flight.MaxWarp} is our limit, Captain.");
                    Flight.WarpFactor++;
                    return Ok(station, $"Warp factor {Flight.WarpFactor}.");
                case CommandId.WarpDown:
                    if (Flight.WarpFactor <= 1)
                        return No(station, "Already at warp one.");
                    Flight.WarpFactor--;
                    return Ok(station, $"Warp factor {Flight.WarpFactor}.");
                case CommandId.DropToImpulse:
                    if (!Flight.AtWarp)
                        return No(station, "We're not at warp, Captain.");
                    Flight.AtWarp = false;
                    Flight.ImpulseSetting = 0.5f;
                    return Ok(station, "Dropping to half impulse.");
                case CommandId.ReportPosition:
                    return Ok(station, PositionReport());

                // Tactical
                case CommandId.RedAlert:
                    if (Alert == AlertLevel.Red)
                    {
                        SetAlert(AlertLevel.Normal);
                        return Ok(station, "Standing down from red alert.");
                    }
                    SetAlert(AlertLevel.Red);
                    Shields.Raise();
                    return Ok(station, "Red alert! Raising shields.");
                case CommandId.RaiseShields:
                    if (Shields.State != ShieldState.Down)
                        return No(station, "Shields are already up.");
                    if (Damage.Health(ShipSystem.Shields) <= 0f)
                        return No(station, "Shield generators are down!");
                    Shields.Raise();
                    return Ok(station, "Raising shields.");
                case CommandId.LowerShields:
                    if (Shields.State == ShieldState.Down)
                        return No(station, "Shields are already down.");
                    Shields.Lower();
                    return Ok(station, "Shields down.");
                case CommandId.ArmPhasers:
                    if (Weapons.PhasersArmed)
                        return No(station, "Phasers are already armed.");
                    Weapons.PhasersArmed = true;
                    return Ok(station, "Arming phasers.");
                case CommandId.FirePhasers:
                {
                    if (!Weapons.PhasersArmed)
                        return No(station, "Phasers aren't armed, Captain.");
                    if (Weapons.PhaserCharge < 1f)
                        return No(station, $"Phasers are still charging, {Weapons.PhaserCharge:P0}.");
                    var refusal = FireControlRefusal() ?? WeaponReachRefusal(PhaserRangeKm, PhaserBlindAftDegrees, true);
                    if (refusal != null)
                        return No(station, refusal);
                    Weapons.PhaserCharge = 0f;
                    FirePhasersAt(Sensors.Selected);
                    return Ok(station, $"Firing phasers at the {Sensors.Selected.Name}'s {Weapons.TargetSystem.ToString().ToLowerInvariant()}.");
                }
                case CommandId.LoadTorpedoes:
                    if (Weapons.TorpedoLoaded || Weapons.TorpedoLoading)
                        return No(station, "A torpedo is already in the tube.");
                    if (Weapons.Torpedoes <= 0)
                        return No(station, "We're out of torpedoes!");
                    Weapons.StartLoading();
                    return Ok(station, "Loading photon torpedo.");
                case CommandId.FireTorpedo:
                {
                    if (!Weapons.TorpedoLoaded)
                        return No(station, "No torpedo loaded, Captain.");
                    var refusal = FireControlRefusal() ?? WeaponReachRefusal(TorpedoRangeKm, TorpedoArcDegrees, false);
                    if (refusal != null)
                        return No(station, refusal);
                    Weapons.TorpedoLoaded = false;
                    Weapons.Torpedoes--;
                    LaunchTorpedoAt(Sensors.Selected);
                    return Ok(station, $"Torpedo away! {Weapons.Torpedoes} remaining.");
                }
                case CommandId.CycleTargetSystem:
                    Weapons.TargetSystem = (TargetSystem)(((int)Weapons.TargetSystem + 1) % 4);
                    return Ok(station, $"Targeting their {Weapons.TargetSystem.ToString().ToLowerInvariant()}.");

                // Science
                case CommandId.ScanContact:
                {
                    var c = Sensors.Selected;
                    if (c == null)
                        return No(station, "Nothing on sensors to scan.");
                    if (!StartScan(station, $"Scanning the {c.Name}", 3f, () => c.ScanReport))
                        return No(station, "A scan is already running.");
                    return Ok(station, $"Scanning the {c.Name}.");
                }
                case CommandId.NextContact:
                {
                    int count = Sensors.Visible.Count;
                    if (count == 0)
                        return No(station, "No contacts.");
                    Sensors.SelectedIndex = (Sensors.SelectedIndex + 1) % count;
                    var c = Sensors.Selected;
                    return Ok(station, $"Contact: {c.Name}, {Flight.DistanceTo(c):N0} km, bearing {Flight.BearingTo(c):000}.");
                }
                case CommandId.LongRangeScan:
                    if (!StartScan(station, "Long-range sweep", 4f, LongRangeReport))
                        return No(station, "A scan is already running.");
                    return Ok(station, "Beginning long-range sensor sweep.");
                case CommandId.ScanLifeSigns:
                {
                    var c = Sensors.Selected;
                    if (c == null)
                        return No(station, "No contact selected.");
                    if (!StartScan(station, "Life-sign scan", 3f, () => c.LifeSigns > 0
                            ? $"{c.LifeSigns} life signs aboard the {c.Name}."
                            : $"No life signs aboard the {c.Name}."))
                        return No(station, "A scan is already running.");
                    return Ok(station, $"Scanning the {c.Name} for life signs.");
                }
                case CommandId.CloakSweep:
                    if (!StartScan(station, "Cloak sweep", 5f, CloakSweepReport))
                        return No(station, "A scan is already running.");
                    return Ok(station, "Sweeping for cloaked vessels.");
                case CommandId.AnalyseShields:
                {
                    var c = Sensors.Selected;
                    if (c == null)
                        return No(station, "No contact selected.");
                    if (c.Kind != ContactKind.Hostile)
                        return Ok(station, $"The {c.Name} has no shields up, Captain.");
                    return Ok(station, $"The {c.Name}'s shields are at {c.ShieldPercent:P0}.");
                }
                case CommandId.MineScan:
                    if (!StartScan(station, "Mine scan", 4f, MineReport))
                        return No(station, "A scan is already running.");
                    return Ok(station, "Scanning for mines.");

                // Communications
                case CommandId.HailContact:
                {
                    var c = Sensors.Selected;
                    if (c == null)
                        return No(station, "There's no one to hail, Captain.");
                    if (c.Kind == ContactKind.Hostile)
                        return Ok(station, $"No response from the {c.Name}.");
                    return OpenChannelTo(station, c);
                }
                case CommandId.AnswerDistressCall:
                    if (DistressShip == null)
                        return No(station, "No distress call on any frequency.");
                    return OpenChannelTo(station, DistressShip);
                case CommandId.JamComms:
                    Comms.Jamming = !Comms.Jamming;
                    return Ok(station, Comms.Jamming ? "Jamming all frequencies." : "Jamming stopped.");
                case CommandId.CallStarfleet:
                    if (Comms.Jamming)
                        return No(station, "Not while we're jamming, Captain.");
                    Comms.StarfleetNotified = true;
                    Comms.LastMessage = "Starfleet acknowledges. Reminder: entering the Neutral Zone violates the treaty.";
                    return Ok(station, "Starfleet Command acknowledges. They remind us that the Neutral Zone is off-limits.");
                case CommandId.ShipwideIntercom:
                    Comms.ShipwideOpen = !Comms.ShipwideOpen;
                    return Ok(station, Comms.ShipwideOpen ? "Ship-wide channel open, Captain." : "Ship-wide channel closed.");
                case CommandId.CloseChannel:
                    if (Comms.OpenChannel == null)
                        return No(station, "No channel is open.");
                    Comms.OpenChannel = null;
                    return Ok(station, "Channel closed.");

                // Engineering
                case CommandId.PowerToShields: Power.Focus(PowerTarget.Shields); return Ok(station, "Diverting power to the shields.");
                case CommandId.PowerToWeapons: Power.Focus(PowerTarget.Weapons); return Ok(station, "Diverting power to weapons.");
                case CommandId.PowerToEngines: Power.Focus(PowerTarget.Engines); return Ok(station, "More power to the engines.");
                case CommandId.BalancePower: Power.Balance(); return Ok(station, "Power distribution balanced.");
                case CommandId.EmergencyPower:
                    Power.Emergency = !Power.Emergency;
                    if (Power.Emergency)
                        Power.Venting = false;
                    return Ok(station, Power.Emergency ? "Emergency power! I can't hold it forever, Captain." : "Back to normal power.");
                case CommandId.CoolWarpCore:
                    Power.Emergency = false;
                    Power.Venting = true;
                    return Ok(station, "Venting core heat.");
                case CommandId.EngineeringReport:
                    return Ok(station, $"Warp core at {Power.Output:P0} output, temperature {Power.CoreHeat:P0}. " +
                                       $"Shields {Power.Share(PowerTarget.Shields):P0}, weapons {Power.Share(PowerTarget.Weapons):P0}, engines {Power.Share(PowerTarget.Engines):P0}.");

                // Environmental
                case CommandId.BoostLifeSupport:
                    Environment.LifeSupportBoost = !Environment.LifeSupportBoost;
                    return Ok(station, Environment.LifeSupportBoost ? "Boosting life support." : "Life support back to normal.");
                case CommandId.BoostInertialDampers:
                    Environment.DampersBoost = !Environment.DampersBoost;
                    return Ok(station, Environment.DampersBoost ? "Reinforcing the inertial dampers." : "Inertial dampers to normal.");
                case CommandId.FireSuppression:
                    if (Environment.Fires == 0)
                        return No(station, "No fires reported, Captain.");
                    Environment.FireSuppressionSeconds = 5f;
                    return Ok(station, "Fire suppression systems activated.");
                case CommandId.EmergencyLighting:
                    Environment.EmergencyLighting = !Environment.EmergencyLighting;
                    return Ok(station, Environment.EmergencyLighting ? "Emergency lighting on." : "Normal lighting restored.");
                case CommandId.EnvironmentReport:
                    return Ok(station, $"Life support {Environment.LifeSupport(Power, Damage.Health(ShipSystem.LifeSupport)):P0}, " +
                                       $"inertial dampers {Environment.Dampers(Damage.Health(ShipSystem.Engines)):P0}, {(Environment.Fires == 0 ? "no fires" : Environment.Fires + " fires")}.");

                // Security
                case CommandId.IntruderAlert:
                    Security.IntruderAlert = !Security.IntruderAlert;
                    return Ok(station, Security.IntruderAlert ? "Intruder alert! All decks." : "Intruder alert cancelled.");
                case CommandId.ForceFieldsBridge:
                    Security.FieldsBridge = !Security.FieldsBridge;
                    return Ok(station, Security.FieldsBridge ? "Force fields up around the bridge." : "Bridge force fields down.");
                case CommandId.ForceFieldsEngineering:
                    Security.FieldsEngineering = !Security.FieldsEngineering;
                    return Ok(station, Security.FieldsEngineering ? "Force fields up in engineering." : "Engineering force fields down.");
                case CommandId.SecurityToBridge:
                    Security.TeamLocation = "Bridge";
                    return Ok(station, "Security team on its way to the bridge.");
                case CommandId.SecurityToEngineering:
                    Security.TeamLocation = "Engineering";
                    return Ok(station, "Security team heading to engineering.");
                case CommandId.LockTurbolifts:
                    Security.TurboliftsLocked = !Security.TurboliftsLocked;
                    return Ok(station, Security.TurboliftsLocked ? "Turbolifts locked out." : "Turbolifts back on-line.");

                // Damage control
                case CommandId.RepairShields: return Repair(station, ShipSystem.Shields);
                case CommandId.RepairWeapons: return Repair(station, ShipSystem.Weapons);
                case CommandId.RepairEngines: return Repair(station, ShipSystem.Engines);
                case CommandId.RepairHull:
                    if (Damage.HullAverage >= 1f)
                        return No(station, "No hull damage to repair, Captain.");
                    Damage.RepairingHull = true;
                    return Ok(station, "Repair crews to the hull breaches.");
                case CommandId.SealBulkheads:
                    Damage.BulkheadsSealed = !Damage.BulkheadsSealed;
                    return Ok(station, Damage.BulkheadsSealed ? "Emergency bulkheads sealed." : "Bulkheads open.");
                case CommandId.StructuralIntegrityBoost:
                    Damage.StructuralBoost = !Damage.StructuralBoost;
                    return Ok(station, Damage.StructuralBoost ? "Reinforcing structural integrity." : "Structural integrity field to normal.");

                // Mission orders, when no mission takes them
                case CommandId.AwayTeam: return No(station, "There's nowhere to send an away team, Captain.");
                case CommandId.BeamSurvivors: return No(station, "No one to beam aboard, Captain.");
                case CommandId.Surrender:
                    return No(station, ActiveHostilesCount() == 0 ? "Surrender to whom, Captain?" : "They're not answering, Captain.");
                case CommandId.AutoDestruct: return No(station, "Auto-destruct is locked out, Captain.");
            }
            return No(station, "I don't understand the order, Captain.");
        }

        public Contact SelectedHostile
        {
            get
            {
                var c = Sensors.Selected;
                return c != null && c.Kind == ContactKind.Hostile ? c : null;
            }
        }

        string FireControlRefusal()
        {
            var c = Sensors.Selected;
            if (c == null)
                return "No target, Captain.";
            if (c.Kind == ContactKind.Civilian)
                return $"Captain, the {c.Name} is a civilian ship. Holding fire.";
            if (c.Kind != ContactKind.Hostile)
                return "That's not a valid target.";
            return null;
        }

        CommandResult SetImpulse(StationRole station, float setting, string reply)
        {
            if (Flight.AtWarp)
            {
                Flight.AtWarp = false;
                reply = "Dropping out of warp. " + reply;
            }
            Flight.ImpulseSetting = setting;
            return Ok(station, reply);
        }

        CommandResult OpenChannelTo(StationRole station, Contact c)
        {
            if (Comms.Jamming)
                return No(station, "Not while we're jamming, Captain.");
            Comms.OpenChannel = c.Name;
            if (c == DistressShip && Comms.DistressSignal != null)
            {
                Comms.LastMessage = $"{c.Name}: Starship, thank you for answering! Our life support is failing. Please hurry.";
                return Ok(station, $"Channel open to the {c.Name}. They're asking us to hurry, Captain.");
            }
            Comms.LastMessage = $"Channel open to the {c.Name}.";
            return Ok(station, $"Hailing frequencies open to the {c.Name}.");
        }

        CommandResult Repair(StationRole station, ShipSystem system)
        {
            if (Damage.Health(system) >= 1f)
                return No(station, $"The {Names.Of(system)} are at full efficiency, Captain.");
            Damage.RepairingSystem = system;
            return Ok(station, $"Repair crews to the {Names.Of(system)}.");
        }

        bool StartScan(StationRole station, string name, float seconds, Func<string> result)
        {
            if (Sensors.Scanning)
                return false;
            Sensors.StartScan(name, seconds, result);
            return true;
        }

        string PositionReport()
        {
            var sb = new StringBuilder();
            sb.Append(InNeutralZone ? "We're inside the Klingon Neutral Zone." : $"{DistanceToNeutralZone:N0} km from the Neutral Zone border.");
            if (DistressShip != null)
                sb.Append($" The {DistressShip.Name} is {Flight.DistanceTo(DistressShip):N0} km away, bearing {Flight.BearingTo(DistressShip):000}.");
            return sb.ToString();
        }

        string LongRangeReport()
        {
            var visible = Sensors.Visible;
            if (visible.Count == 0)
                return "Long-range sweep: no contacts.";
            var sb = new StringBuilder($"Long-range sweep: {visible.Count} contact{(visible.Count == 1 ? "" : "s")}. ");
            foreach (var c in visible)
                sb.Append($"{c.Name}, {Flight.DistanceTo(c):N0} km. ");
            return sb.ToString().TrimEnd();
        }

        string CloakSweepReport()
        {
            foreach (var c in Sensors.Contacts)
                if (c.Cloaked && Flight.DistanceTo(c) < 500000)
                    return "Inconclusive, Captain. Faint ion traces near the freighter. Something may be out there.";
            return "No cloaked vessels detected within sensor range.";
        }

        string MineReport()
        {
            if (DistressShip == null || Flight.DistanceTo(DistressShip) > 600000)
                return "Out of range for a mine sweep. Recommend closing distance.";
            return $"Gravitic mine fragments around the {DistressShip.Name}. That matches their distress call.";
        }

        internal void SetAlert(AlertLevel level)
        {
            if (level == Alert)
                return;
            Alert = level;
            AlertChanged?.Invoke(level);
        }

        static CommandResult Ok(StationRole s, string reply) => new CommandResult(true, s, reply);
        static CommandResult No(StationRole s, string reply) => new CommandResult(false, s, reply);
        void Say(StationRole s, string text) => EventRaised?.Invoke(new ShipEvent(s, text));

        // ------------------------------------------------------------------ simulation

        public void Tick(float dt)
        {
            if (dt <= 0f)
                return;
            Time += dt;

            Power.Tick(dt);
            if (!warnedHeat && Power.CoreHeat >= 0.85f)
            {
                warnedHeat = true;
                Say(StationRole.Engineering, "Core temperature's rising, Captain!");
            }
            if (!warnedCritical && Power.Critical)
            {
                warnedCritical = true;
                Say(StationRole.Engineering, "Warp core temperature is critical!");
            }
            if (Power.CoreHeat < 0.6f)
                warnedHeat = warnedCritical = false;

            if (Shields.Tick(dt, Power.Factor(PowerTarget.Shields), Damage.Health(ShipSystem.Shields)))
                Say(StationRole.Tactical, "Shields are up, Captain.");
            if (Weapons.Tick(dt, Power.Factor(PowerTarget.Weapons), Damage.Health(ShipSystem.Weapons)))
                Say(StationRole.Tactical, "Torpedo loaded and armed.");

            TickFlight(dt);
            TickCombat(dt);

            string scan = Sensors.Tick(dt, Damage.Health(ShipSystem.Sensors));
            if (scan != null)
                Say(StationRole.Science, scan);

            string repair = Damage.Tick(dt);
            if (repair != null)
                Say(StationRole.DamageControl, repair);

            if (Environment.FireSuppressionSeconds > 0f)
            {
                Environment.FireSuppressionSeconds -= dt;
                if (Environment.FireSuppressionSeconds <= 0f && Environment.Fires > 0)
                {
                    Environment.Fires = 0;
                    Say(StationRole.Environmental, "All fires are out.");
                }
            }
        }

        void TickFlight(float dt)
        {
            var f = Flight;
            float engines = Power.Factor(PowerTarget.Engines) * Damage.Health(ShipSystem.Engines);
            bool wasInZone = InNeutralZone;

            // Steering
            float target = f.TargetHeading;
            switch (f.Manoeuvre)
            {
                case Manoeuvre.Evasive:
                    f.EvasiveClock += dt;
                    target = Flight.Normalise(f.EvasiveBase + 35f * (float)Math.Sin(f.EvasiveClock * 2.0 * Math.PI / 6.0));
                    break;
                case Manoeuvre.AttackPattern:
                    if (SelectedHostile != null)
                        target = f.BearingTo(SelectedHostile);
                    break;
                case Manoeuvre.None:
                    if (f.CourseTarget != null)
                        target = f.BearingTo(f.CourseTarget);
                    break;
            }
            float turn = Flight.TurnDegreesPerSecond * Math.Min(1.5f, Math.Max(0.2f, engines)) * dt;
            float delta = Flight.DeltaAngle(f.Heading, target);
            f.Heading = Flight.Normalise(f.Heading + Math.Max(-turn, Math.Min(turn, delta)));
            if (f.Manoeuvre == Manoeuvre.ComeAbout && Math.Abs(Flight.DeltaAngle(f.Heading, f.TargetHeading)) < 0.5f)
            {
                f.Manoeuvre = Manoeuvre.None;
                Say(StationRole.Helm, $"Now on heading {f.Heading:000}.");
            }
            if (f.Manoeuvre == Manoeuvre.None && f.CourseTarget == null)
                f.TargetHeading = target;

            // Speed: warp only once the bow is on course.
            bool aligned = Math.Abs(delta) < 5f;
            double wanted = f.AtWarp && aligned
                ? Flight.WarpSpeedKmS(f.WarpFactor)
                : f.ImpulseSetting * Flight.FullImpulseKmS * Math.Min(1.25, engines);
            if (f.AtWarp && aligned)
                f.SpeedKmS = wanted;
            else
            {
                double accel = Flight.FullImpulseKmS * 0.3 * dt;
                f.SpeedKmS = f.SpeedKmS < wanted ? Math.Min(wanted, f.SpeedKmS + accel) : Math.Max(wanted, f.SpeedKmS - accel * 2);
            }

            // Warp arrival
            double step = f.SpeedKmS * dt;
            if (f.AtWarp && f.CourseTarget != null && f.DistanceTo(f.CourseTarget) - Flight.ArrivalStandoffKm <= step)
            {
                var c = f.CourseTarget;
                double d = f.DistanceTo(c);
                double k = (d - Flight.ArrivalStandoffKm) / Math.Max(1.0, d);
                f.X += (c.X - f.X) * k;
                f.Y += (c.Y - f.Y) * k;
                f.AtWarp = false;
                f.ImpulseSetting = 0f;
                f.SpeedKmS = 0;
                Say(StationRole.Navigation, $"Dropping out of warp. The {c.Name} is dead ahead, {Flight.ArrivalStandoffKm:N0} km.");
            }
            else
            {
                double rad = f.Heading * Math.PI / 180.0;
                f.X += Math.Sin(rad) * step;
                f.Y += Math.Cos(rad) * step;
            }

            if (!wasInZone && InNeutralZone)
                Say(StationRole.Science, "Captain, we've crossed into the Klingon Neutral Zone. We're in violation of the treaty.");
        }

        // ------------------------------------------------------------------ scenarios

        /// <summary>
        /// The Kobayashi Maru test (GAME_PROMPT §8): a Class III neutronic fuel carrier with 81 crew and
        /// 300 passengers, disabled by a gravitic mine in Gamma Hydra Section 10, inside the Neutral Zone.
        /// The Klingon cruisers start cloaked.
        /// </summary>
        public static Starship KobayashiMaruTest()
        {
            var ship = new Starship("USS Resolute", "NCC-1741") { NeutralZoneY = 1200000 };
            var maru = new Contact
            {
                Name = "Kobayashi Maru",
                Description = "Class III neutronic fuel carrier",
                Kind = ContactKind.Civilian,
                X = 0, Y = 1800000,
                LifeSigns = 381,
                ScanReport = "Kobayashi Maru: Class III neutronic fuel carrier. No power, hull breached aft, life support failing. 381 aboard: 81 crew, 300 passengers.",
            };
            ship.DistressShip = maru;
            ship.Sensors.Contacts.Add(maru);
            ship.Sensors.Contacts.Add(new Contact
            {
                Name = "Mine debris",
                Description = "Gravitic mine fragments",
                Kind = ContactKind.Debris,
                X = 25000, Y = 1790000,
                ScanReport = "Debris field: fragments of a gravitic mine, recently detonated.",
            });
            for (int i = 0; i < 3; i++)
                ship.Sensors.Contacts.Add(new Contact
                {
                    Name = "Klingon cruiser " + (char)('A' + i),
                    Description = "K't'inga-class battle cruiser",
                    Kind = ContactKind.Hostile,
                    X = -60000 + 60000 * i, Y = 1760000,
                    Cloaked = true,
                    ShieldPercent = 1f,
                    ScanReport = "K't'inga-class battle cruiser. Disruptors charged, shields at full.",
                });
            ship.Comms.DistressSignal = "Mayday, mayday! This is the civilian freighter Kobayashi Maru. We have struck a gravitic mine and lost all power. " +
                                        "Our hull is breached and life support is failing. 81 crew, 300 passengers. Position: Gamma Hydra, Section 10. Any vessel, please respond.";
            ship.Comms.LastMessage = "Distress call received from the Kobayashi Maru.";
            return ship;
        }
    }
}

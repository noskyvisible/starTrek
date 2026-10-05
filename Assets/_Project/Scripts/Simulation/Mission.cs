using System;

namespace StarTrek.Simulation
{
    /// <summary>The Kobayashi Maru beats (GAME_PROMPT §8) in the order the mission moves through them.</summary>
    public enum MissionBeat
    {
        /// <summary>The cadet walks onto the simulator bridge and takes the chair.</summary>
        Arrival,
        /// <summary>The Maru's call comes in. Go in, or leave them.</summary>
        DistressCall,
        /// <summary>Inside the Neutral Zone, closing on the Maru.</summary>
        Rescue,
        /// <summary>The away team is aboard the freighter.</summary>
        AwayMission,
        /// <summary>Three K't'ingas de-cloak.</summary>
        Ambush,
        /// <summary>Shields down: Klingons beam onto the bridge.</summary>
        Boarded,
        /// <summary>The warp core is going. The last choice.</summary>
        CoreBreach,
        Ended
    }

    public enum MissionOutcome
    {
        None,
        /// <summary>Never crossed into the Neutral Zone.</summary>
        DeclinedRescue,
        /// <summary>Fought to the end; the ship was lost.</summary>
        ShipDestroyed,
        CaptainKilled,
        Surrendered,
        /// <summary>Auto-destruct, taking a cruiser with them.</summary>
        SelfDestructed,
        /// <summary>Escaped at warp and left the Maru behind.</summary>
        Retreated,
        /// <summary>Only possible in a reprogrammed simulation.</summary>
        MaruSaved
    }

    /// <summary>Everything the evaluation board looks at, collected as the test runs.</summary>
    public sealed class MissionRecord
    {
        public bool Reprogrammed;
        public MissionOutcome Outcome;
        public double EndTime = -1;

        // The decision
        public double DistressTime = -1;
        public double EnteredZoneTime = -1;
        public bool CalledStarfleet, ScannedMaru, ScannedLifeSigns, SweptForCloaks;
        public bool ShieldsUpEnteringZone;

        // The rescue
        public bool AwayTeamSent, LeakSealed;
        public int SurvivorsRescued;
        public bool TransportedUnderFire;

        // The fight
        public double AmbushTime = -1;
        /// <summary>Seconds from the ambush to red alert or shields up; -1 if never.</summary>
        public double DefenceDelay = -1;
        public int CruisersDestroyed;
        public int BoardersStunned, BoardersKilled;
        public int CrewLost;

        // Command
        public int OrdersGiven, OrdersRefused;
        /// <summary>Seconds into the core breach when the final choice was made; -1 if none.</summary>
        public double ChoiceSeconds = -1;
    }

    /// <summary>
    /// The mission director for the Kobayashi Maru test: moves through the beats, owns the mission
    /// orders (away team, beam survivors, surrender, auto-destruct), tells the scenes when to beam
    /// people about, and fills in the <see cref="MissionRecord"/> the evaluation is built from.
    /// Plain C#, ticked after the ship, so the whole test can run in unit tests.
    /// </summary>
    public sealed class Mission
    {
        public const int SoulsAboard = 381;
        public const float DistressDelaySeconds = 8f;
        /// <summary>The call comes in even if the cadet never sits down.</summary>
        public const float ArrivalMaxSeconds = 45f;
        public const float DeclineAfterSeconds = 300f;
        public const double TransporterRangeKm = 100000;
        /// <summary>Without an away team, the cruisers stop waiting.</summary>
        public const float AmbushAfterArrivalSeconds = 75f;
        public const float AwayTeamMaxSeconds = 300f;
        /// <summary>If the scene never reports the away team back, the ambush starts anyway.</summary>
        public const float ReturnFallbackSeconds = 6f;
        public const float BoardingAfterAmbushMin = 35f;
        public const float BoardingAfterAmbushMax = 160f;
        public const float BoardingMaxSeconds = 90f;
        public const float BreachSeconds = 60f;
        public const float AutoDestructSeconds = 10f;
        public const float AutoDestructConfirmSeconds = 10f;
        public const float TransportCycleSeconds = 5f;
        public const int TransportBatch = 40;
        public const string EscapeVector = "Escape vector";

        public Starship Ship { get; }
        public MissionRecord Record { get; } = new MissionRecord();
        public MissionBeat Beat { get; private set; }
        public string Objective { get; private set; }
        public double Time { get; private set; }
        public double BeatTime { get; private set; }
        public bool Reprogrammed => Record.Reprogrammed;

        public bool AwayTeamAboard { get; private set; }
        public bool Transporting { get; private set; }
        /// <summary>People the away team has tagged with transponders, waiting for a lock.</summary>
        public int SurvivorsTagged { get; private set; }
        public int SurvivorsRemaining => Math.Max(0, SoulsAboard - Record.SurvivorsRescued - SurvivorsTagged);

        public int BoardersExpected { get; private set; }
        public int BoardersRemaining { get; private set; }
        /// <summary>Seconds left before the core goes; -1 before the breach.</summary>
        public float BreachRemaining { get; private set; } = -1f;
        /// <summary>Seconds left on the auto-destruct; -1 when it isn't running.</summary>
        public float AutoDestructRemaining { get; private set; } = -1f;

        public event Action<MissionBeat> BeatChanged;
        public event Action<string> ObjectiveChanged;
        /// <summary>A voice that isn't a bridge station: the Maru, the Klingons, the bridge calling the away team.</summary>
        public event Action<string, string> Spoke;
        /// <summary>Beam the away team over to the freighter (load it).</summary>
        public event Action AwayTeamBeamOut;
        /// <summary>Beam the away team home (load the bridge).</summary>
        public event Action AwayTeamBeamBack;
        /// <summary>This many Klingons materialise on the bridge.</summary>
        public event Action<int> BoardersBeamIn;
        public event Action<MissionOutcome> Ended;

        bool seated, arrivedAtMaru, beamingBack, autoDestructArmed, reinforced;
        double arrivedTime = -1, returnTime = -1, autoDestructArmTime = -1;
        float transportClock;

        public Mission(Starship ship, bool reprogrammed = false)
        {
            Ship = ship;
            Record.Reprogrammed = reprogrammed;
            ship.HoldAmbush = true;
            ship.HostileShieldsFail = reprogrammed;
            ship.MissionOrders = HandleOrder;
            ship.CommandExecuted += OnCommandExecuted;
            ship.ContactDestroyed += c => { if (c.Kind == ContactKind.Hostile) Record.CruisersDestroyed++; };
            SetObjective("Take the captain's chair.");
        }

        // ------------------------------------------------------------------ reports from the scenes

        /// <summary>The cadet sat in the captain's chair.</summary>
        public void CaptainSeated()
        {
            if (seated)
                return;
            seated = true;
            if (Beat == MissionBeat.Arrival)
                BeatTime = Math.Max(BeatTime, ArrivalMaxSeconds - DistressDelaySeconds);
        }

        /// <summary>The away team tagged survivors with transponders.</summary>
        public void ReportSurvivorsTagged(int people)
        {
            if (Beat != MissionBeat.AwayMission || people <= 0)
                return;
            SurvivorsTagged += Math.Min(people, SurvivorsRemaining);
        }

        /// <summary>The away team sealed the fuel leak; the transporter can lock on now.</summary>
        public void ReportLeakSealed()
        {
            if (Record.LeakSealed)
                return;
            Record.LeakSealed = true;
            Spoke?.Invoke("Bridge", "Bridge to away team. Radiation's dropping. We have a transporter lock on you and the tagged survivors. Say the word.");
            SetObjective("Tag any more survivors you can reach, then call for transport.");
        }

        /// <summary>The away team asks to be beamed back. Needs the leak sealed (the radiation blocks a lock).</summary>
        public bool RequestBeamBack()
        {
            if (Beat != MissionBeat.AwayMission || beamingBack)
                return false;
            if (!Record.LeakSealed)
            {
                Spoke?.Invoke("Bridge", "We can't lock on through that radiation, away team. You'll have to seal the leak first.");
                return false;
            }
            Spoke?.Invoke("Bridge", "Energising.");
            BeamBack();
            return true;
        }

        /// <summary>The bridge scene is up again after the away mission.</summary>
        public void AwayTeamArrivedBack()
        {
            if (!AwayTeamAboard)
                return;
            AwayTeamAboard = false;
            Ship.HoldAmbush = false;
        }

        /// <summary>The cadet went down on the freighter: the bridge pulls them out.</summary>
        public void ReportAwayTeamInjured()
        {
            if (Beat != MissionBeat.AwayMission || beamingBack)
                return;
            Spoke?.Invoke("Bridge", "The away team leader is down! Emergency transport, now!");
            BeamBack();
        }

        public void ReportBoarderDown(bool killed)
        {
            if (killed) Record.BoardersKilled++;
            else Record.BoardersStunned++;
            BoardersRemaining = Math.Max(0, BoardersRemaining - 1);
        }

        public void ReportCrewDown() => Record.CrewLost++;

        public void ReportCaptainDown()
        {
            Spoke?.Invoke("Simulator", "Captain down.");
            End(MissionOutcome.CaptainKilled);
        }

        /// <summary>
        /// Development shortcut: jump straight to a later beat, setting the ship up as if the cadet had
        /// got there (warped to the Maru, sprung the ambush...). Used by tests and the dev keys.
        /// </summary>
        public void SkipTo(MissionBeat beat)
        {
            if (beat <= Beat || Beat == MissionBeat.Ended)
                return;
            if (Beat == MissionBeat.Arrival)
                StartDistressCall();
            if (beat == MissionBeat.DistressCall)
                return;
            if (Beat == MissionBeat.DistressCall)
            {
                var maru = Ship.DistressShip;
                Ship.Flight.X = maru.X;
                Ship.Flight.Y = maru.Y - Flight.ArrivalStandoffKm;
                Ship.Flight.Heading = 0f;
                Ship.Flight.AtWarp = false;
                Ship.Flight.SpeedKmS = 0;
                Ship.Flight.ImpulseSetting = 0f;
                EnterZone();
                TickRescue();
            }
            if (beat == MissionBeat.Rescue || beat == MissionBeat.AwayMission)
                return;
            if (Beat < MissionBeat.Ambush)
            {
                AwayTeamAboard = false;
                Ship.HoldAmbush = false;
                Ship.SpringAmbush();
                StartAmbush();
            }
            if (beat == MissionBeat.Ambush)
                return;
            if (Beat < MissionBeat.Boarded)
                StartBoarding();
            if (beat == MissionBeat.Boarded)
                return;
            if (Beat < MissionBeat.CoreBreach)
                StartBreach();
        }

        /// <summary>
        /// Development shortcut for playing the freighter scene on its own: the ship is at the Maru and
        /// the away team is already aboard (no beam-out event, the scene is already loaded).
        /// </summary>
        public void BeginAwayMissionHere()
        {
            if (Beat < MissionBeat.Rescue)
                SkipTo(MissionBeat.Rescue);
            if (Beat != MissionBeat.Rescue)
                return;
            AwayTeamAboard = true;
            Record.AwayTeamSent = true;
            SetBeat(MissionBeat.AwayMission);
            SetObjective("Find the survivors and seal the fuel leak. Tricorder [T], hand phaser [F].");
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt)
        {
            if (Beat == MissionBeat.Ended || dt <= 0f)
                return;
            Time += dt;
            BeatTime += dt;

            switch (Beat)
            {
                case MissionBeat.Arrival:
                    if (BeatTime >= ArrivalMaxSeconds)
                        StartDistressCall();
                    break;
                case MissionBeat.DistressCall:
                    if (Ship.InNeutralZone)
                        EnterZone();
                    else if (Retreating() || BeatTime >= DeclineAfterSeconds)
                        End(MissionOutcome.DeclinedRescue);
                    break;
                case MissionBeat.Rescue:
                    TickRescue();
                    break;
                case MissionBeat.AwayMission:
                    if (!beamingBack && BeatTime >= AwayTeamMaxSeconds)
                    {
                        Spoke?.Invoke("Bridge", "Bridge to away team! Sensors are picking up ships de-cloaking! Emergency transport, now!");
                        BeamBack();
                    }
                    if (Ship.AmbushSprung)
                        StartAmbush();
                    break;
                case MissionBeat.Ambush:
                    TickAmbush();
                    break;
                case MissionBeat.Boarded:
                    if (BoardersRemaining <= 0 || BeatTime >= BoardingMaxSeconds)
                        StartBreach();
                    break;
                case MissionBeat.CoreBreach:
                    TickBreach(dt);
                    break;
            }

            // The away team is home but the scene never said so (or there is no scene).
            if (beamingBack && AwayTeamAboard && Time - returnTime >= ReturnFallbackSeconds)
                AwayTeamArrivedBack();

            if (Beat == MissionBeat.Ended)
                return;
            TickTransport(dt);
            TickAutoDestruct(dt);
            if (Beat != MissionBeat.Ended && Ship.Damage.HullAverage <= 0.01f)
            {
                Ship.Report(StationRole.DamageControl, "Hull breach on all decks! We're breaking up!");
                End(MissionOutcome.ShipDestroyed);
            }
        }

        void TickRescue()
        {
            if (!arrivedAtMaru && Ship.DistressShip != null && !Ship.Flight.AtWarp
                && Ship.Flight.DistanceTo(Ship.DistressShip) <= TransporterRangeKm)
            {
                arrivedAtMaru = true;
                arrivedTime = Time;
                Ship.Report(StationRole.Science, $"The {Ship.DistressShip.Name} is dead in space, Captain. {SoulsAboard} life signs, a lot of them weak. Their neutronic fuel is leaking. The radiation is blocking a transporter lock.");
                Ship.Report(StationRole.Security, "Recommend an away team, Captain. Someone has to seal that leak by hand.");
                SetObjective("Send an away team (Security) to seal the leak and find survivors. Shields must be down to transport.");
            }
            if (Retreating())
                End(MissionOutcome.Retreated);
            else if (arrivedAtMaru && Time - arrivedTime >= AmbushAfterArrivalSeconds)
                Ship.HoldAmbush = false;
            if (Ship.AmbushSprung)
                StartAmbush();
        }

        void TickAmbush()
        {
            if (Retreating())
            {
                End(MissionOutcome.Retreated);
                return;
            }
            bool noneLeft = !HasHostiles();
            if (Reprogrammed)
            {
                if (noneLeft)
                {
                    Ship.Report(StationRole.Tactical, "That's the last of them, Captain! The Neutral Zone is clear!");
                    End(MissionOutcome.MaruSaved);
                }
                return;
            }
            if (noneLeft && !reinforced)
            {
                // The test is built not to be won.
                reinforced = true;
                Ship.DecloakReinforcements(3);
                return;
            }
            bool shieldsGone = !Ship.Shields.IsUp || MinShield() <= 0.5f;
            if ((BeatTime >= BoardingAfterAmbushMin && (shieldsGone || Ship.Damage.HullAverage < 0.6f)) || BeatTime >= BoardingAfterAmbushMax)
                StartBoarding();
        }

        void TickBreach(float dt)
        {
            if (Retreating())
            {
                End(MissionOutcome.Retreated);
                return;
            }
            float before = BreachRemaining;
            BreachRemaining = Math.Max(0f, BreachRemaining - dt);
            if (before > 30f && BreachRemaining <= 30f)
                Ship.Report(StationRole.Engineering, "Thirty seconds to breach!");
            else if (before > 10f && BreachRemaining <= 10f)
                Ship.Report(StationRole.Engineering, "Ten seconds! She's going!");
            if (BreachRemaining <= 0f)
            {
                Ship.Report(StationRole.Engineering, "Containment's gone!");
                End(MissionOutcome.ShipDestroyed);
            }
        }

        void TickTransport(float dt)
        {
            if (!Transporting)
                return;
            if (Ship.Shields.State != ShieldState.Down)
            {
                transportClock = 0f;
                return;
            }
            transportClock += dt;
            if (transportClock < TransportCycleSeconds)
                return;
            transportClock = 0f;
            int batch = Math.Min(TransportBatch, SurvivorsRemaining);
            Record.SurvivorsRescued += batch;
            if (Ship.AmbushSprung)
                Record.TransportedUnderFire = true;
            if (SurvivorsRemaining <= 0)
            {
                Transporting = false;
                Ship.Report(StationRole.Security, $"Transporter room reports that's everyone, Captain. {Record.SurvivorsRescued} survivors aboard.");
            }
            else
                Ship.Report(StationRole.Security, $"{batch} more aboard. {SurvivorsRemaining} still on the Maru.");
        }

        void TickAutoDestruct(float dt)
        {
            if (autoDestructArmed && AutoDestructRemaining < 0f && Time - autoDestructArmTime > AutoDestructConfirmSeconds)
                autoDestructArmed = false;
            if (AutoDestructRemaining < 0f)
                return;
            float before = AutoDestructRemaining;
            AutoDestructRemaining = Math.Max(0f, AutoDestructRemaining - dt);
            if (Math.Ceiling(before) != Math.Ceiling(AutoDestructRemaining) && AutoDestructRemaining > 0f && AutoDestructRemaining <= 5f)
                Ship.Report(StationRole.Engineering, $"{Math.Ceiling(AutoDestructRemaining):0}...");
            if (AutoDestructRemaining <= 0f)
            {
                var taken = Ship.DestroyNearestHostile();
                if (taken != null)
                    Spoke?.Invoke("Simulator", $"USS Resolute destroyed. The {taken.Name} was caught in the blast.");
                End(MissionOutcome.SelfDestructed);
            }
        }

        // ------------------------------------------------------------------ beats

        void StartDistressCall()
        {
            SetBeat(MissionBeat.DistressCall);
            Record.DistressTime = Time;
            Ship.Report(StationRole.Communications, "Captain, a distress call on the emergency band. It's breaking up...");
            if (Ship.Comms.DistressSignal != null)
                Spoke?.Invoke("Kobayashi Maru", Ship.Comms.DistressSignal);
            Ship.Report(StationRole.Science, "Captain, Gamma Hydra Section 10 is inside the Klingon Neutral Zone. Going in would break the treaty.");
            SetObjective("Answer the distress call. The Kobayashi Maru is inside the Neutral Zone. Go in, or leave them.");
        }

        void EnterZone()
        {
            Record.EnteredZoneTime = Time;
            Record.ShieldsUpEnteringZone = Ship.Shields.State != ShieldState.Down;
            SetBeat(MissionBeat.Rescue);
            SetObjective("Reach the Kobayashi Maru.");
        }

        void BeamBack()
        {
            beamingBack = true;
            returnTime = Time;
            // Tagged survivors only come too if the transporter can lock on through the radiation.
            if (Record.LeakSealed)
                Record.SurvivorsRescued += SurvivorsTagged;
            SurvivorsTagged = 0;
            AwayTeamBeamBack?.Invoke();
        }

        void StartAmbush()
        {
            Record.AmbushTime = Time;
            if (Ship.Alert == AlertLevel.Red || Ship.Shields.State != ShieldState.Down)
                Record.DefenceDelay = 0;
            SetBeat(MissionBeat.Ambush);
            SetObjective(Reprogrammed ? "Defeat the Klingon cruisers." : "Survive. Protect the Kobayashi Maru.");
        }

        void StartBoarding()
        {
            bool ready = Ship.Security.FieldsBridge || Ship.Security.IntruderAlert;
            BoardersExpected = BoardersRemaining = ready ? 2 : 3;
            Ship.HostileFireScale = 0.25f;
            Ship.SetAlert(AlertLevel.Red);
            SetBeat(MissionBeat.Boarded);
            Ship.Report(StationRole.Security, ready
                ? "Transporter beams on the bridge! The force fields stopped one of them, Captain!"
                : "Intruder alert! Klingons beaming onto the bridge!");
            SetObjective("Repel the boarders.");
            BoardersBeamIn?.Invoke(BoardersExpected);
        }

        void StartBreach()
        {
            SetBeat(MissionBeat.CoreBreach);
            BreachRemaining = BreachSeconds;
            Ship.HostileFireScale = 0.6f;
            Ship.Damage.RestoreTo(ShipSystem.Engines, 0.35f);
            Ship.Report(StationRole.Engineering, "Captain! The warp core is breaching! Containment is failing. Sixty seconds, maybe less!");
            Ship.Report(StationRole.Engineering, "I can give you one jump to warp. One.");
            SetObjective("The last choice: fight on, surrender (Communications), auto-destruct (Engineering), or retreat (Navigation: escape vector, engage).");
        }

        void End(MissionOutcome outcome)
        {
            if (Beat == MissionBeat.Ended)
                return;
            if (Beat == MissionBeat.CoreBreach && Record.ChoiceSeconds < 0 && outcome != MissionOutcome.ShipDestroyed)
                Record.ChoiceSeconds = BreachSeconds - BreachRemaining;
            Record.Outcome = outcome;
            Record.EndTime = Time;
            Transporting = false;
            AutoDestructRemaining = -1f;
            Ship.HoldAmbush = true;
            SetBeat(MissionBeat.Ended);
            SetObjective("The simulation is over.");
            Ended?.Invoke(outcome);
        }

        void SetBeat(MissionBeat beat)
        {
            Beat = beat;
            BeatTime = 0;
            BeatChanged?.Invoke(beat);
        }

        void SetObjective(string text)
        {
            Objective = text;
            ObjectiveChanged?.Invoke(text);
        }

        bool Retreating() => Ship.Flight.AtWarp && Ship.Flight.CourseName == EscapeVector;

        bool HasHostiles()
        {
            foreach (var _ in Ship.ActiveHostiles)
                return true;
            return false;
        }

        float MinShield()
        {
            float min = float.MaxValue;
            foreach (ShieldFacing f in Enum.GetValues(typeof(ShieldFacing)))
                min = Math.Min(min, Ship.Shields.Strength(f));
            return min;
        }

        // ------------------------------------------------------------------ orders

        CommandResult? HandleOrder(CommandId id)
        {
            switch (id)
            {
                case CommandId.AwayTeam: return AwayTeamOrder();
                case CommandId.BeamSurvivors: return BeamSurvivorsOrder();
                case CommandId.Surrender: return SurrenderOrder();
                case CommandId.AutoDestruct: return AutoDestructOrder();
            }
            return null;
        }

        CommandResult AwayTeamOrder()
        {
            const StationRole s = StationRole.Security;
            if (Beat == MissionBeat.AwayMission)
                return new CommandResult(false, s, "The away team is already over there, Captain.");
            if (Beat != MissionBeat.Rescue || !arrivedAtMaru)
                return new CommandResult(false, s, Beat >= MissionBeat.Ambush ? "Not with Klingons out there, Captain!" : "We're not in transporter range of anyone, Captain.");
            if (Ship.Shields.State != ShieldState.Down)
                return new CommandResult(false, s, "We can't transport through our own shields, Captain.");
            AwayTeamAboard = true;
            Record.AwayTeamSent = true;
            SetBeat(MissionBeat.AwayMission);
            SetObjective("Find the survivors and seal the fuel leak. Tricorder [T], hand phaser [F].");
            AwayTeamBeamOut?.Invoke();
            return new CommandResult(true, s, "Away team to the transporter room. You're leading it, Captain.");
        }

        CommandResult BeamSurvivorsOrder()
        {
            const StationRole s = StationRole.Security;
            if (Ship.DistressShip == null || !arrivedAtMaru)
                return new CommandResult(false, s, "They're out of transporter range, Captain.");
            if (!Record.LeakSealed)
                return new CommandResult(false, s, "The radiation from their fuel leak is scattering the beam. We can't get a lock.");
            if (SurvivorsRemaining <= 0)
                return new CommandResult(false, s, "There's no one left to beam over, Captain.");
            if (Transporting)
                return new CommandResult(false, s, "The transporter room is already working on it.");
            Transporting = true;
            transportClock = 0f;
            return new CommandResult(true, s, Ship.Shields.State == ShieldState.Down
                ? "Transporter room, energise. Forty at a time, Captain."
                : "Standing by. We can't transport until the shields are down.");
        }

        CommandResult SurrenderOrder()
        {
            const StationRole s = StationRole.Communications;
            if (!Ship.AmbushSprung || !HasHostiles())
                return new CommandResult(false, s, "Surrender to whom, Captain?");
            Spoke?.Invoke("Klingon commander", "Starfleet vessel. You will lower your shields and prepare to be boarded. Your crew are prisoners of the Klingon Empire.");
            End(MissionOutcome.Surrendered);
            return new CommandResult(true, s, "Signalling our surrender on all frequencies...");
        }

        CommandResult AutoDestructOrder()
        {
            const StationRole s = StationRole.Engineering;
            if (AutoDestructRemaining >= 0f)
            {
                AutoDestructRemaining = -1f;
                autoDestructArmed = false;
                return new CommandResult(true, s, "Auto-destruct cancelled.");
            }
            if (!Ship.AmbushSprung)
                return new CommandResult(false, s, "Captain? There's no reason to... I won't arm it without cause.");
            if (!autoDestructArmed)
            {
                autoDestructArmed = true;
                autoDestructArmTime = Time;
                return new CommandResult(true, s, "Auto-destruct needs your confirmation, Captain. Give the order again.");
            }
            autoDestructArmed = false;
            AutoDestructRemaining = AutoDestructSeconds;
            return new CommandResult(true, s, "Auto-destruct engaged. Ten seconds. It's been an honour, Captain.");
        }

        void OnCommandExecuted(CommandId id, CommandResult result)
        {
            Record.OrdersGiven++;
            if (!result.Accepted)
                Record.OrdersRefused++;
            if (Beat == MissionBeat.Ambush && Record.DefenceDelay < 0
                && result.Accepted && (id == CommandId.RedAlert || id == CommandId.RaiseShields))
                Record.DefenceDelay = Time - Record.AmbushTime;
            if (!result.Accepted || Record.EnteredZoneTime >= 0)
                return;
            // Prudence before crossing the line.
            switch (id)
            {
                case CommandId.CallStarfleet: Record.CalledStarfleet = true; break;
                case CommandId.ScanContact: Record.ScannedMaru |= Ship.Sensors.Selected == Ship.DistressShip; break;
                case CommandId.ScanLifeSigns: Record.ScannedLifeSigns = true; break;
                case CommandId.CloakSweep: Record.SweptForCloaks = true; break;
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StarTrek.Simulation;

namespace StarTrek.Tests
{
    public class MissionTests
    {
        Starship ship;
        Mission mission;
        List<MissionBeat> beats;
        int beamOuts, beamBacks, boarders;

        [SetUp]
        public void SetUp() => Start(false);

        void Start(bool reprogrammed)
        {
            ship = Starship.KobayashiMaruTest();
            mission = new Mission(ship, reprogrammed);
            beats = new List<MissionBeat>();
            beamOuts = beamBacks = boarders = 0;
            mission.BeatChanged += b => beats.Add(b);
            mission.AwayTeamBeamOut += () => beamOuts++;
            mission.AwayTeamBeamBack += () => beamBacks++;
            mission.BoardersBeamIn += n => boarders = n;
        }

        void Run(float seconds, float dt = 0.05f)
        {
            for (float t = 0f; t < seconds && mission.Beat != MissionBeat.Ended; t += dt)
            {
                ship.Tick(dt);
                mission.Tick(dt);
            }
        }

        void ReceiveTheCall()
        {
            mission.CaptainSeated();
            Run(Mission.DistressDelaySeconds + 0.5f);
        }

        void GoToTheMaru()
        {
            ReceiveTheCall();
            ship.Execute(CommandId.PlotCourseDistressCall);
            ship.Execute(CommandId.EngageWarp);
            Run(2f, 0.02f);
            Run(0.5f);
        }

        void DoTheAwayMission()
        {
            GoToTheMaru();
            Assert.That(ship.Execute(CommandId.AwayTeam).Accepted, Is.True);
            mission.ReportSurvivorsTagged(60);
            mission.ReportLeakSealed();
            Assert.That(mission.RequestBeamBack(), Is.True);
            mission.AwayTeamArrivedBack();
        }

        void ReachTheAmbush()
        {
            DoTheAwayMission();
            Run(Starship.AmbushDelaySeconds + 1f);
            Assert.That(mission.Beat, Is.EqualTo(MissionBeat.Ambush));
        }

        void RunUntil(System.Func<bool> done, float maxSeconds, float dt = 0.05f)
        {
            for (float t = 0f; t < maxSeconds && !done() && mission.Beat != MissionBeat.Ended; t += dt)
            {
                ship.Tick(dt);
                mission.Tick(dt);
            }
        }

        void ReachBoarding()
        {
            ReachTheAmbush();
            ship.Execute(CommandId.RaiseShields);
            RunUntil(() => mission.Beat == MissionBeat.Boarded, Mission.BoardingAfterAmbushMax + 1f);
            Assert.That(mission.Beat, Is.EqualTo(MissionBeat.Boarded));
        }

        void ReachTheBreach()
        {
            ReachBoarding();
            for (int i = 0; i < boarders; i++)
                mission.ReportBoarderDown(killed: false);
            Run(0.2f);
            Assert.That(mission.Beat, Is.EqualTo(MissionBeat.CoreBreach));
        }

        [Test]
        public void TheDistressCall_ComesSoonAfterTheCaptainSits()
        {
            Run(5f);
            Assert.That(mission.Beat, Is.EqualTo(MissionBeat.Arrival));
            ReceiveTheCall();
            Assert.That(mission.Beat, Is.EqualTo(MissionBeat.DistressCall));
            Assert.That(mission.Record.DistressTime, Is.GreaterThan(0));
        }

        [Test]
        public void TheDistressCall_ComesEvenIfTheCaptainNeverSits()
        {
            Run(Mission.ArrivalMaxSeconds + 0.5f);
            Assert.That(mission.Beat, Is.EqualTo(MissionBeat.DistressCall));
        }

        [Test]
        public void FleeingBeforeTheZone_DeclinesTheRescue()
        {
            ReceiveTheCall();
            ship.Execute(CommandId.PlotEscapeVector);
            ship.Execute(CommandId.EngageWarp);
            Run(0.5f);
            Assert.That(mission.Record.Outcome, Is.EqualTo(MissionOutcome.DeclinedRescue));
            var eval = Evaluation.Build(mission.Record);
            Assert.That(eval.OutcomeTitle, Is.EqualTo("Declined the rescue"));
        }

        [Test]
        public void WaitingOutTheClock_DeclinesTheRescue()
        {
            ReceiveTheCall();
            Run(Mission.DeclineAfterSeconds + 1f, 0.25f);
            Assert.That(mission.Record.Outcome, Is.EqualTo(MissionOutcome.DeclinedRescue));
        }

        [Test]
        public void TheAmbush_WaitsForTheRescueBeat()
        {
            GoToTheMaru();
            Assert.That(mission.Beat, Is.EqualTo(MissionBeat.Rescue));
            Run(Mission.AmbushAfterArrivalSeconds - 5f);
            Assert.That(ship.AmbushSprung, Is.False, "the cruisers wait while the cadet decides");
            Run(10f + Starship.AmbushDelaySeconds);
            Assert.That(ship.AmbushSprung, Is.True, "they stop waiting in the end");
            Assert.That(mission.Beat, Is.EqualTo(MissionBeat.Ambush));
        }

        [Test]
        public void TheAwayTeam_NeedsShieldsDown()
        {
            GoToTheMaru();
            ship.Execute(CommandId.RaiseShields);
            Run(Shields.RaiseSeconds + 0.5f);
            var r = ship.Execute(CommandId.AwayTeam);
            Assert.That(r.Accepted, Is.False);
            Assert.That(r.Reply, Does.Contain("shields"));
            ship.Execute(CommandId.LowerShields);
            Assert.That(ship.Execute(CommandId.AwayTeam).Accepted, Is.True);
            Assert.That(beamOuts, Is.EqualTo(1));
            Assert.That(mission.Beat, Is.EqualTo(MissionBeat.AwayMission));
        }

        [Test]
        public void BeamBack_NeedsTheLeakSealed_AndBringsTheTaggedSurvivors()
        {
            GoToTheMaru();
            ship.Execute(CommandId.AwayTeam);
            mission.ReportSurvivorsTagged(45);
            Assert.That(mission.RequestBeamBack(), Is.False, "radiation still blocks a lock");
            mission.ReportLeakSealed();
            Assert.That(mission.RequestBeamBack(), Is.True);
            Assert.That(beamBacks, Is.EqualTo(1));
            Assert.That(mission.Record.SurvivorsRescued, Is.EqualTo(45));
            Run(Mission.ReturnFallbackSeconds + Starship.AmbushDelaySeconds + 1f);
            Assert.That(ship.AmbushSprung, Is.True, "the trap springs once the team is home");
        }

        [Test]
        public void TheAwayTeam_IsRecalledIfItTakesTooLong()
        {
            GoToTheMaru();
            ship.Execute(CommandId.AwayTeam);
            Run(Mission.AwayTeamMaxSeconds + 1f, 0.25f);
            Assert.That(beamBacks, Is.EqualTo(1));
        }

        [Test]
        public void BeamSurvivors_WorksOnceTheLeakIsSealed_WithShieldsDown()
        {
            GoToTheMaru();
            Assert.That(ship.Execute(CommandId.BeamSurvivors).Accepted, Is.False, "no lock through the radiation");
            DoTheAwayMissionFromHere();
            Assert.That(ship.Execute(CommandId.BeamSurvivors).Accepted, Is.True);
            int before = mission.Record.SurvivorsRescued;
            Run(Mission.TransportCycleSeconds * 2 + 0.2f);
            Assert.That(mission.Record.SurvivorsRescued, Is.EqualTo(before + 2 * Mission.TransportBatch));
        }

        void DoTheAwayMissionFromHere()
        {
            ship.Execute(CommandId.AwayTeam);
            mission.ReportSurvivorsTagged(60);
            mission.ReportLeakSealed();
            mission.RequestBeamBack();
            mission.AwayTeamArrivedBack();
        }

        [Test]
        public void Transport_PausesWhileTheShieldsAreUp()
        {
            DoTheAwayMission();
            ship.Execute(CommandId.RaiseShields);
            ship.Execute(CommandId.BeamSurvivors);
            int before = mission.Record.SurvivorsRescued;
            Run(Mission.TransportCycleSeconds * 2);
            Assert.That(mission.Record.SurvivorsRescued, Is.EqualTo(before));
        }

        [Test]
        public void Boarding_FollowsTheAmbush()
        {
            ReachBoarding();
            Assert.That(mission.BeatTime, Is.LessThan(1.0), "just boarded");
            Assert.That(boarders, Is.EqualTo(3));
            Assert.That(ship.HostileFireScale, Is.LessThan(1f));
        }

        [Test]
        public void ForceFields_KeepOneBoarderOut()
        {
            ReachTheAmbush();
            ship.Execute(CommandId.ForceFieldsBridge);
            RunUntil(() => mission.Beat == MissionBeat.Boarded, Mission.BoardingAfterAmbushMax + 1f);
            Assert.That(boarders, Is.EqualTo(2));
        }

        [Test]
        public void TheBreach_DestroysTheShipIfNothingIsDone()
        {
            ReachTheBreach();
            Run(Mission.BreachSeconds + 1f);
            Assert.That(mission.Record.Outcome, Is.EqualTo(MissionOutcome.ShipDestroyed));
        }

        [Test]
        public void Surrender_EndsTheTest()
        {
            ReachTheBreach();
            Assert.That(ship.Execute(CommandId.Surrender).Accepted, Is.True);
            Assert.That(mission.Record.Outcome, Is.EqualTo(MissionOutcome.Surrendered));
            Assert.That(mission.Record.ChoiceSeconds, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void AutoDestruct_NeedsConfirmation_AndTakesACruiserWithUs()
        {
            ReachTheBreach();
            int hostiles = ship.ActiveHostiles.Count();
            ship.Execute(CommandId.AutoDestruct);
            Assert.That(mission.AutoDestructRemaining, Is.LessThan(0f), "first order only asks for confirmation");
            ship.Execute(CommandId.AutoDestruct);
            Assert.That(mission.AutoDestructRemaining, Is.EqualTo(Mission.AutoDestructSeconds));
            Run(Mission.AutoDestructSeconds + 0.5f);
            Assert.That(mission.Record.Outcome, Is.EqualTo(MissionOutcome.SelfDestructed));
            Assert.That(ship.ActiveHostiles.Count(), Is.EqualTo(hostiles - 1));
        }

        [Test]
        public void Retreat_DuringTheBreach_LeavesTheMaru()
        {
            ReachTheBreach();
            ship.Execute(CommandId.PlotEscapeVector);
            Assert.That(ship.Execute(CommandId.EngageWarp).Accepted, Is.True, "engineering restored one warp jump");
            Run(0.5f);
            Assert.That(mission.Record.Outcome, Is.EqualTo(MissionOutcome.Retreated));
        }

        [Test]
        public void DestroyingEveryCruiser_BringsMore()
        {
            ReachTheAmbush();
            foreach (var c in ship.ActiveHostiles.ToList())
            {
                c.Combat.Destroyed = true;
                ship.Sensors.Contacts.Remove(c);
            }
            Run(0.2f);
            Assert.That(ship.ActiveHostiles.Count(), Is.EqualTo(3), "the test does not let you win");
        }

        [Test]
        public void AReprogrammedSimulation_CanBeWon()
        {
            Start(reprogrammed: true);
            ReachTheAmbush();
            Assert.That(ship.ActiveHostiles.All(c => c.Combat.ShieldsDisabled), Is.True);
            foreach (var c in ship.ActiveHostiles.ToList())
            {
                c.Combat.Destroyed = true;
                ship.Sensors.Contacts.Remove(c);
            }
            Run(0.2f);
            Assert.That(mission.Record.Outcome, Is.EqualTo(MissionOutcome.MaruSaved));
            var eval = Evaluation.Build(mission.Record);
            Assert.That(eval.IntegrityNote, Does.Contain("COMPROMISED"));
            Assert.That(eval.OutcomeTitle, Does.Contain("saved"));
        }

        [Test]
        public void TheEvaluation_GradesEverythingButTheFinalChoice()
        {
            ReachTheBreach();
            ship.Execute(CommandId.Surrender);
            var eval = Evaluation.Build(mission.Record);
            Assert.That(eval.Categories.Count, Is.EqualTo(4));
            var final = eval.Categories.Last();
            Assert.That(final.Grade, Is.EqualTo(-1));
            Assert.That(final.GradeName, Is.EqualTo("Not graded"));
            foreach (var c in eval.Categories.Take(3))
            {
                Assert.That(c.Grade, Is.InRange(0, 4), c.Title);
                Assert.That(c.Notes, Is.Not.Empty, c.Title);
            }
            Assert.That(eval.Categories[1].Notes.Any(n => n.Contains("60 of 381")), Is.True);
            Assert.That(eval.OverallGrade, Is.InRange(0, 4));
            Assert.That(eval.IntegrityNote, Is.Null);
        }

        [Test]
        public void ADestroyedHull_EndsTheTest()
        {
            ReachTheAmbush();
            ship.Damage.DamageHull(HullSection.Saucer, 5f);
            ship.Damage.DamageHull(HullSection.EngineeringHull, 5f);
            ship.Damage.DamageHull(HullSection.PortNacelle, 5f);
            ship.Damage.DamageHull(HullSection.StarboardNacelle, 5f);
            Run(0.1f);
            Assert.That(mission.Record.Outcome, Is.EqualTo(MissionOutcome.ShipDestroyed));
        }
    }
}

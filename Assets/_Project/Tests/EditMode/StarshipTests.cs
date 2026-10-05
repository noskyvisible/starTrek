using System;
using System.Collections.Generic;
using NUnit.Framework;
using StarTrek.Simulation;

namespace StarTrek.Tests
{
    public class StarshipTests
    {
        Starship ship;
        List<ShipEvent> events;

        [SetUp]
        public void SetUp()
        {
            ship = Starship.KobayashiMaruTest();
            events = new List<ShipEvent>();
            ship.EventRaised += e => events.Add(e);
        }

        void Run(float seconds, float dt = 0.05f)
        {
            for (float t = 0f; t < seconds; t += dt)
                ship.Tick(dt);
        }

        [Test]
        public void Catalog_EveryCommandHasOneStation_AndStationsHaveAtMostEight()
        {
            var seen = new HashSet<CommandId>();
            foreach (StationRole role in Enum.GetValues(typeof(StationRole)))
            {
                var list = CommandCatalog.For(role);
                Assert.That(list.Count, Is.InRange(1, CommandCatalog.MaxPerStation), role.ToString());
                foreach (var d in list)
                {
                    Assert.That(d.Station, Is.EqualTo(role));
                    Assert.That(seen.Add(d.Id), Is.True, "duplicate " + d.Id);
                    Assert.That(d.Button, Is.Not.Empty);
                    Assert.That(d.Order, Is.Not.Empty);
                }
            }
            foreach (CommandId id in Enum.GetValues(typeof(CommandId)))
                Assert.That(seen.Contains(id), Is.True, "not on any station: " + id);
        }

        [Test]
        public void EveryCommand_ExecutesWithAReply()
        {
            foreach (CommandId id in Enum.GetValues(typeof(CommandId)))
            {
                var result = ship.Execute(id);
                Assert.That(result.Reply, Is.Not.Null.And.Not.Empty, id.ToString());
                Assert.That(result.Reply, Does.Not.Contain("don't understand"), id.ToString());
                ship.Tick(0.1f);
            }
        }

        [Test]
        public void RaiseShields_ComesUpAfterRaiseTime()
        {
            Assert.That(ship.Execute(CommandId.RaiseShields).Accepted, Is.True);
            Assert.That(ship.Shields.State, Is.EqualTo(ShieldState.Raising));
            Run(Shields.RaiseSeconds + 0.1f);
            Assert.That(ship.Shields.IsUp, Is.True);
            Assert.That(ship.Shields.Strength(ShieldFacing.Fore), Is.GreaterThan(0f));
            Assert.That(events.Exists(e => e.Text.Contains("Shields are up")), Is.True);
        }

        [Test]
        public void PowerToShields_RaisesShieldStrength()
        {
            ship.Execute(CommandId.RaiseShields);
            Run(20f);
            float balanced = ship.Shields.Strength(ShieldFacing.Aft);
            ship.Execute(CommandId.PowerToShields);
            Run(20f);
            Assert.That(ship.Shields.Strength(ShieldFacing.Aft), Is.GreaterThan(balanced));
        }

        [Test]
        public void FiringOnTheCivilianFreighter_IsRefused()
        {
            ship.Execute(CommandId.ArmPhasers);
            Run(5f);
            var result = ship.Execute(CommandId.FirePhasers);
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Reply, Does.Contain("civilian"));
        }

        [Test]
        public void Torpedo_LoadsAfterLoadTime()
        {
            ship.Execute(CommandId.LoadTorpedoes);
            Run(Weapons.TorpedoLoadSeconds + 0.2f);
            Assert.That(ship.Weapons.TorpedoLoaded, Is.True);
            Assert.That(ship.Execute(CommandId.LoadTorpedoes).Accepted, Is.False);
        }

        [Test]
        public void Warp_NeedsACourse_ThenDropsOutShortOfTheFreighter()
        {
            Assert.That(ship.Execute(CommandId.EngageWarp).Accepted, Is.False);
            Assert.That(ship.Execute(CommandId.PlotCourseDistressCall).Reply, Does.Contain("Neutral Zone"));
            Assert.That(ship.Execute(CommandId.EngageWarp).Accepted, Is.True);
            Run(10f, 0.02f);
            Assert.That(ship.Flight.AtWarp, Is.False);
            Assert.That(ship.Flight.DistanceTo(ship.DistressShip), Is.EqualTo(Flight.ArrivalStandoffKm).Within(1000));
            Assert.That(events.Exists(e => e.Station == StationRole.Navigation && e.Text.Contains("Dropping out of warp")), Is.True);
            Assert.That(ship.InNeutralZone, Is.True);
            Assert.That(events.Exists(e => e.Text.Contains("Neutral Zone")), Is.True);
        }

        [Test]
        public void ComeAbout_TurnsOneHundredEighty()
        {
            ship.Execute(CommandId.ComeAbout);
            Run(20f);
            Assert.That(ship.Flight.Heading, Is.EqualTo(180f).Within(1f));
            Assert.That(ship.Flight.Manoeuvre, Is.EqualTo(Manoeuvre.None));
        }

        [Test]
        public void LifeSignScan_ReportsEveryoneAboard()
        {
            Assert.That(ship.Execute(CommandId.ScanLifeSigns).Accepted, Is.True);
            Assert.That(ship.Execute(CommandId.ScanContact).Accepted, Is.False, "one scan at a time");
            Run(3.5f);
            Assert.That(ship.Sensors.LastResult, Does.Contain("381"));
        }

        [Test]
        public void CloakedKlingons_AreHiddenFromContacts()
        {
            Assert.That(ship.Sensors.Visible.Exists(c => c.Kind == ContactKind.Hostile), Is.False);
            Assert.That(ship.Execute(CommandId.AttackPattern).Accepted, Is.False);
        }

        [Test]
        public void RedAlert_TogglesAndRaisesShields()
        {
            AlertLevel? changed = null;
            ship.AlertChanged += a => changed = a;
            ship.Execute(CommandId.RedAlert);
            Assert.That(ship.Alert, Is.EqualTo(AlertLevel.Red));
            Assert.That(changed, Is.EqualTo(AlertLevel.Red));
            Assert.That(ship.Shields.State, Is.Not.EqualTo(ShieldState.Down));
            ship.Execute(CommandId.RedAlert);
            Assert.That(ship.Alert, Is.EqualTo(AlertLevel.Normal));
        }

        [Test]
        public void EmergencyPower_HeatsTheCore_AndVentingCoolsIt()
        {
            ship.Execute(CommandId.EmergencyPower);
            Run(25f);
            Assert.That(ship.Power.CoreHeat, Is.GreaterThan(PowerGrid.NominalHeat + 0.5f));
            Assert.That(events.Exists(e => e.Station == StationRole.Engineering), Is.True);
            ship.Execute(CommandId.CoolWarpCore);
            Run(10f);
            Assert.That(ship.Power.CoreHeat, Is.EqualTo(PowerGrid.NominalHeat).Within(0.01f));
            Assert.That(ship.Power.Emergency, Is.False);
        }

        [Test]
        public void EveryStationScreen_HasContent()
        {
            var lines = new List<ReadoutLine>();
            foreach (StationRole role in Enum.GetValues(typeof(StationRole)))
                for (int screen = 0; screen < StationReadouts.ScreensPerStation; screen++)
                {
                    StationReadouts.Build(ship, role, screen, lines);
                    Assert.That(lines.Count, Is.GreaterThanOrEqualTo(2), $"{role} screen {screen}");
                    Assert.That(lines[0].Kind, Is.EqualTo(ReadoutKind.Header));
                }
        }
    }
}

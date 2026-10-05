using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StarTrek.Simulation;

namespace StarTrek.Tests
{
    public class ShipCombatTests
    {
        Starship ship;
        List<ShipEvent> events;
        List<ImpactEvent> impacts;

        [SetUp]
        public void SetUp()
        {
            ship = Starship.KobayashiMaruTest();
            events = new List<ShipEvent>();
            impacts = new List<ImpactEvent>();
            ship.EventRaised += e => events.Add(e);
            ship.Impact += i => impacts.Add(i);
        }

        void Run(float seconds, float dt = 0.05f)
        {
            for (float t = 0f; t < seconds; t += dt)
                ship.Tick(dt);
        }

        void WarpToTheFreighter()
        {
            ship.Execute(CommandId.PlotCourseDistressCall);
            ship.Execute(CommandId.EngageWarp);
            Run(2f, 0.02f);
        }

        [Test]
        public void Ambush_SpringsAfterEnteringTheZoneNearTheFreighter()
        {
            Run(5f);
            Assert.That(ship.AmbushSprung, Is.False, "nothing happens outside the Neutral Zone");
            WarpToTheFreighter();
            Run(Starship.AmbushDelaySeconds + 0.5f);
            Assert.That(ship.AmbushSprung, Is.True);
            Assert.That(ship.ActiveHostiles.Count(), Is.EqualTo(3));
            Assert.That(ship.Sensors.Selected.Kind, Is.EqualTo(ContactKind.Hostile), "Klingons auto-selected as target");
            Assert.That(events.Exists(e => e.Text.Contains("lost contact")), Is.True);
            Assert.That(events.Exists(e => e.Text.Contains("de-cloaking")), Is.True);
        }

        [Test]
        public void Phasers_StripEnemyShieldsThenDamageHull()
        {
            WarpToTheFreighter();
            ship.SpringAmbush();
            var target = ship.Sensors.Selected;
            ship.Execute(CommandId.ArmPhasers);
            int fired = 0;
            for (int i = 0; i < 12 && !target.Combat.Destroyed; i++)
            {
                ship.Weapons.PhaserCharge = 1f;
                // Keep the target in arc and in range for the test.
                target.X = ship.Flight.X;
                target.Y = ship.Flight.Y + 20000;
                if (ship.Execute(CommandId.FirePhasers).Accepted)
                    fired++;
            }
            Assert.That(fired, Is.GreaterThan(4));
            Assert.That(target.Combat.Shields, Is.LessThan(100f));
            Assert.That(target.Combat.Hull, Is.LessThan(1f));
            Assert.That(impacts.Exists(i => !i.OnPlayer), Is.True);
        }

        [Test]
        public void Phasers_RefuseWhenOutOfRange()
        {
            WarpToTheFreighter();
            ship.SpringAmbush();
            var target = ship.Sensors.Selected;
            target.X = ship.Flight.X;
            target.Y = ship.Flight.Y + Starship.PhaserRangeKm * 2;
            ship.Execute(CommandId.ArmPhasers);
            ship.Weapons.PhaserCharge = 1f;
            var r = ship.Execute(CommandId.FirePhasers);
            Assert.That(r.Accepted, Is.False);
            Assert.That(r.Reply, Does.Contain("out of range"));
        }

        [Test]
        public void Torpedo_FliesToTheTargetAndHits()
        {
            WarpToTheFreighter();
            ship.SpringAmbush();
            var target = ship.Sensors.Selected;
            ship.Execute(CommandId.LoadTorpedoes);
            Run(Weapons.TorpedoLoadSeconds + 0.1f);
            // Put the target dead ahead just before firing.
            target.X = ship.Flight.X;
            target.Y = ship.Flight.Y + 30000;
            float shieldsBefore = target.Combat.Shields;
            Assert.That(ship.Execute(CommandId.FireTorpedo).Accepted, Is.True);
            Assert.That(ship.Projectiles.Exists(p => p.FromPlayer), Is.True);
            Run(2.5f);
            Assert.That(ship.Projectiles.Exists(p => p.FromPlayer), Is.False, "torpedo has arrived");
            Assert.That(target.Combat.Shields + (1f - target.Combat.Hull) * 100f, Is.Not.EqualTo(shieldsBefore).Within(0.01f));
        }

        [Test]
        public void KlingonFire_HitsTheFacingTowardTheAttacker()
        {
            ship.Execute(CommandId.RaiseShields);
            Run(Shields.RaiseSeconds + 10f);   // fully charged, so regeneration can't hide the hit
            WarpToTheFreighter();
            ship.SpringAmbush();
            // Put one cruiser dead ahead, pointed at us, and park the others far away.
            var hostiles = ship.ActiveHostiles.ToList();
            for (int i = 1; i < hostiles.Count; i++)
            {
                hostiles[i].X = ship.Flight.X + 900000;
                hostiles[i].Y = ship.Flight.Y;
            }
            var k = hostiles[0];
            float heading = ship.Flight.Heading;
            double rad = heading * System.Math.PI / 180.0;
            k.X = ship.Flight.X + System.Math.Sin(rad) * 20000;
            k.Y = ship.Flight.Y + System.Math.Cos(rad) * 20000;
            k.Combat.Heading = Flight.Normalise(heading + 180f);
            float foreBefore = ship.Shields.Strength(ShieldFacing.Fore);
            float aftBefore = ship.Shields.Strength(ShieldFacing.Aft);
            // Shields regenerate, so watch for the dip rather than the end value.
            float foreLowest = foreBefore, aftLowest = aftBefore;
            for (float t = 0f; t < 3f; t += 0.05f)
            {
                ship.Tick(0.05f);
                foreLowest = System.Math.Min(foreLowest, ship.Shields.Strength(ShieldFacing.Fore));
                aftLowest = System.Math.Min(aftLowest, ship.Shields.Strength(ShieldFacing.Aft));
            }
            Assert.That(impacts.Exists(i => i.OnPlayer && i.Facing == ShieldFacing.Fore), Is.True);
            Assert.That(foreLowest, Is.LessThan(foreBefore));
            Assert.That(aftLowest, Is.GreaterThanOrEqualTo(aftBefore - 0.01f), "aft shields untouched");
        }

        [Test]
        public void WithShieldsDown_HitsDamageTheHull()
        {
            WarpToTheFreighter();
            ship.SpringAmbush();
            Run(25f);
            Assert.That(ship.Damage.HullAverage, Is.LessThan(1f));
            Assert.That(impacts.Exists(i => i.OnPlayer && !i.ShieldsHeld), Is.True);
        }

        [Test]
        public void AttackPattern_BringsTheBowOntoAStrafingCruiser()
        {
            WarpToTheFreighter();
            ship.SpringAmbush();
            ship.Execute(CommandId.ImpulseHalf);
            ship.Execute(CommandId.AttackPattern);
            float bestOffBow = 180f;
            for (float t = 0f; t < 40f; t += 0.05f)
            {
                ship.Tick(0.05f);
                var target = ship.SelectedHostile;
                if (target == null)
                    break;
                bestOffBow = System.Math.Min(bestOffBow, System.Math.Abs(Flight.DeltaAngle(ship.Flight.Heading, ship.Flight.BearingTo(target))));
            }
            Assert.That(bestOffBow, Is.LessThan(Starship.TorpedoArcDegrees), "the torpedo launcher should come to bear");
        }

        [Test]
        public void TheTestIsTunedToWin_ShieldsEventuallyFail()
        {
            // GAME_PROMPT §8: the Klingons are tuned to win. Fighting back with shields up still
            // ends with the hull taking damage.
            ship.Execute(CommandId.RaiseShields);
            WarpToTheFreighter();
            Run(Shields.RaiseSeconds + 1f);
            ship.SpringAmbush();
            ship.Execute(CommandId.ImpulseHalf);
            ship.Execute(CommandId.AttackPattern);
            Run(60f);
            Assert.That(ship.Damage.HullAverage, Is.LessThan(1f));
        }

        [Test]
        public void Klingons_HoldTheirStrafingRange()
        {
            WarpToTheFreighter();
            ship.SpringAmbush();
            Run(30f);
            foreach (var k in ship.ActiveHostiles)
                Assert.That(ship.Flight.DistanceTo(k), Is.GreaterThan(Starship.StrafeRangeKm * 0.3), k.Name + " stays out of knife range");
        }

        [Test]
        public void DestroyingACruiser_RemovesItAndReports()
        {
            WarpToTheFreighter();
            ship.SpringAmbush();
            var target = ship.Sensors.Selected;
            Contact destroyed = null;
            ship.ContactDestroyed += c => destroyed = c;
            target.Combat.Shields = 0f;
            target.Combat.Hull = 0.05f;
            target.X = ship.Flight.X;
            target.Y = ship.Flight.Y + 15000;
            ship.Execute(CommandId.ArmPhasers);
            ship.Weapons.PhaserCharge = 1f;
            ship.Execute(CommandId.FirePhasers);
            Assert.That(destroyed, Is.SameAs(target));
            Assert.That(ship.Sensors.Contacts.Contains(target), Is.False);
            Assert.That(ship.ActiveHostiles.Count(), Is.EqualTo(2));
            Assert.That(ship.Sensors.Selected.Kind, Is.EqualTo(ContactKind.Hostile), "fire control moves on to the next cruiser");
        }
    }
}

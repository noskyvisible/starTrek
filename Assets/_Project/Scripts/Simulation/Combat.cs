using System;
using System.Collections.Generic;

namespace StarTrek.Simulation
{
    public enum KlingonTactic { Approach, Strafe, BreakOff }

    /// <summary>A hostile ship's fighting state (the Klingon cruisers).</summary>
    public sealed class HostileState
    {
        public float Heading;
        public double SpeedKmS;
        /// <summary>0..1</summary>
        public float Hull = 1f;
        /// <summary>Shield strength, 0..100.</summary>
        public float Shields = 100f;
        public float WeaponsDamage;
        public float EnginesDamage;
        public bool ShieldsDisabled;
        public bool Destroyed;
        public KlingonTactic Tactic;
        public float OrbitSign = 1f;
        internal float DisruptorCooldown;
        internal float TorpedoCooldown;
        internal float TacticTimer;
        internal float SinceHit = 99f;
    }

    public enum ProjectileKind { Disruptor, PhotonTorpedo }

    /// <summary>Something in flight between ships. Homing; hits when it reaches its target.</summary>
    public sealed class Projectile
    {
        public int Id;
        public ProjectileKind Kind;
        public bool FromPlayer;
        /// <summary>Who fired it (null = the player's ship).</summary>
        public Contact Source;
        /// <summary>Who it's aimed at (null = the player's ship).</summary>
        public Contact Target;
        public double X, Y;
        public double SpeedKmS;
        public float Damage;
        public bool Done;
    }

    public enum WeaponEffect { Phaser, Disruptor, TorpedoLaunch }

    /// <summary>A weapon going off, for visuals and sound. Null source/target means the player's ship.</summary>
    public readonly struct WeaponEvent
    {
        public readonly WeaponEffect Effect;
        public readonly Contact Source;
        public readonly Contact Target;

        public WeaponEvent(WeaponEffect effect, Contact source, Contact target)
        {
            Effect = effect;
            Source = source;
            Target = target;
        }
    }

    /// <summary>A hit. On the player it carries the facing struck and how hard the bridge shakes.</summary>
    public readonly struct ImpactEvent
    {
        public readonly bool OnPlayer;
        public readonly Contact Target;
        public readonly ShieldFacing Facing;
        /// <summary>0..1, how violent the hit felt.</summary>
        public readonly float Strength;
        public readonly bool ShieldsHeld;
        public readonly double X, Y;

        public ImpactEvent(bool onPlayer, Contact target, ShieldFacing facing, float strength, bool shieldsHeld, double x, double y)
        {
            OnPlayer = onPlayer;
            Target = target;
            Facing = facing;
            Strength = strength;
            ShieldsHeld = shieldsHeld;
            X = x;
            Y = y;
        }
    }

    public sealed partial class Starship
    {
        // Player weapons
        public const double PhaserRangeKm = 120000;
        public const float PhaserBlindAftDegrees = 25f;     // phaser banks can't bear directly astern
        public const float PhaserDamage = 24f;
        public const double TorpedoRangeKm = 300000;
        public const float TorpedoArcDegrees = 30f;         // forward launcher
        public const double TorpedoSpeedKmS = 50000;
        public const float TorpedoDamage = 45f;

        // Klingon weapons and handling
        public const double DisruptorRangeKm = 100000;
        public const float DisruptorArcDegrees = 50f;
        public const double DisruptorSpeedKmS = 100000;
        public const float DisruptorDamage = 11f;
        public const float DisruptorCooldown = 2.0f;
        public const float KlingonTorpedoDamage = 30f;
        public const float KlingonTorpedoCooldown = 9f;
        public const double KlingonCruiseKmS = Flight.FullImpulseKmS * 0.6;
        public const float KlingonTurnDegreesPerSecond = 18f;
        public const double StrafeRangeKm = 60000;

        /// <summary>The ambush starts this close to the freighter, once inside the Neutral Zone.</summary>
        public const double AmbushRangeKm = 250000;
        public const float AmbushDelaySeconds = 4f;

        public readonly List<Projectile> Projectiles = new List<Projectile>();

        public event Action<WeaponEvent> WeaponFired;
        public event Action<ImpactEvent> Impact;
        public event Action<Contact> ContactDestroyed;

        public bool AmbushSprung { get; private set; }
        public bool HullCritical => Damage.HullAverage <= 0.25f;

        /// <summary>The mission keeps the cruisers cloaked until its rescue beat is over.</summary>
        public bool HoldAmbush { get; set; }
        /// <summary>Scales Klingon damage to the player (the mission eases off while the bridge is boarded).</summary>
        public float HostileFireScale { get; set; } = 1f;
        /// <summary>A reprogrammed simulation: the cruisers' shields never work.</summary>
        public bool HostileShieldsFail { get; set; }

        float ambushTimer = -1f;
        int nextProjectileId;
        float sinceTacticalReport = 99f, sinceDamageReport = 99f;
        bool warnedHull;
        readonly Random rng = new Random(1701);

        public IEnumerable<Contact> ActiveHostiles
        {
            get
            {
                foreach (var c in Sensors.Contacts)
                    if (c.Combat != null && !c.Cloaked && !c.Combat.Destroyed)
                        yield return c;
            }
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>Bearing from a point to the player's ship.</summary>
        static float BearingBetween(double fromX, double fromY, double toX, double toY)
            => Flight.Normalise((float)(Math.Atan2(toX - fromX, toY - fromY) * 180.0 / Math.PI));

        static double Distance(double ax, double ay, double bx, double by)
            => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

        /// <summary>Which of the player's shield facings faces a point.</summary>
        public ShieldFacing FacingToward(double x, double y)
        {
            float rel = Flight.DeltaAngle(Flight.Heading, BearingBetween(Flight.X, Flight.Y, x, y));
            if (Math.Abs(rel) <= 45f) return ShieldFacing.Fore;
            if (Math.Abs(rel) >= 135f) return ShieldFacing.Aft;
            return rel > 0f ? ShieldFacing.Starboard : ShieldFacing.Port;
        }

        static HullSection SectionFor(ShieldFacing f)
        {
            switch (f)
            {
                case ShieldFacing.Fore: return HullSection.Saucer;
                case ShieldFacing.Aft: return HullSection.EngineeringHull;
                case ShieldFacing.Port: return HullSection.PortNacelle;
                default: return HullSection.StarboardNacelle;
            }
        }

        string WeaponReachRefusal(double range, float arcOrBlindAft, bool blindAft)
        {
            var c = Sensors.Selected;
            double d = Flight.DistanceTo(c);
            if (d > range)
                return $"The {c.Name} is out of range, Captain. {d:N0} kilometres.";
            float rel = Math.Abs(Flight.DeltaAngle(Flight.Heading, Flight.BearingTo(c)));
            if (blindAft ? rel > 180f - arcOrBlindAft : rel > arcOrBlindAft)
                return blindAft ? "They're directly astern, Captain. The phasers can't bear." : "The target's outside the torpedo's firing arc. Bring us about.";
            return null;
        }

        // ------------------------------------------------------------------ player weapons

        void FirePhasersAt(Contact target)
        {
            WeaponFired?.Invoke(new WeaponEvent(WeaponEffect.Phaser, null, target));
            float damage = PhaserDamage * Power.Factor(PowerTarget.Weapons) * Damage.Health(ShipSystem.Weapons);
            DamageHostile(target, damage);
        }

        void LaunchTorpedoAt(Contact target)
        {
            WeaponFired?.Invoke(new WeaponEvent(WeaponEffect.TorpedoLaunch, null, target));
            Projectiles.Add(new Projectile
            {
                Id = ++nextProjectileId, Kind = ProjectileKind.PhotonTorpedo, FromPlayer = true,
                Target = target, X = Flight.X, Y = Flight.Y, SpeedKmS = TorpedoSpeedKmS, Damage = TorpedoDamage,
            });
        }

        void DamageHostile(Contact target, float damage)
        {
            var h = target.Combat;
            if (h == null || h.Destroyed)
                return;
            h.SinceHit = 0f;
            float absorbed = h.ShieldsDisabled ? 0f : Math.Min(h.Shields, damage);
            h.Shields -= absorbed;
            float through = damage - absorbed;
            if (through > 0f)
            {
                h.Hull = Math.Max(0f, h.Hull - through / 110f);
                switch (Weapons.TargetSystem)
                {
                    case TargetSystem.Weapons: h.WeaponsDamage = Math.Min(1f, h.WeaponsDamage + through / 60f); break;
                    case TargetSystem.Engines: h.EnginesDamage = Math.Min(1f, h.EnginesDamage + through / 60f); break;
                    case TargetSystem.Shields: if (through > 15f) h.ShieldsDisabled = true; break;
                }
            }
            target.ShieldPercent = h.Shields / 100f;
            Impact?.Invoke(new ImpactEvent(false, target, ShieldFacing.Fore, Math.Min(1f, damage / 40f), through <= 0f, target.X, target.Y));

            if (h.Hull <= 0f)
            {
                h.Destroyed = true;
                Say(StationRole.Tactical, $"Direct hit! The {target.Name} is breaking up!");
                ContactDestroyed?.Invoke(target);
                Sensors.Contacts.Remove(target);
                SelectNextHostile();
            }
            else if (through > 0f && sinceTacticalReport > 1.5f)
            {
                sinceTacticalReport = 0f;
                Say(StationRole.Tactical, $"Hit! Their shields are down. Hull damage on the {target.Name}.");
            }
        }

        // ------------------------------------------------------------------ incoming fire

        void DamagePlayer(Contact attacker, double fromX, double fromY, float damage)
        {
            var facing = FacingToward(fromX, fromY);
            float before = Shields.Strength(facing);
            float through = Shields.Absorb(facing, damage);
            bool held = through <= 0.01f;
            float dampers = Environment.Dampers(Damage.Health(ShipSystem.Engines));
            float strength = Math.Min(1f, damage / 30f) * (held ? 0.45f : 1f) / Math.Max(0.6f, dampers);
            Impact?.Invoke(new ImpactEvent(true, null, facing, strength, held, Flight.X, Flight.Y));

            if (!held)
            {
                Damage.DamageHull(SectionFor(facing), through / 120f);
                if (rng.NextDouble() < through / 25.0)
                {
                    var sys = (ShipSystem)rng.Next(0, 5);
                    Damage.Damage(sys, through / 90f);
                    if (sinceDamageReport > 3f)
                    {
                        sinceDamageReport = 0f;
                        Say(StationRole.DamageControl, $"Damage to the {Names.Of(sys)}!");
                    }
                }
                if (rng.NextDouble() < through / 40.0)
                {
                    Environment.Fires++;
                    Say(StationRole.Environmental, $"Fire on deck {rng.Next(3, 16)}!");
                }
            }

            if (sinceTacticalReport > 2f)
            {
                sinceTacticalReport = 0f;
                string side = facing.ToString().ToLowerInvariant();
                if (held && before > 0f)
                    Say(StationRole.Tactical, $"Hit on the {side} shields! Down to {Shields.Strength(facing) / Shields.MaxStrength:P0}.");
                else
                    Say(StationRole.Tactical, Shields.IsUp ? $"{char.ToUpperInvariant(side[0]) + side.Substring(1)} shields have failed! Hull damage!" : "Direct hit! Our shields are down!");
            }
            if (!warnedHull && HullCritical)
            {
                warnedHull = true;
                Say(StationRole.DamageControl, "Hull integrity is failing, Captain! We can't take much more of this!");
            }
        }

        // ------------------------------------------------------------------ tick

        void TickCombat(float dt)
        {
            sinceTacticalReport += dt;
            sinceDamageReport += dt;
            TickAmbush(dt);
            TickProjectiles(dt);
            foreach (var c in Sensors.Contacts)
                if (c.Combat != null && !c.Cloaked && !c.Combat.Destroyed)
                    TickKlingon(c, dt);
        }

        void TickAmbush(float dt)
        {
            if (AmbushSprung || DistressShip == null || HoldAmbush)
                return;
            if (ambushTimer < 0f)
            {
                if (InNeutralZone && Flight.DistanceTo(DistressShip) < AmbushRangeKm)
                {
                    ambushTimer = AmbushDelaySeconds;
                    Comms.LastMessage = "Signal from the Kobayashi Maru lost.";
                    Say(StationRole.Communications, "Captain, I've lost contact with the Kobayashi Maru!");
                }
                return;
            }
            ambushTimer -= dt;
            if (ambushTimer > 0f)
                return;
            SpringAmbush();
        }

        /// <summary>The three cruisers de-cloak around the ship (GAME_PROMPT §8, beat 4).</summary>
        public void SpringAmbush()
        {
            if (AmbushSprung)
                return;
            AmbushSprung = true;
            float[] offsets = { -55f, 0f, 55f };
            int i = 0;
            foreach (var c in Sensors.Contacts)
            {
                if (c.Kind != ContactKind.Hostile || !c.Cloaked)
                    continue;
                float bearing = Flight.Normalise(Flight.Heading + offsets[i % offsets.Length]);
                double r = 85000 + 8000 * i;
                double rad = bearing * Math.PI / 180.0;
                c.X = Flight.X + Math.Sin(rad) * r;
                c.Y = Flight.Y + Math.Cos(rad) * r;
                Decloak(c, bearing, i);
                i++;
            }
            SelectNextHostile();
            Say(StationRole.Science, $"Captain! Klingon warships de-cloaking! {i} K't'inga-class battle cruisers!");
            if (HostileShieldsFail && i > 0)
                Say(StationRole.Science, "Wait... their shields aren't coming up. None of them. That's not possible.");
        }

        void Decloak(Contact c, float bearing, int index)
        {
            c.Cloaked = false;
            c.Combat = new HostileState
            {
                Heading = Flight.Normalise(bearing + 180f),
                SpeedKmS = KlingonCruiseKmS * 0.5,
                OrbitSign = index % 2 == 0 ? 1f : -1f,
                DisruptorCooldown = 1.5f + index * 0.7f,
                TorpedoCooldown = 6f + index * 2f,
            };
            if (HostileShieldsFail)
            {
                c.Combat.ShieldsDisabled = true;
                c.Combat.Shields = 0f;
            }
            c.ShieldPercent = c.Combat.Shields / 100f;
        }

        int ActiveHostilesCount()
        {
            int n = 0;
            foreach (var _ in ActiveHostiles)
                n++;
            return n;
        }

        /// <summary>More cruisers de-cloak around the ship: the test does not let you win.</summary>
        public int DecloakReinforcements(int count)
        {
            float[] offsets = { 130f, -130f, 180f };
            for (int i = 0; i < count; i++)
            {
                var c = new Contact
                {
                    Name = "Klingon cruiser " + (char)('D' + reinforcementCount++),
                    Description = "K't'inga-class battle cruiser",
                    Kind = ContactKind.Hostile,
                    ScanReport = "K't'inga-class battle cruiser. Disruptors charged, shields at full.",
                };
                float bearing = Flight.Normalise(Flight.Heading + offsets[i % offsets.Length]);
                double rad = bearing * Math.PI / 180.0;
                c.X = Flight.X + Math.Sin(rad) * 90000;
                c.Y = Flight.Y + Math.Cos(rad) * 90000;
                Sensors.Contacts.Add(c);
                Decloak(c, bearing, i);
            }
            SelectNextHostile();
            Say(StationRole.Science, count == 1 ? "Another Klingon cruiser de-cloaking!" : $"{count} more Klingon cruisers de-cloaking, Captain!");
            return count;
        }

        /// <summary>The nearest fighting cruiser is lost with the ship (auto-destruct). Returns it, or null.</summary>
        public Contact DestroyNearestHostile()
        {
            Contact nearest = null;
            double best = double.MaxValue;
            foreach (var c in ActiveHostiles)
            {
                double d = Flight.DistanceTo(c);
                if (d < best)
                {
                    best = d;
                    nearest = c;
                }
            }
            if (nearest == null)
                return null;
            nearest.Combat.Destroyed = true;
            ContactDestroyed?.Invoke(nearest);
            Sensors.Contacts.Remove(nearest);
            return nearest;
        }

        int reinforcementCount;

        /// <summary>Point sensors (and so fire control) at the nearest fighting enemy, if any.</summary>
        void SelectNextHostile()
        {
            var visible = Sensors.Visible;
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int k = 0; k < visible.Count; k++)
            {
                var c = visible[k];
                if (c.Combat == null || c.Combat.Destroyed)
                    continue;
                double d = Flight.DistanceTo(c);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = k;
                }
            }
            Sensors.SelectedIndex = best >= 0 ? best : 0;
            if (best >= 0 && AmbushSprung)
                Say(StationRole.Tactical, $"Targeting the {visible[best].Name}.");
        }

        void TickProjectiles(float dt)
        {
            foreach (var p in Projectiles)
            {
                if (p.Done)
                    continue;
                double tx, ty;
                if (p.Target == null) { tx = Flight.X; ty = Flight.Y; }
                else if (p.Target.Combat != null && p.Target.Combat.Destroyed) { p.Done = true; continue; }
                else { tx = p.Target.X; ty = p.Target.Y; }

                double d = Distance(p.X, p.Y, tx, ty);
                double step = p.SpeedKmS * dt;
                if (d <= step + 300)
                {
                    p.Done = true;
                    if (p.Target == null)
                        DamagePlayer(p.Source, p.Source != null ? p.Source.X : p.X, p.Source != null ? p.Source.Y : p.Y, p.Damage * DamageScale(p) * HostileFireScale);
                    else
                        DamageHostile(p.Target, p.Damage);
                    continue;
                }
                p.X += (tx - p.X) / d * step;
                p.Y += (ty - p.Y) / d * step;
            }
            Projectiles.RemoveAll(p => p.Done);
        }

        static float DamageScale(Projectile p)
            => p.Source != null && p.Source.Combat != null ? 1f - 0.6f * p.Source.Combat.WeaponsDamage : 1f;

        void TickKlingon(Contact c, float dt)
        {
            var h = c.Combat;
            h.SinceHit += dt;
            h.TacticTimer -= dt;
            if (!h.ShieldsDisabled && h.SinceHit > 4f)
                h.Shields = Math.Min(100f, h.Shields + 3f * dt);
            c.ShieldPercent = h.Shields / 100f;

            double dist = Distance(c.X, c.Y, Flight.X, Flight.Y);
            float toPlayer = BearingBetween(c.X, c.Y, Flight.X, Flight.Y);

            // Choose a tactic: close in, circle-strafe on the player's weakest side, or break off to recover.
            if (h.Hull < 0.3f && h.Tactic != KlingonTactic.BreakOff)
            {
                h.Tactic = KlingonTactic.BreakOff;
                h.TacticTimer = 10f;
            }
            else if (h.Tactic == KlingonTactic.BreakOff && h.TacticTimer <= 0f)
                h.Tactic = KlingonTactic.Approach;
            else if (h.Tactic != KlingonTactic.BreakOff)
                h.Tactic = dist > StrafeRangeKm * 1.15 ? KlingonTactic.Approach : KlingonTactic.Strafe;

            if (h.Tactic == KlingonTactic.Strafe && h.TacticTimer <= 0f)
            {
                // Swing round toward whichever of our flanks is weaker.
                h.TacticTimer = 8f;
                float port = Shields.Strength(ShieldFacing.Port), starboard = Shields.Strength(ShieldFacing.Starboard);
                h.OrbitSign = port < starboard ? -1f : 1f;
            }

            float desired;
            double speed;
            switch (h.Tactic)
            {
                case KlingonTactic.BreakOff:
                    desired = Flight.Normalise(toPlayer + 180f);
                    speed = KlingonCruiseKmS;
                    break;
                case KlingonTactic.Strafe:
                {
                    // Hold a strafing range: open out when too close, close in when too far, otherwise
                    // circle. Too close and they'd whip round faster than the player can turn to answer.
                    float offset = dist < StrafeRangeKm * 0.5 ? 125f : dist > StrafeRangeKm ? 35f : 90f;
                    desired = Flight.Normalise(toPlayer + h.OrbitSign * offset);
                    speed = KlingonCruiseKmS * 0.6;
                    break;
                }
                default:
                    desired = toPlayer;
                    speed = KlingonCruiseKmS;
                    break;
            }
            speed *= 1f - 0.7f * h.EnginesDamage;
            float turn = KlingonTurnDegreesPerSecond * (1f - 0.5f * h.EnginesDamage) * dt;
            float delta = Flight.DeltaAngle(h.Heading, desired);
            h.Heading = Flight.Normalise(h.Heading + Math.Max(-turn, Math.Min(turn, delta)));
            h.SpeedKmS += (speed - h.SpeedKmS) * Math.Min(1.0, dt * 0.8);
            double rad = h.Heading * Math.PI / 180.0;
            c.X += Math.Sin(rad) * h.SpeedKmS * dt;
            c.Y += Math.Cos(rad) * h.SpeedKmS * dt;

            if (h.Tactic == KlingonTactic.BreakOff)
                return;

            // Fire when the player is inside the forward arc.
            bool inArc = Math.Abs(Flight.DeltaAngle(h.Heading, toPlayer)) <= DisruptorArcDegrees;
            h.DisruptorCooldown -= dt;
            h.TorpedoCooldown -= dt;
            if (inArc && dist <= DisruptorRangeKm && h.DisruptorCooldown <= 0f)
            {
                h.DisruptorCooldown = DisruptorCooldown * (1f + 1.5f * h.WeaponsDamage) * (0.85f + 0.3f * (float)rng.NextDouble());
                Fire(c, ProjectileKind.Disruptor, DisruptorSpeedKmS, DisruptorDamage);
            }
            if (inArc && dist <= DisruptorRangeKm * 1.5 && h.TorpedoCooldown <= 0f)
            {
                h.TorpedoCooldown = KlingonTorpedoCooldown * (1f + h.WeaponsDamage);
                Fire(c, ProjectileKind.PhotonTorpedo, TorpedoSpeedKmS, KlingonTorpedoDamage);
            }
        }

        void Fire(Contact source, ProjectileKind kind, double speed, float damage)
        {
            WeaponFired?.Invoke(new WeaponEvent(kind == ProjectileKind.Disruptor ? WeaponEffect.Disruptor : WeaponEffect.TorpedoLaunch, source, null));
            Projectiles.Add(new Projectile
            {
                Id = ++nextProjectileId, Kind = kind, FromPlayer = false, Source = source,
                X = source.X, Y = source.Y, SpeedKmS = speed, Damage = damage,
            });
        }
    }
}

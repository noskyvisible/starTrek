using System.Collections.Generic;
using StarTrek.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTrek.Bridge
{
    /// <summary>
    /// What the viewscreen sees: the space around the ship, drawn from the simulation. The camera sits
    /// at this rig's origin facing the ship's heading. Simulated distances (thousands of km) are
    /// compressed to a few kilometres so ships read on screen; directions stay true.
    /// Also draws weapons fire: phaser beams, disruptor bolts, torpedoes, hits and explosions.
    /// </summary>
    public class SpaceView : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] Transform starfield;
        [SerializeField] GameObject hostileModel;
        [SerializeField] GameObject civilianModel;
        [SerializeField] Material phaserMaterial;
        [SerializeField] Material disruptorMaterial;
        [SerializeField] Material torpedoMaterial;
        [SerializeField] Material explosionMaterial;
        [SerializeField] Material shieldMaterial;
        [Tooltip("Rendering layer the space sun lights (ships only, never the bridge).")]
        [SerializeField] int shipRenderingLayer = 1;

        const float MetresAtTenThousandKm = 600f;
        const float MaxBankDegrees = 35f;

        // Per-ship presentation: a stable elevation (so nothing sits edge-on in the camera's plane)
        // and the last heading, for banking into turns.
        readonly Dictionary<Contact, float> elevation = new Dictionary<Contact, float>();
        readonly Dictionary<Contact, float> lastHeading = new Dictionary<Contact, float>();
        readonly Dictionary<Contact, float> bank = new Dictionary<Contact, float>();

        class Effect
        {
            public Transform T;
            public LineRenderer Line;
            public float Born, Life, Size;
            public Vector3 Velocity;
            public Contact Follow;
        }

        ShipSimHost host;
        Starship ship;
        int spaceLayer;
        readonly Dictionary<Contact, Transform> models = new Dictionary<Contact, Transform>();
        readonly Dictionary<int, Transform> shots = new Dictionary<int, Transform>();
        readonly List<Effect> effects = new List<Effect>();
        readonly HashSet<int> liveIds = new HashSet<int>();
        readonly List<int> deadIds = new List<int>();
        readonly List<Contact> deadModels = new List<Contact>();

        void Start()
        {
            host = ShipSimHost.Instance;
            if (host == null)
                return;
            ship = host.Ship;
            spaceLayer = LayerMask.NameToLayer("Space");
            ship.WeaponFired += OnWeapon;
            ship.Impact += OnImpact;
            ship.ContactDestroyed += OnDestroyed;
        }

        void OnDestroy()
        {
            if (ship == null)
                return;
            ship.WeaponFired -= OnWeapon;
            ship.Impact -= OnImpact;
            ship.ContactDestroyed -= OnDestroyed;
        }

        /// <summary>Where a simulated point (km) appears in the viewscreen world.</summary>
        public Vector3 ViewPosition(double x, double y)
        {
            double dx = x - ship.Flight.X, dy = y - ship.Flight.Y;
            double d = System.Math.Sqrt(dx * dx + dy * dy);
            if (d < 1.0)
                return transform.position;
            float r = Mathf.Clamp(MetresAtTenThousandKm * Mathf.Sqrt((float)(d / 10000.0)), 250f, 30000f);
            return transform.position + new Vector3((float)(dx / d), 0f, (float)(dy / d)) * r;
        }

        /// <summary>A contact's view position, lifted or dropped by its own elevation angle.</summary>
        Vector3 ViewPosition(Contact c)
        {
            Vector3 flat = ViewPosition(c.X, c.Y);
            if (!elevation.TryGetValue(c, out float e))
            {
                // Stable per ship: -12..+12 degrees, never near zero.
                int h = c.Name.GetHashCode() & 0x7fffffff;
                e = (4f + h % 9) * ((h / 9) % 2 == 0 ? 1f : -1f);
                elevation[c] = e;
            }
            Vector3 offset = flat - transform.position;
            float r = offset.magnitude;
            float rad = e * Mathf.Deg2Rad;
            return transform.position + offset * Mathf.Cos(rad) + Vector3.up * (r * Mathf.Sin(rad));
        }

        /// <summary>Roll into turns: compare the heading with last frame's.</summary>
        float Bank(Contact c, float heading)
        {
            float last = lastHeading.TryGetValue(c, out var l) ? l : heading;
            lastHeading[c] = heading;
            float turnRate = Mathf.DeltaAngle(last, heading) / Mathf.Max(Time.deltaTime, 1e-4f);
            float target = Mathf.Clamp(-turnRate * 2f, -MaxBankDegrees, MaxBankDegrees);
            float current = bank.TryGetValue(c, out var b) ? b : 0f;
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
            bank[c] = current;
            return current;
        }

        /// <summary>Muzzle point for our own weapons: just below and ahead of the viewscreen camera.</summary>
        Vector3 OurEmitter => viewCamera.transform.TransformPoint(new Vector3(0f, -40f, 90f));

        void LateUpdate()
        {
            if (ship == null)
                return;
            float bank = ship.Flight.Manoeuvre == Manoeuvre.Evasive ? Mathf.Sin(Time.time * 1.3f) * 8f : 0f;
            viewCamera.transform.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, ship.Flight.Heading, bank));
            if (starfield != null)
                starfield.position = transform.position;

            SyncContacts();
            SyncShots();
            UpdateEffects();
        }

        void SyncContacts()
        {
            foreach (var c in ship.Sensors.Contacts)
            {
                if (c.Cloaked || (c.Kind != ContactKind.Hostile && c.Kind != ContactKind.Civilian))
                    continue;
                if (!models.TryGetValue(c, out var t) || t == null)
                {
                    t = Spawn(c.Kind == ContactKind.Hostile ? hostileModel : civilianModel, c.Name);
                    models[c] = t;
                    if (c.Kind == ContactKind.Hostile)
                        Flash(ViewPosition(c), 300f, 0.9f, shieldMaterial);   // de-cloak shimmer
                }
                t.position = ViewPosition(c);
                t.rotation = c.Combat != null
                    ? Quaternion.Euler(0f, c.Combat.Heading, Bank(c, c.Combat.Heading))
                    : Quaternion.Euler(4f, 35f, Mathf.Sin(Time.time * 0.15f) * 6f);   // the dead freighter drifts
            }
            deadModels.Clear();
            foreach (var kv in models)
                if (!ship.Sensors.Contacts.Contains(kv.Key))
                    deadModels.Add(kv.Key);
            foreach (var c in deadModels)
            {
                if (models[c] != null)
                    Destroy(models[c].gameObject);
                models.Remove(c);
            }
        }

        void SyncShots()
        {
            liveIds.Clear();
            foreach (var p in ship.Projectiles)
            {
                liveIds.Add(p.Id);
                if (!shots.TryGetValue(p.Id, out var t))
                {
                    bool bolt = p.Kind == ProjectileKind.Disruptor;
                    t = Primitive(PrimitiveType.Sphere, bolt ? disruptorMaterial : torpedoMaterial, "Shot");
                    t.localScale = bolt ? new Vector3(26f, 26f, 110f) : Vector3.one * 55f;
                    shots[p.Id] = t;
                }
                Vector3 pos = ShotPosition(p);
                Vector3 target = p.Target != null ? ViewPosition(p.Target) : transform.position;
                if (target != pos)
                    t.rotation = Quaternion.LookRotation(target - pos);
                t.position = pos;
                if (p.Kind == ProjectileKind.PhotonTorpedo)
                    t.localScale = Vector3.one * (55f + Mathf.Sin(Time.time * 30f + p.Id) * 12f);   // pulsing orb
            }
            deadIds.Clear();
            foreach (var kv in shots)
                if (!liveIds.Contains(kv.Key))
                    deadIds.Add(kv.Key);
            foreach (int id in deadIds)
            {
                Destroy(shots[id].gameObject);
                shots.Remove(id);
            }
        }

        /// <summary>
        /// A shot drawn on the line between its shooter and target as seen on screen, by how far it
        /// has travelled, so bolts meet the (elevated) ship models.
        /// </summary>
        Vector3 ShotPosition(Projectile p)
        {
            double fromX = p.Source != null ? p.Source.X : ship.Flight.X, fromY = p.Source != null ? p.Source.Y : ship.Flight.Y;
            double toX = p.Target != null ? p.Target.X : ship.Flight.X, toY = p.Target != null ? p.Target.Y : ship.Flight.Y;
            double total = System.Math.Sqrt((toX - fromX) * (toX - fromX) + (toY - fromY) * (toY - fromY));
            double left = System.Math.Sqrt((toX - p.X) * (toX - p.X) + (toY - p.Y) * (toY - p.Y));
            float t = total < 1.0 ? 1f : Mathf.Clamp01(1f - (float)(left / total));
            Vector3 a = p.Source != null ? ViewPosition(p.Source) : OurEmitter;
            Vector3 b = p.Target != null ? ViewPosition(p.Target) : transform.position + viewCamera.transform.forward * 150f;
            return Vector3.Lerp(a, b, t);
        }

        void OnWeapon(WeaponEvent w)
        {
            if (w.Effect != WeaponEffect.Phaser || w.Target == null)
                return;
            var go = new GameObject("PhaserBeam");
            go.layer = spaceLayer;
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = phaserMaterial;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.numCapVertices = 4;
            effects.Add(new Effect { T = go.transform, Line = line, Born = Time.time, Life = 1.1f, Size = 16f, Follow = w.Target });
        }

        void OnImpact(ImpactEvent e)
        {
            if (e.OnPlayer)
            {
                // Hits on the forward shields flare right in front of the viewscreen.
                if (e.Facing == ShieldFacing.Fore)
                    Flash(viewCamera.transform.TransformPoint(new Vector3(Random.Range(-60f, 60f), Random.Range(-20f, 30f), 520f)),
                        e.ShieldsHeld ? 90f : 130f, 0.35f, e.ShieldsHeld ? shieldMaterial : explosionMaterial);
                return;
            }
            Vector3 at = ViewPosition(e.Target);
            Vector3 toCamera = (transform.position - at).normalized;
            Flash(at + toCamera * 120f, e.ShieldsHeld ? 140f : 110f, e.ShieldsHeld ? 0.35f : 0.5f, e.ShieldsHeld ? shieldMaterial : explosionMaterial);
        }

        void OnDestroyed(Contact c)
        {
            Vector3 at = models.TryGetValue(c, out var t) && t != null ? t.position : ViewPosition(c);
            Flash(at, 380f, 1.2f, explosionMaterial);
            Flash(at, 220f, 2.0f, torpedoMaterial);
            for (int i = 0; i < 14; i++)
            {
                var debris = Primitive(PrimitiveType.Cube, explosionMaterial, "Debris");
                debris.position = at;
                debris.localScale = Vector3.one * Random.Range(8f, 22f);
                debris.rotation = Random.rotation;
                effects.Add(new Effect { T = debris, Born = Time.time, Life = 2.5f, Size = debris.localScale.x, Velocity = Random.onUnitSphere * Random.Range(80f, 260f) });
            }
        }

        void Flash(Vector3 at, float size, float life, Material mat)
        {
            var t = Primitive(PrimitiveType.Sphere, mat, "Flash");
            t.position = at;
            t.localScale = Vector3.zero;
            effects.Add(new Effect { T = t, Born = Time.time, Life = life, Size = size });
        }

        void UpdateEffects()
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var e = effects[i];
                float k = (Time.time - e.Born) / e.Life;
                if (k >= 1f || e.T == null)
                {
                    if (e.T != null)
                        Destroy(e.T.gameObject);
                    effects.RemoveAt(i);
                    continue;
                }
                if (e.Line != null)
                {
                    Vector3 end = e.Follow != null && models.TryGetValue(e.Follow, out var target) && target != null ? target.position : ViewPosition(e.Follow);
                    e.Line.SetPosition(0, OurEmitter);
                    e.Line.SetPosition(1, end);
                    // Thin at our emitter (close to the camera), fuller at the target, flickering.
                    float w = e.Size * (1f - k * 0.6f) * Random.Range(0.75f, 1.15f);
                    e.Line.startWidth = w * 0.2f;
                    e.Line.endWidth = w * 0.7f;
                }
                else if (e.Velocity != Vector3.zero)
                {
                    e.T.position += e.Velocity * Time.deltaTime;
                    e.T.Rotate(e.Velocity.normalized * 120f * Time.deltaTime, Space.World);
                    e.T.localScale = Vector3.one * e.Size * (1f - k);
                }
                else
                {
                    // Flash: snap open, then shrink away.
                    float s = k < 0.12f ? k / 0.12f : 1f - (k - 0.12f) / 0.88f;
                    e.T.localScale = Vector3.one * e.Size * s;
                }
            }
        }

        Transform Spawn(GameObject prefab, string name)
        {
            var go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "View_" + name;
            go.transform.SetParent(transform, false);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = spaceLayer;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.renderingLayerMask = 1u << shipRenderingLayer;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                Destroy(col);
            return go.transform;
        }

        Transform Primitive(PrimitiveType type, Material mat, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = spaceLayer;
            go.transform.SetParent(transform, false);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.renderingLayerMask = 1u << shipRenderingLayer;
            return go.transform;
        }
    }
}

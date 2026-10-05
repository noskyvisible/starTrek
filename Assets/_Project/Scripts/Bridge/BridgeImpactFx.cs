using StarTrek.Audio;
using StarTrek.Player;
using StarTrek.Ship;
using StarTrek.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTrek.Bridge
{
    /// <summary>
    /// The bridge reacting to the battle (GAME_PROMPT §4.5): hits shake the camera, make the lights
    /// stutter and, when the shields don't hold, blow sparks out of a console. Our own weapons are
    /// heard firing.
    /// </summary>
    public class BridgeImpactFx : MonoBehaviour
    {
        [SerializeField] AlertController lights;
        [SerializeField] CameraShake shake;
        [SerializeField] AudioSource audioSource;
        [SerializeField] Material sparkMaterial;

        Starship ship;
        StationConsole[] consoles;
        ParticleSystem sparks;

        void Start()
        {
            var host = ShipSimHost.Instance;
            if (host == null)
                return;
            ship = host.Ship;
            ship.Impact += OnImpact;
            ship.WeaponFired += OnWeapon;
            consoles = FindObjectsByType<StationConsole>();
            sparks = CreateSparks();
        }

        void OnDestroy()
        {
            if (ship == null)
                return;
            ship.Impact -= OnImpact;
            ship.WeaponFired -= OnWeapon;
        }

        void OnImpact(ImpactEvent e)
        {
            if (!e.OnPlayer)
                return;
            if (shake != null)
                shake.Add(0.2f + 0.7f * e.Strength);
            if (lights != null)
                lights.Flicker(e.Strength);
            if (audioSource != null)
                audioSource.PlayOneShot(SfxBank.HullHit, 0.5f + 0.5f * e.Strength);

            bool hurt = !e.ShieldsHeld || e.Strength > 0.55f;
            if (hurt && consoles != null && consoles.Length > 0)
            {
                var console = consoles[Random.Range(0, consoles.Length)];
                sparks.transform.position = console.transform.position + Vector3.up * 1.05f;
                sparks.Emit(Mathf.RoundToInt(Mathf.Lerp(20f, 70f, e.Strength)));
                if (audioSource != null && e.Strength > 0.6f)
                    audioSource.PlayOneShot(SfxBank.Explosion, 0.7f);
            }
        }

        void OnWeapon(WeaponEvent w)
        {
            if (w.Source != null || audioSource == null)
                return;   // only our own weapons are heard on the bridge
            audioSource.PlayOneShot(w.Effect == WeaponEffect.Phaser ? SfxBank.PhaserFire : SfxBank.TorpedoLaunch, 0.6f);
        }

        ParticleSystem CreateSparks()
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1f, 0.85f, 0.5f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);   // spray upward

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3f;
            renderer.velocityScale = 0.04f;
            var mat = sparkMaterial;
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_Sparks" };
                mat.SetColor("_BaseColor", new Color(6f, 3.6f, 1.2f, 1f));
            }
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }
    }
}

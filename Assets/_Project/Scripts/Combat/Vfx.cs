using System.Collections;
using UnityEngine;

namespace StarTrek.Combat
{
    /// <summary>
    /// Small runtime effects for hand weapons and the transporter: beams, spark bursts and flashes.
    /// Materials come from Resources/VFX (made by the scene builders); missing ones fall back to a
    /// plain URP particle material in the given colour.
    /// </summary>
    public static class Vfx
    {
        public const string PhaserStun = "M_VFX_PhaserStun";
        public const string PhaserKill = "M_VFX_PhaserKill";
        public const string Disruptor = "M_VFX_HandDisruptor";
        public const string Sparks = "M_VFX_Sparks";
        public const string Transporter = "M_VFX_Transporter";

        static readonly System.Collections.Generic.Dictionary<string, Material> cache = new System.Collections.Generic.Dictionary<string, Material>();

        public static Material Get(string name, Color fallback)
        {
            if (cache.TryGetValue(name, out var m) && m != null)
                return m;
            m = Resources.Load<Material>("VFX/" + name);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
                m.SetColor("_BaseColor", fallback);
            }
            cache[name] = m;
            return m;
        }

        /// <summary>A straight beam that thins out over its lifetime.</summary>
        public static void Beam(Vector3 from, Vector3 to, Material material, float width, float seconds)
        {
            var go = new GameObject("FX_Beam");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            go.AddComponent<FxLifetime>().Begin(seconds, t => line.widthMultiplier = width * (1f - t * t));
        }

        /// <summary>A burst of sparks thrown along <paramref name="normal"/>.</summary>
        public static void SparkBurst(Vector3 at, Vector3 normal, int count = 18, float speed = 3.5f)
        {
            var go = new GameObject("FX_Sparks");
            go.transform.position = at;
            go.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.01f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Get(Sparks, new Color(6f, 3.6f, 1.2f));
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            Object.Destroy(go, 1.2f);
        }

        /// <summary>A brief point light, for muzzle flashes and hits in dark rooms.</summary>
        public static void Flash(Vector3 at, Color colour, float intensity, float range, float seconds)
        {
            var go = new GameObject("FX_Flash");
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            go.AddComponent<FxLifetime>().Begin(seconds, t => light.intensity = intensity * (1f - t));
        }
    }

    /// <summary>Runs a 0..1 callback over a lifetime, then destroys the object.</summary>
    public class FxLifetime : MonoBehaviour
    {
        public void Begin(float seconds, System.Action<float> step) => StartCoroutine(Run(seconds, step));

        IEnumerator Run(float seconds, System.Action<float> step)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                step(t / seconds);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}

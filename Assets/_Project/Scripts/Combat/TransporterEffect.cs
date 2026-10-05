using System;
using System.Collections;
using System.Collections.Generic;
using StarTrek.Audio;
using UnityEngine;

namespace StarTrek.Combat
{
    /// <summary>
    /// Beams a character (or anything with renderers) in or out: a column of sparkles and light, the
    /// shimmer sound, and the body flickering into or out of existence.
    /// </summary>
    public class TransporterEffect : MonoBehaviour
    {
        public const float DefaultSeconds = 2.4f;

        /// <summary>Materialise (true) or dematerialise (false) <paramref name="target"/>.</summary>
        public static void Play(GameObject target, bool materialise, float seconds = DefaultSeconds, Action done = null)
        {
            var go = new GameObject("FX_Transporter");
            go.transform.position = target.transform.position;
            go.AddComponent<TransporterEffect>().StartCoroutine(go.GetComponent<TransporterEffect>().Run(target, materialise, seconds, done));
        }

        IEnumerator Run(GameObject target, bool materialise, float seconds, Action done)
        {
            var renderers = new List<Renderer>();
            foreach (var r in target.GetComponentsInChildren<Renderer>())
                if (!(r is ParticleSystemRenderer))
                    renderers.Add(r);
            SetVisible(renderers, !materialise);

            var ps = Sparkles();
            var light = ps.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.7f, 0.85f, 1f);
            light.range = 4f;
            light.shadows = LightShadows.None;
            var audio = gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = 1f;
            audio.minDistance = 2f;
            audio.maxDistance = 25f;
            audio.PlayOneShot(ProceduralSfx.Transporter, 0.7f);

            var rng = new System.Random(target.name.GetHashCode() ^ Time.frameCount);
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float t = time / seconds;
                if (target == null)
                    break;
                transform.position = target.transform.position;
                float glow = Mathf.Sin(Mathf.PI * t);
                light.intensity = 3f * glow;
                var emission = ps.emission;
                emission.rateOverTime = 260f * glow;
                // Flicker between the sparkle and the solid body.
                float solid = materialise ? Mathf.InverseLerp(0.35f, 0.8f, t) : 1f - Mathf.InverseLerp(0.2f, 0.65f, t);
                SetVisible(renderers, rng.NextDouble() < solid);
                yield return null;
            }
            if (target != null)
                SetVisible(renderers, materialise);
            var stop = ps.emission;
            stop.rateOverTime = 0f;
            light.intensity = 0f;
            done?.Invoke();
            Destroy(gameObject, 1.5f);
        }

        ParticleSystem Sparkles()
        {
            var go = new GameObject("Sparkles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.055f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 600;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.55f, 1.9f, 0.45f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.35f);
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Vfx.Get(Vfx.Transporter, new Color(2.5f, 3.2f, 5f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            ps.Play();
            return ps;
        }

        static void SetVisible(List<Renderer> renderers, bool visible)
        {
            foreach (var r in renderers)
                if (r != null)
                    r.enabled = visible;
        }
    }
}

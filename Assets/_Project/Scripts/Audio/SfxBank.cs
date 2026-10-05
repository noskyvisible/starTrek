using UnityEngine;

namespace StarTrek.Audio
{
    /// <summary>
    /// The project's sound effects in one asset, so clips can be swapped without code changes.
    /// Empty slots fall back to the synthesised placeholders in <see cref="ProceduralSfx"/>.
    /// Every clip must be free-licensed or our own; log it in docs/audio_licenses.md.
    /// </summary>
    [CreateAssetMenu(menuName = "StarTrek/SFX Bank", fileName = "SFX_Bank")]
    public class SfxBank : ScriptableObject
    {
        [Header("Consoles")]
        public AudioClip buttonPress;
        public AudioClip buttonDenied;

        [Header("Doors")]
        public AudioClip doorOpen;
        public AudioClip doorClose;

        [Header("Ambience")]
        public AudioClip bridgeAmbience;

        [Header("Combat")]
        public AudioClip shieldsUp;
        public AudioClip phaserFire;
        public AudioClip torpedoLaunch;
        public AudioClip hullHit;
        public AudioClip explosion;

        /// <summary>The bank for the running scene, set by <see cref="AudioDirector"/>.</summary>
        public static SfxBank Current { get; internal set; }

        public static AudioClip ButtonPress => Pick(Current ? Current.buttonPress : null, ProceduralSfx.Chirp);
        public static AudioClip ButtonDenied => Pick(Current ? Current.buttonDenied : null, ProceduralSfx.Denied);
        public static AudioClip DoorOpen => Pick(Current ? Current.doorOpen : null, ProceduralSfx.DoorWhoosh);
        public static AudioClip DoorClose => Pick(Current ? Current.doorClose : null, ProceduralSfx.DoorWhoosh);
        public static AudioClip ShieldsUp => Pick(Current ? Current.shieldsUp : null, ProceduralSfx.Chirp);
        public static AudioClip PhaserFire => Pick(Current ? Current.phaserFire : null, ProceduralSfx.Chirp);
        public static AudioClip TorpedoLaunch => Pick(Current ? Current.torpedoLaunch : null, ProceduralSfx.DoorWhoosh);
        public static AudioClip HullHit => Pick(Current ? Current.hullHit : null, ProceduralSfx.DoorWhoosh);
        public static AudioClip Explosion => Pick(Current ? Current.explosion : null, ProceduralSfx.DoorWhoosh);

        static AudioClip Pick(AudioClip clip, AudioClip fallback) => clip != null ? clip : fallback;
    }
}

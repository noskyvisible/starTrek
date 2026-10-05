using System.Collections.Generic;
using StarTrek.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace StarTrek.EditorTools
{
    /// <summary>
    /// Turns the Blender character exports (Art/Characters/CHR_*.glb) into game-ready prefabs:
    /// glTFast animations imported for Mecanim, clips copied out as editable .anim assets with the
    /// right loop settings, one Animator controller per character, project materials, and a
    /// <see cref="CharacterAnimator"/>.
    /// </summary>
    public static class CharacterSetup
    {
        public static readonly string[] Archetypes = { "Cadet", "Klingon", "Civilian" };
        const string ModelFolder = ProjectSetup.Root + "/Art/Characters";
        const string ClipFolder = ProjectSetup.Root + "/Animation/Clips";
        const string ControllerFolder = ProjectSetup.Root + "/Animation";
        public const string PrefabFolder = ProjectSetup.Root + "/Prefabs/Characters";

        static readonly HashSet<string> Looping = new HashSet<string>
        {
            CharacterAnimator.Idle, CharacterAnimator.Walk, CharacterAnimator.Run, CharacterAnimator.SitIdle,
            CharacterAnimator.SitConsole, CharacterAnimator.AimPistol, CharacterAnimator.InjuredLie,
        };

        public static string PrefabPath(string archetype) => $"{PrefabFolder}/CHR_{archetype}.prefab";

        [MenuItem("StarTrek/Setup/Build Character Prefabs")]
        public static void BuildAll()
        {
            foreach (string a in Archetypes)
                Build(a);
            AssetDatabase.SaveAssets();
            Debug.Log("[StarTrek] Character prefabs built.");
        }

        static void Build(string archetype)
        {
            string modelPath = $"{ModelFolder}/CHR_{archetype}.glb";
            EnsureMecanim(modelPath);

            ProjectSetup.EnsureFolder(ClipFolder);
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                // Blender action names are "<Archetype>_<State>". The rigs share bone names, so every
                // export carries all archetypes' actions; keep only this character's.
                if (!(asset is AnimationClip source) || !source.name.StartsWith(archetype + "_"))
                    continue;
                string state = source.name.Substring(archetype.Length + 1);
                string clipPath = $"{ClipFolder}/{archetype}_{state}.anim";
                var copy = Object.Instantiate(source);
                copy.name = $"{archetype}_{state}";
                var settings = AnimationUtility.GetAnimationClipSettings(copy);
                settings.loopTime = Looping.Contains(state);
                AnimationUtility.SetAnimationClipSettings(copy, settings);
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(copy, existing);
                    clips[state] = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(copy, clipPath);
                    clips[state] = copy;
                }
            }
            if (clips.Count == 0)
            {
                Debug.LogError($"[StarTrek] No animation clips found in {modelPath}.");
                return;
            }

            string controllerPath = $"{ControllerFolder}/AC_{archetype}.controller";
            // Reuse the controller so prefabs and scenes keep their reference to it.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm = controller.layers[0].stateMachine;
            foreach (var child in sm.states)
                sm.RemoveState(child.state);
            foreach (var kv in clips)
            {
                var st = sm.AddState(kv.Key);
                st.motion = kv.Value;
                if (kv.Key == CharacterAnimator.Idle)
                    sm.defaultState = st;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "CHR_" + archetype;
            MaterialLibrary.ApplyTo(instance);
            var animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            instance.AddComponent<CharacterAnimator>();
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            ProjectSetup.EnsureFolder(PrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath(archetype));
            Object.DestroyImmediate(instance);
            // The in-memory variant can keep the old overrides (a null controller) until it is reimported.
            AssetDatabase.ImportAsset(PrefabPath(archetype), ImportAssetOptions.ForceUpdate);
        }

        static void EnsureMecanim(string modelPath)
        {
            var importer = AssetImporter.GetAtPath(modelPath);
            if (importer == null)
                return;
            var so = new SerializedObject(importer);
            var method = so.FindProperty("importSettings.animationMethod");
            if (method == null)
            {
                Debug.LogWarning("[StarTrek] glTFast importer has no importSettings.animationMethod; clips may import as Legacy.");
                return;
            }
            // GLTFast.AnimationMethod: None = 0, Legacy = 1, Mecanim = 2
            if (method.intValue == 2)
                return;
            method.intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
        }
    }
}

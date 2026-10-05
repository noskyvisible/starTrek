using System.Collections.Generic;
using StarTrek.Interaction;
using StarTrek.Player;
using StarTrek.Ship;
using StarTrek.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StarTrek.EditorTools
{
    /// <summary>Shared steps for the scene builders: colliders, doors, lights, alert, player.</summary>
    public static class LevelBuildKit
    {
        public const string ControlsPath = ProjectSetup.Root + "/Input/StarTrekControls.inputactions";
        public const string LightMaterialName = "M_Fed_Light_Warm";
        public static readonly Color WarmLight = new Color(1f, 0.93f, 0.82f);

        public struct Inputs
        {
            public GameObject Model;
            public InputActionAsset Controls;
            public VolumeProfile Volume;
        }

        /// <summary>
        /// Starts a fresh scene and loads the shared inputs. The scene is created first because
        /// NewScene unloads unused assets, which would invalidate anything loaded before it.
        /// </summary>
        public static bool BeginScene(string modelPath, out Inputs inputs)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            inputs = new Inputs
            {
                Model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),
                Controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath),
                Volume = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProjectSetup.VolumeProfilePath),
            };
            if (inputs.Model != null && inputs.Controls != null && inputs.Volume != null)
                return true;
            Debug.LogError($"[StarTrek] Missing input: model={inputs.Model != null} controls={inputs.Controls != null} volume={inputs.Volume != null}. Run StarTrek/Setup/Run All and export the level first.");
            return false;
        }

        public static GameObject InstantiateLevel(GameObject model, string name)
        {
            var level = (GameObject)PrefabUtility.InstantiatePrefab(model);
            level.name = name;
            MaterialLibrary.ApplyTo(level);
            return level;
        }

        public static void SaveScene(string scenePath)
        {
            ProjectSetup.EnsureFolder(System.IO.Path.GetDirectoryName(scenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == scenePath))
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Static mesh colliders everywhere, except box colliders on objects under a moving prefix.</summary>
        public static void AddColliders(GameObject level, params string[] movingPrefixes)
        {
            foreach (var renderer in level.GetComponentsInChildren<MeshRenderer>(true))
            {
                GameObject go = renderer.gameObject;
                bool moving = false;
                foreach (string prefix in movingPrefixes)
                    moving |= IsUnder(go.transform, prefix);
                if (moving)
                {
                    if (!go.GetComponent<BoxCollider>())
                        go.AddComponent<BoxCollider>();
                    continue;
                }
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                    StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ReflectionProbeStatic);
                if (!go.GetComponent<MeshCollider>())
                    go.AddComponent<MeshCollider>();
            }
        }

        static bool IsUnder(Transform t, string namePrefix)
        {
            for (; t != null; t = t.parent)
                if (t.name.StartsWith(namePrefix))
                    return true;
            return false;
        }

        public static Transform Find(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            throw new System.InvalidOperationException($"'{name}' not found in the imported level.");
        }

        public static SlidingDoor SetUpDoor(GameObject level, string doorName, string leafPrefix, bool locked, string lockedPrompt)
        {
            Transform frame = Find(level, doorName);
            var door = frame.gameObject.AddComponent<SlidingDoor>();
            var audio = AddAudio(frame.gameObject, spatial: true);

            var so = new SerializedObject(door);
            so.FindProperty("leftLeaf").objectReferenceValue = Find(level, leafPrefix + "_L");
            so.FindProperty("rightLeaf").objectReferenceValue = Find(level, leafPrefix + "_R");
            so.FindProperty("locked").boolValue = locked;
            if (lockedPrompt != null)
                so.FindProperty("lockedPrompt").stringValue = lockedPrompt;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        public static Light PointLight(Transform parent, string name, Vector3 position, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = WarmLight;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        public static AlertController AddAlertController(GameObject level, List<Light> lights)
        {
            var go = new GameObject("Ship_Alert");
            var alert = go.AddComponent<AlertController>();
            var klaxon = AddAudio(go, spatial: false);
            klaxon.volume = 0.35f;

            var materials = new List<Material>();
            foreach (var r in level.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name.StartsWith(LightMaterialName) && !materials.Contains(m))
                        materials.Add(m);

            var so = new SerializedObject(alert);
            var lightsProp = so.FindProperty("lights");
            lightsProp.arraySize = lights.Count;
            for (int i = 0; i < lights.Count; i++)
                lightsProp.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            var matsProp = so.FindProperty("emissiveMaterials");
            matsProp.arraySize = materials.Count;
            for (int i = 0; i < materials.Count; i++)
                matsProp.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
            so.FindProperty("klaxonSource").objectReferenceValue = klaxon;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (materials.Count == 0)
                Debug.LogWarning($"[StarTrek] No '{LightMaterialName}' material found; Red Alert will only tint lights.");
            return alert;
        }

        /// <summary>Make <paramref name="target"/> a push button wired to <paramref name="onPressed"/>.</summary>
        public static PushButton AddPushButton(Transform target, string prompt, UnityAction onPressed)
        {
            var button = target.gameObject.AddComponent<PushButton>();
            var audio = AddAudio(target.gameObject, spatial: true);
            var so = new SerializedObject(button);
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(button.OnPressed, onPressed);
            EditorUtility.SetDirty(button);
            return button;
        }

        /// <summary>An invisible button: a box collider at a local position on <paramref name="parent"/>.</summary>
        public static PushButton AddHotspotButton(Transform parent, string name, Vector3 localPosition, Vector3 size,
            string prompt, UnityAction onPressed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<BoxCollider>().size = size;
            return AddPushButton(go.transform, prompt, onPressed);
        }

        /// <summary>Make a chair takeable. Eye and exit points are given in the chair's local space.</summary>
        public static Seat AddSeat(Transform chair, string prompt, Vector3 localEye, Vector3 localExit)
        {
            var eye = new GameObject("SeatEye").transform;
            eye.SetParent(chair, false);
            eye.localPosition = localEye;
            eye.localRotation = Quaternion.identity;
            var exit = new GameObject("SeatExit").transform;
            exit.SetParent(chair, false);
            exit.localPosition = localExit;

            var seat = chair.gameObject.AddComponent<Seat>();
            var so = new SerializedObject(seat);
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("eyePoint").objectReferenceValue = eye;
            so.FindProperty("exitPoint").objectReferenceValue = exit;
            so.ApplyModifiedPropertiesWithoutUndo();
            return seat;
        }

        public static void SetUpEnvironment(VolumeProfile profile, Color ambient)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.fog = false;

            var go = new GameObject("PostProcess_Global");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        public static GameObject AddPlayer(InputActionAsset controls, Vector3 position, Quaternion rotation, int cullingMask = ~0)
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            // Keeps the player's own capsule out of the interaction ray and headroom checks.
            player.layer = LayerMask.NameToLayer("Ignore Raycast");
            player.transform.SetPositionAndRotation(position, rotation);

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.03f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.68f, 0f);
            // The camera sits on a child so shake never disturbs the look pivot or the interaction ray.
            var eyes = new GameObject("Eyes");
            eyes.transform.SetParent(pivot, false);
            eyes.AddComponent<CameraShake>();
            var cam = eyes.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;
            cam.fieldOfView = 70f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = cullingMask;
            eyes.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var fpc = player.AddComponent<FirstPersonController>();
            var fpcSo = new SerializedObject(fpc);
            fpcSo.FindProperty("actions").objectReferenceValue = controls;
            fpcSo.FindProperty("cameraPivot").objectReferenceValue = pivot;
            fpcSo.ApplyModifiedPropertiesWithoutUndo();

            var interactor = player.AddComponent<Interactor>();
            var intSo = new SerializedObject(interactor);
            intSo.FindProperty("actions").objectReferenceValue = controls;
            intSo.FindProperty("view").objectReferenceValue = pivot;
            intSo.ApplyModifiedPropertiesWithoutUndo();

            var hud = player.AddComponent<GrayboxHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("interactor").objectReferenceValue = interactor;
            hudSo.ApplyModifiedPropertiesWithoutUndo();
            return player;
        }

        /// <summary>Makes the SFX bank current for the scene and plays a quiet room ambience loop.</summary>
        public static void AddAudioDirector(float ambienceVolume)
        {
            var bank = AssetDatabase.LoadAssetAtPath<StarTrek.Audio.SfxBank>(ProjectSetup.SfxBankPath);
            if (bank == null)
            {
                ProjectSetup.CreateSfxBank();
                bank = AssetDatabase.LoadAssetAtPath<StarTrek.Audio.SfxBank>(ProjectSetup.SfxBankPath);
            }
            var go = new GameObject("Audio_Director");
            var ambience = AddAudio(go, spatial: false);
            ambience.volume = ambienceVolume;
            var director = go.AddComponent<StarTrek.Audio.AudioDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("bank").objectReferenceValue = bank;
            so.FindProperty("ambience").objectReferenceValue = ambience;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static AudioSource AddAudio(GameObject go, bool spatial)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.minDistance = 1.5f;
            source.maxDistance = 20f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        /// <summary>
        /// Names a rendering layer. URP ignores rendering-layer bits that have no name in
        /// Tags and Layers, so a light filtered to an unnamed layer lights nothing.
        /// </summary>
        public static void EnsureRenderingLayer(int index, string name)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_RenderingLayers");
            if (layers.arraySize <= index)
                layers.arraySize = index + 1;
            if (layers.GetArrayElementAtIndex(index).stringValue == name)
                return;
            layers.GetArrayElementAtIndex(index).stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Returns the index of a user layer with this name, creating it in the first free slot.</summary>
        public static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0)
                return existing;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue))
                    continue;
                slot.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new System.InvalidOperationException("No free user layer for " + name);
        }
    }
}

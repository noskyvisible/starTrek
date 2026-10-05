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
    /// <summary>
    /// Builds the Milestone 1 test scene from the Blender graybox export: colliders, doors, the
    /// Red Alert console button, lights, post-processing and a first-person player.
    /// Rebuilding replaces the scene, so tweak this script rather than the scene by hand.
    /// </summary>
    public static class TestLevelBuilder
    {
        const string ModelPath = ProjectSetup.Root + "/Art/Interiors/ENV_TestLevel.glb";
        const string ScenePath = ProjectSetup.Root + "/Scenes/Test/TestCorridor.unity";
        const string ControlsPath = ProjectSetup.Root + "/Input/StarTrekControls.inputactions";
        const string LightMaterialName = "M_Fed_Light_Warm";

        static readonly Color WarmLight = new Color(1f, 0.93f, 0.82f);

        [MenuItem("StarTrek/Build Test Corridor Scene")]
        public static void Build()
        {
            // Create the scene first: NewScene unloads unused assets, which would invalidate
            // anything loaded before it (the imported InputActionAsset ended up saved as null).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            var volumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProjectSetup.VolumeProfilePath);
            if (model == null || controls == null || volumeProfile == null)
            {
                Debug.LogError($"[StarTrek] Missing input: model={model != null} controls={controls != null} volume={volumeProfile != null}. Run StarTrek/Setup/Run All and export the level first.");
                return;
            }

            var level = (GameObject)PrefabUtility.InstantiatePrefab(model);
            level.name = "ENV_TestLevel";

            MaterialLibrary.ApplyTo(level);
            AddColliders(level);
            var turbolift = SetUpDoor(level, "ENV_Bulkhead_Turbolift", "PROP_Door_Turbolift", locked: true, "Turbolift offline");
            var simRoom = SetUpDoor(level, "ENV_Bulkhead_SimRoom", "PROP_Door_SimRoom", locked: false, null);

            var lights = AddInteriorLights(level);
            var alert = AddAlertController(level, lights);
            SetUpConsoleButton(level, alert);
            SetUpEnvironment(volumeProfile);
            AddPlayer(controls, turbolift, simRoom);

            ProjectSetup.EnsureFolder(System.IO.Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[StarTrek] Built {ScenePath} with {lights.Count} lights.");
        }

        static void AddColliders(GameObject level)
        {
            foreach (var renderer in level.GetComponentsInChildren<MeshRenderer>(true))
            {
                GameObject go = renderer.gameObject;
                bool moving = IsUnder(go.transform, "PROP_Door_") || IsUnder(go.transform, "PROP_Console_Button");
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

        static Transform Find(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            throw new System.InvalidOperationException($"'{name}' not found in the imported level.");
        }

        static SlidingDoor SetUpDoor(GameObject level, string bulkheadName, string leafPrefix, bool locked, string lockedPrompt)
        {
            Transform bulkhead = Find(level, bulkheadName);
            var door = bulkhead.gameObject.AddComponent<SlidingDoor>();
            var audio = AddAudio(bulkhead.gameObject, spatial: true);

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

        static List<Light> AddInteriorLights(GameObject level)
        {
            var lights = new List<Light>();
            var parent = new GameObject("Lights_Interior").transform;

            foreach (var t in level.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("ENV_Corridor_Straight_4m_"))
                    continue;
                Bounds b = t.GetComponentInChildren<Renderer>().bounds;
                lights.Add(PointLight(parent, "Light_" + t.name, new Vector3(b.center.x, 2.55f, b.center.z), 6f, 6f));
            }

            Bounds room = Find(level, "ENV_Room_Simulator").GetComponentInChildren<Renderer>().bounds;
            foreach (float dx in new[] { -2f, 2f })
            foreach (float dz in new[] { -2f, 2f })
            {
                Vector3 p = new Vector3(room.center.x + dx, 3.2f, room.center.z + dz);
                lights.Add(PointLight(parent, $"Light_SimRoom_{lights.Count}", p, 8f, 7f));
            }
            return lights;
        }

        static Light PointLight(Transform parent, string name, Vector3 position, float intensity, float range)
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

        static AlertController AddAlertController(GameObject level, List<Light> lights)
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

        static void SetUpConsoleButton(GameObject level, AlertController alert)
        {
            Transform buttonT = Find(level, "PROP_Console_Button");
            var button = buttonT.gameObject.AddComponent<PushButton>();
            var audio = AddAudio(buttonT.gameObject, spatial: true);

            var so = new SerializedObject(button);
            so.FindProperty("prompt").stringValue = "Red Alert";
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(button.OnPressed, new UnityAction(alert.ToggleRedAlert));
            EditorUtility.SetDirty(button);

            // Make the small button easy to hit with the crosshair.
            var box = buttonT.GetComponentInChildren<BoxCollider>();
            if (box != null)
                box.size = Vector3.Scale(box.size, new Vector3(1.6f, 3f, 1.6f));
        }

        static void SetUpEnvironment(VolumeProfile profile)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.09f, 0.11f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.fog = false;

            var go = new GameObject("PostProcess_Global");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static void AddPlayer(InputActionAsset controls, SlidingDoor turbolift, SlidingDoor simRoom)
        {
            Vector3 start = turbolift.transform.position;
            Vector3 toRoom = simRoom.transform.position - start;
            toRoom.y = 0f;
            toRoom.Normalize();

            var player = new GameObject("Player");
            player.tag = "Player";
            // Keeps the player's own capsule out of the interaction ray and headroom checks.
            player.layer = LayerMask.NameToLayer("Ignore Raycast");
            player.transform.SetPositionAndRotation(new Vector3(start.x, 0.05f, start.z) + toRoom * 1.6f,
                Quaternion.LookRotation(toRoom, Vector3.up));

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
            var cam = pivot.gameObject.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;
            cam.fieldOfView = 70f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            pivot.gameObject.AddComponent<AudioListener>();
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
        }

        static AudioSource AddAudio(GameObject go, bool spatial)
        {
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.minDistance = 1.5f;
            source.maxDistance = 20f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }
    }
}

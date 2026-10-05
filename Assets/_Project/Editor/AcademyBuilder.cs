using System.Collections.Generic;
using StarTrek.Academy;
using StarTrek.Characters;
using StarTrek.Combat;
using UnityEditor;
using UnityEngine;

namespace StarTrek.EditorTools
{
    /// <summary>
    /// Builds the game's first scene: the Starfleet Academy corridor outside Simulator Room 4, the
    /// maintenance alcove (easter egg), and the briefing room with Commander Hollis. Makes it the first
    /// scene in the build.
    /// </summary>
    public static class AcademyBuilder
    {
        const string ModelPath = ProjectSetup.Root + "/Art/Interiors/ENV_Academy.glb";
        public const string ScenePath = ProjectSetup.Root + "/Scenes/Academy/Academy_Simulator.unity";

        [MenuItem("StarTrek/Build Academy Scene")]
        public static void Build()
        {
            GameplaySetup.EnsureBuilt();
            if (!LevelBuildKit.BeginScene(ModelPath, out var inputs))
                return;

            var level = LevelBuildKit.InstantiateLevel(inputs.Model, "ENV_Academy");
            LevelBuildKit.AddColliders(level, "PROP_Hatch_");
            LevelBuildKit.SetUpDoor(level, "ENV_Hatch_Briefing", "PROP_Hatch_Briefing", false, null);
            var simDoor = LevelBuildKit.SetUpDoor(level, "ENV_Hatch_Simulator", "PROP_Hatch_Simulator", true,
                "Simulator 4: wait for your briefing");

            AddLights(level);
            var terminal = AddTerminal(level);
            var instructor = AddPerson(level, "CHR_Cadet", "Instructor_Hollis", "MK_Instructor", CharacterAnimator.Idle);
            AddPerson(level, "CHR_Cadet", "Cadet_Waiting_0", "MK_Cadet_0", CharacterAnimator.SitIdle);
            AddPerson(level, "CHR_Civilian", "Cadet_Waiting_1", "MK_Cadet_1", CharacterAnimator.SitIdle);
            // A cadet at the hallway windows, near the alcove, with a rumour to share.
            var rumour = AddPersonAt("CHR_Cadet", "Cadet_Ito", LevelBuildKit.Find(level, "MK_Spawn").position + new Vector3(-1.2f, 0f, 11.5f), -90f, CharacterAnimator.Idle);

            LevelBuildKit.SetUpEnvironment(inputs.Volume, new Color(0.34f, 0.35f, 0.38f));
            var sun = new GameObject("Sun_ThroughWindows").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(28f, -70f, 0f);
            LevelBuildKit.AddAudioDirector(ambienceVolume: 0.04f);

            var spawn = LevelBuildKit.Find(level, "MK_Spawn");
            var kit = new LevelBuildKit.PlayerKit { Tricorder = true };
            LevelBuildKit.AddPlayer(inputs.Controls, spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f), kit);

            var director = new GameObject("Academy_Director").AddComponent<AcademyDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("instructor").objectReferenceValue = instructor;
            so.FindProperty("briefingPoint").objectReferenceValue = LevelBuildKit.Find(level, "MK_BriefingTrigger");
            so.FindProperty("simulatorDoor").objectReferenceValue = simDoor;
            so.FindProperty("simulatorEntrance").objectReferenceValue = LevelBuildKit.Find(level, "MK_SimulatorDoor");
            so.FindProperty("rumourCadet").objectReferenceValue = rumour;
            so.ApplyModifiedPropertiesWithoutUndo();

            LevelBuildKit.SaveScene(ScenePath);
            MakeFirstScene(ScenePath);
            Debug.Log($"[StarTrek] Built {ScenePath}.");
        }

        static void AddLights(GameObject level)
        {
            var parent = new GameObject("Lights_Academy").transform;
            foreach (var t in level.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("MK_Light_"))
                {
                    var l = LevelBuildKit.PointLight(parent, t.name.Replace("MK_", ""), t.position, t.name.Contains("Alcove") ? 0.8f : 2.4f, 8f);
                    l.color = t.name.Contains("Alcove") ? new Color(1f, 0.75f, 0.45f) : new Color(1f, 0.96f, 0.9f);
                }
        }

        static MaintenanceTerminal AddTerminal(GameObject level)
        {
            var go = LevelBuildKit.Find(level, "PROP_MaintTerminal").gameObject;
            var terminal = go.AddComponent<MaintenanceTerminal>();
            var audio = LevelBuildKit.AddAudio(go, spatial: true);
            var so = new SerializedObject(terminal);
            so.FindProperty("screen").objectReferenceValue = LevelBuildKit.Find(level, "PROP_MaintScreen").GetComponent<Renderer>();
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();
            return terminal;
        }

        static Transform AddPerson(GameObject level, string prefab, string name, string marker, string pose)
        {
            var m = LevelBuildKit.Find(level, marker);
            return AddPersonAt(prefab, name, m.position, m.eulerAngles.y, pose);
        }

        static Transform AddPersonAt(string prefab, string name, Vector3 position, float yaw, string pose)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterSetup.PrefabFolder}/{prefab}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.name = name;
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var anim = go.GetComponent<CharacterAnimator>();
            var so = new SerializedObject(anim);
            so.FindProperty("startState").stringValue = pose;
            so.ApplyModifiedPropertiesWithoutUndo();
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 0.3f;
            capsule.height = pose == CharacterAnimator.SitIdle ? 1.3f : 1.8f;
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
            var scan = go.AddComponent<ScanTarget>();
            scan.Configure(name.Replace('_', ' '), "Human. Healthy. Pulse a little fast: test nerves.", 1);
            return go.transform;
        }

        /// <summary>The Academy opens the game; the other scenes follow.</summary>
        static void MakeFirstScene(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int i = scenes.FindIndex(s => s.path == path);
            if (i <= 0)
                return;
            var academy = scenes[i];
            scenes.RemoveAt(i);
            scenes.Insert(0, academy);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

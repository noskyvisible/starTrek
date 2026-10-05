using System.Collections.Generic;
using StarTrek.Combat;
using StarTrek.Ground;
using StarTrek.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace StarTrek.EditorTools
{
    /// <summary>
    /// Builds the away-mission scene aboard the Kobayashi Maru (docs/reference/kobayashi_maru.md §3)
    /// from the Blender export: dark emergency lighting and a head lamp, the jammed passenger hatch
    /// (tricorder bypass or phaser-cut weld), survivors to tag, the radiation leak and its console,
    /// sparking conduits, and things to scan. Gameplay positions come from the MK_* markers.
    /// </summary>
    public static class KMFreighterBuilder
    {
        const string ModelPath = ProjectSetup.Root + "/Art/Interiors/ENV_KM_Freighter.glb";
        public const string ScenePath = ProjectSetup.Root + "/Scenes/KobayashiMaru/KM_Freighter.unity";

        static readonly Color Amber = new Color(1f, 0.55f, 0.25f);
        static readonly Color Red = new Color(1f, 0.15f, 0.1f);
        static readonly Color FuelBlue = new Color(0.4f, 0.75f, 1f);

        // Survivor groups: marker, people, lying injured (or sitting huddled), who they are.
        static readonly (string marker, int people, bool lying, string name)[] Survivors =
        {
            ("MK_Survivor_Lounge", 26, true, "Passengers"),
            ("MK_Survivor_Bench", 14, false, "Passengers"),
            ("MK_Survivor_CabinA", 9, true, "Passengers"),
            ("MK_Survivor_CabinC", 11, false, "Passengers"),
            ("MK_Survivor_Bridge", 7, true, "Freighter crew"),
        };

        static readonly Dictionary<string, (string name, string reading)> Scans = new Dictionary<string, (string, string)>
        {
            { "MK_Scan_Manifest", ("Cargo manifest, hold B", "Medical supplies and machine parts for the Gamma Hydra colonies. Neutronic fuel in the main tanks. Passenger deck forward: 300 berths, all taken.") },
            { "MK_Scan_Breach", ("Hull breach", "Sealed with emergency foam. A fragment of a gravitic mine is buried in the plating. No mines were charted in this sector.") },
            { "MK_Scan_Tank0", ("Fuel tank 1", "Intact. Neutronic fuel stable.") },
            { "MK_Scan_Tank1", ("Fuel tank 2", "Intact. Pressure low but holding.") },
            { "MK_Scan_Tank2", ("Fuel tank 3", "Intact. Neutronic fuel stable.") },
            { "MK_Scan_Tank3", ("Fuel tank 4", "CRACKED. Neutronic fuel is venting, and the radiation is scattering transporter beams. Close the feed valves at the fuel-control console.") },
            { "MK_Scan_ShipLog", ("Ship's log, last entry", "\"Struck a mine without warning. Main power gone, life support failing. Before the sensors died we caught three shadows at the edge of the zone. Sensor ghosts, or... Sending a distress call on all frequencies.\"") },
            { "MK_Scan_Viewport", ("Forward viewport", "Stars. And faint ion traces just beyond the Maru, the kind a cloaked ship leaves behind.") },
        };

        [MenuItem("StarTrek/Build Kobayashi Maru Interior")]
        public static void Build()
        {
            GameplaySetup.EnsureBuilt();
            if (!LevelBuildKit.BeginScene(ModelPath, out var inputs))
                return;

            var level = LevelBuildKit.InstantiateLevel(inputs.Model, "ENV_KM_Freighter");
            LevelBuildKit.AddColliders(level, "PROP_Hatch_");
            Object.DestroyImmediate(LevelBuildKit.Find(level, "PROP_FuelVapour").GetComponent<Collider>());

            LevelBuildKit.SetUpDoor(level, "ENV_Hatch_CargoSpine", "PROP_Hatch_CargoSpine", false, null);
            LevelBuildKit.SetUpDoor(level, "ENV_Hatch_Fuel", "PROP_Hatch_Fuel", false, null);
            LevelBuildKit.SetUpDoor(level, "ENV_Hatch_Bridge", "PROP_Hatch_Bridge", false, null);
            var jammed = LevelBuildKit.SetUpDoor(level, "ENV_Hatch_Passenger", "PROP_Hatch_Passenger", true,
                "Hatch jammed. Find its lock panel, or cut the weld (phaser on kill)");
            AddJammedHatch(level, jammed);

            var lights = AddLights(level);
            AddFuelLeak(level, lights);
            AddSurvivors(level);
            AddScans(level);
            AddSparks(level);

            LevelBuildKit.SetUpEnvironment(inputs.Volume, new Color(0.03f, 0.032f, 0.04f));
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.035f;
            RenderSettings.fogColor = new Color(0.02f, 0.025f, 0.03f);
            LevelBuildKit.AddAudioDirector(ambienceVolume: 0.08f);

            var spawn = LevelBuildKit.Find(level, "MK_Spawn");
            var kit = LevelBuildKit.PlayerKit.Full;
            kit.Communicator = true;
            var player = LevelBuildKit.AddPlayer(inputs.Controls, spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f), kit);
            AddHeadLamp(player);
            LevelBuildKit.AddSpawnPoint(StarTrek.Core.GameSession.ArrivalSpawn, spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f));
            new GameObject("AwayMission").AddComponent<AwayMissionScene>();

            LevelBuildKit.SaveScene(ScenePath);
            Debug.Log($"[StarTrek] Built {ScenePath}: {Survivors.Length} survivor groups, {lights.Count} lights.");
        }

        static void AddJammedHatch(GameObject level, SlidingDoor door)
        {
            var panel = LevelBuildKit.Find(level, "PROP_BypassPanel").gameObject;
            var bypass = panel.AddComponent<BypassPanel>();
            var audio = LevelBuildKit.AddAudio(panel, spatial: true);
            var so = new SerializedObject(bypass);
            so.FindProperty("door").objectReferenceValue = door;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();

            var weld = LevelBuildKit.Find(level, "PROP_Weld").gameObject;
            var cut = weld.AddComponent<PhaserCuttable>();
            var cso = new SerializedObject(cut);
            cso.FindProperty("glow").objectReferenceValue = weld.GetComponent<Renderer>();
            cso.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(cut.OnCut, new UnityAction(bypass.Open));
            // Once open, the weld bead in the doorway goes (it would block the way through).
            UnityEventTools.AddBoolPersistentListener(bypass.OnOpened, new UnityAction<bool>(weld.SetActive), false);
            EditorUtility.SetDirty(cut);
            EditorUtility.SetDirty(bypass);
        }

        static List<Light> AddLights(GameObject level)
        {
            var lights = new List<Light>();
            var parent = new GameObject("Lights_Emergency").transform;
            foreach (var t in level.GetComponentsInChildren<Transform>(true))
            {
                bool amber = t.name.StartsWith("MK_LightAmber"), red = t.name.StartsWith("MK_LightRed"), blue = t.name.StartsWith("MK_LightBlue");
                if (!amber && !red && !blue)
                    continue;
                bool hold = t.name.Contains("_CH");   // the 6 m tall cargo hold needs more reach
                var light = LevelBuildKit.PointLight(parent, t.name.Replace("MK_", "Light_"), t.position,
                    amber ? (hold ? 2.6f : 1.8f) : red ? 1.5f : 2.4f, amber ? (hold ? 10f : 7.5f) : 6.5f);
                light.color = amber ? Amber : red ? Red : FuelBlue;
                lights.Add(light);
            }
            // One overhead work light in the hold still runs on battery: cold and dim.
            var spawn = LevelBuildKit.Find(level, "MK_Spawn");
            var work = LevelBuildKit.PointLight(parent, "Light_HoldWork", spawn.position + new Vector3(0f, 5.4f, -1.5f), 1.4f, 11f);
            work.color = new Color(0.75f, 0.85f, 1f);
            lights.Add(work);
            return lights;
        }

        static void AddFuelLeak(GameObject level, List<Light> lights)
        {
            var centre = LevelBuildKit.Find(level, "MK_Radiation");
            var zoneGo = new GameObject("Radiation_FuelLeak");
            zoneGo.transform.position = centre.position;
            var zone = zoneGo.AddComponent<RadiationZone>();
            var zso = new SerializedObject(zone);
            zso.FindProperty("radius").floatValue = 5.5f;
            zso.FindProperty("damagePerSecond").floatValue = 6f;
            zso.FindProperty("detectRange").floatValue = 14f;
            zso.ApplyModifiedPropertiesWithoutUndo();

            var console = LevelBuildKit.Find(level, "PROP_FuelConsole").gameObject;
            var leak = console.AddComponent<LeakControl>();
            var audio = LevelBuildKit.AddAudio(console, spatial: true);
            var so = new SerializedObject(leak);
            var zones = so.FindProperty("radiation");
            zones.arraySize = 1;
            zones.GetArrayElementAtIndex(0).objectReferenceValue = zone;
            so.FindProperty("warningLight").objectReferenceValue = lights.Find(l => l.name == "Light_LightRed_FC");
            var hide = so.FindProperty("hideWhenSealed");
            var blue = lights.Find(l => l.name == "Light_LightBlue_FC");
            hide.arraySize = 2;
            hide.GetArrayElementAtIndex(0).objectReferenceValue = LevelBuildKit.Find(level, "PROP_FuelVapour").gameObject;
            hide.GetArrayElementAtIndex(1).objectReferenceValue = blue != null ? blue.gameObject : null;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddSurvivors(GameObject level)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplaySetup.SurvivorPath);
            var parent = new GameObject("Survivors").transform;
            foreach (var (marker, people, lying, name) in Survivors)
            {
                var m = LevelBuildKit.Find(level, marker);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = "Survivor_" + marker.Substring("MK_Survivor_".Length);
                go.transform.SetPositionAndRotation(m.position, Quaternion.Euler(0f, m.eulerAngles.y, 0f));
                var s = go.GetComponent<Survivor>();
                var so = new SerializedObject(s);
                so.FindProperty("people").intValue = people;
                so.FindProperty("injured").boolValue = lying;
                so.FindProperty("groupName").stringValue = name;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void AddScans(GameObject level)
        {
            var parent = new GameObject("ScanTargets").transform;
            foreach (var kv in Scans)
            {
                var m = LevelBuildKit.Find(level, kv.Key);
                var go = new GameObject("Scan_" + kv.Key.Substring("MK_Scan_".Length));
                go.transform.SetParent(parent, false);
                go.transform.position = m.position;
                var scan = go.AddComponent<ScanTarget>();
                var so = new SerializedObject(scan);
                so.FindProperty("scanName").stringValue = kv.Value.name;
                so.FindProperty("reading").stringValue = kv.Value.reading;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void AddSparks(GameObject level)
        {
            var parent = new GameObject("Hazards_Sparks").transform;
            foreach (var t in level.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("MK_Spark_"))
                    continue;
                var go = new GameObject(t.name.Replace("MK_", "Hazard_"));
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(t.position, Quaternion.identity);
                var light = LevelBuildKit.PointLight(go.transform, "Flicker", t.position, 0.8f, 3.5f);
                light.color = new Color(0.7f, 0.8f, 1f);
                var audio = LevelBuildKit.AddAudio(go, spatial: true);
                var spark = go.AddComponent<SparkingConduit>();
                var so = new SerializedObject(spark);
                so.FindProperty("flickerLight").objectReferenceValue = light;
                so.FindProperty("audioSource").objectReferenceValue = audio;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>The away team's lamp: a soft spot on the camera, the main light in a dead ship.</summary>
        static void AddHeadLamp(GameObject player)
        {
            var eyes = player.GetComponentInChildren<Camera>().transform;
            var go = new GameObject("HeadLamp");
            go.transform.SetParent(eyes, false);
            go.transform.localPosition = new Vector3(0.12f, -0.1f, 0.05f);
            var lamp = go.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.range = 20f;
            lamp.spotAngle = 62f;
            lamp.innerSpotAngle = 30f;
            lamp.intensity = 8f;
            lamp.color = new Color(1f, 0.96f, 0.88f);
            lamp.shadows = LightShadows.None;
        }
    }
}

using System.Collections.Generic;
using StarTrek.Ship;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

namespace StarTrek.EditorTools
{
    /// <summary>
    /// Builds the Milestone 2 graybox bridge (Deck 1) from the Blender export: takeable seats,
    /// Red Alert on the captain's armrest and at Tactical, locked turbolifts, lighting, and a live
    /// starfield on the viewscreen. Rebuilding replaces the scene.
    /// </summary>
    public static class BridgeBuilder
    {
        const string ModelPath = ProjectSetup.Root + "/Art/Interiors/ENV_Bridge_Refit.glb";
        const string ScenePath = ProjectSetup.Root + "/Scenes/Ship_Decks/Deck01_Bridge.unity";
        const string ViewscreenRTPath = ProjectSetup.RenderingDir + "/RT_Viewscreen.renderTexture";
        const string StarsMaterialPath = MaterialLibrary.Folder + "/M_Space_Stars.mat";
        const string ViewscreenMaterialPath = MaterialLibrary.Folder + "/M_Fed_Viewscreen.mat";

        const float RingHeight = 0.45f;

        // Bridge stations around the raised ring: object suffix and HUD name.
        static readonly (string id, string label)[] Stations =
        {
            ("Science", "Science"), ("Communications", "Communications"), ("Environmental", "Environmental"),
            ("Security", "Security"), ("Engineering", "Engineering"), ("Tactical", "Tactical"),
            ("DamageControl", "Damage Control"),
        };

        [MenuItem("StarTrek/Build Bridge Scene")]
        public static void Build()
        {
            if (!LevelBuildKit.BeginScene(ModelPath, out var inputs))
                return;
            int spaceLayer = LevelBuildKit.EnsureLayer("Space");

            var level = LevelBuildKit.InstantiateLevel(inputs.Model, "ENV_Bridge_Refit");
            LevelBuildKit.AddColliders(level, "PROP_Door_");
            LevelBuildKit.SetUpDoor(level, "ENV_BridgeDoor_TurboliftStarboard", "PROP_Door_TurboliftStarboard", true, "Turbolift offline");
            LevelBuildKit.SetUpDoor(level, "ENV_BridgeDoor_TurboliftPort", "PROP_Door_TurboliftPort", true, "Turbolift offline");

            var lights = AddBridgeLights();
            var alert = LevelBuildKit.AddAlertController(level, lights);
            AddSeats(level);
            AddRedAlertButtons(level, alert);
            AddViewscreenFeed(spaceLayer);

            LevelBuildKit.SetUpEnvironment(inputs.Volume, new Color(0.07f, 0.07f, 0.08f));

            // Spawn just off the starboard turbolift, as if the player has stepped out onto the bridge.
            var spawn = new Vector3(1.52f, RingHeight + 0.05f, -3.26f);
            var facing = Quaternion.LookRotation(new Vector3(-1.52f, 0f, 6.3f), Vector3.up);
            LevelBuildKit.AddPlayer(inputs.Controls, spawn, facing, ~(1 << spaceLayer));

            LevelBuildKit.SaveScene(ScenePath);
            Debug.Log($"[StarTrek] Built {ScenePath} with {lights.Count} lights, Space layer {spaceLayer}.");
        }

        static List<Light> AddBridgeLights()
        {
            var parent = new GameObject("Lights_Bridge").transform;
            var lights = new List<Light>
            {
                LevelBuildKit.PointLight(parent, "Light_Bridge_Centre", new Vector3(0f, 3.75f, 0f), 7f, 8f)
            };
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.Deg2Rad * (22.5f + 45f * i);
                var p = new Vector3(3.45f * Mathf.Sin(a), 3.35f, 3.45f * Mathf.Cos(a));
                lights.Add(LevelBuildKit.PointLight(parent, $"Light_Bridge_Ring_{i}", p, 3f, 5f));
            }

            // Soft spill from the viewscreen; stays the same during Red Alert, so it isn't in the list.
            var spill = LevelBuildKit.PointLight(parent, "Light_Viewscreen_Spill", new Vector3(0f, 2.0f, 3.4f), 1.2f, 4.5f);
            spill.color = new Color(0.6f, 0.75f, 1f);
            return lights;
        }

        static void AddSeats(GameObject level)
        {
            // Unity local axes for every seat: +Z is the sitter's facing.
            LevelBuildKit.AddSeat(LevelBuildKit.Find(level, "PROP_Bridge_CaptainChair"), "Take the captain's chair",
                new Vector3(0f, 1.17f, 0.05f), new Vector3(0.8f, 0f, 0f));
            LevelBuildKit.AddSeat(LevelBuildKit.Find(level, "PROP_Seat_Helm"), "Take the helm",
                new Vector3(0f, 1.15f, 0f), new Vector3(-0.75f, 0f, 0f));
            LevelBuildKit.AddSeat(LevelBuildKit.Find(level, "PROP_Seat_Navigation"), "Take navigation",
                new Vector3(0f, 1.15f, 0f), new Vector3(0.75f, 0f, 0f));
            foreach (var (id, label) in Stations)
                LevelBuildKit.AddSeat(LevelBuildKit.Find(level, "PROP_Seat_" + id), "Sit at " + label,
                    new Vector3(0f, 1.15f, 0f), new Vector3(0f, 0f, -0.7f));
        }

        static void AddRedAlertButtons(GameObject level, AlertController alert)
        {
            var toggle = new UnityAction(alert.ToggleRedAlert);
            // Red pad on the captain's left armrest.
            LevelBuildKit.AddHotspotButton(LevelBuildKit.Find(level, "PROP_Bridge_CaptainChair"), "BTN_RedAlert_Chair",
                new Vector3(-0.38f, 0.67f, 0.09f), new Vector3(0.12f, 0.05f, 0.3f), "Red Alert", toggle);
            // Button row on the Tactical console.
            LevelBuildKit.AddHotspotButton(LevelBuildKit.Find(level, "STATION_Tactical"), "BTN_RedAlert_Tactical",
                new Vector3(0f, 0.94f, -0.42f), new Vector3(1.1f, 0.06f, 0.14f), "Red Alert", toggle);
        }

        static void AddViewscreenFeed(int spaceLayer)
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(ViewscreenRTPath);
            if (rt == null)
            {
                rt = new RenderTexture(768, 432, 16, RenderTextureFormat.DefaultHDR) { name = "RT_Viewscreen" };
                AssetDatabase.CreateAsset(rt, ViewscreenRTPath);
            }

            var stars = AssetDatabase.LoadAssetAtPath<Material>(StarsMaterialPath);
            if (stars == null)
            {
                stars = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_Space_Stars" };
                stars.SetColor("_BaseColor", new Color(2.2f, 2.2f, 2.2f, 1f));
                AssetDatabase.CreateAsset(stars, StarsMaterialPath);
            }

            var screen = AssetDatabase.LoadAssetAtPath<Material>(ViewscreenMaterialPath);
            screen.SetColor("_BaseColor", Color.black);
            screen.SetTexture("_EmissionMap", rt);
            screen.SetColor("_EmissionColor", new Color(1.3f, 1.3f, 1.3f));
            screen.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(screen);

            // The space view lives far below the ship on its own layer; only its camera sees it.
            var rig = new GameObject("SpaceView");
            rig.transform.position = new Vector3(0f, -2000f, 0f);

            var field = new GameObject("Starfield", typeof(MeshFilter), typeof(MeshRenderer));
            field.layer = spaceLayer;
            field.transform.SetParent(rig.transform, false);
            field.GetComponent<MeshRenderer>().sharedMaterial = stars;
            field.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            field.AddComponent<Starfield>();

            var camGo = new GameObject("ViewscreenCamera");
            camGo.transform.SetParent(rig.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.cullingMask = 1 << spaceLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.004f, 0.006f, 0.016f);
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 1000f;
            cam.depth = -10f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camGo.AddComponent<SpaceDrift>();

            AssetDatabase.SaveAssets();
        }
    }
}

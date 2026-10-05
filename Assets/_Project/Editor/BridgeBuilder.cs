using System;
using System.Collections.Generic;
using StarTrek.Bridge;
using StarTrek.Ship;
using StarTrek.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

namespace StarTrek.EditorTools
{
    /// <summary>
    /// Builds the graybox bridge (Deck 1) from the Blender export: the ship simulation, a working
    /// console at every station (buttons + live screens), captain's orders by looking, takeable seats,
    /// Red Alert on the captain's armrest, locked turbolifts, lighting, and a live starfield on the
    /// viewscreen. Rebuilding replaces the scene.
    /// </summary>
    public static class BridgeBuilder
    {
        const string ModelPath = ProjectSetup.Root + "/Art/Interiors/ENV_Bridge_Refit.glb";
        const string ScenePath = ProjectSetup.Root + "/Scenes/Ship_Decks/Deck01_Bridge.unity";
        const string ViewscreenRTPath = ProjectSetup.RenderingDir + "/RT_Viewscreen.renderTexture";
        const string StarsMaterialPath = MaterialLibrary.Folder + "/M_Space_Stars.mat";
        const string ViewscreenMaterialPath = MaterialLibrary.Folder + "/M_Fed_Viewscreen.mat";

        // Must match st_bridge.py in Interiors_Federation.blend (scaled up from the ~9.1 m film set).
        const float RingHeight = 0.45f;
        const float WallRadius = 6.4f;
        const float Ceiling = 4.2f;
        const float Dome = 4.8f;

        // Bridge stations around the raised ring: object suffix and HUD name.
        static readonly (string id, string label)[] Stations =
        {
            ("Science", "Science"), ("Communications", "Communications"), ("Environmental", "Environmental"),
            ("Security", "Security"), ("Engineering", "Engineering"), ("Tactical", "Tactical"),
            ("DamageControl", "Damage Control"),
        };

        static readonly Dictionary<StationRole, Color> Accents = new Dictionary<StationRole, Color>
        {
            { StationRole.Helm, new Color(0.4f, 0.75f, 1f) }, { StationRole.Navigation, new Color(1f, 0.75f, 0.3f) },
            { StationRole.Tactical, new Color(1f, 0.38f, 0.22f) }, { StationRole.Science, new Color(0.35f, 0.7f, 1f) },
            { StationRole.Communications, new Color(1f, 0.7f, 0.25f) }, { StationRole.Engineering, new Color(1f, 0.55f, 0.15f) },
            { StationRole.Environmental, new Color(0.35f, 1f, 0.5f) }, { StationRole.Security, new Color(1f, 0.8f, 0.3f) },
            { StationRole.DamageControl, new Color(0.7f, 1f, 0.3f) },
        };

        [MenuItem("StarTrek/Build Bridge Scene")]
        public static void Build()
        {
            GameplaySetup.EnsureBuilt();
            if (!LevelBuildKit.BeginScene(ModelPath, out var inputs))
                return;
            int spaceLayer = LevelBuildKit.EnsureLayer("Space");

            var level = LevelBuildKit.InstantiateLevel(inputs.Model, "ENV_Bridge_Refit");
            LevelBuildKit.AddColliders(level, "PROP_Door_");
            LevelBuildKit.SetUpDoor(level, "ENV_BridgeDoor_TurboliftStarboard", "PROP_Door_TurboliftStarboard", true, "Turbolift offline");
            LevelBuildKit.SetUpDoor(level, "ENV_BridgeDoor_TurboliftPort", "PROP_Door_TurboliftPort", true, "Turbolift offline");

            var lights = AddBridgeLights();
            var alert = LevelBuildKit.AddAlertController(level, lights);
            var host = AddSimulation(alert);
            var consoles = AddStationConsoles(level);
            AddSeats(level, consoles);
            AddRedAlertButtons(level, host);
            AddViewscreenFeed(spaceLayer);

            LevelBuildKit.SetUpEnvironment(inputs.Volume, new Color(0.07f, 0.07f, 0.08f));
            LevelBuildKit.AddAudioDirector(ambienceVolume: 0.22f);

            // Spawn just off the starboard turbolift (155 deg), as if the player has stepped out onto the bridge.
            var spawn = OnRing(WallRadius - 1.2f, 155f) + Vector3.up * (RingHeight + 0.05f);
            var facing = Quaternion.LookRotation(new Vector3(-spawn.x, 0f, 3f - spawn.z), Vector3.up);
            var player = LevelBuildKit.AddPlayer(inputs.Controls, spawn, facing, ~(1 << spaceLayer));
            AddCaptainTools(player);
            AddImpactFx(alert, player);
            LevelBuildKit.Find(level, "PROP_Bridge_CaptainChair").gameObject.AddComponent<CaptainsChair>();

            // The away team beams back into the well behind the captain's chair, facing the viewscreen.
            LevelBuildKit.AddSpawnPoint(StarTrek.Core.GameSession.ReturnSpawn, OnRing(1.7f, 180f) + Vector3.up * 0.05f, Quaternion.identity);

            // NPCs: bake their NavMesh first, so their capsules don't cut holes in it.
            var navMesh = LevelBuildKit.BakeNavMesh(ScenePath, new Bounds(new Vector3(0f, 2f, 0f), new Vector3(WallRadius * 2.4f, 6f, WallRadius * 2.4f)));
            AddCrew(level, consoles);
            var boarding = AddBoarding(navMesh);
            AddSimulationEnd(level, alert, boarding);

            LevelBuildKit.SaveScene(ScenePath);
            Debug.Log($"[StarTrek] Built {ScenePath} with {lights.Count} lights, Space layer {spaceLayer}.");
        }

        static List<Light> AddBridgeLights()
        {
            var parent = new GameObject("Lights_Bridge").transform;
            var lights = new List<Light>
            {
                LevelBuildKit.PointLight(parent, "Light_Bridge_Centre", new Vector3(0f, Dome - 0.4f, 0f), 9f, 10f)
            };
            const int ringLights = 10;
            for (int i = 0; i < ringLights; i++)
            {
                var p = OnRing(WallRadius * 0.76f, 18f + 360f / ringLights * i) + Vector3.up * (Ceiling - 0.3f);
                lights.Add(LevelBuildKit.PointLight(parent, $"Light_Bridge_Ring_{i}", p, 3.5f, 6.5f));
            }

            // Soft spill from the viewscreen; stays the same during Red Alert, so it isn't in the list.
            var spill = LevelBuildKit.PointLight(parent, "Light_Viewscreen_Spill", new Vector3(0f, 2.4f, WallRadius * 0.78f), 1.4f, 6f);
            spill.color = new Color(0.6f, 0.75f, 1f);
            return lights;
        }

        /// <summary>
        /// Unity position on the bridge floor plan: radius r, angle phi in degrees from forward (+Z),
        /// positive to starboard (+X). Same convention as P2() in st_bridge.py after the glTF axis swap.
        /// </summary>
        static Vector3 OnRing(float r, float phi)
        {
            float a = phi * Mathf.Deg2Rad;
            return new Vector3(r * Mathf.Sin(a), 0f, r * Mathf.Cos(a));
        }

        static void AddSeats(GameObject level, Dictionary<StationRole, StationConsole> consoles)
        {
            // Unity local axes for every seat: +Z is the sitter's facing.
            LevelBuildKit.AddSeat(LevelBuildKit.Find(level, "PROP_Bridge_CaptainChair"), "Take the captain's chair",
                new Vector3(0f, 1.17f, 0.05f), new Vector3(0.8f, 0f, 0f));
            AddStationSeat(level, "PROP_Seat_Helm", "Take the helm", new Vector3(-0.75f, 0f, 0f), consoles[StationRole.Helm]);
            AddStationSeat(level, "PROP_Seat_Navigation", "Take navigation", new Vector3(0.75f, 0f, 0f), consoles[StationRole.Navigation]);
            foreach (var (id, label) in Stations)
                AddStationSeat(level, "PROP_Seat_" + id, "Sit at " + label, new Vector3(0f, 0f, -0.7f),
                    consoles[(StationRole)Enum.Parse(typeof(StationRole), id)]);
        }

        static void AddStationSeat(GameObject level, string seatName, string prompt, Vector3 localExit, StationConsole console)
        {
            var seat = LevelBuildKit.Find(level, seatName);
            LevelBuildKit.AddSeat(seat, prompt, new Vector3(0f, 1.15f, 0f), localExit);
            var link = seat.gameObject.AddComponent<StationLink>();
            var so = new SerializedObject(link);
            so.FindProperty("console").objectReferenceValue = console;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Where a seated officer's feet go, in the chair's local space (+Z = facing the console).
        static readonly Vector3 CrewSeatOffset = new Vector3(0f, 0f, 0.02f);

        /// <summary>A cadet officer at every station: seated, working the console, ready to stand aside.</summary>
        static void AddCrew(GameObject level, Dictionary<StationRole, StationConsole> consoles)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplaySetup.OfficerPath);
            var parent = new GameObject("Crew").transform;
            foreach (StationRole role in Enum.GetValues(typeof(StationRole)))
            {
                string seatName = "PROP_Seat_" + role;
                Vector3 exit = role == StationRole.Helm ? new Vector3(-0.75f, 0f, 0f)
                             : role == StationRole.Navigation ? new Vector3(0.75f, 0f, 0f)
                             : new Vector3(0f, 0f, -0.7f);
                var chair = LevelBuildKit.Find(level, seatName);
                var pose = Child(chair, "CrewSeatPose", CrewSeatOffset, Quaternion.identity);
                var stand = Child(chair, "CrewStandPoint", exit + new Vector3(0f, 0f, -0.1f), Quaternion.identity);
                // Stand points sit on the floor, whatever height the chair's origin is at.
                if (Physics.Raycast(stand.position + Vector3.up, Vector3.down, out var floor, 2.5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    stand.position = floor.point;

                var officer = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                officer.name = "Crew_" + role;
                officer.transform.SetPositionAndRotation(pose.position, pose.rotation);
                var crew = officer.GetComponent<StarTrek.Crew.CrewOfficer>();
                var so = new SerializedObject(crew);
                so.FindProperty("role").enumValueIndex = (int)role;
                so.FindProperty("seat").objectReferenceValue = chair.GetComponent<StarTrek.Interaction.Seat>();
                so.FindProperty("seatPose").objectReferenceValue = pose;
                so.FindProperty("standPoint").objectReferenceValue = stand;
                so.FindProperty("fightsBack").boolValue = role == StationRole.Security || role == StationRole.Tactical;
                so.ApplyModifiedPropertiesWithoutUndo();
                // Looking at the officer gives orders to their station.
                var link = officer.AddComponent<StationLink>();
                var lso = new SerializedObject(link);
                lso.FindProperty("console").objectReferenceValue = consoles[role];
                lso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static StarTrek.Boarding.BoardingDirector AddBoarding(UnityEngine.AI.NavMeshData navMesh)
        {
            // The baked NavMesh is only live in Play mode; load it briefly to check the spawn points.
            var live = UnityEngine.AI.NavMesh.AddNavMeshData(navMesh);
            var go = new GameObject("Boarding");
            var director = go.AddComponent<StarTrek.Boarding.BoardingDirector>();
            // Aft by the turbolifts, the port ring, and the well in front of the captain.
            var points = new[]
            {
                (OnRing(WallRadius - 1.4f, 200f) + Vector3.up * RingHeight, 20f),
                (OnRing(WallRadius - 1.4f, 110f) + Vector3.up * RingHeight, -70f),
                (OnRing(2.6f, -40f), 140f),
            };
            var spawns = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                spawns[i] = Child(go.transform, "BoarderSpawn_" + i, points[i].Item1, Quaternion.Euler(0f, points[i].Item2, 0f));
                if (UnityEngine.AI.NavMesh.SamplePosition(spawns[i].position, out var hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
                    spawns[i].position = hit.position;
                else
                    Debug.LogWarning($"[StarTrek] Boarder spawn {i} is off the NavMesh at {spawns[i].position}.");
            }
            live.Remove();
            var so = new SerializedObject(director);
            so.FindProperty("boarderPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(GameplaySetup.BoarderPath);
            var arr = so.FindProperty("spawnPoints");
            arr.arraySize = spawns.Length;
            for (int i = 0; i < spawns.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return director;
        }

        static void AddSimulationEnd(GameObject level, AlertController alert, StarTrek.Boarding.BoardingDirector boarding)
        {
            var go = new GameObject("Simulation_End");
            var end = go.AddComponent<SimulationEnd>();
            var evaluation = go.AddComponent<StarTrek.UI.EvaluationScreen>();
            var audio = LevelBuildKit.AddAudio(go, spatial: false);
            Renderer screen = null;
            foreach (var r in level.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name.StartsWith("M_Fed_Viewscreen"))
                        screen = r;
            var so = new SerializedObject(end);
            so.FindProperty("lights").objectReferenceValue = alert;
            so.FindProperty("viewscreen").objectReferenceValue = screen;
            so.FindProperty("spaceView").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<SpaceView>();
            var cam = GameObject.Find("ViewscreenCamera");
            so.FindProperty("viewscreenCamera").objectReferenceValue = cam != null ? cam.GetComponent<Camera>() : null;
            so.FindProperty("boarding").objectReferenceValue = boarding;
            so.FindProperty("evaluation").objectReferenceValue = evaluation;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (screen == null)
                Debug.LogWarning("[StarTrek] No viewscreen renderer found for the end-of-simulation test pattern.");
        }

        static void AddRedAlertButtons(GameObject level, ShipSimHost host)
        {
            // Red pad on the captain's left armrest; Tactical has its own RED ALERT button.
            LevelBuildKit.AddHotspotButton(LevelBuildKit.Find(level, "PROP_Bridge_CaptainChair"), "BTN_RedAlert_Chair",
                new Vector3(-0.38f, 0.67f, 0.09f), new Vector3(0.12f, 0.05f, 0.3f), "Red Alert", new UnityAction(host.ToggleRedAlert));
        }

        static ShipSimHost AddSimulation(AlertController alert)
        {
            var go = new GameObject("Ship_Simulation");
            var host = go.AddComponent<ShipSimHost>();
            var so = new SerializedObject(host);
            so.FindProperty("alertDisplay").objectReferenceValue = alert;
            so.ApplyModifiedPropertiesWithoutUndo();
            return host;
        }

        /// <summary>A working console at every station. The helm desk is shared by Helm and Navigation.</summary>
        static Dictionary<StationRole, StationConsole> AddStationConsoles(GameObject level)
        {
            var consoles = new Dictionary<StationRole, StationConsole>();

            // Ring stations (Unity local: -Z faces the room; desk top slopes up toward the wall at ~19.5 deg).
            foreach (var (id, _) in Stations)
            {
                var role = (StationRole)Enum.Parse(typeof(StationRole), id);
                var desk = LevelBuildKit.Find(level, "STATION_" + id).gameObject;
                var surface = Child(desk.transform, "ControlSurface", new Vector3(0f, 0.966f, -0.33f), Quaternion.Euler(-19.5f, 0f, 0f));
                var left = Child(desk.transform, "ScreenAnchor_0", new Vector3(-0.355f, 1.85f, -0.085f), Quaternion.identity);
                var right = Child(desk.transform, "ScreenAnchor_1", new Vector3(0.355f, 1.85f, -0.085f), Quaternion.identity);
                consoles[role] = AddConsole(desk, role, surface, new Vector2(1.3f, 0.42f), new[] { left, right }, new Vector2(0.6f, 0.86f));
            }

            // Helm/navigation desk: one half per station, each with a pair of small monitors on the far edge.
            var helmDesk = LevelBuildKit.Find(level, "STATION_Helm");
            // The whole-desk mesh collider would not resolve to either half; each half gets its own colliders.
            UnityEngine.Object.DestroyImmediate(helmDesk.GetComponent<MeshCollider>());
            foreach (var (role, x) in new[] { (StationRole.Helm, -0.62f), (StationRole.Navigation, 0.62f) })
            {
                var half = new GameObject("CONSOLE_" + role);
                half.transform.SetParent(helmDesk, false);
                var body = half.AddComponent<BoxCollider>();
                body.center = new Vector3(x, 0.34f, 0f);
                body.size = new Vector3(1.2f, 0.68f, 0.6f);   // below the sloped top, so rays reach the buttons
                var surface = Child(half.transform, "ControlSurface", new Vector3(x, 0.808f, 0.01f), Quaternion.Euler(-14.9f, 0f, 0f));
                // Sloped top plate just under the buttons: rays that miss a button still find this station.
                var top = Child(surface, "TopPlate", new Vector3(0f, -0.012f, 0f), Quaternion.identity);
                top.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.2f, 0.02f, 0.62f);
                var a = Child(half.transform, "ScreenAnchor_0", new Vector3(x - 0.24f, 1.08f, 0.27f), Quaternion.Euler(15f, 0f, 0f));
                var b = Child(half.transform, "ScreenAnchor_1", new Vector3(x + 0.24f, 1.08f, 0.27f), Quaternion.Euler(15f, 0f, 0f));
                foreach (var anchor in new[] { a, b })
                    MonitorBacking(anchor, new Vector2(0.48f, 0.32f));
                consoles[role] = AddConsole(half, role, surface, new Vector2(0.96f, 0.4f), new[] { a, b }, new Vector2(0.46f, 0.3f));
            }
            return consoles;
        }

        static StationConsole AddConsole(GameObject owner, StationRole role, Transform surface, Vector2 surfaceSize,
            Transform[] screens, Vector2 screenSize)
        {
            var console = owner.AddComponent<StationConsole>();
            var audio = LevelBuildKit.AddAudio(owner, spatial: true);
            var so = new SerializedObject(console);
            so.FindProperty("role").enumValueIndex = (int)role;
            so.FindProperty("officer").stringValue = CrewRoster.Officer(role);
            so.FindProperty("controlSurface").objectReferenceValue = surface;
            so.FindProperty("surfaceSize").vector2Value = surfaceSize;
            var anchors = so.FindProperty("screenAnchors");
            anchors.arraySize = screens.Length;
            for (int i = 0; i < screens.Length; i++)
                anchors.GetArrayElementAtIndex(i).objectReferenceValue = screens[i];
            so.FindProperty("screenSize").vector2Value = screenSize;
            so.FindProperty("accent").colorValue = Accents[role];
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();
            return console;
        }

        static Transform Child(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            return t;
        }

        /// <summary>A dark plate behind a free-standing monitor screen.</summary>
        static void MonitorBacking(Transform anchor, Vector2 size)
        {
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "MonitorBacking";
            plate.transform.SetParent(anchor, false);
            plate.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            plate.transform.localScale = new Vector3(size.x, size.y, 0.015f);
            plate.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialLibrary.Folder + "/M_Fed_Bridge_Panel.mat");
            GameObjectUtility.SetStaticEditorFlags(plate, StaticEditorFlags.BatchingStatic);
        }

        static void AddCaptainTools(GameObject player)
        {
            var orders = player.AddComponent<CaptainOrders>();
            var so = new SerializedObject(orders);
            so.FindProperty("view").objectReferenceValue = player.transform.Find("CameraPivot");
            so.FindProperty("interactor").objectReferenceValue = player.GetComponent<StarTrek.Interaction.Interactor>();
            so.FindProperty("body").objectReferenceValue = player.GetComponent<StarTrek.Player.FirstPersonController>();
            so.ApplyModifiedPropertiesWithoutUndo();
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

            // Stars sit far beyond the ships (which are drawn a few km out), so they never cover them.
            var field = new GameObject("Starfield", typeof(MeshFilter), typeof(MeshRenderer));
            field.layer = spaceLayer;
            field.transform.SetParent(rig.transform, false);
            field.GetComponent<MeshRenderer>().sharedMaterial = stars;
            field.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var starfield = field.AddComponent<Starfield>();
            var sfSo = new SerializedObject(starfield);
            sfSo.FindProperty("radius").floatValue = 60000f;
            sfSo.FindProperty("minSize").floatValue = 75f;
            sfSo.FindProperty("maxSize").floatValue = 300f;
            sfSo.ApplyModifiedPropertiesWithoutUndo();

            var camGo = new GameObject("ViewscreenCamera");
            camGo.transform.SetParent(rig.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.cullingMask = 1 << spaceLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.004f, 0.006f, 0.016f);
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 10f;
            cam.farClipPlane = 130000f;
            cam.depth = -10f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            // A sun that lights only the ships out there (Space layer + ship rendering layer), never the bridge.
            var sunGo = new GameObject("SpaceSun");
            sunGo.transform.SetParent(rig.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.None;
            // Rendering layers keep the sun off the bridge (bridge objects stay on "Default").
            LevelBuildKit.EnsureRenderingLayer(ShipRenderingLayer, "Space");
            sun.cullingMask = -1;
            sun.renderingLayerMask = 1 << ShipRenderingLayer;
            sun.GetUniversalAdditionalLightData().renderingLayers = (uint)(1 << ShipRenderingLayer);

            var view = rig.AddComponent<SpaceView>();
            var so = new SerializedObject(view);
            so.FindProperty("viewCamera").objectReferenceValue = cam;
            so.FindProperty("starfield").objectReferenceValue = field.transform;
            so.FindProperty("hostileModel").objectReferenceValue = ShipPrefab(KtingaModelPath, "SHIP_Klingon_Ktinga");
            so.FindProperty("civilianModel").objectReferenceValue = ShipPrefab(FreighterModelPath, "SHIP_Civil_KobayashiMaru");
            so.FindProperty("phaserMaterial").objectReferenceValue = VfxMaterial("M_VFX_Phaser", new Color(4.5f, 1.1f, 0.3f));
            so.FindProperty("disruptorMaterial").objectReferenceValue = VfxMaterial("M_VFX_Disruptor", new Color(0.5f, 6f, 0.9f));
            so.FindProperty("torpedoMaterial").objectReferenceValue = VfxMaterial("M_VFX_Torpedo", new Color(7f, 1.4f, 0.4f));
            so.FindProperty("explosionMaterial").objectReferenceValue = VfxMaterial("M_VFX_Explosion", new Color(8f, 3.5f, 1f));
            so.FindProperty("shieldMaterial").objectReferenceValue = VfxMaterial("M_VFX_ShieldFlash", new Color(1.6f, 3.2f, 7f));
            so.FindProperty("shipRenderingLayer").intValue = ShipRenderingLayer;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
        }

        const int ShipRenderingLayer = 1;
        const string KtingaModelPath = ProjectSetup.Root + "/Art/Ships/Klingon/SHIP_Klingon_Ktinga_Graybox.glb";
        const string FreighterModelPath = ProjectSetup.Root + "/Art/Ships/Civilian/SHIP_Civil_KobayashiMaru_Graybox.glb";

        /// <summary>
        /// Wraps an imported ship model in a prefab that uses the project's URP Lit materials
        /// (glTFast's own materials didn't take the space sun's light).
        /// </summary>
        static GameObject ShipPrefab(string modelPath, string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError("[StarTrek] Missing ship model " + modelPath);
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = name;
            MaterialLibrary.ApplyTo(instance);
            string folder = ProjectSetup.Root + "/Prefabs/Ships";
            ProjectSetup.EnsureFolder(folder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, $"{folder}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        /// <summary>A glowing unlit HDR material for weapons and effects (bloom does the rest).</summary>
        static Material VfxMaterial(string name, Color hdr)
        {
            string path = MaterialLibrary.Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", hdr);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void AddImpactFx(AlertController alert, GameObject player)
        {
            var go = new GameObject("Bridge_ImpactFx");
            var fx = go.AddComponent<BridgeImpactFx>();
            var audio = LevelBuildKit.AddAudio(go, spatial: false);
            var so = new SerializedObject(fx);
            so.FindProperty("lights").objectReferenceValue = alert;
            so.FindProperty("shake").objectReferenceValue = player.GetComponentInChildren<StarTrek.Player.CameraShake>();
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("sparkMaterial").objectReferenceValue = VfxMaterial("M_VFX_Sparks", new Color(6f, 3.6f, 1.2f));
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

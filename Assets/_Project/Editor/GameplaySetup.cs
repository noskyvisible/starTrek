using StarTrek.Boarding;
using StarTrek.Characters;
using StarTrek.Combat;
using StarTrek.Crew;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace StarTrek.EditorTools
{
    /// <summary>
    /// Gameplay assets the scene builders share: weapon and transporter effect materials (in
    /// Resources so runtime code can load them), the first-person phaser and tricorder, and the NPC
    /// prefabs (Klingon boarder, crew officer, survivor) built on the character prefabs.
    /// </summary>
    public static class GameplaySetup
    {
        public const string VfxFolder = ProjectSetup.Root + "/Resources/VFX";
        const string HandheldsModel = ProjectSetup.Root + "/Art/Props/PROP_Handhelds.glb";
        public const string PrefabFolder = ProjectSetup.Root + "/Prefabs/Gameplay";
        public static string PhaserViewPath => PrefabFolder + "/VM_HandPhaser.prefab";
        public static string TricorderViewPath => PrefabFolder + "/VM_Tricorder.prefab";
        public static string BoarderPath => CharacterSetup.PrefabFolder + "/NPC_KlingonBoarder.prefab";
        public static string OfficerPath => CharacterSetup.PrefabFolder + "/NPC_CrewOfficer.prefab";
        public static string SurvivorPath => CharacterSetup.PrefabFolder + "/NPC_Survivor.prefab";

        [MenuItem("StarTrek/Setup/Build Gameplay Assets")]
        public static void BuildAll()
        {
            BuildVfxMaterials();
            BuildViewModels();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSetup.PrefabPath("Klingon")) == null)
                CharacterSetup.BuildAll();
            BuildNpcs();
            AssetDatabase.SaveAssets();
            Debug.Log("[StarTrek] Gameplay assets built.");
        }

        /// <summary>Builds everything that is missing (the scene builders call this first).</summary>
        public static void EnsureBuilt()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BoarderPath) == null
                || AssetDatabase.LoadAssetAtPath<GameObject>(SurvivorPath) == null
                || AssetDatabase.LoadAssetAtPath<GameObject>(PhaserViewPath) == null
                || AssetDatabase.LoadAssetAtPath<Material>(VfxFolder + "/" + StarTrek.Combat.Vfx.Transporter + ".mat") == null)
                BuildAll();
        }

        static void BuildVfxMaterials()
        {
            ProjectSetup.EnsureFolder(VfxFolder);
            VfxMaterial(StarTrek.Combat.Vfx.PhaserStun, new Color(2.2f, 3.2f, 6.5f));
            VfxMaterial(StarTrek.Combat.Vfx.PhaserKill, new Color(6.5f, 1.5f, 0.4f));
            VfxMaterial(StarTrek.Combat.Vfx.Disruptor, new Color(6f, 1.1f, 0.45f));
            VfxMaterial(StarTrek.Combat.Vfx.Sparks, new Color(6f, 3.6f, 1.2f));
            VfxMaterial(StarTrek.Combat.Vfx.Transporter, new Color(2.6f, 3.4f, 6f));
        }

        static void VfxMaterial(string name, Color hdr)
        {
            string path = $"{VfxFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", hdr);
            EditorUtility.SetDirty(m);
        }

        static void BuildViewModels()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HandheldsModel);
            if (model == null)
            {
                Debug.LogError("[StarTrek] Missing " + HandheldsModel + " (export it from Characters_Graybox.blend).");
                return;
            }
            ProjectSetup.EnsureFolder(PrefabFolder);
            SaveViewModel(model, "VM_HandPhaser", PhaserViewPath);
            SaveViewModel(model, "VM_Tricorder", TricorderViewPath);
        }

        static void SaveViewModel(GameObject model, string rootName, string path)
        {
            Transform source = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == rootName)
                    source = t;
            if (source == null)
            {
                Debug.LogError($"[StarTrek] {rootName} not found in {HandheldsModel}.");
                return;
            }
            var copy = Object.Instantiate(source.gameObject);
            copy.name = rootName;
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            MaterialLibrary.ApplyTo(copy);
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            PrefabUtility.SaveAsPrefabAsset(copy, path);
            Object.DestroyImmediate(copy);
        }

        static void BuildNpcs()
        {
            // Klingon boarder: fights on the bridge.
            var boarder = Begin("Klingon");
            AddBody(boarder, 0.3f);
            var agent = boarder.AddComponent<NavMeshAgent>();
            agent.radius = 0.32f;
            agent.height = 1.8f;
            agent.speed = 1.7f;
            agent.angularSpeed = 540f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 1f;
            ConfigureHealth(boarder.AddComponent<Health>(), 100f, 0f, 100f);
            LevelBuildKit.AddAudio(boarder, spatial: true);
            boarder.AddComponent<KlingonBoarder>();
            Save(boarder, BoarderPath);

            // Crew officer: a cadet at a bridge station.
            var officer = Begin("Cadet");
            AddBody(officer, 0.28f);
            ConfigureHealth(officer.AddComponent<Health>(), 45f, 0f, 100f);
            LevelBuildKit.AddAudio(officer, spatial: true);
            officer.AddComponent<CrewOfficer>();
            Save(officer, OfficerPath);

            // Survivor: a freighter passenger or crew member waiting for rescue.
            var survivor = Begin("Civilian");
            AddBody(survivor, 0.3f);
            ConfigureHealth(survivor.AddComponent<Health>(), 60f, 0f, 60f);
            survivor.AddComponent<ScanTarget>();
            LevelBuildKit.AddAudio(survivor, spatial: true);
            survivor.AddComponent<StarTrek.Ground.Survivor>();
            Save(survivor, SurvivorPath);
        }

        static GameObject Begin(string archetype)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSetup.PrefabPath(archetype));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            return go;
        }

        static void AddBody(GameObject go, float radius)
        {
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
        }

        static void ConfigureHealth(Health health, float max, float regen, float stunAt)
        {
            var so = new SerializedObject(health);
            so.FindProperty("maxHealth").floatValue = max;
            so.FindProperty("regenPerSecond").floatValue = regen;
            so.FindProperty("stunThreshold").floatValue = stunAt;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Save(GameObject go, string path)
        {
            go.name = System.IO.Path.GetFileNameWithoutExtension(path);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}

using System.Collections.Generic;
using StarTrek.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

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

        [MenuItem("StarTrek/Build Test Corridor Scene")]
        public static void Build()
        {
            if (!LevelBuildKit.BeginScene(ModelPath, out var inputs))
                return;

            var level = LevelBuildKit.InstantiateLevel(inputs.Model, "ENV_TestLevel");
            LevelBuildKit.AddColliders(level, "PROP_Door_", "PROP_Console_Button");
            var turbolift = LevelBuildKit.SetUpDoor(level, "ENV_Bulkhead_Turbolift", "PROP_Door_Turbolift", true, "Turbolift offline");
            var simRoom = LevelBuildKit.SetUpDoor(level, "ENV_Bulkhead_SimRoom", "PROP_Door_SimRoom", false, null);

            var lights = AddInteriorLights(level);
            var alert = LevelBuildKit.AddAlertController(level, lights);

            Transform buttonT = LevelBuildKit.Find(level, "PROP_Console_Button");
            LevelBuildKit.AddPushButton(buttonT, "Red Alert", new UnityAction(alert.ToggleRedAlert));
            // Make the small button easy to hit with the crosshair.
            var box = buttonT.GetComponentInChildren<BoxCollider>();
            if (box != null)
                box.size = Vector3.Scale(box.size, new Vector3(1.6f, 3f, 1.6f));

            LevelBuildKit.SetUpEnvironment(inputs.Volume, new Color(0.09f, 0.09f, 0.11f));
            LevelBuildKit.AddAudioDirector(ambienceVolume: 0.15f);

            Vector3 start = turbolift.transform.position;
            Vector3 toRoom = simRoom.transform.position - start;
            toRoom.y = 0f;
            toRoom.Normalize();
            LevelBuildKit.AddPlayer(inputs.Controls, new Vector3(start.x, 0.05f, start.z) + toRoom * 1.6f,
                Quaternion.LookRotation(toRoom, Vector3.up));

            LevelBuildKit.SaveScene(ScenePath);
            Debug.Log($"[StarTrek] Built {ScenePath} with {lights.Count} lights.");
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
                lights.Add(LevelBuildKit.PointLight(parent, "Light_" + t.name, new Vector3(b.center.x, 2.55f, b.center.z), 6f, 6f));
            }

            Bounds room = LevelBuildKit.Find(level, "ENV_Room_Simulator").GetComponentInChildren<Renderer>().bounds;
            foreach (float dx in new[] { -2f, 2f })
            foreach (float dz in new[] { -2f, 2f })
            {
                Vector3 p = new Vector3(room.center.x + dx, 3.2f, room.center.z + dz);
                lights.Add(LevelBuildKit.PointLight(parent, $"Light_SimRoom_{lights.Count}", p, 8f, 7f));
            }
            return lights;
        }
    }
}

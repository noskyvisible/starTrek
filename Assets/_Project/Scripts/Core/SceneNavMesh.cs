using UnityEngine;
using UnityEngine.AI;

namespace StarTrek.Core
{
    /// <summary>Adds the scene's baked NavMesh (built by the scene builder) while the scene is loaded.</summary>
    public class SceneNavMesh : MonoBehaviour
    {
        [SerializeField] NavMeshData data;

        NavMeshDataInstance instance;

        public NavMeshData Data
        {
            get => data;
            set => data = value;
        }

        void OnEnable()
        {
            if (data != null)
                instance = NavMesh.AddNavMeshData(data);
        }

        void OnDisable()
        {
            if (instance.valid)
                instance.Remove();
        }
    }
}

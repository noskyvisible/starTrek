using UnityEngine;

namespace StarTrek.Ship
{
    /// <summary>
    /// Procedural starfield: one mesh of small star quads on a sphere around this object, facing the
    /// centre. Seen by the space camera that feeds the bridge viewscreen.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Starfield : MonoBehaviour
    {
        [SerializeField] int starCount = 2500;
        [SerializeField] float radius = 400f;
        [SerializeField] float minSize = 0.5f;
        [SerializeField] float maxSize = 2.0f;
        [SerializeField] int seed = 1701;

        void Awake() => GetComponent<MeshFilter>().sharedMesh = Build();

        Mesh Build()
        {
            var rng = new System.Random(seed);
            var verts = new Vector3[starCount * 4];
            var colors = new Color[starCount * 4];
            var tris = new int[starCount * 6];

            for (int i = 0; i < starCount; i++)
            {
                Vector3 dir = RandomDirection(rng);
                float brightness = Mathf.Pow((float)rng.NextDouble(), 3f);   // mostly faint, a few bright
                float size = Mathf.Lerp(minSize, maxSize, brightness);
                // Star colour from cool blue-white to warm yellow-white.
                float temp = (float)rng.NextDouble();
                Color tint = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.9f, 0.75f), temp);
                Color c = tint * Mathf.Lerp(0.35f, 1f, brightness);
                c.a = 1f;

                Vector3 centre = dir * radius;
                Vector3 right = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right).normalized * size;
                Vector3 up = Vector3.Cross(right, dir).normalized * size;
                int v = i * 4;
                verts[v + 0] = centre - right - up;
                verts[v + 1] = centre + right - up;
                verts[v + 2] = centre + right + up;
                verts[v + 3] = centre - right + up;
                for (int k = 0; k < 4; k++)
                    colors[v + k] = c;
                // Clockwise as seen from the centre, so the quad's front face points at the camera.
                int t = i * 6;
                tris[t + 0] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "Starfield" };
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (radius * 2.2f));
            return mesh;
        }

        static Vector3 RandomDirection(System.Random rng)
        {
            // Marsaglia: uniform point on the unit sphere.
            while (true)
            {
                float a = (float)(rng.NextDouble() * 2.0 - 1.0);
                float b = (float)(rng.NextDouble() * 2.0 - 1.0);
                float s = a * a + b * b;
                if (s >= 1f || s < 1e-6f)
                    continue;
                float k = 2f * Mathf.Sqrt(1f - s);
                return new Vector3(a * k, b * k, 1f - 2f * s);
            }
        }
    }
}

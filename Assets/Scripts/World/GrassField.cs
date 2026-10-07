using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MutantPlants
{
    /// <summary>
    /// Draws thousands of grass tufts with GPU instancing (no GameObjects).
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        public Material material;
        public int count = 9000;
        public float outerRadius = 75f;
        [Tooltip("No grass inside this XZ rectangle (the garden).")]
        public Vector2 innerHalfExtents = new Vector2(31f, 31f);
        public Bounds[] exclusions;
        public Vector2 sizeRange = new Vector2(0.5f, 1.1f);
        public int seed = 3;

        Mesh mesh;
        readonly List<Matrix4x4[]> batches = new List<Matrix4x4[]>();
        RenderParams renderParams;

        void Start()
        {
            mesh = BuildTuft();
            var rng = new System.Random(seed);
            var all = new List<Matrix4x4>(count);
            int guard = 0;
            while (all.Count < count && guard++ < count * 4)
            {
                var p = new Vector3((float)(rng.NextDouble() * 2 - 1) * outerRadius, 0f, (float)(rng.NextDouble() * 2 - 1) * outerRadius);
                if (Mathf.Abs(p.x) < innerHalfExtents.x && Mathf.Abs(p.z) < innerHalfExtents.y) continue;
                if (IsExcluded(p)) continue;
                float s = Mathf.Lerp(sizeRange.x, sizeRange.y, (float)rng.NextDouble());
                var rot = Quaternion.Euler((float)(rng.NextDouble() * 16 - 8), (float)rng.NextDouble() * 360f, (float)(rng.NextDouble() * 16 - 8));
                all.Add(Matrix4x4.TRS(transform.position + p, rot, new Vector3(s, s * Mathf.Lerp(0.8f, 1.4f, (float)rng.NextDouble()), s)));
            }
            for (int i = 0; i < all.Count; i += 1023)
                batches.Add(all.GetRange(i, Mathf.Min(1023, all.Count - i)).ToArray());

            renderParams = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(transform.position, Vector3.one * outerRadius * 2.2f),
            };
        }

        bool IsExcluded(Vector3 p)
        {
            if (exclusions == null) return false;
            foreach (var b in exclusions)
                if (b.Contains(new Vector3(p.x, b.center.y, p.z))) return true;
            return false;
        }

        void Update()
        {
            if (material == null || mesh == null) return;
            foreach (var batch in batches)
                Graphics.RenderMeshInstanced(renderParams, mesh, 0, batch);
        }

        /// <summary>Three crossed, tapered blades.</summary>
        static Mesh BuildTuft()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int b = 0; b < 3; b++)
            {
                var rot = Quaternion.Euler(0f, b * 60f, 0f);
                for (int blade = -1; blade <= 1; blade++)
                {
                    int i = verts.Count;
                    float lean = blade * 0.12f;
                    Vector3 baseL = rot * new Vector3(-0.06f + blade * 0.1f, 0f, 0f);
                    Vector3 baseR = rot * new Vector3(0.06f + blade * 0.1f, 0f, 0f);
                    Vector3 tip = rot * new Vector3(blade * 0.1f + lean, 0.45f + (blade == 0 ? 0.15f : 0f), 0.05f);
                    verts.Add(baseL); verts.Add(baseR); verts.Add(tip);
                    normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up);
                    uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0.5f, 1f));
                    tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                }
            }
            var mesh = new Mesh { name = "GrassTuft" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

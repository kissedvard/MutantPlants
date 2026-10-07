using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MutantPlants.EditorTools
{
    /// <summary>
    /// Cartoon enemy models (smooth procedural meshes, big eyes, inverted-hull outlines)
    /// and their shared Animator Controller with Idle / Walk / Attack / Hit / Die clips.
    ///
    /// Every enemy uses the same transform hierarchy, so one set of clips drives them all:
    ///   Root (NavMeshAgent, EnemyPlant, Animator)
    ///     Rig                  - procedural: rise from the soil, hit punch, size
    ///       Hips               - static, at hip height
    ///         Body             - animated (bob, squash, lean)
    ///           BodyMesh, Outline, EyeL/EyeR (blink), Mouth (animated), Top (animated)
    ///           ArmL/Swing, ArmR/Swing (animated)
    ///       LegL/Swing, LegR/Swing (animated)
    /// </summary>
    public static partial class MutantPlantsBuilder
    {
        const string ModelDir = "Assets/Models/Generated";
        const string AnimDir = "Assets/Animations";

        const string PB = "Rig/Hips/Body";
        const string PArmL = PB + "/ArmL/Swing";
        const string PArmR = PB + "/ArmR/Swing";
        const string PLegL = "Rig/LegL/Swing";
        const string PLegR = "Rig/LegR/Swing";
        const string PTop = PB + "/Top";
        const string PMouth = PB + "/Mouth";

        struct EnemySet { public EnemyPlant carrot, eggplant, pickle, pumpkin, king; }

        // ------------------------------------------------------------------ Lathe shape

        /// <summary>Surface of revolution around Y, with optional radial ribs and a bend.</summary>
        class Lathe
        {
            public float height = 1f;
            public Func<float, float> radius;               // t (0 bottom .. 1 top) -> radius
            public Func<float, float> heightAt;             // optional custom height profile
            public Func<float, float, float> radialMod;     // (t, angle) -> radius multiplier
            public Func<float, Vector2> bend;               // t -> xz offset

            public float H(float t) => heightAt != null ? heightAt(t) : t * height;
            public float R(float t, float a)
            {
                float r = radius(Mathf.Clamp01(t)) * (radialMod != null ? radialMod(t, a) : 1f);
                return float.IsNaN(r) ? 0f : Mathf.Max(0f, r);
            }
            public Vector2 Off(float t) => bend != null ? bend(t) : Vector2.zero;

            /// <summary>Point on the surface (local to the mesh) and an approximate outward normal.</summary>
            public Vector3 Point(float t, float angleDeg, out Vector3 normal)
            {
                float a = angleDeg * Mathf.Deg2Rad;
                float r = R(t, a);
                var o = Off(t);
                var p = new Vector3(Mathf.Cos(a) * r + o.x, H(t), Mathf.Sin(a) * r + o.y);
                float dt = 0.01f;
                float dr = R(Mathf.Min(1f, t + dt), a) - R(Mathf.Max(0f, t - dt), a);
                float dh = H(Mathf.Min(1f, t + dt)) - H(Mathf.Max(0f, t - dt));
                normal = new Vector3(Mathf.Cos(a) * dh, -dr, Mathf.Sin(a) * dh).normalized;
                return p;
            }

            public Mesh Build(string name, int segments = 40, int rings = 32)
            {
                var verts = new List<Vector3>();
                var tris = new List<int>();
                verts.Add(new Vector3(Off(0f).x, H(0f), Off(0f).y));
                for (int i = 1; i < rings; i++)
                {
                    float t = i / (float)rings;
                    for (int j = 0; j < segments; j++)
                    {
                        float a = j / (float)segments * Mathf.PI * 2f;
                        float r = R(t, a);
                        var o = Off(t);
                        verts.Add(new Vector3(Mathf.Cos(a) * r + o.x, H(t), Mathf.Sin(a) * r + o.y));
                    }
                }
                verts.Add(new Vector3(Off(1f).x, H(1f), Off(1f).y));
                int top = verts.Count - 1;
                int Ring(int i, int j) => 1 + (i - 1) * segments + ((j % segments) + segments) % segments;

                for (int j = 0; j < segments; j++)
                {
                    tris.Add(0); tris.Add(Ring(1, j)); tris.Add(Ring(1, j + 1));
                    tris.Add(top); tris.Add(Ring(rings - 1, j + 1)); tris.Add(Ring(rings - 1, j));
                }
                for (int i = 1; i < rings - 1; i++)
                    for (int j = 0; j < segments; j++)
                    {
                        int a = Ring(i, j), b = Ring(i, j + 1), c = Ring(i + 1, j + 1), d = Ring(i + 1, j);
                        tris.Add(a); tris.Add(d); tris.Add(c);
                        tris.Add(a); tris.Add(c); tris.Add(b);
                    }
                return FinishMesh(name, verts, tris, Ring(rings / 2, 0));
            }
        }

        /// <summary>Builds the mesh and makes sure the triangles face outward.</summary>
        static Mesh FinishMesh(string name, List<Vector3> verts, List<int> tris, int probeVertex)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            var center = mesh.bounds.center;
            var probe = verts[probeVertex];
            var outward = probe - new Vector3(center.x, probe.y, center.z);
            if (Vector3.Dot(mesh.normals[probeVertex], outward) < 0f)
            {
                for (int i = 0; i < tris.Count; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return SaveMesh(mesh);
        }

        static Mesh SaveMesh(Mesh mesh)
        {
            var path = $"{ModelDir}/{mesh.name}.asset";
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Inverted-hull outline: the same mesh pushed out along its normals.</summary>
        static Mesh OutlineMesh(Mesh source, float width)
        {
            var verts = source.vertices;
            var normals = source.normals;
            for (int i = 0; i < verts.Length; i++) verts[i] += normals[i] * width;
            var mesh = new Mesh { name = source.name + "_Outline" };
            mesh.vertices = verts;
            mesh.triangles = source.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        /// <summary>Curved, pointed leaf (double-sided material).</summary>
        static Mesh LeafMesh(string name, float length, float width, float curl)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int rows = 10, cols = 4;
            for (int i = 0; i <= rows; i++)
            {
                float u = i / (float)rows;
                float w = width * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Lerp(0.05f, 1f, u))), 0.7f) * (1f - 0.25f * u);
                for (int k = 0; k <= cols; k++)
                {
                    float v = k / (float)cols * 2f - 1f;
                    float fold = (1f - Mathf.Abs(v)) * width * 0.25f; // raised midrib
                    float y = u * length;
                    float z = curl * u * u * length + fold;
                    verts.Add(new Vector3(v * w, y, z));
                }
            }
            for (int i = 0; i < rows; i++)
                for (int k = 0; k < cols; k++)
                {
                    int a = i * (cols + 1) + k, b = a + 1, c = a + cols + 2, d = a + cols + 1;
                    tris.Add(a); tris.Add(d); tris.Add(c);
                    tris.Add(a); tris.Add(c); tris.Add(b);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        // ------------------------------------------------------------------ Part helpers

        static GameObject MeshPart(string name, Transform parent, Mesh mesh, string mat, Vector3 pos, Vector3? euler = null, Vector3? scale = null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale ?? Vector3.one;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mats[mat];
            return go;
        }

        /// <summary>Primitive with a black inverted-hull outline child.</summary>
        static GameObject Toon(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, string mat, Vector3? euler = null, float outline = 0.12f)
        {
            var go = Prim(type, name, parent, pos, scale, mat, false, euler);
            if (outline > 0f)
            {
                var o = Prim(type, "Outline", go.transform, Vector3.zero, Vector3.one, "Outline", false);
                // Thicker in thin directions so the line looks even.
                var s = scale;
                float w = outline * Mathf.Min(s.x, Mathf.Min(s.y, s.z));
                o.transform.localScale = new Vector3(1f + w / s.x * 2f, 1f + w / s.y * 2f, 1f + w / s.z * 2f);
                o.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        static Transform Pivot(string name, Transform parent, Vector3 pos, Vector3? euler = null) =>
            Empty(name, parent, pos, euler).transform;

        /// <summary>Big cartoon eye: white ball, coloured iris, pupil and a shine.</summary>
        static Transform Eye(string name, Transform parent, Vector3 pos, Vector3 outward, float size, string irisMat)
        {
            var pivot = Pivot(name, parent, pos);
            pivot.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            Toon(PrimitiveType.Sphere, "White", pivot, Vector3.zero, new Vector3(1f, 1.1f, 0.85f) * size, "EyeWhite", null, 0.09f);
            Prim(PrimitiveType.Sphere, "Iris", pivot, new Vector3(0f, -0.04f, 0.33f) * size, new Vector3(0.62f, 0.66f, 0.25f) * size, irisMat, false);
            Prim(PrimitiveType.Sphere, "Pupil", pivot, new Vector3(0f, -0.04f, 0.4f) * size, new Vector3(0.34f, 0.36f, 0.14f) * size, "Black", false);
            Prim(PrimitiveType.Sphere, "Shine", pivot, new Vector3(0.12f, 0.1f, 0.44f) * size, Vector3.one * 0.16f * size, "Shine", false, shadows: false);
            Prim(PrimitiveType.Sphere, "Shine2", pivot, new Vector3(-0.1f, -0.15f, 0.44f) * size, Vector3.one * 0.07f * size, "Shine", false, shadows: false);
            return pivot;
        }

        /// <summary>Open cartoon smile with tongue and two fangs.</summary>
        static Transform Mouth(Transform parent, Vector3 pos, Vector3 outward, float width, bool fangs = true, bool jagged = false)
        {
            var pivot = Pivot("Mouth", parent, pos);
            pivot.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            Toon(PrimitiveType.Sphere, "Inside", pivot, Vector3.zero, new Vector3(width, width * 0.42f, width * 0.22f), "MouthDark", null, 0.25f);
            Prim(PrimitiveType.Sphere, "Tongue", pivot, new Vector3(0f, -width * 0.1f, width * 0.04f), new Vector3(width * 0.55f, width * 0.2f, width * 0.15f), "Tongue", false);
            if (jagged)
            {
                for (int i = 0; i < 4; i++)
                    Prim(PrimitiveType.Cube, "Tooth", pivot, new Vector3((i - 1.5f) * width * 0.22f, width * 0.14f, width * 0.07f),
                        new Vector3(width * 0.12f, width * 0.12f, width * 0.06f), "Teeth", false, new Vector3(0f, 0f, 45f));
            }
            else if (fangs)
            {
                for (int s = -1; s <= 1; s += 2)
                    Prim(PrimitiveType.Cube, "Fang", pivot, new Vector3(s * width * 0.24f, width * 0.12f, width * 0.08f),
                        new Vector3(width * 0.1f, width * 0.1f, width * 0.05f), "Teeth", false, new Vector3(0f, 0f, 45f));
            }
            return pivot;
        }

        /// <summary>Noodle arm with a three-finger hand. The static pivot sets the rest angle; "Swing" is animated.</summary>
        static void Arm(string name, Transform body, Vector3 shoulder, float side, float length, float thickness, string mat)
        {
            var pivot = Pivot(name, body, shoulder, new Vector3(0f, 0f, side * 55f));
            var swing = Pivot("Swing", pivot, Vector3.zero);
            Toon(PrimitiveType.Capsule, "Upper", swing, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length * 0.5f, thickness), mat);
            var hand = Pivot("Hand", swing, new Vector3(0f, -length, 0f));
            Toon(PrimitiveType.Sphere, "Palm", hand, Vector3.zero, new Vector3(thickness * 1.7f, thickness * 1.3f, thickness * 1.5f), mat);
            for (int f = -1; f <= 1; f++)
                Toon(PrimitiveType.Capsule, "Finger", hand, new Vector3(f * thickness * 0.55f, -thickness * 0.9f, 0f),
                    new Vector3(thickness * 0.5f, thickness * 0.6f, thickness * 0.5f), mat, new Vector3(0f, 0f, f * 25f), 0.15f);
        }

        /// <summary>Stubby leg with a three-toed foot, hinged at the hip.</summary>
        static void Leg(string name, Transform rig, Vector3 hip, float length, float thickness, string mat)
        {
            var pivot = Pivot(name, rig, hip);
            var swing = Pivot("Swing", pivot, Vector3.zero);
            Toon(PrimitiveType.Capsule, "Shin", swing, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length * 0.55f, thickness), mat);
            var foot = Pivot("Foot", swing, new Vector3(0f, -length, thickness * 0.4f));
            Toon(PrimitiveType.Sphere, "Sole", foot, Vector3.zero, new Vector3(thickness * 2f, thickness * 0.9f, thickness * 2.4f), mat);
            for (int f = -1; f <= 1; f++)
                Toon(PrimitiveType.Sphere, "Toe", foot, new Vector3(f * thickness * 0.6f, 0f, thickness * 1.15f),
                    new Vector3(thickness * 0.75f, thickness * 0.6f, thickness * 0.8f), mat, null, 0.15f);
        }

        /// <summary>Body mesh + outline, placed so its bottom sits at world height 'baseY'.</summary>
        static Mesh BodyMesh(Transform body, Lathe shape, string name, string mat, float baseY, float hipY, float outline = 0.03f)
        {
            var mesh = shape.Build(name);
            var pos = new Vector3(0f, baseY - hipY, 0f);
            MeshPart("BodyMesh", body, mesh, mat, pos);
            var o = MeshPart("Outline", body, OutlineMesh(mesh, outline), "Outline", pos);
            o.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return mesh;
        }

        // ------------------------------------------------------------------ Enemies

        static void CreateCartoonMaterials()
        {
            var outline = Mat("Outline", Shader.Find("Universal Render Pipeline/Unlit"));
            outline.SetColor("_BaseColor", UISkinOutline);
            outline.SetFloat("_Cull", (float)CullMode.Front);
            mats["Outline"] = outline;

            Lit("CarrotToon", new Color(1f, 0.46f, 0.08f), 0.35f);
            Lit("EggplantToon", new Color(0.48f, 0.16f, 0.62f), 0.55f);
            Lit("PickleToon", new Color(0.66f, 0.8f, 0.26f), 0.35f);
            Lit("PickleSpot", new Color(0.42f, 0.58f, 0.14f), 0.3f);
            Lit("PumpkinToon", new Color(1f, 0.52f, 0.06f), 0.3f);
            Lit("LeafToon", new Color(0.42f, 0.66f, 0.16f), 0.3f).SetFloat("_Cull", 0f);
            Lit("StemToon", new Color(0.36f, 0.5f, 0.12f), 0.25f);
            Lit("IrisBlue", new Color(0.35f, 0.55f, 0.95f), 0.8f);
            Lit("IrisBrown", new Color(0.55f, 0.3f, 0.12f), 0.8f);
            Lit("IrisAmber", new Color(0.95f, 0.6f, 0.15f), 0.8f);
            Lit("IrisRed", new Color(0.9f, 0.15f, 0.08f), 0.8f, 0f, new Color(1f, 0.2f, 0.05f) * 1.5f);
            Lit("Shine", Color.white, 1f, 0f, Color.white * 1.2f);
            Lit("MouthDark", new Color(0.35f, 0.04f, 0.08f), 0.4f);
            Lit("Tongue", new Color(1f, 0.45f, 0.55f), 0.6f);
        }

        static readonly Color UISkinOutline = new Color(0.12f, 0.06f, 0.03f);

        static EnemySet CreateEnemyPrefabs(EnemyProjectile spit)
        {
            AssetDatabase.DeleteAsset(ModelDir);
            EnsureDir(ModelDir);
            foreach (var old in new[] { "Enemy_Tomato", "Enemy_Chili" }) AssetDatabase.DeleteAsset($"{PrefabDir}/{old}.prefab");
            CreateCartoonMaterials();
            var controller = CreateEnemyAnimator();
            var leaf = LeafMesh("Leaf", 1f, 0.32f, 0.35f);
            var set = new EnemySet();

            // ---------------- Carrot: fast, fragile. Ridged cone with a big leafy top.
            set.carrot = MakeEnemy("Enemy_Carrot", "e_carrot", 0.4f, 1.8f, new Color(1f, 0.5f, 0.1f), controller, 0.45f, 1f, (rig, body) =>
            {
                var shape = new Lathe
                {
                    height = 1.35f,
                    radius = t => 0.46f * Mathf.Pow(t, 0.62f) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(t, 7f))),
                    radialMod = (t, a) => 1f - 0.07f * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(t * Mathf.PI * 2f * 5.5f)), 10f),
                    bend = t => new Vector2(0.12f * (1f - t) * (1f - t), 0f),
                };
                float baseY = 0.22f, hip = 0.45f;
                BodyMesh(body, shape, "Carrot_Body", "CarrotToon", baseY, hip);
                Vector3 S(float t, float ang, out Vector3 n) => shape.Point(t, ang, out n) + new Vector3(0f, baseY - hip, 0f);

                var top = Pivot("Top", body, S(1f, 90f, out _) - new Vector3(0f, 0.03f, 0f));
                for (int i = 0; i < 5; i++)
                    MeshPart("Leaf", top, leaf, "LeafToon", Vector3.zero, new Vector3(-18f + (i % 2) * 12f, i * 72f, 0f), new Vector3(0.75f, 0.7f + (i % 3) * 0.12f, 0.75f));
                for (int s = -1; s <= 1; s += 2)
                    Eye(s < 0 ? "EyeL" : "EyeR", body, S(0.72f, 90f + s * 21f, out var n) + n * 0.04f, Vector3.Lerp(n, Vector3.forward, 0.5f), 0.26f, "IrisBlue");
                Mouth(body, S(0.5f, 90f, out var mn), mn, 0.26f, true);
                Arm("ArmL", body, S(0.55f, 180f, out _), -1f, 0.38f, 0.07f, "CarrotToon");
                Arm("ArmR", body, S(0.55f, 0f, out _), 1f, 0.38f, 0.07f, "CarrotToon");
                Leg("LegL", rig, new Vector3(-0.14f, hip, 0f), 0.38f, 0.1f, "CarrotToon");
                Leg("LegR", rig, new Vector3(0.14f, hip, 0f), 0.38f, 0.1f, "CarrotToon");
            }, e => { e.maxHealth = 30f; e.moveSpeed = 5.6f; e.attackDamage = 7f; e.attackCooldown = 0.8f; e.scoreValue = 10; e.attackRange = 1.6f; });

            // ---------------- Eggplant: bruiser. Glossy purple pear with a green cap.
            set.eggplant = MakeEnemy("Enemy_Eggplant", "e_eggplant", 0.55f, 1.6f, new Color(0.55f, 0.2f, 0.7f), controller, 0.4f, 1f, (rig, body) =>
            {
                var shape = new Lathe
                {
                    height = 1.45f,
                    radius = t => Mathf.Lerp(0.6f, 0.36f, Mathf.SmoothStep(0f, 1f, t)) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(2f * t - 1f, 2f))) * 1.05f,
                    bend = t => new Vector2(-0.1f * t * t, 0f),
                };
                float baseY = 0.3f, hip = 0.4f;
                BodyMesh(body, shape, "Eggplant_Body", "EggplantToon", baseY, hip);
                Vector3 S(float t, float ang, out Vector3 n) => shape.Point(t, ang, out n) + new Vector3(0f, baseY - hip, 0f);

                var top = Pivot("Top", body, S(1f, 90f, out _) - new Vector3(0f, 0.05f, 0f));
                for (int i = 0; i < 6; i++)
                    MeshPart("Sepal", top, leaf, "LeafToon", Vector3.zero, new Vector3(-100f, i * 60f, 0f), new Vector3(0.5f, 0.32f, 0.5f));
                Toon(PrimitiveType.Capsule, "Stem", top, new Vector3(0.03f, 0.14f, 0f), new Vector3(0.09f, 0.14f, 0.09f), "StemToon", new Vector3(0f, 0f, -15f));
                for (int s = -1; s <= 1; s += 2)
                    Eye(s < 0 ? "EyeL" : "EyeR", body, S(0.62f, 90f + s * 22f, out var n) + n * 0.05f, Vector3.Lerp(n, Vector3.forward, 0.5f), 0.32f, "IrisBrown");
                Mouth(body, S(0.36f, 90f, out var mn), mn, 0.42f, true);
                Arm("ArmL", body, S(0.42f, 180f, out _), -1f, 0.34f, 0.08f, "EggplantToon");
                Arm("ArmR", body, S(0.42f, 0f, out _), 1f, 0.34f, 0.08f, "EggplantToon");
                Leg("LegL", rig, new Vector3(-0.2f, hip, 0f), 0.34f, 0.12f, "EggplantToon");
                Leg("LegR", rig, new Vector3(0.2f, hip, 0f), 0.34f, 0.12f, "EggplantToon");
            }, e => { e.maxHealth = 75f; e.moveSpeed = 3.8f; e.attackDamage = 12f; e.scoreValue = 20; e.attackRange = 1.9f; });

            // ---------------- Pickle: ranged spitter. Bumpy lime sausage with spots.
            set.pickle = MakeEnemy("Enemy_Pickle", "e_pickle", 0.45f, 1.9f, new Color(0.55f, 0.85f, 0.2f), controller, 0.4f, 1f, (rig, body) =>
            {
                var shape = new Lathe
                {
                    height = 1.5f,
                    radius = t => 0.42f * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(2f * t - 1f), 2.6f)), 0.45f),
                    radialMod = (t, a) => 1f + 0.025f * Mathf.Sin(a * 7f + t * 20f) * Mathf.Sin(t * 31f),
                };
                float baseY = 0.28f, hip = 0.4f;
                BodyMesh(body, shape, "Pickle_Body", "PickleToon", baseY, hip);
                Vector3 S(float t, float ang, out Vector3 n) => shape.Point(t, ang, out n) + new Vector3(0f, baseY - hip, 0f);

                var rng = new System.Random(3);
                for (int i = 0; i < 26; i++)
                {
                    float t = 0.12f + (float)rng.NextDouble() * 0.8f;
                    float ang = (float)rng.NextDouble() * 360f;
                    if (Mathf.Abs(Mathf.DeltaAngle(ang, 90f)) < 40f && t > 0.4f && t < 0.85f) continue; // keep the face clear
                    var p = S(t, ang, out var n);
                    var spot = Prim(PrimitiveType.Sphere, "Spot", body, p, new Vector3(0.07f, 0.09f, 0.03f) * (0.8f + (float)rng.NextDouble() * 0.6f), "PickleSpot", false);
                    spot.transform.localRotation = Quaternion.LookRotation(n);
                }
                var top = Pivot("Top", body, S(1f, 90f, out _));
                Toon(PrimitiveType.Capsule, "Stem", top, new Vector3(0.04f, 0.08f, 0f), new Vector3(0.07f, 0.1f, 0.07f), "StemToon", new Vector3(0f, 0f, -30f));
                for (int s = -1; s <= 1; s += 2)
                    Eye(s < 0 ? "EyeL" : "EyeR", body, S(0.74f, 90f + s * 24f, out var n) + n * 0.05f, Vector3.Lerp(n, Vector3.forward, 0.5f), 0.27f, "IrisAmber");
                var mouth = Mouth(body, S(0.56f, 90f, out var mn), mn, 0.34f, false, true);
                Arm("ArmL", body, S(0.5f, 180f, out _), -1f, 0.3f, 0.065f, "PickleToon");
                Arm("ArmR", body, S(0.5f, 0f, out _), 1f, 0.3f, 0.065f, "PickleToon");
                Leg("LegL", rig, new Vector3(-0.16f, hip, 0f), 0.34f, 0.1f, "PickleToon");
                Leg("LegR", rig, new Vector3(0.16f, hip, 0f), 0.34f, 0.1f, "PickleToon");
                Pivot("Muzzle", mouth, new Vector3(0f, 0f, 0.15f));
            }, e =>
            {
                e.behaviour = EnemyPlant.Behaviour.Ranged; e.maxHealth = 45f; e.moveSpeed = 3.4f; e.attackDamage = 10f;
                e.attackCooldown = 2.3f; e.scoreValue = 25; e.preferredRange = 13f; e.projectile = spit; e.projectileSpeed = 17f;
                e.mouth = e.transform.Find(PMouth + "/Muzzle");
            });

            // ---------------- Pumpkin: slow tank.
            set.pumpkin = MakeEnemy("Enemy_Pumpkin", "e_pumpkin", 0.95f, 1.6f, new Color(1f, 0.55f, 0.08f), controller, 0.35f, 1f,
                (rig, body) => PumpkinModel(rig, body, leaf, false),
                e => { e.maxHealth = 220f; e.moveSpeed = 2.3f; e.attackDamage = 22f; e.attackCooldown = 1.4f; e.scoreValue = 50; e.attackRange = 2.6f; e.knockbackScale = 0.3f; });

            // ---------------- Pumpkin King: boss (same model, 2.3x, crown, red eyes).
            set.king = MakeEnemy("Boss_PumpkinKing", "e_king", 1.6f, 4f, new Color(1f, 0.5f, 0.05f), controller, 0.35f, 2.3f,
                (rig, body) => PumpkinModel(rig, body, leaf, true),
                e =>
                {
                    e.behaviour = EnemyPlant.Behaviour.Boss; e.maxHealth = 1600f; e.moveSpeed = 2.6f; e.attackDamage = 30f;
                    e.scoreValue = 300; e.attackRange = 3.5f; e.slamRange = 6.5f; e.slamCooldown = 4f;
                    e.knockbackScale = 0.05f; e.riseDuration = 2f; e.attackAnimSpeed = 0.45f;
                });
            set.king.minionPrefab = set.carrot;
            EditorUtility.SetDirty(set.king);
            PrefabUtility.SavePrefabAsset(set.king.gameObject);
            return set;
        }

        static void EnsureDir(string path) => System.IO.Directory.CreateDirectory(path);

        static void PumpkinModel(Transform rig, Transform body, Mesh leaf, bool king)
        {
            var shape = new Lathe
            {
                radius = t => 0.95f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.7f),
                heightAt = t => 1.25f * t - 0.18f * Mathf.Pow(Mathf.Max(0f, t - 0.8f) / 0.2f, 2f),
                radialMod = (t, a) => 0.9f + 0.1f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 4f)), 0.6f),
            };
            float baseY = 0.3f, hip = 0.35f;
            BodyMesh(body, shape, king ? "King_Body" : "Pumpkin_Body", "PumpkinToon", baseY, hip, 0.035f);
            Vector3 S(float t, float ang, out Vector3 n) => shape.Point(t, ang, out n) + new Vector3(0f, baseY - hip, 0f);

            var top = Pivot("Top", body, S(1f, 90f, out _) + new Vector3(0f, 0.02f, 0f));
            Toon(PrimitiveType.Capsule, "Stem", top, new Vector3(0.04f, 0.1f, 0f), new Vector3(0.14f, 0.14f, 0.14f), "StemToon", new Vector3(10f, 0f, -18f));
            for (int i = 0; i < 2; i++)
                MeshPart("Leaf", top, leaf, "LeafToon", Vector3.zero, new Vector3(-70f, i * 160f + 30f, 0f), new Vector3(0.6f, 0.55f, 0.6f));

            for (int s = -1; s <= 1; s += 2)
            {
                var eye = Eye(s < 0 ? "EyeL" : "EyeR", body, S(0.62f, 90f + s * 22f, out var n) + n * 0.06f, Vector3.Lerp(n, Vector3.forward, 0.4f), 0.3f, king ? "IrisRed" : "IrisAmber");
                // Grumpy lid over the top half.
                Toon(PrimitiveType.Sphere, "Lid", eye, new Vector3(0f, 0.16f, 0.05f), new Vector3(0.34f, 0.16f, 0.27f), "PumpkinToon", new Vector3(0f, 0f, s * -18f), 0.1f);
            }
            Mouth(body, S(0.35f, 90f, out var mn), mn, 0.6f, false, true);
            Arm("ArmL", body, S(0.45f, 180f, out _), -1f, 0.34f, 0.1f, "StemToon");
            Arm("ArmR", body, S(0.45f, 0f, out _), 1f, 0.34f, 0.1f, "StemToon");
            Leg("LegL", rig, new Vector3(-0.32f, hip, 0f), 0.3f, 0.15f, "StemToon");
            Leg("LegR", rig, new Vector3(0.32f, hip, 0f), 0.3f, 0.15f, "StemToon");

            if (!king) return;
            var crown = Pivot("Crown", top, new Vector3(0f, 0.08f, 0f));
            Toon(PrimitiveType.Cylinder, "Band", crown, Vector3.zero, new Vector3(0.62f, 0.09f, 0.62f), "Gold", null, 0.08f);
            for (int i = 0; i < 6; i++)
            {
                var spike = Pivot("Spike", crown, Vector3.zero, new Vector3(0f, i * 60f, 0f));
                Toon(PrimitiveType.Cube, "Point", spike, new Vector3(0f, 0.16f, 0.27f), new Vector3(0.1f, 0.22f, 0.03f), "Gold", new Vector3(-8f, 0f, 45f), 0.1f);
                Prim(PrimitiveType.Sphere, "Gem", spike, new Vector3(0f, 0.03f, 0.31f), Vector3.one * 0.06f, "HealthRed", false);
            }
        }

        static EnemyPlant MakeEnemy(string name, string nameKey, float radius, float height, Color splat, AnimatorController controller,
            float hipY, float size, Action<Transform, Transform> build, Action<EnemyPlant> stats)
        {
            var root = new GameObject(name);
            var rig = Pivot("Rig", root.transform, Vector3.zero);
            rig.localScale = Vector3.one * size;
            var hips = Pivot("Hips", rig, new Vector3(0f, hipY, 0f));
            var body = Pivot("Body", hips, Vector3.zero);
            build(rig, body);

            var col = root.AddComponent<CapsuleCollider>();
            col.radius = radius;
            col.height = Mathf.Max(height, radius * 2f);
            col.center = new Vector3(0f, col.height / 2f, 0f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = Mathf.Min(radius, 0.9f);
            agent.height = height;
            agent.acceleration = 24f;
            agent.angularSpeed = 360f;
            agent.autoBraking = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var enemy = root.AddComponent<EnemyPlant>();
            enemy.animator = animator;
            enemy.rig = rig;
            enemy.eyes = new[] { body.Find("EyeL"), body.Find("EyeR") };
            enemy.splatColor = splat;
            enemy.nameKey = nameKey;
            stats(enemy);

            foreach (var r in root.GetComponentsInChildren<Renderer>())
                if (r.name != "Outline" && r.name != "Shine" && r.name != "Shine2") r.shadowCastingMode = ShadowCastingMode.On;
            SetLayerRecursive(root, EnemyLayer);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab").GetComponent<EnemyPlant>();
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ Animations

        class ClipWriter
        {
            public AnimationClip clip;

            void Curve(string path, string prop, float[] times, float[] values)
            {
                var keys = new Keyframe[times.Length];
                for (int i = 0; i < times.Length; i++) keys[i] = new Keyframe(times[i], values[i]);
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), curve);
            }

            void Vec(string path, string prop, (float t, Vector3 v)[] keys)
            {
                var times = new float[keys.Length];
                var xs = new float[keys.Length];
                var ys = new float[keys.Length];
                var zs = new float[keys.Length];
                for (int i = 0; i < keys.Length; i++) { times[i] = keys[i].t; xs[i] = keys[i].v.x; ys[i] = keys[i].v.y; zs[i] = keys[i].v.z; }
                Curve(path, prop + ".x", times, xs);
                Curve(path, prop + ".y", times, ys);
                Curve(path, prop + ".z", times, zs);
            }

            public void Pos(string path, params (float t, Vector3 v)[] keys) => Vec(path, "m_LocalPosition", keys);
            public void Rot(string path, params (float t, Vector3 v)[] keys) => Vec(path, "localEulerAnglesRaw", keys);
            public void Scale(string path, params (float t, Vector3 v)[] keys) => Vec(path, "m_LocalScale", keys);
        }

        static AnimationClip Clip(string name, bool loop, Action<ClipWriter> write)
        {
            var clip = new AnimationClip { name = name, frameRate = 30f };
            write(new ClipWriter { clip = clip });
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, $"{AnimDir}/{name}.anim");
            return clip;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static readonly Vector3 One = Vector3.one;
        static readonly Vector3 Zero = Vector3.zero;

        static AnimatorController CreateEnemyAnimator()
        {
            AssetDatabase.DeleteAsset(AnimDir);
            EnsureDir(AnimDir);

            var idle = Clip("Enemy_Idle", true, c =>
            {
                c.Pos(PB, (0f, Zero), (0.8f, V(0f, 0.03f, 0f)), (1.6f, Zero));
                c.Scale(PB, (0f, One), (0.8f, V(0.985f, 1.03f, 0.985f)), (1.6f, One));
                c.Rot(PB, (0f, V(0f, 0f, -2f)), (0.8f, V(0f, 0f, 2f)), (1.6f, V(0f, 0f, -2f)));
                c.Rot(PArmL, (0f, V(0f, 0f, -6f)), (0.8f, V(0f, 0f, 6f)), (1.6f, V(0f, 0f, -6f)));
                c.Rot(PArmR, (0f, V(0f, 0f, 6f)), (0.8f, V(0f, 0f, -6f)), (1.6f, V(0f, 0f, 6f)));
                c.Rot(PTop, (0f, V(0f, 0f, 5f)), (0.8f, V(0f, 0f, -5f)), (1.6f, V(0f, 0f, 5f)));
                c.Scale(PMouth, (0f, One), (0.8f, V(1f, 0.85f, 1f)), (1.6f, One));
            });

            var walk = Clip("Enemy_Walk", true, c =>
            {
                c.Pos(PB, (0f, Zero), (0.15f, V(0f, 0.08f, 0f)), (0.3f, Zero), (0.45f, V(0f, 0.08f, 0f)), (0.6f, Zero));
                c.Rot(PB, (0f, V(8f, -6f, -5f)), (0.15f, V(8f, 0f, 0f)), (0.3f, V(8f, 6f, 5f)), (0.45f, V(8f, 0f, 0f)), (0.6f, V(8f, -6f, -5f)));
                c.Scale(PB, (0f, V(1.04f, 0.96f, 1.04f)), (0.15f, V(0.98f, 1.03f, 0.98f)), (0.3f, V(1.04f, 0.96f, 1.04f)),
                    (0.45f, V(0.98f, 1.03f, 0.98f)), (0.6f, V(1.04f, 0.96f, 1.04f)));
                c.Rot(PLegL, (0f, V(35f, 0f, 0f)), (0.3f, V(-35f, 0f, 0f)), (0.6f, V(35f, 0f, 0f)));
                c.Rot(PLegR, (0f, V(-35f, 0f, 0f)), (0.3f, V(35f, 0f, 0f)), (0.6f, V(-35f, 0f, 0f)));
                c.Rot(PArmL, (0f, V(-40f, 0f, 0f)), (0.3f, V(40f, 0f, 0f)), (0.6f, V(-40f, 0f, 0f)));
                c.Rot(PArmR, (0f, V(40f, 0f, 0f)), (0.3f, V(-40f, 0f, 0f)), (0.6f, V(40f, 0f, 0f)));
                c.Rot(PTop, (0f, V(-5f, 0f, 9f)), (0.3f, V(-5f, 0f, -9f)), (0.6f, V(-5f, 0f, 9f)));
            });

            var attack = Clip("Enemy_Attack", false, c =>
            {
                c.Rot(PB, (0f, Zero), (0.22f, V(-18f, 0f, 0f)), (0.32f, V(28f, 0f, 0f)), (0.42f, V(18f, 0f, 0f)), (0.6f, Zero));
                c.Pos(PB, (0f, Zero), (0.22f, V(0f, 0.05f, -0.08f)), (0.32f, V(0f, -0.02f, 0.3f)), (0.42f, V(0f, 0f, 0.15f)), (0.6f, Zero));
                c.Scale(PB, (0f, One), (0.22f, V(0.92f, 1.12f, 0.92f)), (0.32f, V(1.15f, 0.86f, 1.15f)), (0.6f, One));
                c.Rot(PArmL, (0f, Zero), (0.22f, V(-110f, 0f, 0f)), (0.32f, V(-20f, 0f, 0f)), (0.42f, V(-40f, 0f, 0f)), (0.6f, Zero));
                c.Rot(PArmR, (0f, Zero), (0.22f, V(-110f, 0f, 0f)), (0.32f, V(-20f, 0f, 0f)), (0.42f, V(-40f, 0f, 0f)), (0.6f, Zero));
                c.Scale(PMouth, (0f, One), (0.22f, V(1.1f, 1.8f, 1f)), (0.32f, V(1.2f, 0.4f, 1f)), (0.6f, One));
                c.Rot(PTop, (0f, Zero), (0.22f, V(-15f, 0f, 0f)), (0.34f, V(25f, 0f, 0f)), (0.6f, Zero));
            });

            var hit = Clip("Enemy_Hit", false, c =>
            {
                c.Rot(PB, (0f, Zero), (0.06f, V(-20f, 0f, 8f)), (0.16f, V(5f, 0f, -3f)), (0.3f, Zero));
                c.Scale(PB, (0f, One), (0.06f, V(1.18f, 0.82f, 1.18f)), (0.16f, V(0.95f, 1.06f, 0.95f)), (0.3f, One));
                c.Scale(PMouth, (0f, One), (0.06f, V(1f, 1.6f, 1f)), (0.3f, One));
                c.Rot(PArmL, (0f, Zero), (0.06f, V(0f, 0f, -40f)), (0.3f, Zero));
                c.Rot(PArmR, (0f, Zero), (0.06f, V(0f, 0f, 40f)), (0.3f, Zero));
            });

            var die = Clip("Enemy_Die", false, c =>
            {
                c.Scale(PB, (0f, One), (0.12f, V(0.85f, 1.25f, 0.85f)), (0.35f, V(1.5f, 0.3f, 1.5f)), (0.4f, V(1.6f, 0.2f, 1.6f)));
                c.Rot(PB, (0f, Zero), (0.2f, V(0f, 30f, 15f)), (0.4f, V(0f, 60f, 25f)));
                c.Rot(PArmL, (0f, Zero), (0.15f, V(0f, 0f, -90f)), (0.4f, V(0f, 0f, -70f)));
                c.Rot(PArmR, (0f, Zero), (0.15f, V(0f, 0f, 90f)), (0.4f, V(0f, 0f, 70f)));
                c.Scale(PMouth, (0f, One), (0.12f, V(1.2f, 2f, 1f)), (0.4f, V(1.2f, 2f, 1f)));
            });

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath($"{AnimDir}/EnemyAnimator.controller");
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("AttackSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            var ps = ctrl.parameters;
            foreach (var p in ps) if (p.name == "MoveSpeed" || p.name == "AttackSpeed") p.defaultFloat = 1f;
            ctrl.parameters = ps;

            var sm = ctrl.layers[0].stateMachine;
            var loco = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1f);
            loco.speedParameterActive = true;
            loco.speedParameter = "MoveSpeed";
            sm.defaultState = loco;

            var atk = sm.AddState("Attack");
            atk.motion = attack;
            atk.speedParameterActive = true;
            atk.speedParameter = "AttackSpeed";
            AnyTo(sm, atk, "Attack", 0.05f);
            Back(atk, loco, 0.12f);

            var hitState = sm.AddState("Hit");
            hitState.motion = hit;
            AnyTo(sm, hitState, "Hit", 0.02f);
            Back(hitState, loco, 0.08f);

            var dieState = sm.AddState("Die");
            dieState.motion = die;
            AnyTo(sm, dieState, "Die", 0.02f);

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void AnyTo(AnimatorStateMachine sm, AnimatorState state, string trigger, float duration)
        {
            var t = sm.AddAnyStateTransition(state);
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            t.hasExitTime = false;
            t.duration = duration;
            t.canTransitionToSelf = false;
        }

        static void Back(AnimatorState from, AnimatorState to, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.95f;
            t.duration = duration;
        }
    }
}

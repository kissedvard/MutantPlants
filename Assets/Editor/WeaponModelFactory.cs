using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MutantPlants.EditorTools
{
    /// <summary>
    /// Cartoon first-person weapon models in the same style as the enemies. Parts are smooth
    /// procedural meshes (rounded boxes with bevelled edges, lathed barrels and tanks, swept
    /// hoses) with inverted-hull outlines, held by work gloves with denim sleeves.
    /// Each model's animated root sits at GunOffset under the weapon holder; +Z is forward.
    /// </summary>
    public static partial class MutantPlantsBuilder
    {
        static readonly Vector3 GunOffset = new Vector3(0.4f, -0.34f, 1.0f);
        const string WeaponMeshDir = ModelDir + "/Weapons";
        const float Line = 0.0055f; // outline width for weapon parts

        static readonly Dictionary<string, int> meshNames = new Dictionary<string, int>();
        static string weaponPrefix = "W";

        static void CreateWeaponMaterials()
        {
            AssetDatabase.DeleteAsset(WeaponMeshDir);
            EnsureDir(WeaponMeshDir);
            meshNames.Clear();

            Lit("GunRed", new Color(0.88f, 0.2f, 0.13f), 0.6f);
            Lit("GunMetal", new Color(0.26f, 0.29f, 0.36f), 0.7f, 0.6f);
            Lit("GunDark", new Color(0.12f, 0.13f, 0.16f), 0.5f, 0.3f);
            Lit("GunWood", new Color(0.8f, 0.48f, 0.2f), 0.4f);
            Lit("GunYellow", new Color(1f, 0.78f, 0.15f), 0.55f);
            Lit("GunGreen", new Color(0.36f, 0.72f, 0.2f), 0.6f);
            Lit("TankBlue", new Color(0.25f, 0.55f, 0.95f), 0.8f);
            Lit("Glove", new Color(0.98f, 0.78f, 0.38f), 0.2f);
            Lit("Denim", new Color(0.24f, 0.4f, 0.72f), 0.15f);
            Lit("DenimLight", new Color(0.4f, 0.56f, 0.85f), 0.15f);
            Lit("RedDot", new Color(1f, 0.15f, 0.1f), 0.9f, 0f, new Color(1f, 0.1f, 0.05f) * 4f);
            Lit("Lens", new Color(0.2f, 0.45f, 0.5f), 0.95f, 0.2f);
            Lit("PetalToon", new Color(1f, 0.8f, 0.12f), 0.35f).SetFloat("_Cull", 0f);
        }

        static string UniqueMeshName(string part)
        {
            string key = $"{weaponPrefix}_{part}";
            meshNames.TryGetValue(key, out int n);
            meshNames[key] = n + 1;
            return n == 0 ? key : $"{key}_{n}";
        }

        static Mesh SaveWeaponMesh(Mesh mesh)
        {
            AssetDatabase.CreateAsset(mesh, $"{WeaponMeshDir}/{mesh.name}.asset");
            return mesh;
        }

        // ------------------------------------------------------------------ Mesh generators

        /// <summary>Axis coordinates for a rounded box: dense steps across the bevels, one flat span in the middle.</summary>
        static List<float> BevelCoords(float half, float inner, int steps)
        {
            var list = new List<float>();
            for (int j = 0; j <= steps; j++) list.Add(-half + (half - inner) * j / steps);
            for (int j = 0; j <= steps; j++)
            {
                float v = inner + (half - inner) * j / steps;
                if (Mathf.Abs(v - list[list.Count - 1]) > 1e-5f) list.Add(v);
            }
            return list;
        }

        /// <summary>
        /// Box with smoothly rounded edges and corners (analytic normals, so the outline is watertight).
        /// 'deform' bends/tapers the final vertices.
        /// </summary>
        static Mesh RoundedBoxMesh(string name, Vector3 size, float radius, Func<Vector3, Vector3> deform = null, int steps = 5)
        {
            var half = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.999f);
            var inner = half - Vector3.one * radius;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            for (int d = 0; d < 3; d++)
                for (int s = -1; s <= 1; s += 2)
                {
                    int a = (d + 1) % 3, b = (d + 2) % 3;
                    var ca = BevelCoords(half[a], inner[a], steps);
                    var cb = BevelCoords(half[b], inner[b], steps);
                    int start = verts.Count;
                    foreach (var vb in cb)
                        foreach (var va in ca)
                        {
                            var p = Vector3.zero;
                            p[d] = s * half[d];
                            p[a] = va;
                            p[b] = vb;
                            var q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            var n = (p - q).normalized;
                            var v = q + n * radius;
                            if (deform != null) v = deform(v);
                            verts.Add(v);
                            normals.Add(n);
                        }
                    int w = ca.Count;
                    var faceNormal = Vector3.zero;
                    faceNormal[d] = s;
                    for (int j = 0; j < cb.Count - 1; j++)
                        for (int i = 0; i < w - 1; i++)
                        {
                            int i0 = start + j * w + i, i1 = i0 + 1, i2 = i0 + w + 1, i3 = i0 + w;
                            var cross = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]);
                            // Same convention as the tube: keep the order whose cross product points outward.
                            if (Vector3.Dot(cross, faceNormal) >= 0f) { tris.Add(i0); tris.Add(i1); tris.Add(i2); tris.Add(i0); tris.Add(i2); tris.Add(i3); }
                            else { tris.Add(i0); tris.Add(i2); tris.Add(i1); tris.Add(i0); tris.Add(i3); tris.Add(i2); }
                        }
                }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return SaveWeaponMesh(mesh);
        }

        /// <summary>Smooth tube swept along a Catmull-Rom curve through the given points.</summary>
        static Mesh TubeMesh(string name, Vector3[] points, float radius, int samples = 24, int sides = 14, float endRadius = -1f)
        {
            Vector3 Curve(float t)
            {
                float f = t * (points.Length - 1);
                int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, points.Length - 2);
                float u = f - i;
                Vector3 p0 = points[Mathf.Max(0, i - 1)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(points.Length - 1, i + 2)];
                return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            Vector3 prevNormal = Vector3.up;
            for (int k = 0; k <= samples; k++)
            {
                float t = k / (float)samples;
                var c = Curve(t);
                var tangent = (Curve(Mathf.Min(1f, t + 0.01f)) - Curve(Mathf.Max(0f, t - 0.01f))).normalized;
                var nrm = Vector3.Cross(tangent, Vector3.Cross(prevNormal, tangent)).normalized;
                if (nrm.sqrMagnitude < 0.5f) nrm = Vector3.Cross(tangent, Vector3.right).normalized;
                prevNormal = nrm;
                var bin = Vector3.Cross(tangent, nrm);
                float r = endRadius > 0f ? Mathf.Lerp(radius, endRadius, t) : radius;
                for (int j = 0; j < sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2f;
                    var dir = Mathf.Cos(a) * nrm + Mathf.Sin(a) * bin;
                    verts.Add(c + dir * r);
                    normals.Add(dir);
                }
            }
            for (int k = 0; k < samples; k++)
                for (int j = 0; j < sides; j++)
                {
                    int a = k * sides + j, b = k * sides + (j + 1) % sides, c2 = (k + 1) * sides + (j + 1) % sides, d = (k + 1) * sides + j;
                    var cross = Vector3.Cross(verts[b] - verts[a], verts[d] - verts[a]);
                    if (Vector3.Dot(cross, normals[a]) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c2); tris.Add(a); tris.Add(c2); tris.Add(d); }
                    else { tris.Add(a); tris.Add(c2); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c2); }
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return SaveWeaponMesh(mesh);
        }

        static Mesh WeaponOutline(Mesh source, float width)
        {
            var verts = source.vertices;
            var normals = source.normals;
            for (int i = 0; i < verts.Length; i++) verts[i] += normals[i] * width;
            var mesh = new Mesh { name = source.name + "_Outline", vertices = verts, triangles = source.triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveWeaponMesh(mesh);
        }

        /// <summary>Mesh part with a smooth outline child.</summary>
        static GameObject ToonMesh(string name, Transform parent, Mesh mesh, string mat, Vector3 pos, Vector3? euler = null, float outline = Line)
        {
            var go = MeshPart(name, parent, mesh, mat, pos, euler);
            if (outline > 0f)
            {
                var o = MeshPart("Outline", go.transform, WeaponOutline(mesh, outline), "Outline", Vector3.zero);
                o.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        /// <summary>Rounded box part.</summary>
        static GameObject RB(string name, Transform parent, Vector3 size, float radius, string mat, Vector3 pos, Vector3? euler = null,
            Func<Vector3, Vector3> deform = null, float outline = Line)
        {
            var mesh = RoundedBoxMesh(UniqueMeshName(name), size, radius, deform);
            return ToonMesh(name, parent, mesh, mat, pos, euler, outline);
        }

        /// <summary>Superellipse profile: a cylinder with nicely rounded ends (p: higher = flatter caps).</summary>
        static Func<float, float> Capped(float r, float p = 8f) =>
            t => r * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(2f * t - 1f), p)), 1f / p);

        /// <summary>Lathe part pointing along +Z from 'start' (or along +Y with euler override).</summary>
        static GameObject LatheZ(string name, Transform parent, float length, Func<float, float> radius, string mat, Vector3 start,
            Vector3? euler = null, Func<float, float, float> radialMod = null, float outline = Line)
        {
            var shape = new Lathe { height = length, radius = radius, radialMod = radialMod };
            var mesh = shape.Build(UniqueMeshName(name), 36, 28);
            return ToonMesh(name, parent, mesh, mat, start, euler ?? new Vector3(90f, 0f, 0f), outline);
        }

        static Mesh petalMesh;

        // ------------------------------------------------------------------ Shared pieces

        static Transform GunRoot(string name, Transform holder, Vector3 extraOffset)
        {
            weaponPrefix = name;
            var root = Empty(name, holder, GunOffset + extraOffset).transform;
            var pose = Empty("Pose", root, Vector3.zero, new Vector3(0f, -5f, 0f)).transform;
            pose.localScale = Vector3.one * 0.8f;
            return pose;
        }

        /// <summary>Chunky work glove wrapped around a grip, with cuff and a rolled denim sleeve going off-screen.</summary>
        /// <summary>Player camera, set by BuildPlayer so the arms can reach off-screen.</summary>
        static Transform viewCamera;
        // Elbow positions in camera space, well below the bottom edge of the screen.
        static readonly Vector3 RightElbow = new Vector3(0.5f, -0.42f, 0.0f);
        static readonly Vector3 LeftElbow = new Vector3(-0.08f, -0.42f, 0.02f);

        /// <summary>
        /// Chunky work glove wrapped around a grip, with a cuff, a rolled denim sleeve and a tapered
        /// forearm that runs to an elbow below the screen, so the arm never ends in mid-air.
        /// </summary>
        static void Glove(Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 unusedSleeveEuler, float size = 1f)
        {
            var hand = Empty(name, parent, pos, euler).transform;
            hand.localScale = Vector3.one * size;
            RB("Palm", hand, new Vector3(0.095f, 0.08f, 0.115f), 0.035f, "Glove", Vector3.zero);
            for (int i = 0; i < 4; i++)
                Toon(PrimitiveType.Capsule, "Finger", hand, new Vector3(0.048f, 0.012f - i * 0.003f, -0.043f + i * 0.029f),
                    new Vector3(0.03f, 0.048f, 0.03f), "Glove", new Vector3(0f, 0f, 90f), 0.14f);
            Toon(PrimitiveType.Capsule, "Thumb", hand, new Vector3(0.02f, 0.052f, 0.04f), new Vector3(0.03f, 0.045f, 0.03f), "Glove", new Vector3(40f, 0f, 60f), 0.14f);

            // Point the forearm at the elbow (computed in world space, then expressed locally).
            var wristPos = new Vector3(-0.01f, -0.035f, -0.045f);
            var wristWorld = hand.TransformPoint(wristPos);
            var elbowWorld = viewCamera != null
                ? viewCamera.TransformPoint(name.StartsWith("Left") ? LeftElbow : RightElbow)
                : wristWorld + Vector3.down;
            var dirWorld = (elbowWorld - wristWorld).normalized;
            var forearm = Empty("Forearm", hand, wristPos).transform;
            forearm.rotation = Quaternion.LookRotation(dirWorld, Vector3.up);
            float length = forearm.InverseTransformPoint(elbowWorld).z;

            LatheZ("Cuff", forearm, 0.05f, Capped(0.058f, 4f), "Glove", new Vector3(0f, 0f, -0.005f), new Vector3(90f, 0f, 0f));
            LatheZ("SleeveRoll", forearm, 0.05f, Capped(0.07f, 4f), "DenimLight", new Vector3(0f, 0f, 0.04f), new Vector3(90f, 0f, 0f));
            var arm = TubeMesh(UniqueMeshName("Forearm"), new[] { new Vector3(0f, 0f, 0.07f), new Vector3(0f, 0f, length * 0.5f), new Vector3(0f, 0f, length + 0.4f) },
                0.065f, 8, 18, 0.1f);
            ToonMesh("Sleeve", forearm, arm, "Denim", Vector3.zero);
        }

        static ReloadAnimator AddReloadAnimator(Transform pose, ReloadAnimator.Style style, Transform part, Transform bolt, Transform extra)
        {
            // Forearms are re-aimed every frame at elbows below the screen.
            var aim = pose.parent.gameObject.AddComponent<ArmAim>();
            aim.viewCamera = viewCamera;
            var forearms = new System.Collections.Generic.List<Transform>();
            var elbows = new System.Collections.Generic.List<Vector3>();
            foreach (var t in pose.GetComponentsInChildren<Transform>(true))
                if (t.name == "Forearm")
                {
                    forearms.Add(t);
                    elbows.Add(t.parent.name.StartsWith("Left") ? LeftElbow : RightElbow);
                }
            aim.forearms = forearms.ToArray();
            aim.elbows = elbows.ToArray();

            var anim = pose.parent.gameObject.AddComponent<ReloadAnimator>();
            anim.style = style;
            anim.pose = pose;
            anim.leftHand = pose.Find("LeftHand");
            anim.part = part;
            anim.bolt = bolt;
            anim.extra = extra;
            return anim;
        }

        static void TriggerGroup(Transform root, float z, float top)
        {
            var guard = TubeMesh(UniqueMeshName("TriggerGuard"), new[]
            {
                new Vector3(0f, top, z - 0.03f), new Vector3(0f, top - 0.05f, z - 0.015f), new Vector3(0f, top - 0.055f, z + 0.035f), new Vector3(0f, top, z + 0.06f),
            }, 0.007f);
            ToonMesh("TriggerGuard", root, guard, "GunMetal", Vector3.zero, null, 0.003f);
            RB("Trigger", root, new Vector3(0.012f, 0.04f, 0.014f), 0.006f, "GunDark", new Vector3(0f, top - 0.022f, z + 0.012f), new Vector3(-18f, 0f, 0f), null, 0.003f);
        }

        // ------------------------------------------------------------------ 1. Auto rifle

        static GameObject AutoRifleModel(Transform holder, out Transform muzzle)
        {
            var root = GunRoot("AutoRifle", holder, Vector3.zero);

            // Body
            RB("Receiver", root, new Vector3(0.1f, 0.12f, 0.34f), 0.028f, "GunRed", new Vector3(0f, 0f, 0.12f));
            RB("UpperCover", root, new Vector3(0.086f, 0.045f, 0.3f), 0.018f, "GunMetal", new Vector3(0f, 0.073f, 0.12f));
            for (int i = 0; i < 7; i++)
                RB("RailRib", root, new Vector3(0.072f, 0.014f, 0.018f), 0.006f, "GunDark", new Vector3(0f, 0.103f, 0.0f + i * 0.04f), null, null, 0.003f);
            Toon(PrimitiveType.Sphere, "ChargingKnob", root, new Vector3(0.056f, 0.07f, 0.03f), Vector3.one * 0.026f, "GunYellow", null, 0.15f);
            RB("EjectionPort", root, new Vector3(0.102f, 0.035f, 0.07f), 0.01f, "GunDark", new Vector3(0f, 0.02f, 0.13f), null, null, 0f);

            // Barrel, front sight, muzzle brake
            LatheZ("Barrel", root, 0.27f, Capped(0.022f, 12f), "GunMetal", new Vector3(0f, 0.015f, 0.28f));
            RB("FrontSight", root, new Vector3(0.018f, 0.06f, 0.02f), 0.007f, "GunDark", new Vector3(0f, 0.055f, 0.5f));
            LatheZ("MuzzleBrake", root, 0.09f, t => Capped(0.034f, 10f)(t) * (1f - 0.18f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 6f)), 6f)),
                "GunYellow", new Vector3(0f, 0.015f, 0.54f));

            // Wooden handguard with vents
            RB("Handguard", root, new Vector3(0.11f, 0.1f, 0.24f), 0.04f, "GunWood", new Vector3(0f, -0.005f, 0.38f));
            for (int i = 0; i < 3; i++)
                RB("Vent", root, new Vector3(0.114f, 0.02f, 0.045f), 0.009f, "GunDark", new Vector3(0f, 0.012f, 0.32f + i * 0.06f), null, null, 0f);

            // Pistol grip, trigger, curved magazine
            RB("Grip", root, new Vector3(0.056f, 0.13f, 0.072f), 0.024f, "GunWood", new Vector3(0f, -0.115f, 0.01f), new Vector3(-18f, 0f, 0f),
                v => new Vector3(v.x * (1f + v.y * 1.2f), v.y, v.z));
            TriggerGroup(root, 0.07f, -0.06f);
            var magGroup = Empty("MagGroup", root, Vector3.zero).transform;
            RB("Magazine", magGroup, new Vector3(0.055f, 0.18f, 0.078f), 0.018f, "GunYellow", new Vector3(0f, -0.14f, 0.19f), new Vector3(8f, 0f, 0f),
                v => new Vector3(v.x, v.y, v.z + 2.2f * v.y * v.y));
            RB("MagPlate", magGroup, new Vector3(0.062f, 0.02f, 0.088f), 0.008f, "GunDark", new Vector3(0f, -0.225f, 0.245f), new Vector3(30f, 0f, 0f));

            // Stock: wider towards the shoulder, rubber pad
            RB("Stock", root, new Vector3(0.075f, 0.12f, 0.22f), 0.032f, "GunWood", new Vector3(0f, -0.03f, -0.13f), new Vector3(-6f, 0f, 0f),
                v => new Vector3(v.x, v.y * (1f - v.z * 1.6f), v.z));
            RB("ButtPad", root, new Vector3(0.082f, 0.17f, 0.03f), 0.013f, "GunDark", new Vector3(0f, -0.045f, -0.25f), new Vector3(-6f, 0f, 0f));

            Glove(root, "RightHand", new Vector3(-0.005f, -0.12f, 0.01f), new Vector3(-18f, 0f, 0f), new Vector3(-70f, 0f, -10f));
            Glove(root, "LeftHand", new Vector3(-0.02f, -0.065f, 0.38f), new Vector3(0f, 0f, 10f), new Vector3(-40f, 0f, 45f));
            muzzle = Empty("Muzzle", root, new Vector3(0f, 0.015f, 0.64f)).transform;
            AddReloadAnimator(root, ReloadAnimator.Style.Magazine, magGroup, root.Find("ChargingKnob"), null);
            return root.parent.gameObject;
        }

        // ------------------------------------------------------------------ 2. Shotgun

        static GameObject ShotgunModel(Transform holder, out Transform muzzle)
        {
            var root = GunRoot("Shotgun", holder, Vector3.zero);

            RB("Stock", root, new Vector3(0.082f, 0.13f, 0.27f), 0.036f, "GunWood", new Vector3(0f, -0.055f, -0.1f), new Vector3(-8f, 0f, 0f),
                v => new Vector3(v.x, v.y * (1f - v.z * 1.4f), v.z));
            RB("Neck", root, new Vector3(0.066f, 0.09f, 0.11f), 0.03f, "GunWood", new Vector3(0f, -0.025f, 0.04f));
            RB("Receiver", root, new Vector3(0.11f, 0.11f, 0.16f), 0.032f, "GunMetal", new Vector3(0f, 0.012f, 0.13f));
            RB("SidePlate", root, new Vector3(0.116f, 0.07f, 0.1f), 0.022f, "Gold", new Vector3(0f, 0.005f, 0.13f), null, null, 0f);
            Toon(PrimitiveType.Sphere, "Hinge", root, new Vector3(0f, 0.005f, 0.2f), new Vector3(0.12f, 0.05f, 0.05f), "GunMetal", null, 0.1f);

            for (int s = -1; s <= 1; s += 2)
            {
                LatheZ("Barrel", root, 0.43f, t => Capped(0.03f, 14f)(t) * (1f + 0.14f * Mathf.SmoothStep(0.88f, 0.97f, t)), "GunMetal",
                    new Vector3(s * 0.031f, 0.035f, 0.18f));
                LatheZ("RingFront", root, 0.022f, Capped(0.037f, 4f), "Gold", new Vector3(s * 0.031f, 0.035f, 0.56f));
                LatheZ("RingBack", root, 0.022f, Capped(0.035f, 4f), "Gold", new Vector3(s * 0.031f, 0.035f, 0.29f));
            }
            RB("TopRib", root, new Vector3(0.02f, 0.016f, 0.4f), 0.006f, "GunDark", new Vector3(0f, 0.07f, 0.4f), null, null, 0.003f);
            Toon(PrimitiveType.Sphere, "Bead", root, new Vector3(0f, 0.083f, 0.59f), Vector3.one * 0.02f, "Gold", null, 0.15f);

            var pumpGroup = Empty("PumpGroup", root, Vector3.zero).transform;
            RB("Pump", pumpGroup, new Vector3(0.1f, 0.08f, 0.19f), 0.035f, "GunWood", new Vector3(0f, -0.022f, 0.38f));
            for (int i = 0; i < 5; i++)
                RB("PumpGroove", pumpGroup, new Vector3(0.104f, 0.07f, 0.012f), 0.005f, "GunDark", new Vector3(0f, -0.022f, 0.32f + i * 0.03f), null, null, 0f);
            TriggerGroup(root, 0.06f, -0.05f);

            Glove(root, "RightHand", new Vector3(-0.005f, -0.08f, 0.02f), new Vector3(-22f, 0f, 0f), new Vector3(-70f, 0f, -10f));
            Glove(root, "LeftHand", new Vector3(-0.02f, -0.07f, 0.38f), new Vector3(0f, 0f, 10f), new Vector3(-40f, 0f, 45f));
            muzzle = Empty("Muzzle", root, new Vector3(0f, 0.035f, 0.63f)).transform;
            // Red shotgun shell with a brass base, shown while loading.
            var shell = Empty("ShellVisual", root, new Vector3(0f, -0.3f, 0.14f)).transform;
            LatheZ("ShellBody", shell, 0.07f, Capped(0.017f, 10f), "GunRed", new Vector3(0f, 0f, -0.025f));
            LatheZ("ShellBase", shell, 0.022f, Capped(0.018f, 6f), "Gold", new Vector3(0f, 0f, -0.035f));
            shell.gameObject.SetActive(false);
            AddReloadAnimator(root, ReloadAnimator.Style.ShellByShell, pumpGroup, null, shell);
            root.parent.gameObject.SetActive(false);
            return root.parent.gameObject;
        }

        // ------------------------------------------------------------------ 3. Weed sprayer

        static GameObject SprayerModel(Transform holder, out Transform muzzle)
        {
            var root = GunRoot("WeedSprayer", holder, new Vector3(0f, 0.01f, 0f));

            // Pressure tank with yellow bands and a glowing window
            LatheZ("Tank", root, 0.3f, Capped(0.1f, 3f), "TankBlue", new Vector3(0f, -0.02f, -0.13f));
            LatheZ("BandFront", root, 0.02f, Capped(0.103f, 4f), "GunYellow", new Vector3(0f, -0.02f, 0.08f));
            LatheZ("BandBack", root, 0.02f, Capped(0.103f, 4f), "GunYellow", new Vector3(0f, -0.02f, -0.07f));
            var liquid = Empty("Liquid", root, new Vector3(0.088f, -0.0575f, 0.005f)).transform;
            RB("Window", liquid, new Vector3(0.03f, 0.075f, 0.12f), 0.014f, "Ooze", new Vector3(0f, 0.0375f, 0f));
            for (int i = 0; i < 3; i++)
                RB("Mark", root, new Vector3(0.032f, 0.006f, 0.04f), 0.002f, "GunDark", new Vector3(0.09f, -0.04f + i * 0.02f, 0.005f), null, null, 0f);

            // Pressure gauge
            LatheZ("Gauge", root, 0.018f, Capped(0.03f, 4f), "GunMetal", new Vector3(0.045f, 0.075f, -0.07f), new Vector3(0f, 0f, -30f));
            LatheZ("GaugeFace", root, 0.004f, Capped(0.024f, 2f), "EyeWhite", new Vector3(0.054f, 0.091f, -0.07f), new Vector3(0f, 0f, -30f), null, 0f);
            RB("Needle", root, new Vector3(0.004f, 0.002f, 0.018f), 0.001f, "HealthRed", new Vector3(0.056f, 0.095f, -0.065f), new Vector3(0f, 35f, -30f), null, 0f);

            // Pump handle
            var tankPump = Empty("PumpGroup", root, Vector3.zero).transform;
            LatheZ("PumpRod", tankPump, 0.07f, Capped(0.012f, 8f), "GunMetal", new Vector3(-0.06f, 0.055f, 0.06f), new Vector3(0f, 0f, 30f));
            LatheZ("PumpHandle", tankPump, 0.12f, Capped(0.022f, 3f), "GunRed", new Vector3(-0.16f, 0.12f, 0.06f), new Vector3(0f, 0f, -60f));

            // Wand + flared nozzle
            LatheZ("Wand", root, 0.26f, Capped(0.017f, 12f), "GunMetal", new Vector3(0f, 0.03f, 0.13f));
            LatheZ("Nozzle", root, 0.075f, t => Mathf.Lerp(0.02f, 0.04f, t) * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(2f * t - 1f), 10f)), 0.1f),
                "GunYellow", new Vector3(0f, 0.03f, 0.37f));
            LatheZ("NozzleTip", root, 0.006f, Capped(0.028f, 2f), "Ooze", new Vector3(0f, 0.03f, 0.443f), null, null, 0f);

            // Smooth coiled hose from tank to wand
            var hose = TubeMesh(UniqueMeshName("Hose"), new[]
            {
                new Vector3(-0.05f, -0.1f, -0.06f), new Vector3(-0.13f, -0.12f, 0.02f), new Vector3(-0.14f, -0.05f, 0.1f),
                new Vector3(-0.09f, 0.01f, 0.15f), new Vector3(-0.02f, 0.03f, 0.16f),
            }, 0.016f);
            ToonMesh("Hose", root, hose, "GunGreen", Vector3.zero);

            RB("Grip", root, new Vector3(0.05f, 0.12f, 0.062f), 0.022f, "GunRed", new Vector3(0f, -0.155f, 0.02f), new Vector3(-12f, 0f, 0f));
            RB("Lever", root, new Vector3(0.014f, 0.06f, 0.016f), 0.006f, "GunYellow", new Vector3(0f, -0.13f, 0.065f), new Vector3(-20f, 0f, 0f), null, 0.003f);

            Glove(root, "RightHand", new Vector3(-0.005f, -0.16f, 0.01f), new Vector3(-12f, 0f, 0f), new Vector3(-70f, 0f, -10f));
            Glove(root, "LeftHand", new Vector3(-0.02f, 0.0f, 0.28f), new Vector3(0f, 0f, 10f), new Vector3(-40f, 0f, 45f), 0.9f);
            muzzle = Empty("Muzzle", root, new Vector3(0f, 0.03f, 0.46f)).transform;
            AddReloadAnimator(root, ReloadAnimator.Style.Pump, tankPump, root.Find("Needle"), liquid);
            root.parent.gameObject.SetActive(false);
            return root.parent.gameObject;
        }

        // ------------------------------------------------------------------ 4. Seed launcher

        static GameObject LauncherModel(Transform holder, out Transform muzzle)
        {
            var root = GunRoot("SeedLauncher", holder, new Vector3(0.03f, -0.02f, 0.04f));
            root.localScale = Vector3.one * 0.7f;
            if (petalMesh == null || AssetDatabase.GetAssetPath(petalMesh) == "")
                petalMesh = LeafMesh("SeedLauncher_Petal", 0.13f, 0.07f, 0.25f);

            // Main tube with raised rings
            LatheZ("Tube", root, 0.62f, t => Capped(0.075f, 14f)(t) * (1f + 0.07f * Mathf.Exp(-Mathf.Pow((t - 0.2f) * 30f, 2f))
                                                                         + 0.07f * Mathf.Exp(-Mathf.Pow((t - 0.62f) * 30f, 2f))),
                "GunGreen", new Vector3(0f, 0.03f, -0.06f));
            LatheZ("MuzzleRing", root, 0.04f, Capped(0.09f, 4f), "GunYellow", new Vector3(0f, 0.03f, 0.53f));
            LatheZ("Bore", root, 0.006f, Capped(0.058f, 2f), "GunDark", new Vector3(0f, 0.03f, 0.568f), null, null, 0f);
            LatheZ("BackCap", root, 0.035f, Capped(0.082f, 4f), "GunYellow", new Vector3(0f, 0.03f, -0.075f));

            // Sunflower petals around the muzzle
            for (int i = 0; i < 12; i++)
            {
                var pivot = Empty("PetalPivot", root, new Vector3(0f, 0.03f, 0.55f), new Vector3(0f, 0f, i * 30f)).transform;
                var petal = MeshPart("Petal", pivot, petalMesh, "PetalToon", new Vector3(0f, 0.075f, 0f), new Vector3(-25f, 0f, 0f));
                petal.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // Seed drum with glowing seeds
            var drum = Empty("DrumGroup", root, new Vector3(0f, -0.08f, 0.1f)).transform;
            LatheZ("Drum", drum, 0.11f, Capped(0.09f, 5f), "GunWood", new Vector3(-0.055f, 0f, 0f), new Vector3(0f, 0f, -90f));
            var seeds = new Transform[6];
            for (int i = 0; i < seeds.Length; i++)
            {
                float a = i * Mathf.PI * 2f / seeds.Length;
                seeds[i] = Toon(PrimitiveType.Sphere, "Seed", drum, new Vector3(0.06f, Mathf.Sin(a) * 0.055f, Mathf.Cos(a) * 0.055f),
                    new Vector3(0.022f, 0.03f, 0.03f), "SeedGlow", null, 0.12f).transform;
            }
            RB("Sight", root, new Vector3(0.03f, 0.05f, 0.04f), 0.01f, "GunYellow", new Vector3(0f, 0.115f, 0.42f));

            RB("Grip", root, new Vector3(0.06f, 0.14f, 0.074f), 0.026f, "GunWood", new Vector3(0f, -0.15f, -0.01f), new Vector3(-15f, 0f, 0f));
            RB("ForeGrip", root, new Vector3(0.05f, 0.1f, 0.06f), 0.022f, "GunWood", new Vector3(0f, -0.08f, 0.33f));
            TriggerGroup(root, 0.05f, -0.07f);

            Glove(root, "RightHand", new Vector3(-0.005f, -0.15f, -0.01f), new Vector3(-15f, 0f, 0f), new Vector3(-70f, 0f, -10f));
            Glove(root, "LeftHand", new Vector3(-0.005f, -0.1f, 0.33f), new Vector3(0f, 0f, 0f), new Vector3(-40f, 0f, 45f));
            muzzle = Empty("Muzzle", root, new Vector3(0f, 0.03f, 0.6f)).transform;
            AddReloadAnimator(root, ReloadAnimator.Style.Drum, drum, null, null).items = seeds;
            root.parent.gameObject.SetActive(false);
            return root.parent.gameObject;
        }
    }
}

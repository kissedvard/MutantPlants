using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MutantPlants.EditorTools
{
    /// <summary>
    /// Generates the whole game content from code: URP + post-processing setup, procedural
    /// textures, materials, particle effects, enemy/pickup/projectile prefabs, the MainMenu
    /// and Farm scenes and the build settings. Menu: Mutant Plants > Rebuild Scenes & Prefabs.
    /// Re-running it overwrites the generated assets, so once you start hand-editing the
    /// scenes/prefabs, change this generator instead or stop using it.
    /// </summary>
    public static partial class MutantPlantsBuilder
    {
        const string MatDir = "Assets/Materials";
        const string TexDir = "Assets/Textures";
        const string PrefabDir = "Assets/Prefabs";
        const string FxDir = "Assets/Resources/FX";
        const string SettingsDir = "Assets/Settings";
        const string SceneDir = "Assets/Scenes";
        const string MenuScenePath = SceneDir + "/MainMenu.unity";
        const string FarmScenePath = SceneDir + "/Farm.unity";
        const int IgnoreRaycastLayer = 2;
        const int EnemyLayer = 8;

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static VolumeProfile volumeProfile;
        static Material skybox;

        // Arena layout
        const float FenceHalf = 30f;
        static readonly Vector3 BarnCenter = new Vector3(19f, 0f, 19f);
        static readonly Vector3 BarnSize = new Vector3(10f, 5f, 12f);
        static readonly Vector3 FarmhousePos = new Vector3(-12f, 0f, 48f);
        static readonly Vector3 SiloPos = new Vector3(38f, 0f, 22f);
        static readonly Vector3 WindmillPos = new Vector3(-44f, 0f, 30f);

        [MenuItem("Mutant Plants/Rebuild Scenes && Prefabs", priority = 0)]
        public static void BuildAll()
        {
            foreach (var d in new[] { MatDir, TexDir, PrefabDir, FxDir, SettingsDir, SceneDir }) Directory.CreateDirectory(d);

            EnsureLayer(EnemyLayer, "Enemy");
            SetupRenderPipeline();
            CreateTextures();
            CreateMaterials();
            CreateParticleEffects();
            var projectiles = CreateProjectiles();
            var enemies = CreateEnemyPrefabs(projectiles.spit);
            var pickups = CreatePickupPrefabs();
            BuildFarmScene(enemies, pickups, projectiles.seed);
            BuildMenuScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(FarmScenePath, true),
            };
            PlayerSettings.companyName = "Kiss Edvard";
            PlayerSettings.productName = "Mutant Plants";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!Application.isBatchMode) EditorSceneManager.OpenScene(MenuScenePath);
            Debug.Log("[Mutant Plants] Scenes and prefabs generated.");
        }

        [MenuItem("Mutant Plants/Build Windows (x64)", priority = 20)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/MutantPlants.exe");

        [MenuItem("Mutant Plants/Build macOS", priority = 21)]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/MutantPlants.app");

        static void Build(BuildTarget target, string path)
        {
            if (!File.Exists(FarmScenePath)) BuildAll();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, FarmScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            Debug.Log($"[Mutant Plants] Build {target}: {report.summary.result} -> {path}");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        static void EnsureLayer(int index, string name)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(index);
            if (layer.stringValue == name) return;
            layer.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Render pipeline

        static void SetupRenderPipeline()
        {
            string urpPath = SettingsDir + "/MutantPlants_URP.asset";
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
            if (urp == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, SettingsDir + "/MutantPlants_Renderer.asset");
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, urpPath);
            }
            urp.supportsHDR = true;
            urp.msaaSampleCount = 4;
            urp.shadowDistance = 90f;
            urp.shadowCascadeCount = 2;
            var so = new SerializedObject(urp);
            SetProp(so, "m_SoftShadowsSupported", true);
            SetProp(so, "m_MainLightShadowmapResolution", 2048);
            SetProp(so, "m_SupportsCameraDepthTexture", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null; // use the default above
            }
            QualitySettings.SetQualityLevel(current, false);

            // Post-processing profile
            string profilePath = SettingsDir + "/MutantPlants_PostFX.asset";
            AssetDatabase.DeleteAsset(profilePath);
            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(volumeProfile, profilePath);

            var tonemap = AddVolume<Tonemapping>();
            tonemap.mode.value = TonemappingMode.ACES;
            var bloom = AddVolume<Bloom>();
            bloom.threshold.value = 1f;
            bloom.intensity.value = 0.8f;
            bloom.scatter.value = 0.65f;
            var color = AddVolume<ColorAdjustments>();
            color.postExposure.value = 0.35f;
            color.contrast.value = 14f;
            color.saturation.value = 16f;
            color.colorFilter.value = Color.white;
            color.hueShift.value = 0f;
            var white = AddVolume<WhiteBalance>();
            white.temperature.value = 8f;
            white.tint.value = 0f;
            var vignette = AddVolume<Vignette>();
            vignette.intensity.value = 0.28f;
            vignette.smoothness.value = 0.45f;
            vignette.color.value = Color.black;
            vignette.center.value = new Vector2(0.5f, 0.5f);
            vignette.rounded.value = false;
            var chroma = AddVolume<ChromaticAberration>();
            chroma.intensity.value = 0f;
            EditorUtility.SetDirty(volumeProfile);
        }

        static T AddVolume<T>() where T : VolumeComponent
        {
            var c = volumeProfile.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, volumeProfile);
            return c;
        }

        static void SetProp(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) return;
            if (value is bool b) p.boolValue = b;
            else if (value is int i) p.intValue = i;
        }

        // ------------------------------------------------------------------ Procedural textures

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        /// <summary>Tileable fractal noise in [0,1].</summary>
        static float TNoise(float x, float y, float period, int octaves = 4, float seed = 0f)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            float freq = 1f;
            for (int o = 0; o < octaves; o++)
            {
                float p = period * freq;
                float u = x * freq, v = y * freq;
                float a = Mathf.PerlinNoise(u + seed, v + seed);
                float b = Mathf.PerlinNoise(u - p + seed, v + seed);
                float c = Mathf.PerlinNoise(u + seed, v - p + seed);
                float d = Mathf.PerlinNoise(u - p + seed, v - p + seed);
                float fx = (x * freq) / p - Mathf.Floor((x * freq) / p), fy = (y * freq) / p - Mathf.Floor((y * freq) / p);
                float n = Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
                sum += n * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        static float Hash(int x, int y, int s = 0)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        static Texture2D Tex(string name, int size, System.Func<int, int, int, Color> pixel, bool alpha = false)
        {
            string path = $"{TexDir}/{name}.png";
            var tex = new Texture2D(size, size, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = pixel(x, y, size);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaIsTransparency = alpha;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            textures[name] = asset;
            return asset;
        }

        static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        static void CreateTextures()
        {
            Tex("Grass", 256, (x, y, s) =>
            {
                float n = TNoise(x / 32f, y / 32f, s / 32f, 4);
                float blades = Hash(x, y) * 0.25f;
                var c = Color.Lerp(new Color(0.42f, 0.62f, 0.22f), new Color(0.6f, 0.72f, 0.3f), n);
                return Shade(c, 0.85f + blades);
            });
            Tex("Soil", 256, (x, y, s) =>
            {
                float n = TNoise(x / 24f, y / 24f, s / 24f, 5, 3f);
                float pebble = Hash(x / 3, y / 3, 7) > 0.97f ? 0.25f : 0f;
                var c = Color.Lerp(new Color(0.42f, 0.29f, 0.18f), new Color(0.56f, 0.4f, 0.26f), n);
                return Color.Lerp(Shade(c, 0.9f + Hash(x, y) * 0.15f), new Color(0.6f, 0.57f, 0.52f), pebble);
            });
            Tex("Furrows", 256, (x, y, s) =>
            {
                float n = TNoise(x / 16f, y / 16f, s / 16f, 4, 9f);
                float rows = Mathf.Abs(Mathf.Sin(y / (float)s * Mathf.PI * 4f));
                var c = Color.Lerp(new Color(0.25f, 0.16f, 0.1f), new Color(0.4f, 0.27f, 0.16f), rows * 0.7f + n * 0.3f);
                return Shade(c, 0.9f + Hash(x, y) * 0.15f);
            });
            Tex("BarnBoards", 256, (x, y, s) =>
            {
                int board = x / 32;
                float gap = (x % 32) < 2 ? 0.45f : 1f;
                float grain = TNoise(x / 6f + board * 13f, y / 48f, s / 6f, 3, board);
                var c = Color.Lerp(new Color(0.55f, 0.1f, 0.08f), new Color(0.72f, 0.18f, 0.12f), grain);
                return Shade(c, gap * (0.9f + Hash(board, 0) * 0.15f));
            });
            Tex("Wood", 256, (x, y, s) =>
            {
                int board = y / 64;
                float gap = (y % 64) < 2 ? 0.5f : 1f;
                float grain = TNoise(x / 64f, y / 4f + board * 7f, s / 64f, 3, board);
                var c = Color.Lerp(new Color(0.42f, 0.28f, 0.16f), new Color(0.62f, 0.44f, 0.27f), grain);
                return Shade(c, gap);
            });
            Tex("Paint", 128, (x, y, s) =>
            {
                float n = TNoise(x / 16f, y / 16f, s / 16f, 3, 5f);
                return Shade(new Color(0.93f, 0.92f, 0.88f), 0.9f + n * 0.12f);
            });
            Tex("Shingles", 256, (x, y, s) =>
            {
                int row = y / 32;
                int col = (x + (row % 2) * 16) / 32;
                float edge = (y % 32) < 3 ? 0.55f : 1f;
                float v = 0.8f + Hash(col, row, 3) * 0.3f;
                return Shade(new Color(0.3f, 0.29f, 0.32f), edge * v * (0.95f + Hash(x, y) * 0.08f));
            });
            Tex("Hay", 128, (x, y, s) =>
            {
                float streak = TNoise(x / 2f, y / 40f, s / 2f, 3, 2f);
                return Color.Lerp(new Color(0.75f, 0.6f, 0.25f), new Color(0.98f, 0.86f, 0.45f), streak);
            });
            Tex("Bark", 128, (x, y, s) =>
            {
                float n = TNoise(x / 4f, y / 32f, s / 4f, 4, 4f);
                return Color.Lerp(new Color(0.22f, 0.14f, 0.08f), new Color(0.42f, 0.3f, 0.18f), n);
            });
            Tex("Leaves", 128, (x, y, s) =>
            {
                float n = TNoise(x / 8f, y / 8f, s / 8f, 4, 6f);
                float spot = Hash(x / 4, y / 4, 2) * 0.2f;
                return Shade(Color.Lerp(new Color(0.15f, 0.35f, 0.1f), new Color(0.3f, 0.55f, 0.18f), n), 0.85f + spot);
            });
            Tex("Stone", 128, (x, y, s) =>
            {
                float n = TNoise(x / 12f, y / 12f, s / 12f, 4, 8f);
                int row = y / 32;
                bool mortar = (y % 32) < 3 || ((x + row * 21) % 48) < 3;
                return mortar ? new Color(0.5f, 0.48f, 0.45f) : Shade(new Color(0.55f, 0.55f, 0.58f), 0.75f + n * 0.4f);
            });
            Tex("Metal", 128, (x, y, s) =>
            {
                float n = TNoise(x / 64f, y / 2f, s / 64f, 2, 1f);
                return Shade(new Color(0.7f, 0.72f, 0.75f), 0.85f + n * 0.2f);
            });
            Tex("PumpkinSkin", 256, (x, y, s) =>
            {
                float rib = Mathf.Pow(Mathf.Abs(Mathf.Sin(x / (float)s * Mathf.PI * 8f)), 0.4f);
                float n = TNoise(x / 16f, y / 16f, s / 16f, 3, 12f);
                var c = Color.Lerp(new Color(0.75f, 0.32f, 0.02f), new Color(1f, 0.55f, 0.08f), rib * 0.8f + n * 0.2f);
                return c;
            });
            Tex("CarrotSkin", 128, (x, y, s) =>
            {
                float ring = Mathf.Abs(Mathf.Sin(y / (float)s * Mathf.PI * 10f + TNoise(x / 16f, 0f, s / 16f, 2) * 2f));
                float lines = ring > 0.97f ? 0.75f : 1f;
                return Shade(new Color(1f, 0.5f, 0.1f), lines * (0.92f + Hash(x, y) * 0.1f));
            });
            Tex("Crate", 128, (x, y, s) =>
            {
                bool frame = x < 12 || y < 12 || x > s - 12 || y > s - 12 || Mathf.Abs(x - y) < 7;
                float grain = TNoise(x / 32f, y / 3f, s / 32f, 3, 3f);
                var c = Color.Lerp(new Color(0.5f, 0.34f, 0.18f), new Color(0.7f, 0.5f, 0.3f), grain);
                return Shade(c, frame ? 0.75f : 1f);
            });
            Tex("SoftCircle", 64, (x, y, s) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                float a = Mathf.Clamp01(1f - d);
                return new Color(1f, 1f, 1f, a * a);
            }, true);
        }

        // ------------------------------------------------------------------ Materials

        static void CreateMaterials()
        {
            mats.Clear();
            Lit("Grass", Color.white, 0.1f, tex: "Grass", tiling: new Vector2(40f, 40f));
            Lit("Soil", Color.white, 0.08f, tex: "Soil", tiling: new Vector2(12f, 12f));
            Lit("Furrows", Color.white, 0.05f, tex: "Furrows", tiling: new Vector2(4f, 1f));
            Lit("Wood", Color.white, 0.15f, tex: "Wood");
            Lit("DarkWood", new Color(0.55f, 0.5f, 0.48f), 0.15f, tex: "Wood");
            Lit("BarnRed", Color.white, 0.12f, tex: "BarnBoards", tiling: new Vector2(3f, 1.2f));
            Lit("Roof", Color.white, 0.2f, tex: "Shingles", tiling: new Vector2(3f, 4f));
            Lit("White", Color.white, 0.2f, tex: "Paint");
            Lit("Hay", Color.white, 0.05f, tex: "Hay");
            Lit("Bark", Color.white, 0.05f, tex: "Bark");
            Lit("Leaf", new Color(0.35f, 0.75f, 0.22f), 0.35f);
            Lit("TreeLeaf", Color.white, 0.1f, tex: "Leaves", tiling: new Vector2(2f, 2f));
            Lit("Stone", Color.white, 0.15f, tex: "Stone", tiling: new Vector2(2f, 1f));
            Lit("Metal", new Color(0.32f, 0.33f, 0.36f), 0.65f, 0.85f);
            Lit("Silo", Color.white, 0.5f, 0.6f, tex: "Metal", tiling: new Vector2(4f, 1f));
            Lit("Tractor", new Color(0.15f, 0.5f, 0.2f), 0.55f);
            Lit("Black", new Color(0.04f, 0.04f, 0.04f), 0.6f);
            Lit("Carrot", Color.white, 0.3f, tex: "CarrotSkin");
            Lit("Tomato", new Color(0.88f, 0.08f, 0.06f), 0.75f);
            Lit("Chili", new Color(0.75f, 0.05f, 0.04f), 0.8f);
            Lit("Pumpkin", Color.white, 0.25f, tex: "PumpkinSkin");
            Lit("EyeWhite", new Color(1f, 1f, 0.92f), 0.7f);
            Lit("Teeth", new Color(0.95f, 0.93f, 0.8f), 0.5f);
            Lit("Gold", new Color(1f, 0.78f, 0.2f), 0.85f, 1f);
            Lit("Shirt", new Color(0.3f, 0.35f, 0.6f), 0.1f);
            Lit("Sunflower", new Color(1f, 0.82f, 0.1f), 0.2f);
            Lit("Crate", Color.white, 0.15f, tex: "Crate");
            Lit("Cloud", new Color(1f, 1f, 1f), 0f, 0f, new Color(0.35f, 0.37f, 0.4f));
            Lit("Hill", new Color(0.35f, 0.5f, 0.32f), 0f);
            Lit("Window", new Color(1f, 0.85f, 0.4f), 0.9f, 0f, new Color(1f, 0.75f, 0.3f) * 1.8f);
            Lit("Glow", new Color(1f, 0.85f, 0.2f), 0.5f, 0f, new Color(1f, 0.55f, 0.05f) * 2.2f);
            Lit("Ooze", new Color(0.5f, 1f, 0.2f), 0.9f, 0f, new Color(0.4f, 1f, 0.1f) * 2.5f);
            Lit("HealthRed", new Color(0.9f, 0.1f, 0.1f), 0.5f, 0f, new Color(1f, 0.05f, 0.05f) * 2f);
            Lit("PowerOrange", new Color(1f, 0.35f, 0.05f), 0.5f, 0f, new Color(1f, 0.25f, 0f) * 3f);
            Lit("PowerBlue", new Color(0.15f, 0.4f, 1f), 0.5f, 0f, new Color(0.1f, 0.35f, 1f) * 2.5f);
            Lit("Sprayer", new Color(0.2f, 0.55f, 0.85f), 0.65f);
            Lit("SeedGlow", new Color(0.45f, 0.3f, 0.1f), 0.4f, 0f, new Color(0.5f, 1f, 0.1f) * 2f);
            Lit("SpitGlow", new Color(0.55f, 1f, 0.15f), 0.6f, 0f, new Color(0.4f, 1f, 0.05f) * 3f);

            var grass = Lit("GrassBlades", new Color(0.45f, 0.68f, 0.25f), 0.1f);
            grass.SetFloat("_Cull", 0f);
            grass.doubleSidedGI = true;

            var tracer = Mat("Tracer", Shader.Find("Sprites/Default"));
            mats["Tracer"] = tracer;

            var particle = Mat("Particle", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            particle.SetTexture("_BaseMap", textures["SoftCircle"]);
            particle.SetFloat("_Surface", 1f);
            particle.SetFloat("_Blend", 0f);
            particle.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            particle.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            particle.SetFloat("_ZWrite", 0f);
            particle.SetOverrideTag("RenderType", "Transparent");
            particle.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            particle.renderQueue = (int)RenderQueue.Transparent;
            mats["Particle"] = particle;

            skybox = Mat("Skybox", Shader.Find("Skybox/Procedural"));
            skybox.SetFloat("_SunSize", 0.045f);
            skybox.SetFloat("_SunSizeConvergence", 6f);
            skybox.SetFloat("_AtmosphereThickness", 1.05f);
            skybox.SetColor("_SkyTint", new Color(0.45f, 0.6f, 0.85f));
            skybox.SetColor("_GroundColor", new Color(0.35f, 0.42f, 0.3f));
            skybox.SetFloat("_Exposure", 1.25f);
        }

        static Material Mat(string name, Shader shader)
        {
            var path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader != shader)
            {
                if (mat != null) AssetDatabase.DeleteAsset(path);
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material Lit(string name, Color color, float smoothness = 0.15f, float metallic = 0f, Color? emission = null,
            string tex = null, Vector2? tiling = null)
        {
            var shader = GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.defaultShader : Shader.Find("Standard");
            var mat = Mat(name, shader);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", textures[tex]);
                mat.SetTexture("_MainTex", textures[tex]);
                mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
                mat.SetTextureScale("_MainTex", tiling ?? Vector2.one);
            }
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else mat.DisableKeyword("_EMISSION");
            mat.enableInstancing = true;
            mats[name] = mat;
            return mat;
        }

        // ------------------------------------------------------------------ Particles

        static void CreateParticleEffects()
        {
            // Splat: juicy plant guts.
            SaveFx("Splat", root =>
            {
                var ps = Particles(root, "Juice", 26, new Vector2(0.4f, 0.9f), new Vector2(2f, 6.5f), new Vector2(0.07f, 0.24f), 1.6f);
                Cone(ps, 40f, 0.1f);
                var drops = Particles(root, "Mist", 8, new Vector2(0.3f, 0.6f), new Vector2(0.5f, 1.5f), new Vector2(0.3f, 0.6f), 0f);
                Cone(drops, 60f, 0.1f);
                Fade(drops, 0.5f);
            });

            SaveFx("Explosion", root =>
            {
                var fire = Particles(root, "Fire", 30, new Vector2(0.25f, 0.6f), new Vector2(2f, 9f), new Vector2(0.6f, 1.6f), -0.2f);
                SetColor(fire, new Color(6f, 2.6f, 0.6f), new Color(4f, 1.2f, 0.2f));
                Sphere(fire, 0.4f);
                Grow(fire, 1f, 0.2f);
                var smoke = Particles(root, "Smoke", 18, new Vector2(1f, 1.8f), new Vector2(1f, 3.5f), new Vector2(1f, 2f), -0.15f);
                SetColor(smoke, new Color(0.25f, 0.23f, 0.2f, 0.8f), new Color(0.4f, 0.38f, 0.35f, 0.6f));
                Sphere(smoke, 0.8f);
                Grow(smoke, 0.5f, 1.6f);
                var dirt = Particles(root, "Dirt", 24, new Vector2(0.6f, 1.2f), new Vector2(5f, 11f), new Vector2(0.08f, 0.2f), 2.2f);
                SetColor(dirt, new Color(0.3f, 0.2f, 0.12f), new Color(0.45f, 0.32f, 0.2f));
                Cone(dirt, 50f, 0.3f, new Vector3(-90f, 0f, 0f));
                var sparks = Particles(root, "Sparks", 20, new Vector2(0.2f, 0.5f), new Vector2(8f, 16f), new Vector2(0.04f, 0.08f), 1f);
                SetColor(sparks, new Color(8f, 5f, 1.5f), new Color(6f, 3f, 0.5f));
                Sphere(sparks, 0.2f);
            });

            SaveFx("Dust", root =>
            {
                var ps = Particles(root, "Dust", 22, new Vector2(0.7f, 1.3f), new Vector2(0.8f, 3f), new Vector2(0.3f, 0.8f), -0.05f);
                Cone(ps, 70f, 0.6f, new Vector3(-90f, 0f, 0f));
                Grow(ps, 0.6f, 1.8f);
                var clods = Particles(root, "Clods", 12, new Vector2(0.5f, 0.9f), new Vector2(3f, 6f), new Vector2(0.08f, 0.16f), 2f);
                Cone(clods, 35f, 0.4f, new Vector3(-90f, 0f, 0f));
            });

            SaveFx("Impact", root =>
            {
                var ps = Particles(root, "Dust", 7, new Vector2(0.25f, 0.5f), new Vector2(1f, 3f), new Vector2(0.1f, 0.25f), 0.5f);
                SetColor(ps, new Color(0.55f, 0.45f, 0.35f, 0.8f), new Color(0.7f, 0.6f, 0.5f, 0.6f));
                Cone(ps, 30f, 0.05f);
                var sparks = Particles(root, "Sparks", 5, new Vector2(0.1f, 0.25f), new Vector2(4f, 8f), new Vector2(0.03f, 0.05f), 1f);
                SetColor(sparks, new Color(5f, 3.5f, 1.5f), new Color(4f, 2.5f, 1f));
                Cone(sparks, 40f, 0.02f);
            });

            SaveFx("MuzzleFlash", root =>
            {
                var ps = Particles(root, "Flash", 6, new Vector2(0.03f, 0.05f), new Vector2(0.3f, 1.2f), new Vector2(0.04f, 0.1f), 0f);
                Cone(ps, 18f, 0.01f);
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var smoke = Particles(root, "Smoke", 3, new Vector2(0.3f, 0.6f), new Vector2(0.3f, 0.8f), new Vector2(0.08f, 0.15f), -0.1f);
                SetColor(smoke, new Color(0.8f, 0.8f, 0.8f, 0.35f), new Color(0.6f, 0.6f, 0.6f, 0.25f));
                Cone(smoke, 15f, 0.01f);
                Grow(smoke, 1f, 3f);
            });

            SaveFx("Shockwave", root =>
            {
                var ps = Particles(root, "Ring", 70, new Vector2(0.35f, 0.5f), new Vector2(10f, 13f), new Vector2(0.4f, 0.7f), 0f);
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = 0.6f;
                sh.radiusThickness = 0f;
                sh.rotation = new Vector3(90f, 0f, 0f);
            });

            SaveFx("Sparkle", root =>
            {
                var ps = Particles(root, "Sparkle", 22, new Vector2(0.5f, 1f), new Vector2(1f, 3.5f), new Vector2(0.05f, 0.14f), -0.3f);
                Sphere(ps, 0.4f);
            });
        }

        static void SaveFx(string name, System.Action<GameObject> build)
        {
            var root = new GameObject(name);
            build(root);
            PrefabUtility.SaveAsPrefabAsset(root, $"{FxDir}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        static ParticleSystem Particles(GameObject root, string name, int burst, Vector2 lifetime, Vector2 speed, Vector2 size, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;
            main.startColor = Color.white;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });

            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.1f;

            Grow(ps, 1f, 0f);
            Fade(ps, 0.7f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mats["Particle"];
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static void SetColor(ParticleSystem ps, Color a, Color b)
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        }

        static void Cone(ParticleSystem ps, float angle, float radius, Vector3? rotation = null)
        {
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = angle;
            sh.radius = radius;
            sh.rotation = rotation ?? Vector3.zero;
        }

        static void Sphere(ParticleSystem ps, float radius)
        {
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = radius;
        }

        static void Grow(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
        }

        static void Fade(ParticleSystem ps, float fadeStart)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        // ------------------------------------------------------------------ Primitive helpers

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, string mat,
            bool collider = true, Vector3? euler = null, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mats[mat];
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Empty(string name, Transform parent, Vector3 pos, Vector3? euler = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            return go;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static void Leaves(Transform parent, Vector3 pos, int count, float length, float tilt, string mat = "Leaf")
        {
            for (int i = 0; i < count; i++)
            {
                var pivot = Empty("LeafPivot", parent, pos, new Vector3(tilt, i * 360f / count, 0f)).transform;
                Prim(PrimitiveType.Sphere, "Leaf", pivot, new Vector3(0f, length / 2f, 0f), new Vector3(length * 0.28f, length / 2f, length * 0.08f), mat, false);
            }
        }

        // ------------------------------------------------------------------ Projectiles

        struct ProjectileSet { public EnemyProjectile spit; public SeedGrenade seed; }

        static ProjectileSet CreateProjectiles()
        {
            var set = new ProjectileSet();

            var spit = new GameObject("Projectile_Spit");
            Prim(PrimitiveType.Sphere, "Glob", spit.transform, Vector3.zero, new Vector3(0.35f, 0.35f, 0.5f), "SpitGlow", false, shadows: false);
            AddTrail(spit, new Color(0.8f, 2f, 0.2f), 0.3f, 0.25f);
            var light = Empty("Light", spit.transform, Vector3.zero).AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.5f, 1f, 0.2f);
            light.range = 3f;
            light.intensity = 2f;
            spit.AddComponent<EnemyProjectile>().splashColor = new Color(0.55f, 0.9f, 0.15f);
            SetLayerRecursive(spit, IgnoreRaycastLayer);
            set.spit = PrefabUtility.SaveAsPrefabAsset(spit, $"{PrefabDir}/Projectile_Spit.prefab").GetComponent<EnemyProjectile>();
            Object.DestroyImmediate(spit);

            var seed = new GameObject("Projectile_Seed");
            Prim(PrimitiveType.Sphere, "Seed", seed.transform, Vector3.zero, new Vector3(0.22f, 0.22f, 0.32f), "SeedGlow", false, shadows: false);
            Prim(PrimitiveType.Cube, "Sprout", seed.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.04f, 0.12f, 0.1f), "Leaf", false, shadows: false);
            AddTrail(seed, new Color(0.6f, 1.6f, 0.2f), 0.18f, 0.2f);
            seed.AddComponent<SeedGrenade>();
            SetLayerRecursive(seed, IgnoreRaycastLayer);
            set.seed = PrefabUtility.SaveAsPrefabAsset(seed, $"{PrefabDir}/Projectile_Seed.prefab").GetComponent<SeedGrenade>();
            Object.DestroyImmediate(seed);
            return set;
        }

        static void AddTrail(GameObject go, Color color, float width, float time)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = mats["Particle"];
            trail.time = time;
            trail.widthCurve = AnimationCurve.Linear(0f, width, 1f, 0f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ Pickup prefabs

        static Pickup[] CreatePickupPrefabs()
        {
            return new[]
            {
                MakePickup("Pickup_Health", Pickup.Kind.Health, 35f, new Color(1f, 0.3f, 0.3f), v =>
                {
                    Prim(PrimitiveType.Cube, "Box", v, Vector3.zero, new Vector3(0.6f, 0.6f, 0.6f), "White", false);
                    Prim(PrimitiveType.Cube, "CrossH", v, Vector3.zero, new Vector3(0.45f, 0.14f, 0.62f), "HealthRed", false);
                    Prim(PrimitiveType.Cube, "CrossV", v, Vector3.zero, new Vector3(0.14f, 0.45f, 0.62f), "HealthRed", false);
                    Prim(PrimitiveType.Cube, "CrossH2", v, Vector3.zero, new Vector3(0.62f, 0.14f, 0.45f), "HealthRed", false);
                    Prim(PrimitiveType.Cube, "CrossV2", v, Vector3.zero, new Vector3(0.62f, 0.45f, 0.14f), "HealthRed", false);
                }),
                MakePickup("Pickup_DamageBoost", Pickup.Kind.DamageBoost, 10f, new Color(1f, 0.45f, 0.1f), v =>
                {
                    Prim(PrimitiveType.Cube, "Gem", v, Vector3.zero, Vector3.one * 0.5f, "PowerOrange", false, new Vector3(45f, 0f, 45f));
                    Prim(PrimitiveType.Cube, "Gem2", v, Vector3.zero, Vector3.one * 0.38f, "PowerOrange", false, new Vector3(0f, 45f, 0f));
                }),
                MakePickup("Pickup_RapidFire", Pickup.Kind.RapidFire, 10f, new Color(1f, 0.85f, 0.2f), v =>
                {
                    Prim(PrimitiveType.Cylinder, "Cell", v, Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f), "Glow", false);
                    Prim(PrimitiveType.Cylinder, "Cap", v, new Vector3(0f, 0.4f, 0f), new Vector3(0.15f, 0.06f, 0.15f), "Metal", false);
                    Prim(PrimitiveType.Cylinder, "Base", v, new Vector3(0f, -0.37f, 0f), new Vector3(0.37f, 0.04f, 0.37f), "Metal", false);
                }),
                MakePickup("Pickup_NewWeapon", Pickup.Kind.NewWeapon, 0f, new Color(0.3f, 0.55f, 1f), v =>
                {
                    Prim(PrimitiveType.Cube, "Crate", v, Vector3.zero, new Vector3(0.9f, 0.5f, 0.5f), "Crate", false);
                    Prim(PrimitiveType.Cube, "Band", v, Vector3.zero, new Vector3(0.15f, 0.52f, 0.52f), "PowerBlue", false);
                    Prim(PrimitiveType.Cube, "Band2", v, new Vector3(0.3f, 0f, 0f), new Vector3(0.06f, 0.52f, 0.52f), "PowerBlue", false);
                    Prim(PrimitiveType.Cube, "Band3", v, new Vector3(-0.3f, 0f, 0f), new Vector3(0.06f, 0.52f, 0.52f), "PowerBlue", false);
                }),
            };
        }

        static Pickup MakePickup(string name, Pickup.Kind kind, float amount, Color color, System.Action<Transform> build)
        {
            var root = new GameObject(name);
            var visual = Empty("Visual", root.transform, Vector3.zero).transform;
            build(visual);
            var light = Empty("Glow", root.transform, Vector3.zero).AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 4f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;

            var pickup = root.AddComponent<Pickup>();
            pickup.kind = kind;
            pickup.amount = amount;
            pickup.color = color;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab").GetComponent<Pickup>();
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------ Environment

        static GameObject BuildEnvironment()
        {
            var env = new GameObject("Environment").transform;

            Prim(PrimitiveType.Cube, "Field", env, new Vector3(0f, -0.5f, 0f), new Vector3(200f, 1f, 200f), "Grass");
            Prim(PrimitiveType.Cube, "GardenSoil", env, new Vector3(0f, -0.04f, 0f), new Vector3(FenceHalf * 2f, 0.1f, FenceHalf * 2f), "Soil", false);

            BuildFence(env);
            BuildBarn(env);
            BuildBeds(env);
            BuildProps(env);
            BuildFarmhouse(env);
            BuildSilo(env);
            BuildWindmill(env);
            BuildTrees(env);
            BuildHorizon(env);

            // Lighting: warm late-afternoon sun.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.75f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.75f, 0.82f, 0.88f);
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 190f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.7f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.24f, 0.18f);
            RenderSettings.ambientIntensity = 1f;

            return env.gameObject;
        }

        static void BuildFence(Transform env)
        {
            var fence = Empty("Fence", env, Vector3.zero).transform;
            for (int side = 0; side < 4; side++)
            {
                var sideRoot = Empty("Side" + side, fence, Vector3.zero, new Vector3(0f, side * 90f, 0f)).transform;
                for (float x = -FenceHalf; x <= FenceHalf; x += 3f)
                {
                    Prim(PrimitiveType.Cube, "Post", sideRoot, new Vector3(x, 0.7f, FenceHalf), new Vector3(0.18f, 1.4f, 0.18f), "White", false);
                    Prim(PrimitiveType.Cube, "Cap", sideRoot, new Vector3(x, 1.43f, FenceHalf), new Vector3(0.22f, 0.06f, 0.22f), "White", false, shadows: false);
                }
                Prim(PrimitiveType.Cube, "RailLow", sideRoot, new Vector3(0f, 0.5f, FenceHalf), new Vector3(FenceHalf * 2f, 0.12f, 0.06f), "White", false);
                Prim(PrimitiveType.Cube, "RailHigh", sideRoot, new Vector3(0f, 1.1f, FenceHalf), new Vector3(FenceHalf * 2f, 0.12f, 0.06f), "White", false);
                // Invisible wall keeps everything inside the garden.
                var wall = Prim(PrimitiveType.Cube, "Blocker", sideRoot, new Vector3(0f, 2f, FenceHalf + 0.2f), new Vector3(FenceHalf * 2f + 1f, 4f, 0.4f), "White");
                Object.DestroyImmediate(wall.GetComponent<MeshRenderer>());

                // Sunflowers along the inside of the fence.
                for (float x = -FenceHalf + 2.5f; x < FenceHalf - 2f; x += 7f)
                {
                    if (side == 0 && x > 10f) continue; // keep the barn corner clear
                    var flower = Empty("Sunflower", sideRoot, new Vector3(x + Mathf.Sin(x) * 0.8f, 0f, FenceHalf - 0.8f)).transform;
                    float h = 1.8f + Mathf.Abs(Mathf.Sin(x * 3.1f)) * 0.8f;
                    Prim(PrimitiveType.Cylinder, "Stem", flower, new Vector3(0f, h / 2f, 0f), new Vector3(0.06f, h / 2f, 0.06f), "Leaf", false);
                    var head = Empty("Head", flower, new Vector3(0f, h, -0.05f), new Vector3(-20f, 180f, 0f)).transform;
                    Prim(PrimitiveType.Cylinder, "Petals", head, Vector3.zero, new Vector3(0.65f, 0.02f, 0.65f), "Sunflower", false, new Vector3(90f, 0f, 0f));
                    Prim(PrimitiveType.Cylinder, "Seeds", head, new Vector3(0f, 0f, 0.02f), new Vector3(0.35f, 0.02f, 0.35f), "DarkWood", false, new Vector3(90f, 0f, 0f));
                    Leaves(flower, new Vector3(0f, h * 0.5f, 0f), 2, 0.5f, 70f);
                }
            }
        }

        static void BuildBarn(Transform env)
        {
            var barn = Empty("Barn (Pajta)", env, BarnCenter).transform;
            float hx = BarnSize.x / 2f, hz = BarnSize.z / 2f, h = BarnSize.y, t = 0.3f;
            float door = 4f;

            Prim(PrimitiveType.Cube, "Floor", barn, new Vector3(0f, 0.02f, 0f), new Vector3(BarnSize.x, 0.06f, BarnSize.z), "Wood", false);
            Prim(PrimitiveType.Cube, "WallBack", barn, new Vector3(hx, h / 2f, 0f), new Vector3(t, h, BarnSize.z), "BarnRed");
            Prim(PrimitiveType.Cube, "WallNorth", barn, new Vector3(0f, h / 2f, hz), new Vector3(BarnSize.x, h, t), "BarnRed");
            Prim(PrimitiveType.Cube, "WallSouth", barn, new Vector3(0f, h / 2f, -hz), new Vector3(BarnSize.x, h, t), "BarnRed");
            float seg = (BarnSize.z - door) / 2f;
            Prim(PrimitiveType.Cube, "WallFrontL", barn, new Vector3(-hx, h / 2f, -hz + seg / 2f), new Vector3(t, h, seg), "BarnRed");
            Prim(PrimitiveType.Cube, "WallFrontR", barn, new Vector3(-hx, h / 2f, hz - seg / 2f), new Vector3(t, h, seg), "BarnRed");
            Prim(PrimitiveType.Cube, "Lintel", barn, new Vector3(-hx, h - 0.6f, 0f), new Vector3(t, 1.2f, door), "BarnRed", false);

            // White trim + open barn doors with the classic X brace.
            float dh = h - 1.2f;
            Prim(PrimitiveType.Cube, "TrimTop", barn, new Vector3(-hx - 0.16f, dh, 0f), new Vector3(0.06f, 0.25f, door + 0.25f), "White", false);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cube, "Trim", barn, new Vector3(-hx - 0.16f, dh / 2f, s * door / 2f), new Vector3(0.06f, dh, 0.25f), "White", false);
                var doorRoot = Empty("Door", barn, new Vector3(-hx - 0.2f, 0f, s * (door / 2f + 0.1f)), new Vector3(0f, s * -110f, 0f)).transform;
                Prim(PrimitiveType.Cube, "Panel", doorRoot, new Vector3(0f, dh / 2f, s * door / 4f), new Vector3(0.12f, dh, door / 2f), "BarnRed");
                float diag = Mathf.Atan2(dh, door / 2f) * Mathf.Rad2Deg;
                Prim(PrimitiveType.Cube, "BraceA", doorRoot, new Vector3(-0.08f, dh / 2f, s * door / 4f), new Vector3(0.04f, 0.18f, Mathf.Sqrt(dh * dh + door * door / 4f)), "White", false, new Vector3(diag, 0f, 0f));
                Prim(PrimitiveType.Cube, "BraceB", doorRoot, new Vector3(-0.08f, dh / 2f, s * door / 4f), new Vector3(0.04f, 0.18f, Mathf.Sqrt(dh * dh + door * door / 4f)), "White", false, new Vector3(-diag, 0f, 0f));
                Prim(PrimitiveType.Cube, "FrameV", doorRoot, new Vector3(-0.08f, dh / 2f, s * 0.05f), new Vector3(0.04f, dh, 0.18f), "White", false);
                Prim(PrimitiveType.Cube, "FrameV2", doorRoot, new Vector3(-0.08f, dh / 2f, s * (door / 2f - 0.05f)), new Vector3(0.04f, dh, 0.18f), "White", false);
            }

            // Pitched roof (no collider so nothing can stand on it).
            float roofAngle = 32f;
            float slabWidth = hx / Mathf.Cos(roofAngle * Mathf.Deg2Rad) + 0.6f;
            float rise = Mathf.Tan(roofAngle * Mathf.Deg2Rad) * hx;
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, "Roof", barn, new Vector3(s * hx / 2f, h + rise / 2f, 0f), new Vector3(slabWidth, 0.25f, BarnSize.z + 0.8f), "Roof", false,
                    new Vector3(0f, 0f, -s * roofAngle));
            Prim(PrimitiveType.Cube, "Ridge", barn, new Vector3(0f, h + rise + 0.05f, 0f), new Vector3(0.4f, 0.2f, BarnSize.z + 0.9f), "White", false);
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 8; i++)
                {
                    float k = i / 8f;
                    Prim(PrimitiveType.Cube, "Gable", barn, new Vector3(0f, h + rise * k + rise / 16f, s * hz), new Vector3(BarnSize.x * (1f - k), rise / 8f, t), "BarnRed", false);
                }
            // Hay loft window on the front gable.
            Prim(PrimitiveType.Cube, "LoftDoor", barn, new Vector3(0f, h + rise * 0.35f, -hz - 0.17f), new Vector3(1.4f, 1.3f, 0.06f), "White", false);
            Prim(PrimitiveType.Cube, "LoftInner", barn, new Vector3(0f, h + rise * 0.35f, -hz - 0.19f), new Vector3(1.1f, 1f, 0.04f), "DarkWood", false);

            // Hay + lantern inside.
            Prim(PrimitiveType.Cube, "Hay", barn, new Vector3(3.5f, 0.45f, 4f), new Vector3(1.6f, 0.9f, 1.1f), "Hay");
            Prim(PrimitiveType.Cube, "Hay", barn, new Vector3(3.5f, 0.45f, 2.6f), new Vector3(1.6f, 0.9f, 1.1f), "Hay");
            Prim(PrimitiveType.Cube, "Hay", barn, new Vector3(3.5f, 1.35f, 3.3f), new Vector3(1.6f, 0.9f, 1.1f), "Hay", true, new Vector3(0f, 12f, 0f));
            Prim(PrimitiveType.Cube, "Hay", barn, new Vector3(3.3f, 0.45f, -4.2f), new Vector3(1.6f, 0.9f, 1.1f), "Hay", true, new Vector3(0f, 80f, 0f));
            Prim(PrimitiveType.Cylinder, "LanternBody", barn, new Vector3(0f, 3.6f, 0f), new Vector3(0.25f, 0.2f, 0.25f), "Window", false, shadows: false);
            var lantern = Empty("Lantern", barn, new Vector3(0f, 3.3f, 0f)).AddComponent<Light>();
            lantern.type = LightType.Point;
            lantern.color = new Color(1f, 0.75f, 0.35f);
            lantern.range = 9f;
            lantern.intensity = 2.5f;
            lantern.shadows = LightShadows.None;

            var checkpoint = barn.gameObject.AddComponent<BarnCheckpoint>();
            checkpoint.area = new Bounds(BarnCenter + Vector3.up * 2f, new Vector3(BarnSize.x - 1f, 4f, BarnSize.z - 1f));
            checkpoint.lantern = lantern;
        }

        static void BuildBeds(Transform env)
        {
            var beds = Empty("VegetableBeds", env, Vector3.zero).transform;
            var patches = new[] { new Vector3(-16f, 0f, 14f), new Vector3(-16f, 0f, -14f), new Vector3(10f, 0f, -16f), new Vector3(0f, 0f, 2f) };
            var rng = new System.Random(7);
            foreach (var p in patches)
            {
                for (int row = 0; row < 4; row++)
                {
                    var pos = p + new Vector3(0f, 0.1f, row * 2f - 3f);
                    Prim(PrimitiveType.Cube, "Bed", beds, pos, new Vector3(8f, 0.22f, 0.9f), "Furrows");
                    for (int i = 0; i < 7; i++)
                    {
                        float s = 0.3f + (float)rng.NextDouble() * 0.25f;
                        var sprout = Empty("Sprout", beds, pos + new Vector3(-3.3f + i * 1.1f, 0.11f, 0f), new Vector3(0f, (float)rng.NextDouble() * 360f, 0f)).transform;
                        Leaves(sprout, Vector3.zero, 4, s * 1.4f, 45f);
                    }
                }
            }
        }

        static void BuildProps(Transform env)
        {
            var props = Empty("Props", env, Vector3.zero).transform;

            var bales = new[] { new Vector3(6f, 0f, 14f), new Vector3(-6f, 0f, -4f), new Vector3(14f, 0f, 2f), new Vector3(-22f, 0f, 0f),
                                new Vector3(4f, 0f, -24f), new Vector3(-4f, 0f, 22f), new Vector3(22f, 0f, -6f), new Vector3(-10f, 0f, -22f) };
            for (int i = 0; i < bales.Length; i++)
            {
                var bale = Prim(PrimitiveType.Cube, "HayBale", props, bales[i] + Vector3.up * 0.45f, new Vector3(1.6f, 0.9f, 1.1f), "Hay", true, new Vector3(0f, i * 37f, 0f));
                if (i % 3 == 0)
                    Prim(PrimitiveType.Cube, "HayTop", props, bales[i] + Vector3.up * 1.35f, new Vector3(1.6f, 0.9f, 1.1f), "Hay", true, new Vector3(0f, i * 37f + 70f, 0f));
            }

            // Scarecrow
            var sc = Empty("Scarecrow", props, new Vector3(-6f, 0f, 8f)).transform;
            Prim(PrimitiveType.Cylinder, "Pole", sc, new Vector3(0f, 1.2f, 0f), new Vector3(0.12f, 1.2f, 0.12f), "Wood");
            Prim(PrimitiveType.Cylinder, "Arms", sc, new Vector3(0f, 1.7f, 0f), new Vector3(0.08f, 0.9f, 0.08f), "Wood", false, new Vector3(0f, 0f, 90f));
            Prim(PrimitiveType.Cube, "Shirt", sc, new Vector3(0f, 1.5f, 0f), new Vector3(0.6f, 0.7f, 0.3f), "Shirt", false);
            Prim(PrimitiveType.Cube, "Sleeves", sc, new Vector3(0f, 1.7f, 0f), new Vector3(1.4f, 0.2f, 0.22f), "Shirt", false);
            Prim(PrimitiveType.Sphere, "Head", sc, new Vector3(0f, 2.15f, 0f), Vector3.one * 0.45f, "Hay", false);
            Prim(PrimitiveType.Cylinder, "HatBrim", sc, new Vector3(0f, 2.35f, 0f), new Vector3(0.8f, 0.02f, 0.8f), "Hay", false);
            Prim(PrimitiveType.Cylinder, "HatTop", sc, new Vector3(0f, 2.5f, 0f), new Vector3(0.4f, 0.15f, 0.4f), "Hay", false);

            // Tractor
            var tr = Empty("Tractor", props, new Vector3(-20f, 0f, -18f), new Vector3(0f, 30f, 0f)).transform;
            Prim(PrimitiveType.Cube, "Body", tr, new Vector3(0f, 1.1f, 0.2f), new Vector3(1.3f, 0.9f, 2.6f), "Tractor");
            Prim(PrimitiveType.Cube, "Hood", tr, new Vector3(0f, 1.2f, 1.2f), new Vector3(1.1f, 0.8f, 1.2f), "Tractor", false);
            Prim(PrimitiveType.Cube, "Grill", tr, new Vector3(0f, 1.15f, 1.81f), new Vector3(0.9f, 0.6f, 0.04f), "Metal", false);
            Prim(PrimitiveType.Cube, "CabinRoof", tr, new Vector3(0f, 2.75f, -0.6f), new Vector3(1.5f, 0.08f, 1.5f), "Tractor", false);
            foreach (var px in new[] { -0.65f, 0.65f })
                foreach (var pz in new[] { -1.25f, 0.05f })
                    Prim(PrimitiveType.Cube, "CabinPost", tr, new Vector3(px, 2.1f, pz), new Vector3(0.07f, 1.3f, 0.07f), "Black", false);
            Prim(PrimitiveType.Cube, "Seat", tr, new Vector3(0f, 1.75f, -0.7f), new Vector3(0.6f, 0.15f, 0.5f), "Black", false);
            Prim(PrimitiveType.Cylinder, "Exhaust", tr, new Vector3(0.4f, 2.1f, 1.4f), new Vector3(0.1f, 0.5f, 0.1f), "Metal", false);
            foreach (var w in new[] { new Vector3(-0.85f, 0.8f, -0.8f), new Vector3(0.85f, 0.8f, -0.8f) })
            {
                Prim(PrimitiveType.Cylinder, "BigWheel", tr, w, new Vector3(1.6f, 0.22f, 1.6f), "Black", true, new Vector3(0f, 0f, 90f));
                Prim(PrimitiveType.Cylinder, "Hub", tr, w + new Vector3(Mathf.Sign(w.x) * 0.12f, 0f, 0f), new Vector3(0.7f, 0.03f, 0.7f), "Sunflower", false, new Vector3(0f, 0f, 90f));
            }
            foreach (var w in new[] { new Vector3(-0.75f, 0.45f, 1.3f), new Vector3(0.75f, 0.45f, 1.3f) })
                Prim(PrimitiveType.Cylinder, "SmallWheel", tr, w, new Vector3(0.9f, 0.15f, 0.9f), "Black", true, new Vector3(0f, 0f, 90f));

            // Fertilizer barrels with glowing ooze: the cause of it all.
            var barrels = new[] { new Vector3(12f, 0f, 10f), new Vector3(12.9f, 0f, 10.6f), new Vector3(12.3f, 0f, 11.4f) };
            foreach (var b in barrels)
            {
                Prim(PrimitiveType.Cylinder, "FertilizerBarrel", props, b + Vector3.up * 0.6f, new Vector3(0.7f, 0.6f, 0.7f), "Tractor");
                Prim(PrimitiveType.Cylinder, "Ooze", props, b + Vector3.up * 1.21f, new Vector3(0.55f, 0.01f, 0.55f), "Ooze", false, shadows: false);
            }
            Prim(PrimitiveType.Cylinder, "Puddle", props, new Vector3(11.2f, 0.02f, 11.8f), new Vector3(2.4f, 0.005f, 1.6f), "Ooze", false, shadows: false);
            var oozeLight = Empty("OozeLight", props, new Vector3(12f, 1.6f, 11f)).AddComponent<Light>();
            oozeLight.type = LightType.Point;
            oozeLight.color = new Color(0.5f, 1f, 0.2f);
            oozeLight.range = 5f;
            oozeLight.intensity = 2f;

            // Stone well (cover)
            var well = Empty("Well", props, new Vector3(-12f, 0f, -6f)).transform;
            Prim(PrimitiveType.Cylinder, "Ring", well, new Vector3(0f, 0.5f, 0f), new Vector3(1.8f, 0.5f, 1.8f), "Stone");
            Prim(PrimitiveType.Cylinder, "Water", well, new Vector3(0f, 0.9f, 0f), new Vector3(1.5f, 0.02f, 1.5f), "Sprayer", false);
            foreach (var s in new[] { -0.8f, 0.8f })
                Prim(PrimitiveType.Cube, "Post", well, new Vector3(s, 1.5f, 0f), new Vector3(0.12f, 2f, 0.12f), "Wood", false);
            Prim(PrimitiveType.Cube, "RoofL", well, new Vector3(0f, 2.6f, -0.4f), new Vector3(2f, 0.08f, 1f), "Roof", false, new Vector3(-30f, 0f, 0f));
            Prim(PrimitiveType.Cube, "RoofR", well, new Vector3(0f, 2.6f, 0.4f), new Vector3(2f, 0.08f, 1f), "Roof", false, new Vector3(30f, 0f, 0f));

            // Crates near the barn
            var crates = new[] { new Vector3(10f, 0.5f, 24f), new Vector3(11.1f, 0.5f, 24.3f), new Vector3(10.5f, 1.5f, 24.1f), new Vector3(24f, 0.5f, 8f) };
            for (int i = 0; i < crates.Length; i++)
                Prim(PrimitiveType.Cube, "Crate", props, crates[i], Vector3.one, "Crate", true, new Vector3(0f, i * 23f, 0f));

            // Normal (non-mutant) pumpkins
            var rng = new System.Random(5);
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector3(-24f + (float)rng.NextDouble() * 8f, 0.25f, 18f + (float)rng.NextDouble() * 8f);
                float s = 0.5f + (float)rng.NextDouble() * 0.4f;
                Prim(PrimitiveType.Sphere, "Pumpkin", props, p, new Vector3(1f, 0.75f, 1f) * s, "Pumpkin", false);
            }
        }

        static void BuildFarmhouse(Transform env)
        {
            var house = Empty("Farmhouse", env, FarmhousePos).transform;
            float w = 12f, d = 8f, h = 4.5f;
            Prim(PrimitiveType.Cube, "Walls", house, new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d), "White");
            Prim(PrimitiveType.Cube, "Foundation", house, new Vector3(0f, 0.2f, 0f), new Vector3(w + 0.3f, 0.4f, d + 0.3f), "Stone", false);
            float rise = 3f;
            float slab = Mathf.Sqrt(rise * rise + d * d / 4f) + 0.6f;
            float ang = Mathf.Atan2(rise, d / 2f) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, "Roof", house, new Vector3(0f, h + rise / 2f, s * d / 4f), new Vector3(w + 1f, 0.25f, slab), "Roof", false, new Vector3(s * ang, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 6; i++)
                {
                    float k = i / 6f;
                    Prim(PrimitiveType.Cube, "Gable", house, new Vector3(s * w / 2f, h + rise * k + rise / 12f, 0f), new Vector3(0.2f, rise / 6f, d * (1f - k)), "White", false);
                }
            Prim(PrimitiveType.Cube, "Chimney", house, new Vector3(3.5f, h + 2.5f, 1.5f), new Vector3(0.9f, 3f, 0.9f), "Stone", false);
            // Windows facing the garden (-z)
            foreach (var x in new[] { -4f, -1.5f, 2.5f, 4.5f })
            {
                Prim(PrimitiveType.Cube, "Window", house, new Vector3(x, 2.4f, -d / 2f - 0.02f), new Vector3(1.1f, 1.2f, 0.05f), "Window", false, shadows: false);
                Prim(PrimitiveType.Cube, "Frame", house, new Vector3(x, 2.4f, -d / 2f - 0.04f), new Vector3(0.08f, 1.2f, 0.05f), "DarkWood", false);
                Prim(PrimitiveType.Cube, "Shutter", house, new Vector3(x - 0.75f, 2.4f, -d / 2f - 0.04f), new Vector3(0.35f, 1.3f, 0.05f), "BarnRed", false);
                Prim(PrimitiveType.Cube, "Shutter", house, new Vector3(x + 0.75f, 2.4f, -d / 2f - 0.04f), new Vector3(0.35f, 1.3f, 0.05f), "BarnRed", false);
            }
            Prim(PrimitiveType.Cube, "Door", house, new Vector3(1f, 1.1f, -d / 2f - 0.03f), new Vector3(1.1f, 2.2f, 0.05f), "BarnRed", false);
            Prim(PrimitiveType.Cube, "Porch", house, new Vector3(0f, 0.25f, -d / 2f - 1.2f), new Vector3(w * 0.8f, 0.2f, 2.4f), "Wood");
            Prim(PrimitiveType.Cube, "PorchRoof", house, new Vector3(0f, 2.9f, -d / 2f - 1.2f), new Vector3(w * 0.85f, 0.15f, 2.6f), "Roof", false, new Vector3(-12f, 0f, 0f));
            foreach (var x in new[] { -4.4f, -1.5f, 1.5f, 4.4f })
                Prim(PrimitiveType.Cylinder, "PorchPost", house, new Vector3(x, 1.5f, -d / 2f - 2.3f), new Vector3(0.15f, 1.3f, 0.15f), "White", false);
        }

        static void BuildSilo(Transform env)
        {
            var silo = Empty("Silo", env, SiloPos).transform;
            Prim(PrimitiveType.Cylinder, "Tank", silo, new Vector3(0f, 6f, 0f), new Vector3(5f, 6f, 5f), "Silo");
            Prim(PrimitiveType.Sphere, "Dome", silo, new Vector3(0f, 12f, 0f), new Vector3(5.1f, 3f, 5.1f), "Silo", false);
            for (int i = 1; i < 6; i++)
                Prim(PrimitiveType.Cylinder, "Band", silo, new Vector3(0f, i * 2f, 0f), new Vector3(5.08f, 0.06f, 5.08f), "Metal", false);
            Prim(PrimitiveType.Cube, "Ladder", silo, new Vector3(0f, 6f, -2.55f), new Vector3(0.5f, 12f, 0.06f), "Metal", false);
        }

        static void BuildWindmill(Transform env)
        {
            var mill = Empty("Windmill", env, WindmillPos).transform;
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var leg = Empty("LegPivot", mill, Vector3.zero, new Vector3(0f, a, 0f)).transform;
                Prim(PrimitiveType.Cube, "Leg", leg, new Vector3(0f, 6f, 1.1f), new Vector3(0.18f, 12.2f, 0.18f), "Metal", true, new Vector3(-8f, 0f, 0f));
            }
            for (int i = 1; i < 5; i++)
                Prim(PrimitiveType.Cube, "Brace", mill, new Vector3(0f, i * 2.5f, 0f), new Vector3(2.4f - i * 0.35f, 0.1f, 2.4f - i * 0.35f), "Metal", false);
            var hub = Empty("Hub", mill, new Vector3(0f, 12.3f, -0.6f), new Vector3(0f, 25f, 0f)).transform;
            Prim(PrimitiveType.Cube, "Tail", hub, new Vector3(0f, 0f, 1.6f), new Vector3(0.06f, 1f, 1.8f), "BarnRed", false);
            var rotor = Empty("Rotor", hub, Vector3.zero).transform;
            rotor.gameObject.AddComponent<Rotator>().degreesPerSecond = new Vector3(0f, 0f, 70f);
            for (int i = 0; i < 12; i++)
            {
                var blade = Empty("BladePivot", rotor, Vector3.zero, new Vector3(0f, 0f, i * 30f)).transform;
                Prim(PrimitiveType.Cube, "Blade", blade, new Vector3(0f, 1.1f, 0f), new Vector3(0.35f, 1.8f, 0.04f), "White", false, new Vector3(0f, 20f, 0f));
            }
        }

        static void BuildTrees(Transform env)
        {
            var trees = Empty("Trees", env, Vector3.zero).transform;
            var rng = new System.Random(42);
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.12f;
                float r = 42f + (float)rng.NextDouble() * 30f;
                var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (Vector3.Distance(pos, FarmhousePos) < 12f || Vector3.Distance(pos, SiloPos) < 8f || Vector3.Distance(pos, WindmillPos) < 6f) continue;
                float s = 0.8f + (float)rng.NextDouble() * 0.8f;
                var tree = Empty("Tree", trees, pos, new Vector3(0f, (float)rng.NextDouble() * 360f, 0f)).transform;
                bool pine = rng.NextDouble() < 0.35;
                Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 1.6f * s, 0f), new Vector3(0.45f * s, 1.6f * s, 0.45f * s), "Bark", false);
                if (pine)
                {
                    for (int k = 0; k < 3; k++)
                        Prim(PrimitiveType.Capsule, "Cone", tree, new Vector3(0f, (3f + k * 1.5f) * s, 0f), new Vector3(3.2f - k * 0.9f, 1.4f, 3.2f - k * 0.9f) * s, "TreeLeaf", false);
                }
                else
                {
                    Prim(PrimitiveType.Sphere, "Crown", tree, new Vector3(0f, 4.2f * s, 0f), new Vector3(3.8f, 3.2f, 3.8f) * s, "TreeLeaf", false);
                    Prim(PrimitiveType.Sphere, "Crown2", tree, new Vector3(1f * s, 5.2f * s, 0.4f * s), new Vector3(2.6f, 2.3f, 2.6f) * s, "TreeLeaf", false);
                    Prim(PrimitiveType.Sphere, "Crown3", tree, new Vector3(-0.9f * s, 4.8f * s, -0.6f * s), new Vector3(2.4f, 2.1f, 2.4f) * s, "TreeLeaf", false);
                }
            }
        }

        static void BuildHorizon(Transform env)
        {
            var horizon = Empty("Horizon", env, Vector3.zero).transform;
            var rng = new System.Random(9);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                float r = 150f + (float)rng.NextDouble() * 30f;
                float w = 60f + (float)rng.NextDouble() * 50f;
                Prim(PrimitiveType.Sphere, "Hill", horizon, new Vector3(Mathf.Cos(a) * r, -4f, Mathf.Sin(a) * r), new Vector3(w, 18f + (float)rng.NextDouble() * 22f, w), "Hill", false, shadows: false);
            }
            for (int i = 0; i < 12; i++)
            {
                var cloud = Empty("Cloud", horizon, new Vector3(-150f + i * 27f, 55f + (float)rng.NextDouble() * 20f, -80f + (float)rng.NextDouble() * 160f)).transform;
                cloud.gameObject.AddComponent<CloudDrift>().speed = 1f + (float)rng.NextDouble();
                for (int k = 0; k < 5; k++)
                    Prim(PrimitiveType.Sphere, "Puff", cloud, new Vector3((k - 2) * 4f, (float)rng.NextDouble() * 2f, (float)rng.NextDouble() * 4f),
                        new Vector3(9f, 4.5f, 7f) * (0.7f + (float)rng.NextDouble() * 0.6f), "Cloud", false, shadows: false);
            }
        }

        static void AddGrass(Transform parent)
        {
            var grass = new GameObject("Grass (instanced)").AddComponent<GrassField>();
            grass.transform.SetParent(parent, false);
            grass.material = mats["GrassBlades"];
            grass.count = 14000;
            grass.outerRadius = 85f;
            grass.exclusions = new[]
            {
                new Bounds(FarmhousePos + new Vector3(0f, 0f, -1.5f), new Vector3(14f, 10f, 12f)),
                new Bounds(SiloPos, new Vector3(6f, 10f, 6f)),
            };
        }

        static void AddVolume(Transform parent, out Volume volume)
        {
            var go = new GameObject("PostProcessing");
            go.transform.SetParent(parent, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = volumeProfile;
        }

        static void SetupCamera(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        // ------------------------------------------------------------------ Player

        static (PlayerHealth health, WeaponInventory weapons) BuildPlayer(SeedGrenade seed)
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 0.05f, -12f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;

            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = 75f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraFX>();
            SetupCamera(cam);

            var controller = player.AddComponent<PlayerController>();
            controller.playerCamera = cam;
            var health = player.AddComponent<PlayerHealth>();

            CreateWeaponMaterials();
            viewCamera = camGo.transform;
            var holder = Empty("WeaponHolder", camGo.transform, Vector3.zero).transform;
            holder.localScale = Vector3.one * 0.6f;
            var muzzleLight = Empty("MuzzleLight", camGo.transform, new Vector3(0.2f, -0.1f, 0.8f)).AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.range = 9f;
            muzzleLight.intensity = 4f;
            muzzleLight.shadows = LightShadows.None;

            var inventory = player.AddComponent<WeaponInventory>();
            inventory.aimCamera = cam;
            inventory.weaponHolder = holder;
            inventory.movement = controller;
            inventory.muzzleLight = muzzleLight;
            inventory.tracerMaterial = mats["Tracer"];
            inventory.weapons = new[]
            {
                new WeaponData
                {
                    nameKey = "w_rifle", damage = 13f, fireRate = 9.5f, automatic = true, pellets = 1, spread = 1f,
                    range = 90f, recoil = 0.035f, shake = 0.035f, knockback = 0.9f, unlockedAtStart = true, sound = SoundFX.Sfx.AutoRifle,
                    tracerColor = new Color(1f, 0.85f, 0.45f),
                },
                new WeaponData
                {
                    nameKey = "w_shotgun", damage = 14f, fireRate = 1.3f, automatic = false, pellets = 10, spread = 6f,
                    range = 32f, recoil = 0.2f, shake = 0.25f, knockback = 1.2f, sound = SoundFX.Sfx.Shotgun,
                    tracerColor = new Color(1f, 0.65f, 0.3f),
                },
                new WeaponData
                {
                    nameKey = "w_sprayer", damage = 8f, fireRate = 14f, automatic = true, pellets = 1, spread = 2.2f,
                    range = 26f, recoil = 0.02f, shake = 0.02f, knockback = 0.6f, sound = SoundFX.Sfx.Sprayer,
                    tracerColor = new Color(0.5f, 1f, 0.3f),
                },
                new WeaponData
                {
                    nameKey = "w_launcher", damage = 110f, fireRate = 1f, automatic = false, pellets = 1, spread = 0f,
                    range = 0f, recoil = 0.25f, shake = 0.15f, sound = SoundFX.Sfx.Launcher, projectile = seed, projectileSpeed = 26f,
                    tracerColor = new Color(0.6f, 1f, 0.2f),
                },
            };
            inventory.weapons[0].model = AutoRifleModel(holder, out inventory.weapons[0].muzzle);
            inventory.weapons[1].model = ShotgunModel(holder, out inventory.weapons[1].muzzle);
            inventory.weapons[2].model = SprayerModel(holder, out inventory.weapons[2].muzzle);
            inventory.weapons[3].model = LauncherModel(holder, out inventory.weapons[3].muzzle);
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IgnoreRaycastLayer;
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;

            return (health, inventory);
        }

        // ------------------------------------------------------------------ Scenes

        static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go); // we add our own camera & sun
            return scene;
        }

        static void BuildFarmScene(EnemySet enemies, Pickup[] pickups, SeedGrenade seed)
        {
            var scene = NewScene();
            var env = BuildEnvironment();

            var surface = env.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~((1 << IgnoreRaycastLayer) | (1 << EnemyLayer));

            AddGrass(env.transform);
            var (health, inventory) = BuildPlayer(seed);

            var arenaGo = new GameObject("Arena");
            var arena = arenaGo.AddComponent<Arena>();
            arena.halfExtents = new Vector2(FenceHalf - 2f, FenceHalf - 2f);
            arena.exclusionZones = new[] { new Bounds(BarnCenter + Vector3.up * 2f, BarnSize + new Vector3(2f, 0f, 2f)) };

            var waves = arenaGo.AddComponent<EnemySpawner>();
            waves.arena = arena;
            waves.enemies = new[]
            {
                new EnemySpawner.Entry { prefab = enemies.carrot, weight = 5f, fromWave = 1 },
                new EnemySpawner.Entry { prefab = enemies.eggplant, weight = 3f, fromWave = 2 },
                new EnemySpawner.Entry { prefab = enemies.pickle, weight = 2f, fromWave = 3 },
                new EnemySpawner.Entry { prefab = enemies.pumpkin, weight = 1.2f, fromWave = 4 },
            };
            waves.bossPrefab = enemies.king;
            waves.bossEvery = 5;

            var pickupSpawner = arenaGo.AddComponent<PickupSpawner>();
            pickupSpawner.arena = arena;
            pickupSpawner.pickups = new[]
            {
                new PickupSpawner.Entry { prefab = pickups[0], weight = 3f },
                new PickupSpawner.Entry { prefab = pickups[1], weight = 2f },
                new PickupSpawner.Entry { prefab = pickups[2], weight = 2f },
                new PickupSpawner.Entry { prefab = pickups[3], weight = 1.6f },
            };

            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.player = health;
            gm.weapons = inventory;
            gm.waves = waves;
            gm.navMeshSurface = surface;

            AddVolume(gmGo.transform, out var volume);
            var post = gmGo.AddComponent<PostFXController>();
            post.volume = volume;
            post.player = health;
            post.weapons = inventory;

            var uiGo = new GameObject("UI");
            var ui = uiGo.AddComponent<GameUI>();
            ui.game = gm;
            ui.player = health;
            ui.weapons = inventory;
            ui.waves = waves;
            gm.ui = ui;

            EditorSceneManager.SaveScene(scene, FarmScenePath);
        }

        static void BuildMenuScene()
        {
            var scene = NewScene();
            var env = BuildEnvironment();
            AddGrass(env.transform);
            AddVolume(env.transform, out _);

            var camGo = new GameObject("MenuCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<MenuCameraOrbit>();
            SetupCamera(cam);

            // A few frozen mutants posing for the title screen.
            var poses = new (string prefab, Vector3 pos)[]
            {
                ("Boss_PumpkinKing", new Vector3(0f, 0f, -1f)), ("Enemy_Carrot", new Vector3(-4f, 0f, -2f)),
                ("Enemy_Carrot", new Vector3(4.5f, 0f, -1f)), ("Enemy_Eggplant", new Vector3(2f, 0f, -6f)),
                ("Enemy_Eggplant", new Vector3(-2.5f, 0f, -6.5f)), ("Enemy_Pickle", new Vector3(6f, 0f, -4f)),
                ("Enemy_Pumpkin", new Vector3(-6.5f, 0f, -4.5f)),
            };
            var decor = new GameObject("MenuMutants").transform;
            foreach (var (prefabName, pos) in poses)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefabName}.prefab");
                var go = Object.Instantiate(asset, pos, Quaternion.Euler(0f, 180f + pos.x * 6f, 0f), decor);
                go.name = prefabName;
                Object.DestroyImmediate(go.GetComponent<EnemyPlant>());
                Object.DestroyImmediate(go.GetComponent<NavMeshAgent>());
            }

            new GameObject("MainMenu").AddComponent<MainMenuUI>();
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }
    }
}

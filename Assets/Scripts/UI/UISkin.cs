using System.Collections.Generic;
using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Cartoon UI skin (Plants vs. Zombies style): chunky outlined shapes, wooden signs,
    /// seed-packet cards. All sprites are drawn procedurally at startup, so no image assets
    /// are needed. Most sprites are white with a dark outline, so Image.color tints the fill
    /// while the outline stays dark.
    /// </summary>
    public static class UISkin
    {
        // Palette
        public static readonly Color Outline = new Color(0.17f, 0.1f, 0.05f);
        public static readonly Color Grass = new Color(0.42f, 0.78f, 0.18f);
        public static readonly Color GrassDark = new Color(0.25f, 0.55f, 0.1f);
        public static readonly Color Sun = new Color(1f, 0.86f, 0.2f);
        public static readonly Color Cream = new Color(1f, 0.95f, 0.78f);
        public static readonly Color Wood = new Color(0.62f, 0.4f, 0.2f);
        public static readonly Color Red = new Color(0.9f, 0.2f, 0.12f);
        public static readonly Color Orange = new Color(1f, 0.55f, 0.1f);
        public static readonly Color Sky = new Color(0.4f, 0.75f, 1f);

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Font font;
        static bool customFont;

        /// <summary>True when a font was found at Resources/Fonts/UIFont (it is then used as-is, without fake bold).</summary>
        public static bool HasCustomFont { get { _ = Font; return customFont; } }

        /// <summary>
        /// Rounded, chunky font. Drop a .ttf at Resources/Fonts/UIFont to use your own
        /// (e.g. a free cartoon font); otherwise a rounded OS font is picked.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (font != null) return font;
                font = Resources.Load<Font>("Fonts/UIFont");
                customFont = font != null;
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[]
                    {
                        "Arial Rounded MT Bold", "Chalkboard SE", "Comic Sans MS", "Segoe UI Black", "Arial Black", "Arial",
                    }, 32);
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        // ---------- Public sprites ----------

        /// <summary>White rounded box with dark outline and a glossy top. Tint with Image.color.</summary>
        public static Sprite Rounded => Get("rounded", () => RoundedBox(96, 96, 30, 7, true, null));
        /// <summary>Thinner variant for small elements (bars, slots).</summary>
        public static Sprite RoundedSmall => Get("roundedSmall", () => RoundedBox(64, 64, 18, 5, true, null));
        /// <summary>Flat rounded box, no gloss (bar tracks, fills).</summary>
        public static Sprite RoundedFlat => Get("roundedFlat", () => RoundedBox(64, 64, 18, 0, false, null));
        public static Sprite WoodPanel => Get("wood", () => RoundedBox(160, 160, 36, 8, false, WoodPixel, nails: true));
        public static Sprite Card => Get("card", () => SeedPacket(96, 128));
        public static Sprite Circle => Get("circle", () => Shape(96, 6, (x, y) => x * x + y * y - 1f));
        public static Sprite Star => Get("star", () => StarShape(128, 10, 0.55f));
        public static Sprite Heart => Get("heart", () => Shape(96, 6, HeartField, 1.25f));
        public static Sprite SunIcon => Get("sun", () => SunShape(128));
        public static Sprite Check => Get("check", () => CheckShape(64));

        static Sprite Get(string key, System.Func<Sprite> make)
        {
            if (!cache.TryGetValue(key, out var s) || s == null)
            {
                s = make();
                cache[key] = s;
            }
            return s;
        }

        // ---------- High-resolution inventory art ----------

        /// <summary>Large seed-packet card: scalloped green header, paper body, beige label strip. 9-sliced.</summary>
        public static Sprite SeedCard => Get("seedCardHQ", () => SeedCardHQ(240, 320));
        /// <summary>Soft golden glow halo (9-sliced), for the selected card.</summary>
        public static Sprite GlowHalo => Get("glow", () => Halo(160, 34, 40));
        public static Sprite Padlock => Get("padlock", () => Shape(128, 6, PadlockField, 1.05f));
        public static Sprite Plank => Get("plank", () => RoundedBox(192, 64, 18, 6, false, WoodPixel));
        public static Sprite WoodTray => Get("woodTray", () => RoundedBox(256, 256, 54, 10, false, WoodPixel, nails: true));

        static Sprite SeedCardHQ(int w, int h)
        {
            int header = 76, label = 58;
            var sprite = RoundedBox(w, h, 30, 8, false, (x, y) =>
            {
                float wave = Mathf.Sin(x / (float)w * Mathf.PI * 2f * 5f) * 5f;
                float headerEdge = h - header + wave;
                float edgeDist = Mathf.Min(Mathf.Min(x, w - x), Mathf.Min(y, h - y)) / 40f;
                float vignette = Mathf.Lerp(0.86f, 1f, Mathf.Clamp01(edgeDist));
                if (y > headerEdge)
                {
                    float k = (y - headerEdge) / (h - headerEdge);
                    var c = Color.Lerp(GrassDark, Grass * 1.08f, k);
                    if (y > h - 34 && y < h - 22) c = Color.Lerp(c, Color.white, 0.25f); // glossy stripe
                    c *= vignette;
                    c.a = 1f;
                    return c;
                }
                if (y > headerEdge - 4f) return Outline;
                Color result;
                if (y < label)
                {
                    if (y > label - 3) return Outline;
                    result = Color.Lerp(new Color(0.8f, 0.66f, 0.42f), new Color(0.9f, 0.77f, 0.52f), y / (float)label);
                }
                else
                {
                    float paper = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.05f + Mathf.PerlinNoise(x * 0.5f, y * 0.5f) * 0.03f;
                    result = Color.Lerp(new Color(0.97f, 0.9f, 0.7f), Cream, (y - label) / (float)(h - header - label));
                    result = new Color(result.r - paper, result.g - paper, result.b - paper);
                }
                result *= vignette;
                result.a = 1f;
                return result;
            });
            return Sprite.Create(sprite.texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(38, label + 8, 38, header + 10));
        }

        static Sprite Halo(int size, int radius, int falloff)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            var half = new Vector2(size / 2f - falloff, size / 2f - falloff);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f);
                    float d = SdRoundBox(p, half, radius);
                    float a = d <= 0f ? 1f : Mathf.Exp(-d / (falloff * 0.35f));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            int b = radius + falloff;
            return Finish(tex, px, new Vector4(b, b, b, b));
        }

        static float PadlockField(float x, float y)
        {
            var p = new Vector2(x, y);
            float body = Box(p, 0f, -0.28f, 0.58f, 0.42f, 0f, 0.14f);
            float ring = Mathf.Max(Mathf.Abs((p - new Vector2(0f, 0.18f)).magnitude - 0.36f) - 0.1f, 0.18f - p.y);
            float legs = Mathf.Min(Box(p, -0.36f, 0.12f, 0.1f, 0.1f), Box(p, 0.36f, 0.12f, 0.1f, 0.1f));
            float shape = Mathf.Min(body, Mathf.Min(ring, legs));
            float keyhole = Mathf.Min(Disc(p, 0f, -0.22f, 0.1f), Box(p, 0f, -0.38f, 0.04f, 0.12f));
            return Mathf.Max(shape, -keyhole);
        }

        /// <summary>Cartoon silhouette for a weapon slot (0 rifle, 1 shotgun, 2 sprayer, 3 launcher).</summary>
        public static Sprite WeaponIcon(int index) => Get("weapon" + index, () => Shape(128, 6, (x, y) => WeaponField(index, new Vector2(x, y)), 1.05f));

        static float Box(Vector2 p, float cx, float cy, float hx, float hy, float angle = 0f, float r = 0.04f)
        {
            p -= new Vector2(cx, cy);
            if (angle != 0f)
            {
                float a = -angle * Mathf.Deg2Rad;
                p = new Vector2(p.x * Mathf.Cos(a) - p.y * Mathf.Sin(a), p.x * Mathf.Sin(a) + p.y * Mathf.Cos(a));
            }
            return SdRoundBox(p, new Vector2(hx, hy), r);
        }

        static float Disc(Vector2 p, float cx, float cy, float r) => (p - new Vector2(cx, cy)).magnitude - r;

        static float WeaponField(int index, Vector2 p)
        {
            switch (index)
            {
                case 0: // auto rifle: body, barrel, stock, grip, curved magazine, sight
                    return Mathf.Min(Mathf.Min(Mathf.Min(Box(p, -0.05f, 0.05f, 0.36f, 0.13f), Box(p, 0.6f, 0.08f, 0.3f, 0.05f)),
                           Mathf.Min(Box(p, -0.62f, 0.0f, 0.24f, 0.12f, -8f), Box(p, -0.2f, -0.2f, 0.07f, 0.15f, 18f))),
                           Mathf.Min(Mathf.Min(Box(p, 0.13f, -0.25f, 0.08f, 0.2f, -18f), Box(p, -0.05f, 0.24f, 0.1f, 0.06f)), Disc(p, 0.9f, 0.08f, 0.08f)));
                case 1: // shotgun
                    return Mathf.Min(Mathf.Min(Box(p, -0.6f, -0.1f, 0.28f, 0.15f, -12f), Box(p, 0.28f, 0.08f, 0.62f, 0.065f)),
                           Mathf.Min(Box(p, 0.28f, -0.07f, 0.62f, 0.065f), Box(p, 0.22f, -0.2f, 0.2f, 0.07f)));
                case 2: // sprayer
                    return Mathf.Min(Mathf.Min(Disc(p, -0.35f, 0.08f, 0.38f), Box(p, 0.4f, 0.14f, 0.45f, 0.055f)),
                           Mathf.Min(Box(p, -0.32f, -0.42f, 0.08f, 0.16f), Disc(p, 0.85f, 0.14f, 0.1f)));
                default: // seed launcher
                    return Mathf.Min(Mathf.Min(Box(p, 0.12f, 0.1f, 0.66f, 0.18f, 0f, 0.12f), Box(p, 0.76f, 0.1f, 0.07f, 0.24f)),
                           Mathf.Min(Disc(p, -0.28f, -0.16f, 0.22f), Box(p, -0.5f, -0.36f, 0.08f, 0.18f, 12f)));
            }
        }

        // ---------- Generators ----------

        static float SdRoundBox(Vector2 p, Vector2 half, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(r, r);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static Sprite RoundedBox(int w, int h, int radius, int outline, bool gloss, System.Func<int, int, Color> fill, bool nails = false)
        {
            var tex = NewTex(w, h);
            var px = new Color[w * h];
            var half = new Vector2(w / 2f - 1f, h / 2f - 1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f - w / 2f, y + 0.5f - h / 2f);
                    float d = SdRoundBox(p, half, radius);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) { px[y * w + x] = Color.clear; continue; }

                    float v = y / (float)(h - 1);
                    Color c = fill != null ? fill(x, y) : Color.Lerp(new Color(0.78f, 0.78f, 0.78f), Color.white, Mathf.SmoothStep(0f, 1f, v * 1.4f));
                    if (gloss)
                    {
                        // Soft highlight band near the top.
                        float g = SdRoundBox(p - new Vector2(0f, h * 0.22f), new Vector2(half.x - outline - 6f, h * 0.16f), radius * 0.6f);
                        c = Color.Lerp(c, Color.white, Mathf.Clamp01(0.5f - g) * 0.55f);
                    }
                    if (nails)
                    {
                        float nr = 5f;
                        foreach (var n in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                        {
                            var np = new Vector2(n.x * (half.x - radius + 6f), n.y * (half.y - radius + 6f));
                            float nd = (p - np).magnitude - nr;
                            if (nd < 0f) c = Color.Lerp(new Color(0.35f, 0.35f, 0.38f), new Color(0.75f, 0.75f, 0.78f), Mathf.Clamp01(-(p - np - new Vector2(-1.5f, 1.5f)).magnitude / nr + 0.6f));
                            else if (nd < 1.5f) c = Color.Lerp(Outline, c, nd / 1.5f);
                        }
                    }
                    if (outline > 0)
                    {
                        float k = Mathf.Clamp01(d + outline + 0.5f);
                        c = Color.Lerp(c, Outline, k);
                    }
                    c.a = alpha;
                    px[y * w + x] = c;
                }
            int border = radius + outline;
            return Finish(tex, px, new Vector4(border, border, border, border));
        }

        static Color WoodPixel(int x, int y)
        {
            int plank = y / 26;
            float line = (y % 26) < 3 ? 0.62f : 1f;
            float grain = Mathf.PerlinNoise(x * 0.04f + plank * 7.3f, y * 0.35f) * 0.25f + Mathf.PerlinNoise(x * 0.2f, plank * 3.1f) * 0.1f;
            float tone = 0.85f + Mathf.Repeat(plank * 0.37f, 1f) * 0.2f;
            var c = Color.Lerp(new Color(0.5f, 0.3f, 0.14f), new Color(0.78f, 0.53f, 0.28f), grain + 0.35f);
            return c * tone * line;
        }

        static Sprite SeedPacket(int w, int h)
        {
            int band = 34;
            var sprite = RoundedBox(w, h, 16, 6, false, (x, y) =>
            {
                bool top = y > h - band;
                var c = top ? Color.Lerp(GrassDark, Grass, (y - (h - band)) / (float)band) : Color.Lerp(new Color(0.93f, 0.85f, 0.62f), Cream, y / (float)h);
                if (Mathf.Abs(y - (h - band)) < 2) c = Outline;
                return c;
            });
            // Re-slice with a tall top border so the green band never stretches.
            var tex = sprite.texture;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(22, 22, 22, band + 6));
        }

        /// <summary>Generic shape from an implicit field f(x,y) (negative inside), coordinates in [-1,1]*scale.</summary>
        static Sprite Shape(int size, int outline, System.Func<float, float, float> field, float scale = 1.1f)
        {
            var tex = NewTex(size, size);
            var px = new Color[size * size];
            float step = 2f * scale / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = (x + 0.5f) * step - scale, fy = (y + 0.5f) * step - scale;
                    // Approximate distance in pixels from the field value and its gradient.
                    float f = field(fx, fy);
                    float gx = (field(fx + step, fy) - field(fx - step, fy)) / (2f * step);
                    float gy = (field(fx, fy + step) - field(fx, fy - step)) / (2f * step);
                    float g = Mathf.Max(0.0001f, Mathf.Sqrt(gx * gx + gy * gy));
                    float d = f / g / step;
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) { px[y * size + x] = Color.clear; continue; }
                    var c = Color.Lerp(new Color(0.8f, 0.8f, 0.8f), Color.white, y / (float)size + 0.2f);
                    // Little shine dot in the upper-left.
                    float sh = new Vector2(fx + 0.35f * scale, fy - 0.4f * scale).magnitude;
                    c = Color.Lerp(c, Color.white, Mathf.Clamp01(1f - sh / (0.22f * scale)) * 0.8f);
                    c = Color.Lerp(c, Outline, Mathf.Clamp01(d + outline + 0.5f));
                    c.a = alpha;
                    px[y * size + x] = c;
                }
            return Finish(tex, px, Vector4.zero);
        }

        static float HeartField(float x, float y)
        {
            y = y * 1.15f + 0.25f;
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y;
        }

        static Sprite StarShape(int size, int points, float inner)
        {
            return Shape(size, 6, (x, y) =>
            {
                float ang = Mathf.Atan2(y, x) + Mathf.PI / 2f;
                float seg = Mathf.PI * 2f / points;
                float a = Mathf.Repeat(ang, seg) / seg; // 0..1 inside one spike
                float r = Mathf.Lerp(1f, inner, Mathf.Abs(a - 0.5f) * 2f);
                return new Vector2(x, y).magnitude - r * 0.95f;
            });
        }

        static Sprite SunShape(int size)
        {
            return Shape(size, 6, (x, y) =>
            {
                float r = new Vector2(x, y).magnitude;
                float ang = Mathf.Atan2(y, x);
                float rays = 0.78f + 0.2f * Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 6f)), 6f);
                return r - Mathf.Max(0.62f, rays);
            });
        }

        static Sprite CheckShape(int size)
        {
            return Shape(size, 5, (x, y) =>
            {
                float d1 = SegDist(new Vector2(x, y), new Vector2(-0.6f, 0f), new Vector2(-0.15f, -0.5f));
                float d2 = SegDist(new Vector2(x, y), new Vector2(-0.15f, -0.5f), new Vector2(0.65f, 0.55f));
                return Mathf.Min(d1, d2) - 0.17f;
            });
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }

        static Sprite Finish(Texture2D tex, Color[] px, Vector4 border)
        {
            tex.SetPixels(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.hideFlags = HideFlags.DontSave;
            return s;
        }
    }
}

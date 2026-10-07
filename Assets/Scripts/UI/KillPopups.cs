using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MutantPlants
{
    /// <summary>
    /// Kill feedback, in two layers:
    /// 1. A bouncy cartoon "+20" that pops out of the splatted plant (Plants vs. Zombies style),
    ///    with a combo star badge.
    /// 2. A running total and kill list next to the crosshair (modern Call of Duty style).
    /// </summary>
    public class KillPopups
    {
        readonly RectTransform root;
        readonly Canvas canvas;
        readonly Camera cam;

        class Pop
        {
            public RectTransform rt;
            public Text text;
            public RectTransform badge;
            public Vector3 world;
            public Vector2 jitter;
            public float age, life, rot, size;
        }
        readonly List<Pop> pops = new List<Pop>();

        // Crosshair feed
        readonly RectTransform feed;
        readonly Text totalText;
        int total;
        float totalTimer, totalPunch;

        class Line { public Text text; public float age; public float y; }
        readonly List<Line> lines = new List<Line>();

        const float FeedTotalTime = 1.8f;
        const float LineLife = 2.4f;
        const float LineHeight = 30f;

        public KillPopups(RectTransform hudRoot, Canvas canvas, Camera cam)
        {
            this.canvas = canvas;
            this.cam = cam;
            root = UIFactory.Stretch(hudRoot, "KillPopups");

            feed = UIFactory.Rect(hudRoot, "KillFeed", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(70f, 10f), new Vector2(420f, 220f));
            var totalRt = UIFactory.Rect(feed, "Total", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -26f), new Vector2(240f, 56f));
            totalText = UIFactory.Text(totalRt, "", 46, TextAnchor.MiddleLeft, UISkin.Sun, 3.5f);
            totalText.enabled = false;
        }

        public void OnKill(EnemyPlant enemy, int basePoints, int multiplier)
        {
            int points = basePoints * multiplier;
            SpawnPop(enemy, points, multiplier);

            // Running total (resets after a short pause in the killing).
            total = totalTimer > 0f ? total + points : points;
            totalTimer = FeedTotalTime;
            totalPunch = 1f;
            totalText.text = "+" + total;

            string label = enemy.IsBoss ? L.T("bossKilled") : L.T("kill", L.T(enemy.nameKey));
            AddLine($"{label}   <color=#FFDD33>+{points}</color>", enemy.IsBoss ? UISkin.Orange : Color.white);
        }

        void SpawnPop(EnemyPlant enemy, int points, int multiplier)
        {
            bool boss = enemy.IsBoss;
            float size = boss ? 96f : multiplier > 1 ? 64f : 56f;
            var rt = UIFactory.Rect(root, "Pop", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 120f));
            Color color = boss ? UISkin.Orange : multiplier > 1 ? UISkin.Sun : Color.white;
            var text = UIFactory.Text(rt, "+" + points, (int)size, TextAnchor.MiddleCenter, color, boss ? 6f : 4f);

            RectTransform badge = null;
            if (multiplier > 1)
            {
                badge = UIFactory.At(rt, "Combo", new Vector2(0.5f, 0.5f), new Vector2(size * 1.25f, size * 0.45f), new Vector2(72f, 72f));
                var star = UIFactory.Image(badge, UISkin.Red);
                star.sprite = UISkin.Star;
                star.raycastTarget = false;
                UIFactory.Text(badge, "x" + multiplier, 28, TextAnchor.MiddleCenter, Color.white, 2.5f);
            }

            pops.Add(new Pop
            {
                rt = rt,
                text = text,
                badge = badge,
                world = enemy.transform.position + Vector3.up * (boss ? 4.5f : 1.8f),
                jitter = new Vector2(Random.Range(-30f, 30f), Random.Range(-10f, 10f)),
                life = boss ? 2.2f : 1.25f,
                rot = Random.Range(-14f, 14f),
                size = size,
            });
        }

        void AddLine(string message, Color color)
        {
            var rt = UIFactory.Rect(feed, "Line", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -70f), new Vector2(420f, LineHeight));
            var text = UIFactory.Text(rt, message, 22, TextAnchor.MiddleLeft, color, 2f);
            text.supportRichText = true;
            foreach (var l in lines) l.y -= LineHeight;
            lines.Insert(0, new Line { text = text, y = -70f });
            while (lines.Count > 5)
            {
                Object.Destroy(lines[lines.Count - 1].text.transform.parent.gameObject);
                lines.RemoveAt(lines.Count - 1);
            }
        }

        public void Update(float dt)
        {
            UpdatePops(dt);
            UpdateFeed(dt);
        }

        void UpdatePops(float dt)
        {
            float scale = canvas.scaleFactor;
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i];
                p.age += dt;
                float k = p.age / p.life;
                Vector3 sp = cam.WorldToScreenPoint(p.world);
                bool visible = sp.z > 0f;
                p.rt.gameObject.SetActive(visible);
                if (visible)
                {
                    float rise = (1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f)) * 90f;
                    p.rt.anchoredPosition = new Vector2(sp.x, sp.y) / scale + p.jitter + Vector2.up * rise;
                    float pop = PopIn.BackOut(Mathf.Clamp01(p.age / 0.22f), 3f);
                    float squash = 1f + Mathf.Sin(p.age * 25f) * Mathf.Exp(-p.age * 8f) * 0.15f;
                    p.rt.localScale = new Vector3(pop * squash, pop / squash, 1f);
                    p.rt.localRotation = Quaternion.Euler(0f, 0f, p.rot * Mathf.Exp(-p.age * 5f));
                    float alpha = Mathf.Clamp01((1f - k) / 0.3f);
                    SetAlpha(p.rt, alpha);
                    if (p.badge != null) p.badge.localRotation = Quaternion.Euler(0f, 0f, p.age * 120f);
                }
                if (k >= 1f)
                {
                    Object.Destroy(p.rt.gameObject);
                    pops.RemoveAt(i);
                }
            }
        }

        void UpdateFeed(float dt)
        {
            totalTimer -= dt;
            totalPunch = Mathf.MoveTowards(totalPunch, 0f, dt * 5f);
            totalText.enabled = totalTimer > 0f;
            if (totalText.enabled)
            {
                totalText.transform.localScale = Vector3.one * (1f + PopIn.BackOut(totalPunch) * 0.35f * totalPunch);
                var c = totalText.color;
                c.a = Mathf.Clamp01(totalTimer / 0.4f);
                totalText.color = c;
            }

            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var l = lines[i];
                l.age += dt;
                var rt = (RectTransform)l.text.transform.parent;
                float slide = 1f - PopIn.BackOut(Mathf.Clamp01(l.age / 0.25f));
                rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, new Vector2(slide * 40f, l.y), dt * 18f);
                var c = l.text.color;
                c.a = Mathf.Clamp01((LineLife - l.age) / 0.5f);
                l.text.color = c;
                if (l.age > LineLife)
                {
                    Object.Destroy(rt.gameObject);
                    lines.RemoveAt(i);
                }
            }
        }

        static void SetAlpha(RectTransform rt, float alpha)
        {
            var group = rt.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.blocksRaycasts = false;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MutantPlants
{
    /// <summary>
    /// The weapon bar in the bottom-right corner: a wooden tray of seed-packet cards, each
    /// showing a rendered picture of the real weapon, its key, name and fire mode.
    /// The selected card lifts and glows; slots of weapons you don't have yet stay empty,
    /// and newly unlocked cards flip in with a star burst.
    /// </summary>
    public class InventoryBar
    {
        const float CardW = 140f, CardH = 180f, Gap = 16f;
        static readonly float[] FanAngles = { -5f, -1.8f, 1.8f, 5f, 7f, 9f };

        class Card
        {
            public RectTransform rt;
            public Image card, glow;
            public RawImage icon;
            public Text name, tag;
            public Vector2 home;
            public float fan, flip = 1f, flipAge = 10f, wiggle;
            public bool unlocked;
        }

        class Spark { public RectTransform rt; public Vector2 vel; public float age, spin; }

        readonly WeaponInventory weapons;
        readonly RectTransform container;
        readonly Card[] cards;
        readonly List<Spark> sparks = new List<Spark>();
        bool primed;

        public InventoryBar(Transform hud, WeaponInventory weapons, MonoBehaviour host)
        {
            this.weapons = weapons;
            int n = weapons.weapons.Length;
            float width = n * (CardW + Gap) - Gap;

            // Wooden tray the cards sit in
            var tray = UIFactory.At(hud, "WeaponTray", new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(width + 56f, 108f));
            UIFactory.Sliced(tray, UISkin.WoodTray, Color.white, 2.2f, true).raycastTarget = false;
            var lip = UIFactory.Rect(tray, "Lip", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-30f, 12f));
            UIFactory.Image(lip, new Color(0f, 0f, 0f, 0.25f)).sprite = UISkin.RoundedFlat;

            container = UIFactory.At(hud, "WeaponCards", new Vector2(1f, 0f), new Vector2(-48f, 40f), new Vector2(width, CardH));
            cards = new Card[n];
            for (int i = 0; i < n; i++) cards[i] = BuildCard(i);

            host.StartCoroutine(WeaponIconStudio.Render(weapons, textures =>
            {
                for (int i = 0; i < cards.Length; i++)
                    if (textures[i] != null)
                    {
                        cards[i].icon.texture = textures[i];
                        cards[i].icon.enabled = true;
                    }
            }));
        }

        Card BuildCard(int i)
        {
            var w = weapons.weapons[i];
            var c = new Card();
            c.home = new Vector2(i * (CardW + Gap) + CardW / 2f, 0f);
            c.fan = FanAngles[Mathf.Min(i, FanAngles.Length - 1)];

            c.rt = UIFactory.Rect(container, "Card" + (i + 1), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), c.home, new Vector2(CardW, CardH));

            var glowRt = UIFactory.Rect(c.rt, "Glow", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
            c.glow = UIFactory.Sliced(glowRt, UISkin.GlowHalo, UISkin.Sun, 1f);
            c.glow.raycastTarget = false;

            var shadow = UIFactory.Rect(c.rt, "Shadow", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(4f, -6f), Vector2.zero);
            UIFactory.Sliced(shadow, UISkin.SeedCard, new Color(0f, 0f, 0f, 0.35f), 2f).raycastTarget = false;

            c.card = UIFactory.Sliced(UIFactory.Stretch(c.rt, "Card"), UISkin.SeedCard, Color.white, 2f);
            c.card.raycastTarget = false;

            // Weapon picture (filled in once the icon studio has rendered it)
            var iconRt = UIFactory.At(c.rt, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(160f, 80f));
            c.icon = iconRt.gameObject.AddComponent<RawImage>();
            c.icon.raycastTarget = false;
            c.icon.enabled = false;
            iconRt.localRotation = Quaternion.Euler(0f, 0f, 4f);

            // Key badge (sun coin) and fire-mode tag in the green header
            var coin = UIFactory.At(c.rt, "Key", new Vector2(0f, 1f), new Vector2(-12f, 12f), new Vector2(50f, 50f));
            UIFactory.Image(coin, UISkin.Sun).sprite = UISkin.Circle;
            UIFactory.Text(coin, (i + 1).ToString(), 28, TextAnchor.MiddleCenter, UISkin.Outline, 0f);

            var tagRt = UIFactory.At(c.rt, "Tag", new Vector2(1f, 1f), new Vector2(-10f, -9f), new Vector2(76f, 24f));
            UIFactory.Sliced(tagRt, UISkin.RoundedFlat, new Color(0.1f, 0.25f, 0.05f, 0.65f), 3f).raycastTarget = false;
            c.tag = UIFactory.Text(tagRt, FireModeTag(w), 15, TextAnchor.MiddleCenter, Color.white, 1.5f);

            // Name on a little wooden plank
            var plank = UIFactory.Rect(c.rt, "Plank", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(-16f, 38f));
            UIFactory.Sliced(plank, UISkin.Plank, Color.white, 2.2f, true).raycastTarget = false;
            c.name = UIFactory.Text(plank, "", 17, TextAnchor.MiddleCenter, Color.white, 2f);
            c.name.horizontalOverflow = HorizontalWrapMode.Wrap;
            c.name.resizeTextForBestFit = true;
            c.name.resizeTextMinSize = 11;
            c.name.resizeTextMaxSize = 17;
            c.name.rectTransform.offsetMin = new Vector2(8f, 0f);
            c.name.rectTransform.offsetMax = new Vector2(-8f, 0f);

            return c;
        }

        static string FireModeTag(WeaponData w)
        {
            if (w.projectile != null) return L.T("tagBoom");
            if (w.pellets > 1) return L.T("tagSpread");
            return w.automatic ? L.T("tagAuto") : L.T("tagSemi");
        }

        /// <summary>Called whenever the inventory changes (and once from GameUI.Start).</summary>
        public void Refresh()
        {
            // The first refresh only records the starting state, without unlock animations.
            if (!primed)
            {
                primed = true;
                for (int i = 0; i < cards.Length; i++) cards[i].unlocked = weapons.IsUnlocked(i);
            }
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                bool unlocked = weapons.IsUnlocked(i);
                if (unlocked && !c.unlocked) PlayUnlock(i);
                c.unlocked = unlocked;
                c.name.text = weapons.weapons[i].DisplayName.ToUpper();
                c.rt.gameObject.SetActive(unlocked); // empty slot until the weapon is found
            }
        }

        void PlayUnlock(int i)
        {
            var c = cards[i];
            c.flip = 0f;
            c.flipAge = 0f;
            for (int k = 0; k < 10; k++)
            {
                var rt = UIFactory.At(container, "Spark", Vector2.zero, c.home + new Vector2(0f, CardH * 0.55f), Vector2.one * Random.Range(22f, 38f));
                var img = UIFactory.Image(rt, k % 2 == 0 ? UISkin.Sun : Color.white);
                img.sprite = UISkin.Star;
                img.raycastTarget = false;
                float a = k / 10f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                sparks.Add(new Spark { rt = rt, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(220f, 380f), spin = Random.Range(-360f, 360f) });
            }
        }

        public void Update(float dt)
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                bool selected = i == weapons.CurrentIndex;

                // Flip-in after unlocking
                c.flipAge += dt;
                c.flip = Mathf.Clamp01(PopIn.BackOut(Mathf.Clamp01(c.flipAge / 0.5f), 2.5f));

                var target = c.home + (selected ? new Vector2(0f, 26f) : new Vector2(0f, Mathf.Abs(c.fan) * -0.8f));
                c.rt.anchoredPosition = Vector2.Lerp(c.rt.anchoredPosition, target, dt * 14f);
                float rot = selected ? Mathf.Sin(time * 2.2f) * 1.5f : c.fan;
                c.rt.localRotation = Quaternion.Slerp(c.rt.localRotation, Quaternion.Euler(0f, 0f, -rot), dt * 12f);
                float scale = Mathf.Lerp(c.rt.localScale.y, selected ? 1.1f : 0.96f, dt * 14f);
                c.rt.localScale = new Vector3(scale * Mathf.Max(0.02f, c.flip), scale, 1f);

                c.glow.enabled = selected;
                if (selected) c.glow.color = new Color(1f, 0.85f, 0.25f, 0.7f + Mathf.Sin(time * 5f) * 0.25f);
                var tint = selected ? Color.white : new Color(0.9f, 0.9f, 0.88f);
                c.card.color = Color.Lerp(c.card.color, tint, dt * 10f);

                // Selected weapon picture bobs gently
                c.icon.rectTransform.anchoredPosition = new Vector2(0f, 6f + (selected ? Mathf.Sin(time * 3f) * 3f : 0f));
            }

            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.age += dt;
                s.vel += Vector2.down * 500f * dt;
                s.rt.anchoredPosition += s.vel * dt;
                s.rt.localRotation = Quaternion.Euler(0f, 0f, s.spin * s.age);
                float k = s.age / 0.8f;
                s.rt.localScale = Vector3.one * (1f - k);
                if (k >= 1f)
                {
                    Object.Destroy(s.rt.gameObject);
                    sparks.RemoveAt(i);
                }
            }
        }
    }
}

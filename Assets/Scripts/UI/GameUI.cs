using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MutantPlants
{
    /// <summary>
    /// In-game UI in a cartoon (Plants vs. Zombies inspired) style: sun-bank score counter,
    /// heart health bar, seed-packet weapon slots, combo star, boss bar, pop-in banners,
    /// kill popups, damage indicators, pause menu, settings and dead/win screens.
    /// Built from code at startup.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public GameManager game;
        public PlayerHealth player;
        public WeaponInventory weapons;
        public EnemySpawner waves;

        Canvas canvas;
        RectTransform hudRoot;
        GameObject hud, pauseMenu, settingsMenu, deadScreen, winScreen;
        Image healthFill, healthTrail, progressFill, comboFill, bossFill, heart;
        Text healthText, scoreText, scoreTargetText, messageText, buffText, waveText, bannerText, comboText, hintText;
        Text deadStatsText, winStatsText, deadBestText, winBestText, deadBoardText, winBoardText;
        RectTransform sunIcon, comboRoot, bannerRt, messageRt, hintBubble, bossBar;
        GameObject hitMarker;
        RectTransform[] crossBars;
        Slot[] slots;
        Button loadCheckpointButton;
        KillPopups killPopups;
        EnemyPlant boss;

        float messageAge = 10f, bannerAge = 10f, bannerLife, hitMarkerTimer, healthTrailValue = 1f, scorePunch, comboPunch;
        bool bannerShake;
        int shownCombo = 1;

        class Slot { public RectTransform rt; public Image card, glow, icon; public Text name, number; public Vector2 home; }
        class DamageIndicator { public Image image; public Vector3 source; public float time; }
        readonly List<DamageIndicator> indicators = new List<DamageIndicator>();

        static readonly Color[] WeaponColors =
        {
            new Color(0.55f, 0.36f, 0.2f), new Color(0.45f, 0.45f, 0.5f), new Color(0.3f, 0.65f, 0.95f), new Color(0.3f, 0.7f, 0.25f),
        };

        static Vector2 Center => new Vector2(0.5f, 0.5f);

        void Awake()
        {
            GameSettings.EnsureLoaded();
            canvas = UIFactory.Canvas("GameCanvas");
            canvas.transform.SetParent(transform, false);
            BuildHud();
            BuildPauseMenu();
            settingsMenu = SettingsPanel.Build(canvas.transform, () => { settingsMenu.SetActive(false); pauseMenu.SetActive(true); });
            deadScreen = BuildEndScreen("DeadScreen", L.T("dead"), UISkin.Red, new Color(0.25f, 0f, 0f, 0.6f), true,
                out deadStatsText, out deadBestText, out deadBoardText);
            winScreen = BuildEndScreen("WinScreen", L.T("won"), UISkin.Sun, new Color(0f, 0.15f, 0f, 0.55f), false,
                out winStatsText, out winBestText, out winBoardText);

            game.ScoreChanged += OnScoreChanged;
            game.StateChanged += OnStateChanged;
            game.PointsAwarded += (enemy, pts, mult) => { killPopups.OnKill(enemy, pts, mult); scorePunch = 1f; };
            player.HealthChanged += OnHealthChanged;
            player.Damaged += OnDamaged;
            weapons.InventoryChanged += RefreshInventory;
            weapons.EnemyHit += () => hitMarkerTimer = 0.12f;
            weapons.WeaponUnlocked += w => ShowMessage(L.T("pickupWeapon", w.DisplayName.ToUpper()), UISkin.Sky);
            if (waves != null)
            {
                waves.WaveStarted += OnWaveStarted;
                waves.WaveCleared += _ => { ShowBanner(L.T("waveCleared"), UISkin.Sun, 2.2f); SoundFX.Play(SoundFX.Sfx.Checkpoint, 0.7f); };
            }
            EnemyPlant.BossSpawned += OnBossSpawned;
        }

        void OnDestroy() => EnemyPlant.BossSpawned -= OnBossSpawned;

        void Start()
        {
            RefreshInventory();
            OnHealthChanged(player.Current, player.maxHealth);
            StartCoroutine(Tutorial());
        }

        System.Collections.IEnumerator Tutorial()
        {
            hintBubble.gameObject.SetActive(false);
            if (SaveSystem.TopScores.Count > 2) yield break; // experienced players skip it
            yield return new WaitForSeconds(1f);
            foreach (var k in new[] { "tut1", "tut2", "tut3" })
            {
                hintText.text = L.T(k);
                hintBubble.gameObject.SetActive(true);
                hintBubble.GetComponent<PopIn>().Replay();
                yield return new WaitForSeconds(4.5f);
            }
            hintBubble.gameObject.SetActive(false);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Pickup / event message: pop, hold, fade.
            messageAge += dt;
            messageRt.localScale = Vector3.one * PopIn.BackOut(Mathf.Clamp01(messageAge / 0.3f), 3f);
            SetAlpha(messageText, Mathf.Clamp01((2.2f - messageAge) / 0.4f));

            // Banner (waves): elastic in, optional shake, fade.
            bannerAge += dt;
            bannerRt.localScale = Vector3.one * PopIn.ElasticOut(Mathf.Clamp01(bannerAge / 0.6f));
            float shake = bannerShake ? Mathf.Exp(-bannerAge * 2f) * 8f : 0f;
            bannerRt.anchoredPosition = new Vector2(Random.Range(-shake, shake), 230f + Random.Range(-shake, shake));
            bannerRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(bannerAge * 3f) * 2f);
            SetAlpha(bannerText, Mathf.Clamp01((bannerLife - bannerAge) / 0.4f));

            hitMarkerTimer -= Time.deltaTime;
            hitMarker.SetActive(hitMarkerTimer > 0f);

            // Health trail lags behind the real value; heart beats faster when low.
            healthTrailValue = Mathf.MoveTowards(healthTrailValue, healthFill.fillAmount, dt * 0.5f);
            if (healthTrailValue < healthFill.fillAmount) healthTrailValue = healthFill.fillAmount;
            healthTrail.fillAmount = healthTrailValue;
            float hp01 = healthFill.fillAmount;
            float beatSpeed = Mathf.Lerp(12f, 4f, hp01);
            float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.unscaledTime * beatSpeed * 0.5f)), 8f) * Mathf.Lerp(0.25f, 0.06f, hp01);
            heart.rectTransform.localScale = Vector3.one * (1f + beat);

            // Sun icon spins; score text punches on kills.
            sunIcon.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 25f);
            scorePunch = Mathf.MoveTowards(scorePunch, 0f, dt * 4f);
            scoreText.transform.localScale = Vector3.one * (1f + PopIn.BackOut(scorePunch) * 0.2f * scorePunch);
            sunIcon.localScale = Vector3.one * (1f + scorePunch * 0.2f);

            // Dynamic crosshair
            float gap = 10f + weapons.Bloom * 18f + weapons.Current.spread * 2.5f;
            crossBars[0].anchoredPosition = new Vector2(0f, gap);
            crossBars[1].anchoredPosition = new Vector2(0f, -gap);
            crossBars[2].anchoredPosition = new Vector2(gap, 0f);
            crossBars[3].anchoredPosition = new Vector2(-gap, 0f);

            // Buffs
            var buffs = "";
            if (weapons.DamageBoostTimeLeft > 0f) buffs += L.T("dmgBoost", Mathf.CeilToInt(weapons.DamageBoostTimeLeft)) + "\n";
            if (weapons.RapidFireTimeLeft > 0f) buffs += L.T("rapidFire", Mathf.CeilToInt(weapons.RapidFireTimeLeft));
            buffText.text = buffs;

            UpdateCombo(dt);
            UpdateSlots(dt);

            // Waves
            if (waves != null)
            {
                waveText.text = waves.Wave > 0 ? L.T("wave", waves.Wave) + "  •  " + L.T("enemiesLeft", waves.EnemiesRemaining) : "";
                if (waves.InBreak && game.State == GameState.Playing && waves.BreakTimeLeft < 5f && waves.BreakTimeLeft > 0f && bannerAge > bannerLife)
                {
                    bannerText.text = L.T("nextWave", Mathf.CeilToInt(waves.BreakTimeLeft));
                    bannerText.color = Color.white;
                    bannerRt.localScale = Vector3.one;
                    SetAlpha(bannerText, 1f);
                }
            }

            bossBar.gameObject.SetActive(boss != null);
            if (boss != null) bossFill.fillAmount = Mathf.Lerp(bossFill.fillAmount, boss.HealthFraction, dt * 10f);

            killPopups.Update(Time.deltaTime > 0f ? dt : 0f);
            UpdateIndicators();
        }

        void UpdateCombo(float dt)
        {
            bool show = game.Combo > 1;
            comboRoot.gameObject.SetActive(show);
            if (!show) { shownCombo = 1; return; }
            if (game.Combo != shownCombo)
            {
                shownCombo = game.Combo;
                comboPunch = 1f;
                comboText.text = "x" + game.Combo;
            }
            comboPunch = Mathf.MoveTowards(comboPunch, 0f, dt * 3f);
            comboRoot.localScale = Vector3.one * (1f + comboPunch * comboPunch * 0.6f);
            comboRoot.GetChild(0).localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 40f);
            comboFill.fillAmount = game.ComboTimeLeft / game.comboWindow;
        }

        void UpdateSlots(float dt)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                bool selected = i == weapons.CurrentIndex;
                var target = s.home + (selected ? new Vector2(0f, 22f) : Vector2.zero);
                s.rt.anchoredPosition = Vector2.Lerp(s.rt.anchoredPosition, target, dt * 14f);
                float sc = Mathf.Lerp(s.rt.localScale.x, selected ? 1.08f : 1f, dt * 14f);
                s.rt.localScale = new Vector3(sc, sc, 1f);
                s.glow.enabled = selected;
                if (selected) s.glow.color = new Color(1f, 0.9f, 0.3f, 0.65f + Mathf.Sin(Time.unscaledTime * 6f) * 0.25f);
            }
        }

        public void ShowMessage(string message, Color? color = null)
        {
            messageText.text = message;
            messageText.color = color ?? UISkin.Sun;
            messageAge = 0f;
        }

        void ShowBanner(string text, Color color, float life, bool shake = false)
        {
            bannerText.text = text;
            bannerText.color = color;
            bannerAge = 0f;
            bannerLife = life;
            bannerShake = shake;
        }

        void OnWaveStarted(int wave, bool isBoss)
        {
            SoundFX.Play(isBoss ? SoundFX.Sfx.BossRoar : SoundFX.Sfx.WaveStart);
            if (isBoss) ShowBanner(L.T("bossIncoming"), UISkin.Orange, 3f, true);
            else if (wave % 5 == 4) ShowBanner(L.T("hugeWave"), UISkin.Red, 3f, true);
            else ShowBanner(L.T("wave", wave), Color.white, 2.2f);
        }

        void OnBossSpawned(EnemyPlant b)
        {
            boss = b;
            bossFill.fillAmount = 1f;
        }

        // ---------- HUD ----------

        void BuildHud()
        {
            hudRoot = UIFactory.Stretch(canvas.transform, "HUD");
            hud = hudRoot.gameObject;
            var root = hudRoot.transform;

            BuildCrosshair(root);
            killPopups = new KillPopups(hudRoot, canvas, weapons.aimCamera);

            // --- Sun bank: score (top left) ---
            var bank = UIFactory.At(root, "ScoreBank", new Vector2(0f, 1f), new Vector2(70f, -26f), new Vector2(330f, 96f));
            UIFactory.Sliced(bank, UISkin.WoodPanel, Color.white, 1.6f, true);
            sunIcon = UIFactory.Icon(root, "Sun", UISkin.SunIcon, UISkin.Sun, new Vector2(0f, 1f), new Vector2(14f, -14f), 120f).rectTransform;
            var scoreRt = UIFactory.Rect(bank, "Score", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(40f, 10f), new Vector2(-90f, 0f));
            scoreText = UIFactory.Text(scoreRt, "0", 46, TextAnchor.MiddleCenter, Color.white, 3.5f);
            var targetRt = UIFactory.Rect(bank, "Target", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(40f, -26f), new Vector2(-90f, 0f));
            scoreTargetText = UIFactory.Text(targetRt, "", 20, TextAnchor.MiddleCenter, UISkin.Cream, 2f);
            var prog = UIFactory.At(root, "Progress", new Vector2(0f, 1f), new Vector2(84f, -126f), new Vector2(300f, 30f));
            progressFill = UIFactory.Bar(prog, UISkin.Grass, new Color(0.3f, 0.2f, 0.1f));

            var waveRt = UIFactory.At(root, "Wave", new Vector2(0f, 1f), new Vector2(30f, -166f), new Vector2(700f, 40f));
            waveText = UIFactory.Text(waveRt, "", 26, TextAnchor.MiddleLeft, Color.white, 2.5f);
            var hintRt = UIFactory.At(root, "Hint", new Vector2(0f, 1f), new Vector2(30f, -200f), new Vector2(700f, 30f));
            UIFactory.Text(hintRt, L.T("hint"), 18, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.75f), 1.5f);

            // --- Boss bar (top center) ---
            bossBar = UIFactory.At(root, "BossBar", new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(760f, 40f));
            bossFill = UIFactory.Bar(bossBar, UISkin.Orange, new Color(0.25f, 0.1f, 0.05f));
            UIFactory.Icon(bossBar, "PumpkinIcon", UISkin.Circle, UISkin.Orange, new Vector2(0f, 0.5f), new Vector2(-20f, 0f), 74f);
            var bossName = UIFactory.Rect(bossBar, "Name", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(760f, 44f));
            UIFactory.Text(bossName, L.T("bossName"), 32, TextAnchor.LowerCenter, UISkin.Orange, 3f);
            bossBar.gameObject.SetActive(false);

            // --- Combo star (top center, under the boss bar) ---
            comboRoot = UIFactory.At(root, "Combo", new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(130f, 130f));
            var star = UIFactory.Image(UIFactory.Stretch(comboRoot, "Star"), UISkin.Orange);
            star.sprite = UISkin.Star;
            star.raycastTarget = false;
            comboText = UIFactory.Text(comboRoot, "x2", 44, TextAnchor.MiddleCenter, Color.white, 3.5f);
            var comboLabel = UIFactory.Rect(comboRoot, "Label", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(200f, 30f));
            UIFactory.Text(comboLabel, "COMBO", 24, TextAnchor.LowerCenter, UISkin.Sun, 2.5f);
            var comboBar = UIFactory.Rect(comboRoot, "Timer", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(130f, 20f));
            comboFill = UIFactory.Bar(comboBar, UISkin.Sun, new Color(0.3f, 0.2f, 0.1f));
            comboRoot.gameObject.SetActive(false);

            // --- Banner (waves) + message (pickups) ---
            bannerRt = UIFactory.At(root, "Banner", Center, new Vector2(0f, 230f), new Vector2(1600f, 140f));
            bannerText = UIFactory.Text(bannerRt, "", 76, TextAnchor.MiddleCenter, Color.white, 5f);
            messageRt = UIFactory.At(root, "Message", Center, new Vector2(0f, 120f), new Vector2(1400f, 80f));
            messageText = UIFactory.Text(messageRt, "", 46, TextAnchor.MiddleCenter, UISkin.Sun, 4f);

            // --- Tutorial speech bubble ---
            hintBubble = UIFactory.At(root, "Tutorial", new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1040f, 76f));
            UIFactory.Sliced(hintBubble, UISkin.Rounded, UISkin.Cream, 1.2f);
            hintText = UIFactory.Text(hintBubble, "", 28, TextAnchor.MiddleCenter, UISkin.Outline, 0f);
            hintBubble.gameObject.AddComponent<PopIn>();

            // --- Health: heart + bar (bottom left) ---
            var healthBg = UIFactory.At(root, "Health", Vector2.zero, new Vector2(100f, 46f), new Vector2(400f, 50f));
            var trackImg = UIFactory.Sliced(healthBg, UISkin.RoundedSmall, new Color(0.3f, 0.12f, 0.08f), 1.5f);
            trackImg.raycastTarget = false;
            var trailRt = UIFactory.Rect(healthBg, "Trail", Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(-12f, -12f));
            healthTrail = UIFactory.FilledImage(trailRt, new Color(1f, 1f, 1f, 0.5f));
            var fillRt = UIFactory.Rect(healthBg, "Fill", Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(-12f, -12f));
            healthFill = UIFactory.FilledImage(fillRt, UISkin.Grass);
            var hpTextRt = UIFactory.Rect(healthBg, "Text", Vector2.zero, Vector2.one, Center, new Vector2(20f, 0f), Vector2.zero);
            healthText = UIFactory.Text(hpTextRt, "100", 28, TextAnchor.MiddleCenter, Color.white, 2.5f);
            heart = UIFactory.Icon(root, "Heart", UISkin.Heart, UISkin.Red, Vector2.zero, new Vector2(30f, 26f), 100f);

            // --- Seed-packet weapon slots (bottom right) ---
            int count = weapons.weapons.Length;
            const float cardW = 128f, cardH = 160f, gapW = 14f;
            var inv = UIFactory.At(root, "Inventory", new Vector2(1f, 0f), new Vector2(-34f, 26f), new Vector2(count * (cardW + gapW), cardH));
            slots = new Slot[count];
            for (int i = 0; i < count; i++)
            {
                var home = new Vector2(i * (cardW + gapW) + cardW / 2f, cardH / 2f);
                var rt = UIFactory.Rect(inv, "Slot" + (i + 1), Vector2.zero, Vector2.zero, Center, home, new Vector2(cardW, cardH));
                var glowRt = UIFactory.Rect(rt, "Glow", Vector2.zero, Vector2.one, Center, Vector2.zero, new Vector2(22f, 22f));
                var glow = UIFactory.Sliced(glowRt, UISkin.Rounded, UISkin.Sun, 1f);
                var cardImg = UIFactory.Sliced(UIFactory.Stretch(rt, "Card"), UISkin.Card, Color.white, 1.2f);
                var icon = UIFactory.Icon(rt, "Icon", UISkin.WeaponIcon(i), WeaponColors[i % WeaponColors.Length], Center, new Vector2(0f, 2f), 104f);
                var numRt = UIFactory.At(rt, "Key", new Vector2(0f, 1f), new Vector2(-12f, 12f), new Vector2(46f, 46f));
                UIFactory.Image(numRt, UISkin.Sun).sprite = UISkin.Circle;
                var number = UIFactory.Text(numRt, (i + 1).ToString(), 26, TextAnchor.MiddleCenter, UISkin.Outline, 0f);
                var nameRt = UIFactory.Rect(rt, "Name", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-14f, 40f));
                var name = UIFactory.Text(nameRt, "", 17, TextAnchor.MiddleCenter, UISkin.Outline, 0f);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.lineSpacing = 0.85f;
                slots[i] = new Slot { rt = rt, card = cardImg, glow = glow, icon = icon, name = name, number = number, home = home };
            }

            var buffRt = UIFactory.At(root, "Buffs", new Vector2(1f, 0f), new Vector2(-40f, 220f), new Vector2(560f, 80f));
            buffText = UIFactory.Text(buffRt, "", 28, TextAnchor.LowerRight, UISkin.Sun, 3f);

            // --- Damage direction indicators ---
            for (int i = 0; i < 4; i++)
            {
                var rt = UIFactory.At(root, "DamageIndicator", Center, Vector2.zero, new Vector2(150f, 26f));
                var img = UIFactory.Sliced(rt, UISkin.RoundedSmall, new Color(1f, 0.15f, 0.1f, 0f), 2.5f);
                img.raycastTarget = false;
                indicators.Add(new DamageIndicator { image = img, time = -10f });
            }
        }

        void BuildCrosshair(Transform root)
        {
            var cross = UIFactory.At(root, "Crosshair", Center, Vector2.zero, new Vector2(80f, 80f));
            crossBars = new[]
            {
                CrossBar(cross, Vector2.zero, new Vector2(6f, 16f), Color.white),
                CrossBar(cross, Vector2.zero, new Vector2(6f, 16f), Color.white),
                CrossBar(cross, Vector2.zero, new Vector2(16f, 6f), Color.white),
                CrossBar(cross, Vector2.zero, new Vector2(16f, 6f), Color.white),
            };
            CrossBar(cross, Vector2.zero, new Vector2(7f, 7f), Color.white);

            hitMarker = UIFactory.At(root, "HitMarker", Center, Vector2.zero, new Vector2(44f, 44f)).gameObject;
            hitMarker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var hmRt = (RectTransform)hitMarker.transform;
            CrossBar(hmRt, new Vector2(0f, 18f), new Vector2(7f, 16f), UISkin.Red);
            CrossBar(hmRt, new Vector2(0f, -18f), new Vector2(7f, 16f), UISkin.Red);
            CrossBar(hmRt, new Vector2(18f, 0f), new Vector2(16f, 7f), UISkin.Red);
            CrossBar(hmRt, new Vector2(-18f, 0f), new Vector2(16f, 7f), UISkin.Red);
            hitMarker.SetActive(false);
        }

        static RectTransform CrossBar(RectTransform parent, Vector2 pos, Vector2 size, Color color)
        {
            var bar = UIFactory.At(parent, "Bar", Center, pos, size);
            var img = UIFactory.Sliced(bar, UISkin.RoundedSmall, color, 6f);
            img.raycastTarget = false;
            return bar;
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }

        void OnScoreChanged(int score)
        {
            scoreText.text = score.ToString();
            scoreTargetText.text = "/ " + game.targetScore;
            progressFill.fillAmount = game.Progress;
        }

        void OnHealthChanged(float current, float max)
        {
            float k = current / max;
            healthFill.fillAmount = k;
            healthFill.color = Color.Lerp(UISkin.Red, UISkin.Grass, k);
            healthText.text = Mathf.CeilToInt(current).ToString();
        }

        void OnDamaged(Vector3 source)
        {
            DamageIndicator oldest = indicators[0];
            foreach (var ind in indicators) if (ind.time < oldest.time) oldest = ind;
            oldest.source = source;
            oldest.time = Time.unscaledTime;
        }

        void UpdateIndicators()
        {
            var cam = weapons.aimCamera.transform;
            foreach (var ind in indicators)
            {
                float age = Time.unscaledTime - ind.time;
                float a = Mathf.Clamp01(1f - age / 1.2f);
                ind.image.color = new Color(1f, 0.15f, 0.1f, a * 0.9f);
                if (a <= 0f) continue;
                Vector3 to = ind.source - cam.position;
                to.y = 0f;
                Vector3 fwd = cam.forward;
                fwd.y = 0f;
                float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
                float rad = -angle * Mathf.Deg2Rad + Mathf.PI / 2f;
                var rt = ind.image.rectTransform;
                rt.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 180f;
                rt.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }

        void RefreshInventory()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                bool unlocked = weapons.IsUnlocked(i);
                var s = slots[i];
                s.card.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f);
                s.icon.color = unlocked ? WeaponColors[i % WeaponColors.Length] : new Color(0.3f, 0.3f, 0.3f, 0.6f);
                s.name.text = unlocked ? weapons.weapons[i].DisplayName : L.T("locked");
            }
        }

        // ---------- Menus ----------

        void BuildPauseMenu()
        {
            var panel = UIFactory.MenuPanel(canvas.transform, "PauseMenu");
            UIFactory.Label(panel, L.T("paused"), 64, UISkin.Sun, 96f);
            UIFactory.Button(panel, L.T("resume"), () => game.Resume());
            UIFactory.Button(panel, L.T("settings"), () => { pauseMenu.SetActive(false); settingsMenu.SetActive(true); }, 64f, UISkin.Sky);
            UIFactory.Button(panel, L.T("restart"), () => game.RestartRound(false), 64f, UISkin.Orange);
            UIFactory.Button(panel, L.T("mainMenu"), () => game.GoToMainMenu(), 64f, UISkin.Red);
            UIFactory.Label(panel, L.T("controls"), 17, UISkin.Cream, 64f);
            pauseMenu = panel.parent.gameObject;
        }

        GameObject BuildEndScreen(string name, string title, Color titleColor, Color dim, bool dead,
            out Text stats, out Text best, out Text board)
        {
            var panel = UIFactory.MenuPanel(canvas.transform, name, 860f);
            panel.parent.GetComponent<Image>().color = dim;
            UIFactory.Label(panel, title, 56, titleColor, 150f);
            best = UIFactory.Label(panel, "", 38, UISkin.Sun, 50f);
            stats = UIFactory.Label(panel, "", 26, Color.white, 84f);
            board = UIFactory.Label(panel, "", 22, UISkin.Cream, 160f);
            if (dead)
            {
                UIFactory.Button(panel, L.T("restart"), () => game.RestartRound(false));
                loadCheckpointButton = UIFactory.Button(panel, L.T("loadCheckpoint"), () => game.RestartRound(true), 64f, UISkin.Sky);
            }
            else UIFactory.Button(panel, L.T("playAgain"), () => game.RestartRound(false));
            UIFactory.Button(panel, L.T("mainMenu"), () => game.GoToMainMenu(), 64f, UISkin.Red);
            var root = panel.parent.gameObject;
            root.SetActive(false);
            return root;
        }

        public static string LeaderboardText()
        {
            var top = SaveSystem.TopScores;
            if (top.Count == 0) return L.T("noScores");
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < top.Count; i++)
            {
                string medal = i == 0 ? "<color=#FFDD33>" : i == 1 ? "<color=#E6E6F0>" : i == 2 ? "<color=#F0A050>" : "<color=#FFFFFF>";
                sb.Append($"{medal}{i + 1}.  {top[i].score}</color>   <size=20>{L.T("wave", top[i].wave)}{(top[i].won ? "  " + L.T("wonTag") : "")}  •  {top[i].date}</size>\n");
            }
            return sb.ToString().TrimEnd();
        }

        void OnStateChanged(GameState state)
        {
            hud.SetActive(state == GameState.Playing || state == GameState.Paused);
            pauseMenu.SetActive(state == GameState.Paused);
            settingsMenu.SetActive(false);
            deadScreen.SetActive(state == GameState.Dead);
            winScreen.SetActive(state == GameState.Won);

            if (state != GameState.Dead && state != GameState.Won) return;
            var s = game.Stats;
            string stats = L.T("stats", game.Score, waves != null ? waves.Wave : 0, s.kills, s.Accuracy, s.bestCombo, s.TimeText);
            string best = game.NewHighScore ? L.T("newHighScore") : "";
            if (state == GameState.Dead)
            {
                deadStatsText.text = stats;
                deadBestText.text = best;
                deadBoardText.text = LeaderboardText();
                loadCheckpointButton.interactable = SaveSystem.HasCheckpoint;
                SoundFX.PlayMusic(SoundFX.Track.Menu);
            }
            else
            {
                winStatsText.text = stats;
                winBestText.text = best;
                winBoardText.text = LeaderboardText();
                SoundFX.Play(SoundFX.Sfx.Checkpoint);
            }
        }
    }
}

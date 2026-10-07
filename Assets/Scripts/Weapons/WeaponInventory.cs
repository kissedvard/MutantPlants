using System;
using System.Collections;
using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Holds the player's weapons, handles switching (1-4, mouse wheel), firing
    /// (hitscan or projectile), weapon sway/recoil, muzzle flashes and power-up buffs.
    /// </summary>
    public class WeaponInventory : MonoBehaviour
    {
        public Camera aimCamera;
        public Transform weaponHolder;
        public PlayerController movement;
        public Light muzzleLight;
        public WeaponData[] weapons;
        public Material tracerMaterial;
        public LayerMask hitMask = ~(1 << 2); // everything except "Ignore Raycast"

        public int CurrentIndex { get; private set; }
        public WeaponData Current => weapons[CurrentIndex];

        public float DamageBoostTimeLeft { get; private set; }
        public float RapidFireTimeLeft { get; private set; }
        /// <summary>0..1, grows while firing; used by the dynamic crosshair.</summary>
        public float Bloom { get; private set; }

        public event Action InventoryChanged;
        /// <summary>Fired when a shot hits an enemy (for the hit marker).</summary>
        public event Action EnemyHit;
        public event Action<WeaponData> WeaponUnlocked;
        /// <summary>Ammo in the current weapon changed (shot, reload, switch).</summary>
        public event Action AmmoChanged;

        public int CurrentAmmo => ammo[CurrentIndex];
        public int CurrentMagazine => Current.magazineSize;
        public bool IsReloading => reloading;
        /// <summary>0..1 progress of the current reload.</summary>
        public float ReloadProgress { get; private set; }

        bool[] unlocked;
        float nextFireTime;
        float kick;
        float switchAnim;
        Vector3[] modelRestPositions;
        Quaternion swayRotation = Quaternion.identity;
        Vector3 holderRestPosition;
        float muzzleLightTimer;

        // Ammo & reloading
        int[] ammo;
        ReloadAnimator[] reloadAnims;
        bool reloading;
        float reloadTimer;
        int shellPhase; // shotgun: 0 tilt in, 1 loading shells, 2 pump
        float autoReloadTimer = -1f;
        const float ShellEnterTime = 0.22f, ShellPumpTime = 0.6f;

        public int UnlockedMask
        {
            get
            {
                int mask = 0;
                for (int i = 0; i < unlocked.Length; i++) if (unlocked[i]) mask |= 1 << i;
                return mask;
            }
        }

        public bool IsUnlocked(int index) => unlocked[index];

        void Awake()
        {
            unlocked = new bool[weapons.Length];
            modelRestPositions = new Vector3[weapons.Length];
            ammo = new int[weapons.Length];
            reloadAnims = new ReloadAnimator[weapons.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                unlocked[i] = weapons[i].unlockedAtStart;
                ammo[i] = weapons[i].magazineSize;
                if (weapons[i].model != null)
                {
                    modelRestPositions[i] = weapons[i].model.transform.localPosition;
                    reloadAnims[i] = weapons[i].model.GetComponent<ReloadAnimator>();
                    if (reloadAnims[i] != null) reloadAnims[i].SetAmmo(ammo[i], weapons[i].magazineSize);
                }
            }
            if (weaponHolder != null) holderRestPosition = weaponHolder.localPosition;
            if (muzzleLight != null) muzzleLight.enabled = false;
            Equip(0);
        }

        public void SetUnlockedMask(int mask)
        {
            for (int i = 0; i < unlocked.Length; i++) unlocked[i] = weapons[i].unlockedAtStart || (mask & (1 << i)) != 0;
            InventoryChanged?.Invoke();
        }

        /// <summary>Unlocks the next locked weapon. Returns false if everything is already unlocked.</summary>
        public bool UnlockNextWeapon()
        {
            for (int i = 0; i < unlocked.Length; i++)
            {
                if (unlocked[i]) continue;
                unlocked[i] = true;
                Equip(i);
                WeaponUnlocked?.Invoke(weapons[i]);
                return true;
            }
            return false;
        }

        public void AddDamageBoost(float seconds) { DamageBoostTimeLeft = Mathf.Max(DamageBoostTimeLeft, seconds); InventoryChanged?.Invoke(); }
        public void AddRapidFire(float seconds) { RapidFireTimeLeft = Mathf.Max(RapidFireTimeLeft, seconds); InventoryChanged?.Invoke(); }

        public void ReportHit()
        {
            SoundFX.PlayVaried(SoundFX.Sfx.Hit, 0.45f, 0.15f);
            EnemyHit?.Invoke();
            if (GameManager.Instance != null) GameManager.Instance.Stats.shotsHit++;
        }

        void Update()
        {
            if (Time.timeScale == 0f) return;

            DamageBoostTimeLeft = Mathf.Max(0f, DamageBoostTimeLeft - Time.deltaTime);
            RapidFireTimeLeft = Mathf.Max(0f, RapidFireTimeLeft - Time.deltaTime);
            Bloom = Mathf.MoveTowards(Bloom, 0f, Time.deltaTime * 2.5f);

            HandleSwitching();

            if (Input.GetKeyDown(KeyCode.R)) StartReload();
            if (autoReloadTimer >= 0f)
            {
                autoReloadTimer -= Time.deltaTime;
                if (autoReloadTimer < 0f) StartReload();
            }
            UpdateReload(Time.deltaTime);

            bool trigger = Current.automatic ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
            if (trigger && Time.time >= nextFireTime && switchAnim <= 0.2f)
            {
                // A pump shotgun can stop loading shells to fire.
                if (reloading && Current.shellByShell && shellPhase == 1 && ammo[CurrentIndex] > 0) CancelReload();

                if (!reloading)
                {
                    if (ammo[CurrentIndex] > 0) Fire();
                    else if (Input.GetMouseButtonDown(0))
                    {
                        SoundFX.PlayVaried(SoundFX.Sfx.DryFire, 0.8f);
                        StartReload();
                    }
                }
            }

            AnimateModel();

            if (muzzleLight != null)
            {
                muzzleLightTimer -= Time.deltaTime;
                muzzleLight.enabled = muzzleLightTimer > 0f;
            }
        }

        void HandleSwitching()
        {
            for (int i = 0; i < weapons.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && unlocked[i] && i != CurrentIndex) Equip(i);

            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (wheel != 0f)
            {
                int dir = wheel > 0f ? -1 : 1;
                int i = CurrentIndex;
                for (int n = 0; n < weapons.Length; n++)
                {
                    i = (i + dir + weapons.Length) % weapons.Length;
                    if (unlocked[i]) { if (i != CurrentIndex) Equip(i); break; }
                }
            }
        }

        void Equip(int index)
        {
            CancelReload();
            CurrentIndex = index;
            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i].model != null) weapons[i].model.SetActive(i == index);
            nextFireTime = Time.time + 0.2f;
            switchAnim = 1f;
            InventoryChanged?.Invoke();
            AmmoChanged?.Invoke();
        }

        // ---------------------------------------------------------------- Reloading

        ReloadAnimator CurrentAnim => reloadAnims != null ? reloadAnims[CurrentIndex] : null;

        /// <summary>Starts reloading the current weapon (R key, or automatically when empty).</summary>
        public void StartReload()
        {
            autoReloadTimer = -1f;
            if (reloading || ammo[CurrentIndex] >= Current.magazineSize) return;
            reloading = true;
            reloadTimer = 0f;
            shellPhase = 0;
            ReloadProgress = 0f;
            AmmoChanged?.Invoke();
        }

        void CancelReload()
        {
            if (!reloading) return;
            reloading = false;
            ReloadProgress = 0f;
            var anim = CurrentAnim;
            if (anim != null) anim.Rest();
            if (anim != null) anim.SetAmmo(ammo[CurrentIndex], Current.magazineSize);
            AmmoChanged?.Invoke();
        }

        void FinishReload()
        {
            reloading = false;
            ReloadProgress = 1f;
            var anim = CurrentAnim;
            if (anim != null) { anim.Rest(); anim.SetAmmo(ammo[CurrentIndex], Current.magazineSize); }
            AmmoChanged?.Invoke();
        }

        void UpdateReload(float dt)
        {
            if (!reloading) return;
            var w = Current;
            var anim = CurrentAnim;
            float speed = RapidFireTimeLeft > 0f ? 1.5f : 1f; // the rapid-fire power-up also speeds up reloads
            reloadTimer += dt * speed;

            if (!w.shellByShell)
            {
                float t = Mathf.Clamp01(reloadTimer / w.reloadTime);
                ReloadProgress = t;
                if (anim != null) anim.SampleFull(t);
                if (t >= 1f)
                {
                    ammo[CurrentIndex] = w.magazineSize;
                    FinishReload();
                }
                return;
            }

            // Shell by shell: tilt in, load rounds one at a time, then pump.
            switch (shellPhase)
            {
                case 0:
                    if (anim != null) anim.SampleShellEnter(Mathf.Clamp01(reloadTimer / ShellEnterTime));
                    if (reloadTimer >= ShellEnterTime) { shellPhase = 1; reloadTimer = 0f; }
                    break;
                case 1:
                    float k = Mathf.Clamp01(reloadTimer / w.shellLoadTime);
                    if (anim != null) anim.SampleShell(k);
                    if (k >= 1f)
                    {
                        ammo[CurrentIndex]++;
                        AmmoChanged?.Invoke();
                        reloadTimer = 0f;
                        if (ammo[CurrentIndex] >= w.magazineSize) shellPhase = 2;
                    }
                    break;
                case 2:
                    float p = Mathf.Clamp01(reloadTimer / ShellPumpTime);
                    if (anim != null) anim.SamplePumpAction(p);
                    if (p >= 1f) FinishReload();
                    break;
            }
            ReloadProgress = shellPhase == 2 ? 1f : ammo[CurrentIndex] / (float)w.magazineSize;
        }

        void Fire()
        {
            var w = Current;
            float rate = w.fireRate * (RapidFireTimeLeft > 0f ? 2f : 1f);
            float damage = w.damage * (DamageBoostTimeLeft > 0f ? 2f : 1f);
            nextFireTime = Time.time + 1f / rate;
            kick = w.recoil;
            Bloom = Mathf.Clamp01(Bloom + 0.25f + w.spread * 0.04f);
            SoundFX.PlayVaried(w.sound, 0.6f, 0.06f);
            CameraFX.Shake(w.shake);
            if (w.pellets > 1) CameraFX.Kick(2f);
            if (GameManager.Instance != null) GameManager.Instance.Stats.shotsFired++;

            ammo[CurrentIndex] = Mathf.Max(0, ammo[CurrentIndex] - 1);
            var reloadAnim = CurrentAnim;
            if (reloadAnim != null) reloadAnim.SetAmmo(ammo[CurrentIndex], w.magazineSize);
            AmmoChanged?.Invoke();
            if (ammo[CurrentIndex] == 0) autoReloadTimer = 0.35f;

            Transform cam = aimCamera.transform;
            Vector3 muzzlePos = w.muzzle != null ? w.muzzle.position : cam.position + cam.forward * 0.5f;
            if (w.muzzle != null) FX.Spawn(FX.MuzzleFlash, muzzlePos, w.muzzle.rotation, w.tracerColor * 2f, 1f, w.muzzle);
            if (muzzleLight != null)
            {
                muzzleLight.color = w.tracerColor;
                muzzleLight.transform.position = muzzlePos;
                muzzleLightTimer = 0.05f;
            }

            if (w.projectile != null)
            {
                var grenade = Instantiate(w.projectile, muzzlePos + cam.forward * 0.6f, cam.rotation);
                grenade.Launch(cam.forward * w.projectileSpeed + Vector3.up * 2f, damage);
                return;
            }

            bool hitSomething = false;
            float moveSpread = movement != null ? movement.Speed * 0.15f : 0f;
            for (int p = 0; p < Mathf.Max(1, w.pellets); p++)
            {
                float s = w.spread + moveSpread;
                Quaternion spread = Quaternion.Euler(UnityEngine.Random.Range(-s, s), UnityEngine.Random.Range(-s, s), 0f);
                Vector3 dir = cam.rotation * spread * Vector3.forward;
                Vector3 end = cam.position + dir * w.range;

                if (Physics.Raycast(cam.position, dir, out RaycastHit hit, w.range, hitMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    var enemy = hit.collider.GetComponentInParent<EnemyPlant>();
                    if (enemy != null)
                    {
                        enemy.TakeDamage(damage, hit.point, dir, w.knockback);
                        hitSomething = true;
                    }
                    else
                    {
                        FX.Spawn(FX.Impact, hit.point, Quaternion.LookRotation(hit.normal), null, w.pellets > 1 ? 0.6f : 1f);
                    }
                }
                StartCoroutine(Tracer(muzzlePos, end, w.tracerColor));
            }

            if (hitSomething) ReportHit();
        }

        IEnumerator Tracer(Vector3 start, Vector3 end, Color color)
        {
            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.material = tracerMaterial;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            line.startWidth = 0.05f;
            line.endWidth = 0.015f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.SetPositions(new[] { start, end });
            float t = 0f;
            while (t < 0.06f)
            {
                t += Time.deltaTime;
                float a = 1f - t / 0.06f;
                line.startColor = new Color(color.r, color.g, color.b, a);
                yield return null;
            }
            Destroy(go);
        }

        void AnimateModel()
        {
            float dt = Time.deltaTime;
            kick = Mathf.Lerp(kick, 0f, dt * 14f);
            switchAnim = Mathf.MoveTowards(switchAnim, 0f, dt * 4f);

            var model = Current.model;
            if (model != null)
            {
                float lower = Mathf.SmoothStep(0f, 1f, switchAnim) * 0.35f;
                model.transform.localPosition = modelRestPositions[CurrentIndex] + new Vector3(0f, kick * 0.3f - lower, -kick);
                model.transform.localRotation = Quaternion.Euler(-kick * 60f + lower * 60f, 0f, 0f);
            }

            if (weaponHolder == null) return;

            // Sway: the gun lags behind mouse movement.
            Vector2 look = movement != null ? movement.LookDelta : Vector2.zero;
            var targetSway = Quaternion.Euler(Mathf.Clamp(look.y * 1.5f, -6f, 6f), Mathf.Clamp(-look.x * 1.5f, -6f, 6f), Mathf.Clamp(-look.x * 2f, -8f, 8f));
            swayRotation = Quaternion.Slerp(swayRotation, targetSway, dt * 8f);
            weaponHolder.localRotation = swayRotation;

            // Bob: figure-eight while walking.
            var fx = aimCamera.GetComponent<CameraFX>();
            float speed01 = movement != null ? Mathf.Clamp01(movement.Speed / 6f) : 0f;
            float phase = fx != null ? fx.BobPhase : 0f;
            weaponHolder.localPosition = holderRestPosition + new Vector3(Mathf.Cos(phase) * 0.012f, Mathf.Sin(phase * 2f) * 0.008f, 0f) * speed01;
        }
    }
}

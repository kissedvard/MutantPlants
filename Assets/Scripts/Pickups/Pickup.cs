using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Power-up lying in the garden. Collected by walking into it.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        public enum Kind { Health, DamageBoost, RapidFire, NewWeapon }

        public Kind kind;
        public float amount = 35f;   // health restored, or buff duration in seconds
        public float lifetime = 22f;
        public float pickupRadius = 1.5f;
        public Color color = Color.white;

        Vector3 basePosition;
        float spawnTime;
        Renderer[] renderers;

        void Start()
        {
            basePosition = transform.position;
            spawnTime = Time.time;
            renderers = GetComponentsInChildren<Renderer>();
            FX.Spawn(FX.Sparkle, basePosition + Vector3.up * 0.8f, color);
        }

        void Update()
        {
            float age = Time.time - spawnTime;
            float pop = Mathf.Clamp01(age * 3f);
            transform.localScale = Vector3.one * (pop < 1f ? Mathf.Sin(pop * Mathf.PI * 0.75f) / 0.707f : 1f);
            transform.position = basePosition + Vector3.up * (0.9f + Mathf.Sin(age * 2.5f) * 0.18f);
            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);

            // Blink before disappearing.
            if (age > lifetime - 4f)
                foreach (var r in renderers) r.enabled = Mathf.Repeat(age * 6f, 1f) > 0.3f;
            if (age > lifetime) { Destroy(gameObject); return; }

            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            Vector3 d = gm.player.transform.position - basePosition;
            d.y = 0f;
            if (d.magnitude <= pickupRadius) Collect(gm);
        }

        void Collect(GameManager gm)
        {
            string message = null;
            switch (kind)
            {
                case Kind.Health:
                    gm.player.Heal(amount);
                    message = L.T("pickupHealth");
                    break;
                case Kind.DamageBoost:
                    gm.weapons.AddDamageBoost(amount);
                    message = L.T("pickupDamage");
                    break;
                case Kind.RapidFire:
                    gm.weapons.AddRapidFire(amount);
                    message = L.T("pickupRapid");
                    break;
                case Kind.NewWeapon:
                    // The UI announces the weapon via WeaponInventory.WeaponUnlocked.
                    if (!gm.weapons.UnlockNextWeapon())
                    {
                        gm.weapons.AddDamageBoost(10f);
                        message = L.T("pickupDamage");
                    }
                    break;
            }
            SoundFX.Play(SoundFX.Sfx.Pickup);
            FX.Spawn(FX.Sparkle, transform.position, color, 1.5f);
            if (message != null && gm.ui != null) gm.ui.ShowMessage(message, color);
            Destroy(gameObject);
        }
    }
}

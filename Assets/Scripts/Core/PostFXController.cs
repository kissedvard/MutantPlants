using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MutantPlants
{
    /// <summary>
    /// Drives URP post-processing from gameplay: red vignette pulse when hurt,
    /// desaturation + chromatic aberration at low health, warm glow on power-ups.
    /// </summary>
    public class PostFXController : MonoBehaviour
    {
        public Volume volume;
        public PlayerHealth player;
        public WeaponInventory weapons;

        Vignette vignette;
        ColorAdjustments color;
        ChromaticAberration chroma;
        float baseVignette, baseSaturation;
        float hurt;

        void Start()
        {
            var profile = volume.profile; // instanced copy, safe to modify
            profile.TryGet(out vignette);
            profile.TryGet(out color);
            profile.TryGet(out chroma);
            if (vignette != null) baseVignette = vignette.intensity.value;
            if (color != null) baseSaturation = color.saturation.value;
            if (player != null) player.Damaged += _ => hurt = 1f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            hurt = Mathf.MoveTowards(hurt, 0f, dt * 2f);
            float low = player != null ? 1f - Mathf.Clamp01(player.Current / (player.maxHealth * 0.35f)) : 0f;
            float pulse = low > 0f ? (Mathf.Sin(Time.unscaledTime * 6f) * 0.5f + 0.5f) * low : 0f;
            bool boosted = weapons != null && (weapons.DamageBoostTimeLeft > 0f || weapons.RapidFireTimeLeft > 0f);

            if (vignette != null)
            {
                float red = Mathf.Max(hurt, pulse * 0.6f);
                vignette.intensity.value = baseVignette + red * 0.3f;
                vignette.color.value = Color.Lerp(boosted ? new Color(0.35f, 0.2f, 0f) : Color.black, new Color(0.6f, 0f, 0f), red);
            }
            if (color != null) color.saturation.value = baseSaturation - low * 45f;
            if (chroma != null) chroma.intensity.value = Mathf.Max(hurt * 0.6f, low * 0.4f);
        }
    }
}

using UnityEngine;

namespace MutantPlants
{
    [System.Serializable]
    public class WeaponData
    {
        [Tooltip("Localization key, e.g. w_rifle (see Localization.cs).")]
        public string nameKey = "w_rifle";
        public float damage = 20f;
        [Tooltip("Shots per second.")]
        public float fireRate = 4f;
        [Tooltip("Hold the mouse button to keep firing.")]
        public bool automatic;
        [Tooltip("Rays per shot (shotgun > 1).")]
        public int pellets = 1;
        [Tooltip("Random spread cone in degrees.")]
        public float spread = 0.5f;
        public float range = 80f;
        [Tooltip("Push applied to enemies hit.")]
        public float knockback = 1.5f;
        public float recoil = 0.08f;
        [Tooltip("Camera shake per shot (0..1).")]
        public float shake = 0.08f;
        public bool unlockedAtStart;
        public SoundFX.Sfx sound = SoundFX.Sfx.Rifle;
        public Color tracerColor = new Color(1f, 0.9f, 0.5f);

        [Header("Ammo")]
        public int magazineSize = 30;
        [Tooltip("Seconds for a full reload (magazine/pump/drum weapons).")]
        public float reloadTime = 1.8f;
        [Tooltip("Load one round at a time (shotgun); can be interrupted by firing.")]
        public bool shellByShell;
        public float shellLoadTime = 0.45f;

        [Header("Projectile weapons (leave empty for hitscan)")]
        public SeedGrenade projectile;
        public float projectileSpeed = 24f;

        [Header("Visuals")]
        public GameObject model;
        public Transform muzzle;

        public string DisplayName => L.T(nameKey);
    }
}

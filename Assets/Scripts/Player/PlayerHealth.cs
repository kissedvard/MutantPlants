using System;
using UnityEngine;

namespace MutantPlants
{
    public class PlayerHealth : MonoBehaviour
    {
        public float maxHealth = 100f;
        [Tooltip("Seconds of invulnerability after being hit, so swarms can't melt you instantly.")]
        public float hitGrace = 0.25f;

        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        /// <summary>(current, max)</summary>
        public event Action<float, float> HealthChanged;
        /// <summary>World position the damage came from.</summary>
        public event Action<Vector3> Damaged;

        float lastHitTime = -10f;

        void Awake()
        {
            Current = maxHealth;
        }

        void Start()
        {
            HealthChanged?.Invoke(Current, maxHealth);
        }

        public void SetHealth(float value)
        {
            Current = Mathf.Clamp(value, 1f, maxHealth);
            HealthChanged?.Invoke(Current, maxHealth);
        }

        public void TakeDamage(float amount, Vector3 source)
        {
            if (IsDead || GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            if (Time.time - lastHitTime < hitGrace) return;
            lastHitTime = Time.time;

            Current = Mathf.Max(0f, Current - amount);
            HealthChanged?.Invoke(Current, maxHealth);
            Damaged?.Invoke(source);
            SoundFX.PlayVaried(SoundFX.Sfx.PlayerHurt);
            CameraFX.Shake(Mathf.Clamp(amount / 40f, 0.2f, 0.7f));
            if (IsDead) GameManager.Instance.PlayerDied();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            HealthChanged?.Invoke(Current, maxHealth);
        }
    }
}

using UnityEngine;

namespace MutantPlants
{
    /// <summary>Glob of spicy goo spat by the Chili Spitter. Dodgeable.</summary>
    public class EnemyProjectile : MonoBehaviour
    {
        public float radius = 0.25f;
        public float gravity = -3f;
        public float lifetime = 4f;
        public Color splashColor = new Color(1f, 0.3f, 0.05f);

        // Ignore "Ignore Raycast" and "Enemy" layers.
        const int Mask = ~((1 << 2) | (1 << 8));

        Vector3 velocity;
        float damage;
        float age;

        public void Launch(Vector3 velocity, float damage)
        {
            this.velocity = velocity;
            this.damage = damage;
        }

        void Update()
        {
            age += Time.deltaTime;
            velocity.y += gravity * Time.deltaTime;
            Vector3 step = velocity * Time.deltaTime;

            if (Physics.SphereCast(transform.position, radius, step.normalized, out var hit, step.magnitude, Mask, QueryTriggerInteraction.Ignore))
            {
                var player = hit.collider.GetComponentInParent<PlayerHealth>();
                if (player != null) player.TakeDamage(damage, transform.position - velocity.normalized * 3f);
                FX.Spawn(FX.Splat, hit.point, Quaternion.LookRotation(hit.normal), splashColor, 0.6f);
                SoundFX.PlayAt(SoundFX.Sfx.Splat, hit.point, 0.6f);
                Destroy(gameObject);
                return;
            }

            transform.position += step;
            transform.rotation = Quaternion.LookRotation(velocity);
            if (age > lifetime) Destroy(gameObject);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Ballistic explosive seed fired by the Seed Launcher. Explodes on impact,
    /// damaging every enemy in the radius (with falloff).
    /// </summary>
    public class SeedGrenade : MonoBehaviour
    {
        public float gravity = -18f;
        public float radius = 0.15f;
        public float explosionRadius = 4.5f;
        public float fuse = 3f;
        public LayerMask hitMask = ~(1 << 2);

        Vector3 velocity;
        float damage;
        float age;
        bool exploded;

        public void Launch(Vector3 velocity, float damage)
        {
            this.velocity = velocity;
            this.damage = damage;
        }

        void Update()
        {
            if (exploded) return;
            age += Time.deltaTime;
            velocity.y += gravity * Time.deltaTime;
            Vector3 step = velocity * Time.deltaTime;

            if (Physics.SphereCast(transform.position, radius, step.normalized, out var hit, step.magnitude, hitMask, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<PlayerHealth>() == null)
            {
                transform.position = hit.point + hit.normal * 0.1f;
                Explode();
                return;
            }

            transform.position += step;
            transform.Rotate(720f * Time.deltaTime, 0f, 0f, Space.Self);
            if (age > fuse || transform.position.y < -5f) Explode();
        }

        void Explode()
        {
            exploded = true;
            Vector3 p = transform.position;
            FX.Spawn(FX.Explosion, p);
            SoundFX.PlayAt(SoundFX.Sfx.Explosion, p, 1f);

            var gm = GameManager.Instance;
            if (gm != null)
            {
                float dist = Vector3.Distance(gm.player.transform.position, p);
                CameraFX.Shake(Mathf.Lerp(0.6f, 0.1f, dist / 25f));
            }

            var damaged = new HashSet<EnemyPlant>();
            foreach (var col in Physics.OverlapSphere(p, explosionRadius, hitMask, QueryTriggerInteraction.Ignore))
            {
                var enemy = col.GetComponentInParent<EnemyPlant>();
                if (enemy == null || !damaged.Add(enemy)) continue;
                float d = Vector3.Distance(enemy.transform.position + Vector3.up * 0.7f, p);
                float falloff = Mathf.Lerp(1f, 0.35f, d / explosionRadius);
                Vector3 dir = (enemy.transform.position - p).normalized + Vector3.up * 0.3f;
                enemy.TakeDamage(damage * falloff, enemy.transform.position + Vector3.up, dir, 6f);
            }
            if (damaged.Count > 0 && gm != null) gm.weapons.ReportHit();

            Destroy(gameObject);
        }
    }
}

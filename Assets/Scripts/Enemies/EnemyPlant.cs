using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MutantPlants
{
    /// <summary>
    /// A mutant vegetable. Rises out of the soil, then chases the player over the NavMesh.
    /// Melee plants bite when close, Ranged plants keep their distance and spit,
    /// the Boss slams the ground and summons minions.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyPlant : MonoBehaviour
    {
        public enum Behaviour { Melee, Ranged, Boss }

        /// <summary>All living enemies (used by the wave director and the boss bar).</summary>
        public static readonly List<EnemyPlant> Alive = new List<EnemyPlant>();
        public static event Action<EnemyPlant> BossSpawned;

        [Header("Stats")]
        [Tooltip("Localization key for the kill feed, e.g. e_carrot.")]
        public string nameKey = "e_carrot";
        public Behaviour behaviour = Behaviour.Melee;
        public float maxHealth = 50f;
        public float moveSpeed = 3.5f;
        public float attackDamage = 10f;
        public float attackRange = 1.8f;
        public float attackCooldown = 1f;
        public int scoreValue = 10;
        [Tooltip("How much knockback this plant receives (heavy plants < 1).")]
        public float knockbackScale = 1f;

        [Header("Ranged")]
        public EnemyProjectile projectile;
        public float preferredRange = 12f;
        public float projectileSpeed = 16f;
        public Transform mouth;

        [Header("Boss")]
        public float slamRange = 6f;
        public float slamCooldown = 4.5f;
        public EnemyPlant minionPrefab;
        public float summonInterval = 9f;

        [Header("Visuals")]
        [Tooltip("Child that wobbles while walking; usually the mesh root.")]
        public Transform body;
        public Transform[] legs;
        public Color splatColor = new Color(0.9f, 0.3f, 0.1f);
        public float wobbleAmount = 0.08f;
        public float riseDuration = 0.9f;

        public float Health { get; private set; }
        public float HealthFraction => Mathf.Clamp01(Health / maxHealth);
        public bool IsBoss => behaviour == Behaviour.Boss;

        NavMeshAgent agent;
        Transform target;
        PlayerHealth targetHealth;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        float nextAttackTime;
        float nextRepathTime;
        float nextSummonTime;
        float flashTimer;
        Color flashColor = Color.white;
        float spawnTime;
        float lunge;
        float hitPunch;
        float windup;
        Vector3 knockback;
        Vector3 bodyBaseScale, bodyBasePosition;
        float walkPhase;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        const int PlayerMask = ~((1 << 2) | (1 << 8));

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            if (body == null) body = transform;
            bodyBaseScale = body.localScale;
            bodyBasePosition = body.localPosition;
        }

        void OnEnable() => Alive.Add(this);
        void OnDisable() => Alive.Remove(this);

        void Start()
        {
            Health = maxHealth;
            agent.speed = moveSpeed;
            agent.updateRotation = false;
            agent.stoppingDistance = behaviour == Behaviour.Ranged ? preferredRange * 0.8f : attackRange * 0.7f;
            spawnTime = Time.time;
            walkPhase = UnityEngine.Random.value * 10f;

            var player = GameManager.Instance != null ? GameManager.Instance.player : FindAnyObjectByType<PlayerHealth>();
            if (player != null)
            {
                target = player.transform;
                targetHealth = player;
            }
            nextAttackTime = Time.time + riseDuration + 0.5f;
            nextSummonTime = Time.time + summonInterval;

            FX.Spawn(FX.Dust, transform.position, new Color(0.45f, 0.33f, 0.2f), IsBoss ? 2.5f : 1f);
            if (IsBoss)
            {
                SoundFX.Play(SoundFX.Sfx.BossRoar);
                CameraFX.Shake(0.5f);
                BossSpawned?.Invoke(this);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float age = Time.time - spawnTime;
            float rise = Mathf.Clamp01(age / riseDuration);

            Animate(rise, dt);
            UpdateFlash(dt);

            if (target == null || rise < 1f) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return;

            // Knockback decays over time.
            if (knockback.sqrMagnitude > 0.01f && agent.isOnNavMesh)
            {
                agent.Move(knockback * dt);
                knockback = Vector3.Lerp(knockback, Vector3.zero, dt * 8f);
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (windup <= 0f && Time.time >= nextRepathTime && agent.isOnNavMesh)
            {
                agent.SetDestination(target.position);
                nextRepathTime = Time.time + 0.2f;
            }

            if (toTarget.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget), dt * (IsBoss ? 3f : 8f));

            switch (behaviour)
            {
                case Behaviour.Melee: UpdateMelee(distance); break;
                case Behaviour.Ranged: UpdateRanged(distance); break;
                case Behaviour.Boss: UpdateBoss(distance, dt); break;
            }
        }

        void UpdateMelee(float distance)
        {
            if (distance <= attackRange && Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + attackCooldown;
                targetHealth.TakeDamage(attackDamage, transform.position);
                lunge = 0.3f;
            }
        }

        void UpdateRanged(float distance)
        {
            // Back off if the player gets too close.
            if (distance < preferredRange * 0.45f && agent.isOnNavMesh)
            {
                Vector3 away = transform.position - (target.position - transform.position).normalized * 4f;
                agent.SetDestination(away);
                nextRepathTime = Time.time + 0.5f;
            }

            if (distance <= preferredRange * 1.3f && Time.time >= nextAttackTime && HasLineOfSight())
            {
                nextAttackTime = Time.time + attackCooldown * UnityEngine.Random.Range(0.8f, 1.2f);
                Vector3 origin = mouth != null ? mouth.position : transform.position + Vector3.up * 1.2f;
                Vector3 aim = target.position + Vector3.up * 1.2f;
                // Lead the target slightly.
                var cc = target.GetComponent<CharacterController>();
                if (cc != null) aim += cc.velocity * (Vector3.Distance(origin, aim) / projectileSpeed) * 0.6f;
                var p = Instantiate(projectile, origin, Quaternion.LookRotation(aim - origin));
                p.Launch((aim - origin).normalized * projectileSpeed, attackDamage);
                SoundFX.PlayAt(SoundFX.Sfx.Spit, origin, 0.8f);
                lunge = 0.35f;
            }
        }

        void UpdateBoss(float distance, float dt)
        {
            if (windup > 0f)
            {
                windup -= dt;
                flashColor = new Color(1f, 0.4f, 0.1f);
                flashTimer = 0.05f;
                if (windup <= 0f) Slam();
                return;
            }

            if (distance <= slamRange * 0.8f && Time.time >= nextAttackTime)
            {
                windup = 0.9f;
                if (agent.isOnNavMesh) agent.ResetPath();
            }

            if (minionPrefab != null && Time.time >= nextSummonTime)
            {
                nextSummonTime = Time.time + summonInterval;
                for (int i = 0; i < 3; i++)
                {
                    var offset = Quaternion.Euler(0f, i * 120f, 0f) * transform.forward * 3f;
                    if (NavMesh.SamplePosition(transform.position + offset, out var hit, 3f, NavMesh.AllAreas))
                        Instantiate(minionPrefab, hit.position, transform.rotation);
                }
                SoundFX.PlayAt(SoundFX.Sfx.BossRoar, transform.position, 0.6f);
            }
        }

        void Slam()
        {
            nextAttackTime = Time.time + slamCooldown;
            lunge = 0.5f;
            FX.Spawn(FX.Shockwave, transform.position + Vector3.up * 0.1f, new Color(1f, 0.6f, 0.2f), slamRange / 3f);
            FX.Spawn(FX.Dust, transform.position, new Color(0.45f, 0.33f, 0.2f), 3f);
            SoundFX.PlayAt(SoundFX.Sfx.BossSlam, transform.position, 1f);

            float d = Vector3.Distance(target.position, transform.position);
            CameraFX.Shake(Mathf.Lerp(0.8f, 0.2f, d / 25f));
            if (d <= slamRange) targetHealth.TakeDamage(attackDamage, transform.position);
        }

        bool HasLineOfSight()
        {
            Vector3 from = transform.position + Vector3.up * 1.2f;
            Vector3 to = target.position + Vector3.up * 1.2f;
            if (!Physics.Linecast(from, to, out var hit, PlayerMask, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.GetComponentInParent<PlayerHealth>() != null;
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection, float knockbackForce = 1.5f)
        {
            if (Health <= 0f) return;
            Health -= amount;
            flashTimer = 0.08f;
            flashColor = Color.white;
            hitPunch = Mathf.Min(0.25f, hitPunch + 0.12f);
            Vector3 push = hitDirection;
            push.y = 0f;
            knockback += push.normalized * knockbackForce * knockbackScale * 3f;

            FX.Spawn(FX.Splat, hitPoint, Quaternion.LookRotation(-hitDirection), splatColor, 0.5f);

            if (Health <= 0f) Die();
        }

        void Die()
        {
            if (GameManager.Instance != null) GameManager.Instance.EnemyKilled(this);
            SoundFX.PlayAt(SoundFX.Sfx.EnemyDeath, transform.position, IsBoss ? 1f : 0.8f);
            SoundFX.PlayAt(SoundFX.Sfx.Splat, transform.position, 0.7f);

            float size = IsBoss ? 3f : 1f;
            FX.Spawn(FX.Splat, transform.position + Vector3.up * 0.8f * size, splatColor, 1.4f * size);
            var mainRenderer = renderers.Length > 0 ? renderers[0] : null;
            if (mainRenderer != null)
                FX.Chunks(transform.position + Vector3.up * 0.7f * size, mainRenderer.sharedMaterial, IsBoss ? 30 : 10, 0.22f * size, IsBoss ? 7f : 3.5f);
            if (IsBoss) CameraFX.Shake(0.8f);
            Destroy(gameObject);
        }

        void Animate(float rise, float dt)
        {
            float speed01 = agent != null ? Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(0.1f, moveSpeed)) : 0f;
            walkPhase += dt * moveSpeed * 3.2f * Mathf.Max(0.3f, speed01);

            // Wobble/squash so the plants look alive.
            float wobble = Mathf.Sin(walkPhase) * wobbleAmount;
            lunge = Mathf.MoveTowards(lunge, 0f, dt * 2f);
            hitPunch = Mathf.MoveTowards(hitPunch, 0f, dt * 2.5f);
            float windupStretch = windup > 0f ? (0.9f - windup) * 0.3f : 0f;

            Vector3 squash = new Vector3(1f + wobble + hitPunch, 1f - wobble - hitPunch * 0.6f + windupStretch, 1f + wobble + hitPunch);
            body.localScale = Vector3.Scale(bodyBaseScale, squash) * (1f + lunge);

            // Rise out of the soil.
            float rise01 = Mathf.SmoothStep(0f, 1f, rise);
            body.localPosition = bodyBasePosition + Vector3.down * (1f - rise01) * 1.6f * bodyBaseScale.y
                                 + Vector3.up * Mathf.Abs(Mathf.Sin(walkPhase)) * 0.06f * speed01;
            body.localRotation = Quaternion.Euler(lunge * 40f, 0f, Mathf.Sin(walkPhase * 0.5f) * 6f * speed01);

            if (legs != null)
                for (int i = 0; i < legs.Length; i++)
                    if (legs[i] != null)
                        legs[i].localRotation = Quaternion.Euler(Mathf.Sin(walkPhase + i * Mathf.PI) * 35f * Mathf.Max(0.2f, speed01), 0f, 0f);
        }

        void UpdateFlash(float dt)
        {
            if (flashTimer <= 0f) return;
            flashTimer -= dt;
            bool on = flashTimer > 0f;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (on)
                {
                    block.SetColor(BaseColorId, flashColor);
                    block.SetColor(ColorId, flashColor);
                    r.SetPropertyBlock(block);
                }
                else r.SetPropertyBlock(null);
            }
        }
    }
}

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
    /// Body animation comes from the Animator (Idle/Walk/Attack/Hit/Die); this script
    /// sets its parameters and adds the procedural bits (rising, blinking, hit flash).
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
        [Tooltip("Where projectiles come out (the mouth).")]
        public Transform mouth;

        [Header("Boss")]
        public float slamRange = 6f;
        public float slamCooldown = 4.5f;
        public EnemyPlant minionPrefab;
        public float summonInterval = 9f;

        [Header("Animation")]
        public Animator animator;
        [Tooltip("Root of the model; moved/scaled procedurally for rising and hit punches.")]
        public Transform rig;
        public Transform[] eyes;
        [Tooltip("Walk speed (m/s) the walk clip was authored for.")]
        public float referenceWalkSpeed = 3.5f;
        [Tooltip("Playback speed of the attack clip (boss: slower, heavier).")]
        public float attackAnimSpeed = 1f;
        [Tooltip("Seconds into the attack clip (at speed 1) where the hit lands.")]
        public float attackStrikeTime = 0.32f;
        public float riseDuration = 0.9f;

        [Header("Effects")]
        public Color splatColor = new Color(0.9f, 0.3f, 0.1f);

        public float Health { get; private set; }
        public float HealthFraction => Mathf.Clamp01(Health / maxHealth);
        public bool IsBoss => behaviour == Behaviour.Boss;
        public bool IsDead { get; private set; }

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        static readonly int AttackSpeedId = Animator.StringToHash("AttackSpeed");
        static readonly int AttackId = Animator.StringToHash("Attack");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int DieId = Animator.StringToHash("Die");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        const int PlayerMask = ~((1 << 2) | (1 << 8));

        NavMeshAgent agent;
        Collider col;
        Transform target;
        PlayerHealth targetHealth;
        Renderer[] flashRenderers;
        MaterialPropertyBlock block;
        Vector3 rigBaseScale, rigBasePos;
        Vector3[] eyeBaseScale;
        float nextAttackTime, nextRepathTime, nextSummonTime, nextHitAnimTime;
        float strikeTimer = -1f;
        float flashTimer, hitPunch, blinkTimer, nextBlink;
        Color flashColor = Color.white;
        float spawnTime, smoothSpeed;
        Vector3 knockback;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            col = GetComponent<Collider>();
            if (animator == null) animator = GetComponent<Animator>();
            if (rig == null) rig = transform;
            rigBaseScale = rig.localScale;
            rigBasePos = rig.localPosition;
            block = new MaterialPropertyBlock();

            var list = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>())
                if (r.name != "Outline" && !r.name.StartsWith("Shine")) list.Add(r);
            flashRenderers = list.ToArray();

            eyeBaseScale = new Vector3[eyes != null ? eyes.Length : 0];
            for (int i = 0; i < eyeBaseScale.Length; i++) if (eyes[i] != null) eyeBaseScale[i] = eyes[i].localScale;
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
            nextBlink = Time.time + UnityEngine.Random.Range(1f, 4f);

            if (animator != null)
            {
                animator.SetFloat(MoveSpeedId, moveSpeed / referenceWalkSpeed);
                animator.SetFloat(AttackSpeedId, attackAnimSpeed);
                animator.Update(UnityEngine.Random.value); // desync idle cycles
            }

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

        float StrikeDelay => attackStrikeTime / Mathf.Max(0.05f, attackAnimSpeed);

        void Update()
        {
            float dt = Time.deltaTime;
            AnimateProcedural(dt);
            if (IsDead) return;

            float rise = Mathf.Clamp01((Time.time - spawnTime) / riseDuration);
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
            bool attacking = strikeTimer >= 0f;

            if (agent.isOnNavMesh)
            {
                agent.isStopped = attacking;
                if (!attacking && Time.time >= nextRepathTime)
                {
                    agent.SetDestination(target.position);
                    nextRepathTime = Time.time + 0.2f;
                }
            }

            if (toTarget.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget), dt * (IsBoss ? 3f : 8f));

            if (attacking)
            {
                strikeTimer -= dt;
                if (IsBoss) { flashColor = new Color(1f, 0.4f, 0.1f); flashTimer = 0.05f; }
                if (strikeTimer < 0f) Strike(distance);
                return;
            }

            switch (behaviour)
            {
                case Behaviour.Melee:
                    if (distance <= attackRange && Time.time >= nextAttackTime) StartAttack(attackCooldown);
                    break;
                case Behaviour.Ranged:
                    UpdateRanged(distance);
                    break;
                case Behaviour.Boss:
                    if (distance <= slamRange * 0.8f && Time.time >= nextAttackTime) StartAttack(slamCooldown);
                    UpdateSummon();
                    break;
            }
        }

        void StartAttack(float cooldown)
        {
            nextAttackTime = Time.time + cooldown;
            strikeTimer = StrikeDelay;
            if (animator != null) animator.SetTrigger(AttackId);
        }

        /// <summary>The moment the attack animation lands.</summary>
        void Strike(float distance)
        {
            switch (behaviour)
            {
                case Behaviour.Melee:
                    if (distance <= attackRange * 1.4f) targetHealth.TakeDamage(attackDamage, transform.position);
                    break;
                case Behaviour.Ranged:
                    Spit();
                    break;
                case Behaviour.Boss:
                    Slam(distance);
                    break;
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
                StartAttack(attackCooldown * UnityEngine.Random.Range(0.8f, 1.2f));
        }

        void Spit()
        {
            Vector3 origin = mouth != null ? mouth.position : transform.position + Vector3.up * 1.2f;
            Vector3 aim = target.position + Vector3.up * 1.2f;
            // Lead the target slightly.
            var cc = target.GetComponent<CharacterController>();
            if (cc != null) aim += cc.velocity * (Vector3.Distance(origin, aim) / projectileSpeed) * 0.6f;
            var p = Instantiate(projectile, origin, Quaternion.LookRotation(aim - origin));
            p.Launch((aim - origin).normalized * projectileSpeed, attackDamage);
            SoundFX.PlayAt(SoundFX.Sfx.Spit, origin, 0.8f);
        }

        void UpdateSummon()
        {
            if (minionPrefab == null || Time.time < nextSummonTime) return;
            nextSummonTime = Time.time + summonInterval;
            for (int i = 0; i < 3; i++)
            {
                var offset = Quaternion.Euler(0f, i * 120f, 0f) * transform.forward * 3f;
                if (NavMesh.SamplePosition(transform.position + offset, out var hit, 3f, NavMesh.AllAreas))
                    Instantiate(minionPrefab, hit.position, transform.rotation);
            }
            SoundFX.PlayAt(SoundFX.Sfx.BossRoar, transform.position, 0.6f);
        }

        void Slam(float distance)
        {
            FX.Spawn(FX.Shockwave, transform.position + Vector3.up * 0.1f, new Color(1f, 0.6f, 0.2f), slamRange / 3f);
            FX.Spawn(FX.Dust, transform.position, new Color(0.45f, 0.33f, 0.2f), 3f);
            SoundFX.PlayAt(SoundFX.Sfx.BossSlam, transform.position, 1f);
            CameraFX.Shake(Mathf.Lerp(0.8f, 0.2f, distance / 25f));
            if (distance <= slamRange) targetHealth.TakeDamage(attackDamage, transform.position);
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
            if (IsDead) return;
            Health -= amount;
            flashTimer = 0.08f;
            flashColor = Color.white;
            hitPunch = Mathf.Min(0.2f, hitPunch + 0.1f);
            Vector3 push = hitDirection;
            push.y = 0f;
            knockback += push.normalized * knockbackForce * knockbackScale * 3f;

            FX.Spawn(FX.Splat, hitPoint, Quaternion.LookRotation(-hitDirection), splatColor, 0.5f);

            if (Health <= 0f)
            {
                Die();
                return;
            }
            // Flinch, but don't interrupt an attack that is about to land.
            if (animator != null && strikeTimer < 0f && Time.time >= nextHitAnimTime && !IsBoss)
            {
                animator.SetTrigger(HitId);
                nextHitAnimTime = Time.time + 0.35f;
            }
        }

        void Die()
        {
            IsDead = true;
            Alive.Remove(this);
            if (GameManager.Instance != null) GameManager.Instance.EnemyKilled(this);
            SoundFX.PlayAt(SoundFX.Sfx.EnemyDeath, transform.position, IsBoss ? 1f : 0.8f);
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
            if (col != null) col.enabled = false;
            if (animator != null)
            {
                animator.ResetTrigger(AttackId);
                animator.ResetTrigger(HitId);
                animator.SetTrigger(DieId);
            }
            if (IsBoss) CameraFX.Shake(0.8f);
            Invoke(nameof(Splat), 0.36f);
        }

        /// <summary>End of the death squash: burst into juice and chunks.</summary>
        void Splat()
        {
            SoundFX.PlayAt(SoundFX.Sfx.Splat, transform.position, 0.8f);
            float size = rigBaseScale.y;
            FX.Spawn(FX.Splat, transform.position + Vector3.up * 0.8f * size, splatColor, 1.4f * size);
            var mainRenderer = flashRenderers.Length > 0 ? flashRenderers[0] : null;
            if (mainRenderer != null)
                FX.Chunks(transform.position + Vector3.up * 0.7f * size, mainRenderer.sharedMaterial, IsBoss ? 30 : 10, 0.2f * size, IsBoss ? 7f : 3.5f);
            if (IsBoss) CameraFX.Shake(0.6f);
            Destroy(gameObject);
        }

        void AnimateProcedural(float dt)
        {
            // Rise out of the soil.
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - spawnTime) / riseDuration));
            hitPunch = Mathf.MoveTowards(hitPunch, 0f, dt * 2f);
            rig.localPosition = rigBasePos + Vector3.down * (1f - rise) * 1.8f * rigBaseScale.y;
            rig.localScale = rigBaseScale * (1f + hitPunch);

            if (animator != null && !IsDead)
            {
                float speed01 = agent.enabled ? Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(0.1f, moveSpeed)) : 0f;
                smoothSpeed = Mathf.Lerp(smoothSpeed, speed01, dt * 8f);
                animator.SetFloat(SpeedId, smoothSpeed);
            }

            // Blink every few seconds.
            if (Time.time >= nextBlink)
            {
                blinkTimer = 0.14f;
                nextBlink = Time.time + UnityEngine.Random.Range(2f, 5f);
            }
            blinkTimer -= dt;
            float lid = blinkTimer > 0f ? 0.1f : 1f;
            for (int i = 0; i < eyeBaseScale.Length; i++)
                if (eyes[i] != null)
                    eyes[i].localScale = new Vector3(eyeBaseScale[i].x, eyeBaseScale[i].y * (IsDead ? 0.1f : lid), eyeBaseScale[i].z);

            UpdateFlash(dt);
        }

        void UpdateFlash(float dt)
        {
            if (flashTimer <= 0f) return;
            flashTimer -= dt;
            bool on = flashTimer > 0f;
            foreach (var r in flashRenderers)
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

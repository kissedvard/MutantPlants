using System;
using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Wave director: spawns enemy plants at random arena locations in escalating waves,
    /// with short breaks in between and a boss every few waves.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public EnemyPlant prefab;
            public float weight = 1f;
            [Tooltip("First wave this enemy can appear in.")]
            public int fromWave = 1;
        }

        public Arena arena;
        public Entry[] enemies;
        public EnemyPlant bossPrefab;
        public int bossEvery = 5;
        public float minDistanceFromPlayer = 14f;
        public float breakDuration = 7f;
        public float firstWaveDelay = 4f;
        [Tooltip("Enemy health multiplier added per wave.")]
        public float healthPerWave = 0.06f;

        public int Wave { get; private set; }
        public bool InBreak { get; private set; } = true;
        public float BreakTimeLeft { get; private set; }
        public bool IsBossWave => Wave > 0 && Wave % bossEvery == 0;
        public int EnemiesRemaining => (toSpawn - spawned) + EnemyPlant.Alive.Count;

        /// <summary>(wave, isBossWave)</summary>
        public event Action<int, bool> WaveStarted;
        public event Action<int> WaveCleared;

        int toSpawn, spawned;
        float spawnTimer;
        bool bossSpawned;

        void Start()
        {
            BreakTimeLeft = firstWaveDelay;
        }

        /// <summary>Resume from a checkpoint: the next wave started will be this one.</summary>
        public void SetStartWave(int wave)
        {
            Wave = Mathf.Max(0, wave - 1);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            if (InBreak)
            {
                BreakTimeLeft -= Time.deltaTime;
                if (BreakTimeLeft <= 0f) StartWave(gm);
                return;
            }

            if (spawned < toSpawn)
            {
                spawnTimer -= Time.deltaTime;
                int maxAlive = Mathf.Min(6 + Wave * 2, 22);
                if (spawnTimer <= 0f && EnemyPlant.Alive.Count < maxAlive)
                {
                    spawnTimer = Mathf.Max(0.45f, 1.9f - Wave * 0.12f) * UnityEngine.Random.Range(0.7f, 1.3f);
                    if (TrySpawn(gm, PickPrefab())) spawned++;
                }
            }
            else if (EnemyPlant.Alive.Count == 0)
            {
                InBreak = true;
                BreakTimeLeft = breakDuration;
                WaveCleared?.Invoke(Wave);
            }
        }

        void StartWave(GameManager gm)
        {
            Wave++;
            InBreak = false;
            spawned = 0;
            bossSpawned = false;
            toSpawn = IsBossWave ? 4 + Wave : 5 + Wave * 3;
            spawnTimer = 0.5f;

            if (IsBossWave && bossPrefab != null && TrySpawn(gm, bossPrefab, 18f))
                bossSpawned = true;

            WaveStarted?.Invoke(Wave, bossSpawned);
        }

        bool TrySpawn(GameManager gm, EnemyPlant prefab, float minDistance = -1f)
        {
            if (prefab == null) return false;
            if (!arena.TryGetRandomPoint(gm.player.transform.position, minDistance > 0f ? minDistance : minDistanceFromPlayer, out var pos)) return false;

            var facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(gm.player.transform.position - pos, Vector3.up));
            var enemy = Instantiate(prefab, pos, facing);
            enemy.maxHealth *= 1f + healthPerWave * (Wave - 1);
            return true;
        }

        EnemyPlant PickPrefab()
        {
            float total = 0f;
            foreach (var e in enemies) if (Wave >= e.fromWave) total += e.weight;
            float r = UnityEngine.Random.value * total;
            foreach (var e in enemies)
            {
                if (Wave < e.fromWave) continue;
                r -= e.weight;
                if (r <= 0f) return e.prefab;
            }
            return enemies[0].prefab;
        }
    }
}

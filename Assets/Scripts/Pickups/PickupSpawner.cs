using System.Collections.Generic;
using UnityEngine;

namespace MutantPlants
{
    public class PickupSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public Pickup prefab;
            public float weight = 1f;
        }

        public Arena arena;
        public Entry[] pickups;
        public Vector2 intervalRange = new Vector2(9f, 15f);
        public int maxAlive = 3;
        public float minDistanceFromPlayer = 8f;

        readonly List<Pickup> alive = new List<Pickup>();
        float timer = 6f;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            alive.RemoveAll(p => p == null);
            timer -= Time.deltaTime;
            if (timer > 0f || alive.Count >= maxAlive) return;
            timer = Random.Range(intervalRange.x, intervalRange.y);

            if (!arena.TryGetRandomPoint(gm.player.transform.position, minDistanceFromPlayer, out var pos)) return;
            alive.Add(Instantiate(Pick(), pos, Quaternion.identity));
        }

        Pickup Pick()
        {
            float total = 0f;
            foreach (var e in pickups) total += e.weight;
            float r = Random.value * total;
            foreach (var e in pickups)
            {
                r -= e.weight;
                if (r <= 0f) return e.prefab;
            }
            return pickups[pickups.Length - 1].prefab;
        }
    }
}

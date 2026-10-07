using UnityEngine;
using UnityEngine.AI;

namespace MutantPlants
{
    /// <summary>
    /// The playable area (the fenced kitchen garden). Provides random spawn points on the NavMesh.
    /// </summary>
    public class Arena : MonoBehaviour
    {
        [Tooltip("Half size of the spawnable area around this transform (XZ).")]
        public Vector2 halfExtents = new Vector2(27f, 27f);
        [Tooltip("Areas where nothing may spawn (e.g. inside the barn).")]
        public Bounds[] exclusionZones;

        public bool TryGetRandomPoint(Vector3 avoid, float minDistance, out Vector3 point)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var candidate = transform.position + new Vector3(
                    Random.Range(-halfExtents.x, halfExtents.x), 0f,
                    Random.Range(-halfExtents.y, halfExtents.y));

                if (Vector3.Distance(Flat(candidate), Flat(avoid)) < minDistance) continue;
                if (IsExcluded(candidate)) continue;
                if (!NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas)) continue;

                point = hit.position;
                return true;
            }
            point = default;
            return false;
        }

        bool IsExcluded(Vector3 p)
        {
            if (exclusionZones == null) return false;
            foreach (var zone in exclusionZones)
            {
                var flat = new Vector3(p.x, zone.center.y, p.z);
                if (zone.Contains(flat)) return true;
            }
            return false;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(halfExtents.x * 2f, 0.1f, halfExtents.y * 2f));
            Gizmos.color = Color.red;
            if (exclusionZones != null)
                foreach (var zone in exclusionZones) Gizmos.DrawWireCube(zone.center, zone.size);
        }
    }
}

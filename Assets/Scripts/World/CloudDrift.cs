using UnityEngine;

namespace MutantPlants
{
    /// <summary>Slowly drifts clouds across the sky, wrapping around.</summary>
    public class CloudDrift : MonoBehaviour
    {
        public float speed = 1.5f;
        public float wrap = 160f;

        void Update()
        {
            var p = transform.position;
            p.x += speed * Time.deltaTime;
            if (p.x > wrap) p.x = -wrap;
            transform.position = p;
        }
    }
}

using UnityEngine;

namespace MutantPlants
{
    /// <summary>Idle wobble for decorative mutants on the menu screen.</summary>
    public class IdleWobble : MonoBehaviour
    {
        public float speed = 3f;
        public float amount = 0.06f;
        Vector3 baseScale;
        float offset;

        void Start()
        {
            baseScale = transform.localScale;
            offset = Random.value * 10f;
        }

        void Update()
        {
            float w = Mathf.Sin(Time.time * speed + offset) * amount;
            transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + w, 1f - w, 1f + w));
        }
    }
}

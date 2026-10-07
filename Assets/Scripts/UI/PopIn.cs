using UnityEngine;

namespace MutantPlants
{
    /// <summary>Elastic "boing" scale-in whenever the object is enabled (panels, banners).</summary>
    public class PopIn : MonoBehaviour
    {
        public float duration = 0.45f;
        public float delay;
        float t;

        void OnEnable()
        {
            t = -delay;
            transform.localScale = Vector3.zero;
        }

        /// <summary>Restart the animation without toggling the object.</summary>
        public void Replay() => OnEnable();

        void Update()
        {
            if (t > duration) return;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.localScale = Vector3.one * (t < 0f ? 0f : ElasticOut(k));
        }

        public static float ElasticOut(float k)
        {
            if (k <= 0f) return 0f;
            if (k >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * k) * Mathf.Sin((k - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f;
        }

        public static float BackOut(float k, float overshoot = 2.2f)
        {
            k -= 1f;
            return k * k * ((overshoot + 1f) * k + overshoot) + 1f;
        }
    }
}

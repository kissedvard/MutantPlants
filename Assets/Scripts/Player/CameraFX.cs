using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Trauma-based screen shake, head bob and FOV kick for the player camera.
    /// Call CameraFX.Shake(amount) from anywhere.
    /// </summary>
    public class CameraFX : MonoBehaviour
    {
        static CameraFX instance;

        public float maxShakeOffset = 0.15f;
        public float maxShakeAngle = 4f;

        Camera cam;
        Vector3 basePosition;
        float trauma;
        float fovKick;
        float sprintFov;
        float bobPhase, bobAmount;
        float landDip;

        public static Quaternion ShakeRotation { get; private set; } = Quaternion.identity;

        void Awake()
        {
            instance = this;
            cam = GetComponent<Camera>();
            basePosition = transform.localPosition;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Shake(float amount)
        {
            if (instance == null || !GameSettings.ScreenShake) return;
            instance.trauma = Mathf.Clamp01(instance.trauma + amount);
        }

        /// <summary>Adds a momentary FOV punch (e.g. explosions, shotgun).</summary>
        public static void Kick(float fovDegrees)
        {
            if (instance != null) instance.fovKick += fovDegrees;
        }

        public static void Land(float strength)
        {
            if (instance != null) instance.landDip = Mathf.Max(instance.landDip, strength);
        }

        /// <summary>Called each frame by the PlayerController with its horizontal speed.</summary>
        public void SetMovement(float speed, bool grounded, bool sprinting)
        {
            float target = grounded ? Mathf.Clamp01(speed / 6f) : 0f;
            bobAmount = Mathf.Lerp(bobAmount, target, Time.deltaTime * 10f);
            bobPhase += Time.deltaTime * Mathf.Lerp(7f, 10.5f, Mathf.Clamp01((speed - 6f) / 3f)) * (speed > 0.1f ? 1f : 0f);
            sprintFov = Mathf.Lerp(sprintFov, sprinting && speed > 7f ? 8f : 0f, Time.deltaTime * 6f);
        }

        public float BobPhase => bobPhase;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float shake = trauma * trauma;
            float t = Time.time * 25f;
            Vector3 shakeOffset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * 2f * maxShakeOffset * shake;
            ShakeRotation = Quaternion.Euler(
                (Mathf.PerlinNoise(t, 10f) - 0.5f) * 2f * maxShakeAngle * shake,
                (Mathf.PerlinNoise(10f, t) - 0.5f) * 2f * maxShakeAngle * shake,
                (Mathf.PerlinNoise(t, t) - 0.5f) * 2f * maxShakeAngle * shake);
            trauma = Mathf.Max(0f, trauma - dt * 1.6f);

            Vector3 bob = new Vector3(Mathf.Cos(bobPhase) * 0.04f, Mathf.Abs(Mathf.Sin(bobPhase)) * 0.06f, 0f) * bobAmount;
            landDip = Mathf.MoveTowards(landDip, 0f, dt * 1.5f);
            transform.localPosition = basePosition + bob + shakeOffset + Vector3.down * landDip * 0.3f;

            fovKick = Mathf.Lerp(fovKick, 0f, dt * 8f);
            if (cam != null) cam.fieldOfView = GameSettings.FieldOfView + sprintFov + fovKick;
        }
    }
}

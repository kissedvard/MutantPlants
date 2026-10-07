using UnityEngine;

namespace MutantPlants
{
    /// <summary>Spins an object (windmill blades, weather vane).</summary>
    public class Rotator : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new Vector3(0f, 0f, 40f);

        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}

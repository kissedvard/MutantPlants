using UnityEngine;

namespace MutantPlants
{
    /// <summary>Slowly circles the camera around the farm behind the main menu.</summary>
    public class MenuCameraOrbit : MonoBehaviour
    {
        public Vector3 center = Vector3.zero;
        public float radius = 34f;
        public float height = 14f;
        public float degreesPerSecond = 6f;

        float angle = 200f;

        void Update()
        {
            angle += degreesPerSecond * Time.deltaTime;
            float rad = angle * Mathf.Deg2Rad;
            transform.position = center + new Vector3(Mathf.Cos(rad) * radius, height, Mathf.Sin(rad) * radius);
            transform.LookAt(center + Vector3.up * 2f);
        }
    }
}

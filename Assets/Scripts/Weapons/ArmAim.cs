using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Keeps the first-person forearms pointing from the gloves to fixed elbow points below
    /// the bottom of the screen, so the arms always leave the view naturally, whatever the
    /// gun does (recoil, sway, reload animations).
    /// </summary>
    public class ArmAim : MonoBehaviour
    {
        public Transform viewCamera;
        public Transform[] forearms;
        [Tooltip("Elbow position for each forearm, in camera space.")]
        public Vector3[] elbows;

        void LateUpdate()
        {
            if (viewCamera == null || forearms == null) return;
            for (int i = 0; i < forearms.Length; i++)
            {
                var forearm = forearms[i];
                if (forearm == null || i >= elbows.Length) continue;
                var elbow = viewCamera.TransformPoint(elbows[i]);
                var dir = elbow - forearm.position;
                if (dir.sqrMagnitude < 1e-6f) continue;
                forearm.rotation = Quaternion.LookRotation(dir, viewCamera.up);
            }
        }
    }
}

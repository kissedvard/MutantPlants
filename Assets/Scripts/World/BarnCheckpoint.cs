using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Entering the barn (Pajta) saves a checkpoint and patches the player up a little.
    /// </summary>
    public class BarnCheckpoint : MonoBehaviour
    {
        [Tooltip("World-space area inside the barn.")]
        public Bounds area = new Bounds(Vector3.zero, new Vector3(8f, 4f, 8f));
        public float healOnCheckpoint = 25f;
        [Tooltip("Lantern that glows when a new checkpoint is available.")]
        public Light lantern;

        bool playerInside;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

            if (lantern != null)
            {
                bool available = gm.Score > gm.LastCheckpointScore;
                float target = available ? 2.5f + Mathf.Sin(Time.time * 4f) * 0.8f : 0.6f;
                lantern.intensity = Mathf.Lerp(lantern.intensity, target, Time.deltaTime * 5f);
            }

            bool inside = area.Contains(gm.player.transform.position + Vector3.up);
            if (inside && !playerInside && gm.TrySaveCheckpoint())
            {
                gm.player.Heal(healOnCheckpoint);
                SoundFX.Play(SoundFX.Sfx.Checkpoint);
                FX.Spawn(FX.Sparkle, gm.player.transform.position + Vector3.up, new Color(1f, 0.85f, 0.3f), 2f);
                if (gm.ui != null) gm.ui.ShowMessage(L.T("checkpoint"), new Color(1f, 0.85f, 0.3f));
            }
            playerInside = inside;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
            Gizmos.DrawWireCube(area.center, area.size);
        }
    }
}

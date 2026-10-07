using System;
using System.Collections;
using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Renders a side-view picture of each weapon model into a transparent RenderTexture,
    /// so the inventory cards show the real 3D weapons. Runs once at startup: copies of the
    /// models are placed far below the map on a private layer, photographed, then removed.
    /// </summary>
    public static class WeaponIconStudio
    {
        public const int StudioLayer = 31;
        const int Width = 768, Height = 384; // supersampled, scaled down on the card

        public static IEnumerator Render(WeaponInventory inventory, Action<Texture[]> done)
        {
            // Rendering to textures in the first frames after a scene load produces nothing,
            // so give the scene a moment to start up.
            yield return new WaitForSecondsRealtime(0.5f);

            var weapons = inventory.weapons;
            var textures = new Texture[weapons.Length];
            var cleanup = new System.Collections.Generic.List<GameObject>();

            // Keep the studio out of the player's view.
            if (inventory.aimCamera != null) inventory.aimCamera.cullingMask &= ~(1 << StudioLayer);

            var keyLight = new GameObject("IconKeyLight").AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.1f;
            keyLight.cullingMask = 1 << StudioLayer;
            keyLight.shadows = LightShadows.None;
            cleanup.Add(keyLight.gameObject);

            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i].model == null) continue;
                // Stacked vertically (not along the view axis), so each camera only sees its own weapon.
                var origin = new Vector3(0f, -500f - i * 25f, 0f);

                var copy = UnityEngine.Object.Instantiate(weapons[i].model);
                copy.SetActive(true);
                // Remove hands and arms right away so they don't affect the framing.
                foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                    if (t != null && (t.name == "RightHand" || t.name == "LeftHand")) UnityEngine.Object.DestroyImmediate(t.gameObject);
                foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = StudioLayer;
                foreach (var ps in copy.GetComponentsInChildren<ParticleSystem>(true)) UnityEngine.Object.Destroy(ps.gameObject);
                copy.transform.SetParent(null, false);
                copy.transform.localScale = Vector3.one;
                cleanup.Add(copy);

                var camGo = new GameObject("IconCamera" + i);
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.cullingMask = 1 << StudioLayer;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 50f;
                cleanup.Add(camGo);

                // Camera looks along -X at the origin; the gun's muzzle points to screen right, nose tilted up a little.
                camGo.transform.SetPositionAndRotation(origin + Vector3.right * 5f, Quaternion.LookRotation(Vector3.left, Vector3.up));
                copy.transform.rotation = Quaternion.AngleAxis(-12f, cam.transform.forward) * Quaternion.LookRotation(cam.transform.right, Vector3.up);
                copy.transform.position = origin;

                // Frame the model tightly.
                var bounds = new Bounds(origin, Vector3.zero);
                bool first = true;
                foreach (var r in copy.GetComponentsInChildren<Renderer>())
                {
                    if (first) { bounds = r.bounds; first = false; }
                    else bounds.Encapsulate(r.bounds);
                }
                copy.transform.position += origin - bounds.center;
                float aspect = Width / (float)Height;
                cam.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.z / aspect) * 1.12f;

                var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "WeaponIcon" + i };
                rt.Create();
                cam.targetTexture = rt;
                textures[i] = rt;
            }

            keyLight.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, -0.6f, 0.3f));

            // Let the cameras render normally for a few frames, then tear the studio down.
            for (int f = 0; f < 3; f++) yield return null;
            foreach (var go in cleanup)
            {
                if (go == null) continue;
                var cam = go.GetComponent<Camera>();
                if (cam != null) cam.enabled = false; // keep (disabled) so its texture stays valid
                else UnityEngine.Object.Destroy(go);
            }

            done(textures);
        }
    }
}

using UnityEngine;

namespace MutantPlants.EditorTools
{
    /// <summary>
    /// Cartoon first-person weapon models in the same style as the enemies: chunky rounded
    /// parts, bright colours and dark inverted-hull outlines, held by yellow work gloves
    /// with denim sleeves. Each model's root sits at GunOffset under the weapon holder;
    /// +Z is forward (towards the muzzle).
    /// </summary>
    public static partial class MutantPlantsBuilder
    {
        static readonly Vector3 GunOffset = new Vector3(0.4f, -0.34f, 1.0f);

        static void CreateWeaponMaterials()
        {
            Lit("GunRed", new Color(0.88f, 0.22f, 0.14f), 0.55f);
            Lit("GunMetal", new Color(0.26f, 0.29f, 0.36f), 0.7f, 0.6f);
            Lit("GunWood", new Color(0.78f, 0.47f, 0.2f), 0.35f);
            Lit("GunYellow", new Color(1f, 0.78f, 0.15f), 0.5f);
            Lit("GunGreen", new Color(0.36f, 0.72f, 0.2f), 0.55f);
            Lit("TankBlue", new Color(0.25f, 0.55f, 0.95f), 0.7f);
            Lit("Glove", new Color(0.98f, 0.78f, 0.38f), 0.2f);
            Lit("Denim", new Color(0.24f, 0.4f, 0.72f), 0.15f);
            Lit("RedDot", new Color(1f, 0.15f, 0.1f), 0.9f, 0f, new Color(1f, 0.1f, 0.05f) * 4f);
        }

        /// <summary>
        /// Weapon root (moved by recoil/switch animations) with a "Pose" child that angles the
        /// gun slightly inward and scales it. All parts are built under the pose.
        /// </summary>
        static Transform GunRoot(string name, Transform holder, Vector3 extraOffset)
        {
            var root = Empty(name, holder, GunOffset + extraOffset).transform;
            var pose = Empty("Pose", root, Vector3.zero, new Vector3(0f, -5f, 0f)).transform;
            pose.localScale = Vector3.one * 0.8f;
            return pose;
        }

        /// <summary>Chunky work glove wrapped around a grip, with cuff and sleeve going off-screen.</summary>
        static void Glove(Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 sleeveEuler, float size = 1f)
        {
            var hand = Empty(name, parent, pos, euler).transform;
            hand.localScale = Vector3.one * size;
            Toon(PrimitiveType.Sphere, "Palm", hand, Vector3.zero, new Vector3(0.1f, 0.085f, 0.115f), "Glove", null, 0.1f);
            for (int i = 0; i < 4; i++)
                Toon(PrimitiveType.Capsule, "Finger", hand, new Vector3(0.045f, 0.01f - i * 0.002f, -0.042f + i * 0.028f),
                    new Vector3(0.032f, 0.05f, 0.032f), "Glove", new Vector3(0f, 0f, 90f), 0.14f);
            Toon(PrimitiveType.Capsule, "Thumb", hand, new Vector3(0.02f, 0.05f, 0.04f), new Vector3(0.032f, 0.045f, 0.032f), "Glove", new Vector3(40f, 0f, 60f), 0.14f);
            var wrist = Empty("Wrist", hand, new Vector3(-0.01f, -0.04f, -0.05f), sleeveEuler).transform;
            Toon(PrimitiveType.Cylinder, "Cuff", wrist, new Vector3(0f, -0.03f, 0f), new Vector3(0.12f, 0.035f, 0.12f), "Glove", null, 0.1f);
            Toon(PrimitiveType.Capsule, "Sleeve", wrist, new Vector3(0f, -0.25f, 0f), new Vector3(0.13f, 0.22f, 0.13f), "Denim", null, 0.08f);
        }

        // ------------------------------------------------------------------ 1. Auto rifle

        static GameObject AutoRifleModel(Transform holder, out Transform muzzle)
        {
            var root = GunRoot("AutoRifle", holder, Vector3.zero);
            // Receiver + top
            Toon(PrimitiveType.Cube, "Receiver", root, new Vector3(0f, 0f, 0.12f), new Vector3(0.1f, 0.12f, 0.34f), "GunRed");
            Toon(PrimitiveType.Cube, "Rail", root, new Vector3(0f, 0.07f, 0.13f), new Vector3(0.06f, 0.025f, 0.3f), "GunMetal");
            Toon(PrimitiveType.Cube, "SightBase", root, new Vector3(0f, 0.1f, 0.08f), new Vector3(0.05f, 0.04f, 0.08f), "GunMetal");
            Toon(PrimitiveType.Cylinder, "SightRing", root, new Vector3(0f, 0.135f, 0.08f), new Vector3(0.07f, 0.02f, 0.07f), "GunMetal", new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Sphere, "RedDot", root, new Vector3(0f, 0.135f, 0.09f), Vector3.one * 0.02f, "RedDot", false, shadows: false);
            // Barrel + muzzle brake
            Toon(PrimitiveType.Cylinder, "Barrel", root, new Vector3(0f, 0.015f, 0.47f), new Vector3(0.045f, 0.13f, 0.045f), "GunMetal", new Vector3(90f, 0f, 0f));
            Toon(PrimitiveType.Cylinder, "Brake", root, new Vector3(0f, 0.015f, 0.6f), new Vector3(0.07f, 0.04f, 0.07f), "GunYellow", new Vector3(90f, 0f, 0f));
            // Wooden handguard + stock + grip
            Toon(PrimitiveType.Capsule, "Handguard", root, new Vector3(0f, -0.01f, 0.36f), new Vector3(0.1f, 0.12f, 0.1f), "GunWood", new Vector3(90f, 0f, 0f));
            Toon(PrimitiveType.Cube, "Stock", root, new Vector3(0f, -0.03f, -0.13f), new Vector3(0.08f, 0.11f, 0.2f), "GunWood", new Vector3(-6f, 0f, 0f));
            Toon(PrimitiveType.Cube, "ButtPad", root, new Vector3(0f, -0.04f, -0.235f), new Vector3(0.085f, 0.13f, 0.03f), "GunMetal", new Vector3(-6f, 0f, 0f));
            Toon(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.11f, 0.02f), new Vector3(0.06f, 0.13f, 0.07f), "GunWood", new Vector3(-18f, 0f, 0f));
            // Curved banana magazine
            Toon(PrimitiveType.Cube, "MagTop", root, new Vector3(0f, -0.1f, 0.19f), new Vector3(0.06f, 0.1f, 0.07f), "GunYellow", new Vector3(10f, 0f, 0f));
            Toon(PrimitiveType.Cube, "MagBottom", root, new Vector3(0f, -0.18f, 0.22f), new Vector3(0.058f, 0.09f, 0.068f), "GunYellow", new Vector3(28f, 0f, 0f));
            // Hands
            Glove(root, "RightHand", new Vector3(-0.005f, -0.12f, 0.01f), new Vector3(-18f, 0f, 0f), new Vector3(-70f, 0f, -10f));
            Glove(root, "LeftHand", new Vector3(-0.02f, -0.06f, 0.37f), new Vector3(0f, 0f, 10f), new Vector3(-40f, 0f, 45f));
            muzzle = Empty("Muzzle", root, new Vector3(0f, 0.015f, 0.65f)).transform;
            return root.parent.gameObject;
        }

        // ------------------------------------------------------------------ 2. Shotgun

        static GameObject ShotgunModel(Transform holder, out Transform muzzle)
        {
            var root = GunRoot("Shotgun", holder, Vector3.zero);
            Toon(PrimitiveType.Cube, "Stock", root, new Vector3(0f, -0.04f, -0.08f), new Vector3(0.1f, 0.13f, 0.28f), "GunWood", new Vector3(-6f, 0f, 0f));
            Toon(PrimitiveType.Cube, "Receiver", root, new Vector3(0f, 0.01f, 0.12f), new Vector3(0.12f, 0.12f, 0.16f), "GunMetal");
            Toon(PrimitiveType.Cube, "Plate", root, new Vector3(0.062f, 0.01f, 0.12f), new Vector3(0.01f, 0.08f, 0.11f), "Gold", null, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                Toon(PrimitiveType.Cylinder, "Barrel", root, new Vector3(s * 0.032f, 0.03f, 0.4f), new Vector3(0.062f, 0.22f, 0.062f), "GunMetal", new Vector3(90f, 0f, 0f));
                Toon(PrimitiveType.Cylinder, "Ring", root, new Vector3(s * 0.032f, 0.03f, 0.6f), new Vector3(0.072f, 0.02f, 0.072f), "Gold", new Vector3(90f, 0f, 0f));
                Toon(PrimitiveType.Cylinder, "Ring2", root, new Vector3(s * 0.032f, 0.03f, 0.3f), new Vector3(0.07f, 0.015f, 0.07f), "Gold", new Vector3(90f, 0f, 0f));
            }
            Toon(PrimitiveType.Capsule, "Pump", root, new Vector3(0f, -0.035f, 0.38f), new Vector3(0.11f, 0.11f, 0.11f), "GunWood", new Vector3(90f, 0f, 0f));
            Toon(PrimitiveType.Sphere, "Bead", root, new Vector3(0f, 0.08f, 0.6f), Vector3.one * 0.025f, "Gold", null, 0f);
            Toon(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.1f, 0.03f), new Vector3(0.065f, 0.12f, 0.075f), "GunWood", new Vector3(-22f, 0f, 0f));
            Glove(root, "RightHand", new Vector3(-0.005f, -0.11f, 0.02f), new Vector3(-22f, 0f, 0f), new Vector3(-70f, 0f, -10f));
            Glove(root, "LeftHand", new Vector3(-0.02f, -0.08f, 0.38f), new Vector3(0f, 0f, 10f), new Vector3(-40f, 0f, 45f));
            muzzle = Empty("Muzzle", root, new Vector3(0f, 0.03f, 0.64f)).transform;
            root.parent.gameObject.SetActive(false);
            return root.parent.gameObject;
        }

        // ------------------------------------------------------------------ 3. Weed sprayer

        static GameObject SprayerModel(Transform holder, out Transform muzzle)
        {
            var root = GunRoot("WeedSprayer", holder, new Vector3(0f, 0.01f, 0f));
            Toon(PrimitiveType.Capsule, "Tank", root, new Vector3(0f, -0.02f, 0.02f), new Vector3(0.2f, 0.14f, 0.2f), "TankBlue", new Vector3(90f, 0f, 0f));
            Toon(PrimitiveType.Capsule, "Window", root, new Vector3(0.085f, -0.02f, 0.02f), new Vector3(0.05f, 0.09f, 0.08f), "Ooze", new Vector3(90f, 0f, 0f), 0.15f);
            Toon(PrimitiveType.Cylinder, "Band", root, new Vector3(0f, -0.02f, 0.1f), new Vector3(0.21f, 0.015f, 0.21f), "GunYellow", new Vector3(90f, 0f, 0f));
            Toon(PrimitiveType.Cylinder, "Band2", root, new Vector3(0f, -0.02f, -0.06f), new Vector3(0.21f, 0.015f, 0.21f), "GunYellow", new Vector3(90f, 0f, 0f));
            Toon(PrimitiveType.Cylinder, "PumpRod", root, new Vector3(0f, 0.12f, -0.02f), new Vector3(0.025f, 0.05f, 0.025f), "GunMetal");
            Toon(PrimitiveType.Capsule, "PumpHandle", root, new Vector3(0f, 0.17f, -0.02f), new Vector3(0.04f, 0.07f, 0.04f), "GunRed", new Vector3(0f, 0f, 90f));
            Toon(PrimitiveType.Cylinder, "Wand", root, new Vector3(0f, 0.03f, 0.3f), new Vector3(0.03f, 0.16f, 0.03f), "GunMetal", new Vector3(90f, 0f, 0f));
            Toon(PrimitiveType.Cylinder, "Nozzle", root, new Vector3(0f, 0.03f, 0.48f), new Vector3(0.06f, 0.035f, 0.06f), "GunYellow", new Vector3(90f, 0f, 0f));
            Toon(PrimitiveType.Cylinder, "Tip", root, new Vector3(0f, 0.03f, 0.515f), new Vector3(0.035f, 0.01f, 0.035f), "Ooze", new Vector3(90f, 0f, 0f), 0f);
            for (int i = 0; i < 4; i++) // coiled hose
                Toon(PrimitiveType.Sphere, "Hose", root, new Vector3(-0.09f - Mathf.Sin(i * 0.9f) * 0.03f, -0.08f - i * 0.025f, 0.12f - i * 0.05f), Vector3.one * 0.04f, "GunGreen", null, 0.12f);
            Toon(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.15f, 0.02f), new Vector3(0.05f, 0.12f, 0.06f), "GunRed", new Vector3(-12f, 0f, 0f));
            Glove(root, "RightHand", new Vector3(-0.005f, -0.16f, 0.01f), new Vector3(-12f, 0f, 0f), new Vector3(-70f, 0f, -10f));
            Glove(root, "LeftHand", new Vector3(-0.02f, 0.0f, 0.3f), new Vector3(0f, 0f, 10f), new Vector3(-40f, 0f, 45f), 0.9f);
            muzzle = Empty("Muzzle", root, new Vector3(0f, 0.03f, 0.53f)).transform;
            root.parent.gameObject.SetActive(false);
            return root.parent.gameObject;
        }

        // ------------------------------------------------------------------ 4. Seed launcher

        static GameObject LauncherModel(Transform holder, out Transform muzzle)
        {
            var root = GunRoot("SeedLauncher", holder, new Vector3(0.03f, -0.02f, 0.04f));
            root.localScale = Vector3.one * 0.7f;
            Toon(PrimitiveType.Cylinder, "Tube", root, new Vector3(0f, 0.03f, 0.25f), new Vector3(0.15f, 0.3f, 0.15f), "GunGreen", new Vector3(90f, 0f, 0f));
            Toon(PrimitiveType.Cylinder, "Ring", root, new Vector3(0f, 0.03f, 0.52f), new Vector3(0.18f, 0.035f, 0.18f), "GunYellow", new Vector3(90f, 0f, 0f));
            Toon(PrimitiveType.Cylinder, "BackCap", root, new Vector3(0f, 0.03f, -0.05f), new Vector3(0.16f, 0.025f, 0.16f), "GunYellow", new Vector3(90f, 0f, 0f));
            // Sunflower petals around the muzzle
            for (int i = 0; i < 10; i++)
            {
                var petal = Empty("PetalPivot", root, new Vector3(0f, 0.03f, 0.55f), new Vector3(0f, 0f, i * 36f)).transform;
                Toon(PrimitiveType.Sphere, "Petal", petal, new Vector3(0f, 0.11f, 0f), new Vector3(0.045f, 0.07f, 0.02f), "Sunflower", null, 0.15f);
            }
            // Seed drum
            Toon(PrimitiveType.Cylinder, "Drum", root, new Vector3(0f, -0.08f, 0.1f), new Vector3(0.17f, 0.06f, 0.17f), "GunWood", new Vector3(0f, 0f, 90f));
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Sphere, "Seed", root, new Vector3(0.062f, -0.08f + Mathf.Sin(i * 2.1f) * 0.05f, 0.1f + Mathf.Cos(i * 2.1f) * 0.05f), Vector3.one * 0.035f, "SeedGlow", false);
            Toon(PrimitiveType.Cube, "Grip", root, new Vector3(0f, -0.15f, -0.01f), new Vector3(0.06f, 0.14f, 0.07f), "GunWood", new Vector3(-15f, 0f, 0f));
            Toon(PrimitiveType.Cube, "ForeGrip", root, new Vector3(0f, -0.08f, 0.33f), new Vector3(0.05f, 0.1f, 0.06f), "GunWood");
            Glove(root, "RightHand", new Vector3(-0.005f, -0.15f, -0.01f), new Vector3(-15f, 0f, 0f), new Vector3(-70f, 0f, -10f));
            Glove(root, "LeftHand", new Vector3(-0.005f, -0.1f, 0.33f), new Vector3(0f, 0f, 0f), new Vector3(-40f, 0f, 45f));
            muzzle = Empty("Muzzle", root, new Vector3(0f, 0.03f, 0.58f)).transform;
            root.parent.gameObject.SetActive(false);
            return root.parent.gameObject;
        }
    }
}

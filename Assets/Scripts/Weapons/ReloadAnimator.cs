using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Procedural reload animations for a first-person weapon model. WeaponInventory drives it
    /// with normalized progress values; this script moves the gun pose, the left hand and the
    /// moving parts (magazine, pump, drum...) and plays the matching sounds.
    /// </summary>
    public class ReloadAnimator : MonoBehaviour
    {
        public enum Style
        {
            /// <summary>Auto rifle: mag out, new mag in, rack the charging handle.</summary>
            Magazine,
            /// <summary>Shotgun: shells pushed in one at a time, then a pump.</summary>
            ShellByShell,
            /// <summary>Weed sprayer: pump the tank, liquid level and gauge rise.</summary>
            Pump,
            /// <summary>Seed launcher: drum spins while seeds pop into the chambers.</summary>
            Drum,
        }

        public Style style;
        [Tooltip("The model's 'Pose' child, tilted during the reload.")]
        public Transform pose;
        public Transform leftHand;
        [Tooltip("Magazine group / pump group / drum group.")]
        public Transform part;
        [Tooltip("Charging handle (rifle) or gauge needle (sprayer).")]
        public Transform bolt;
        [Tooltip("Shotgun shell visual, or sprayer liquid window.")]
        public Transform extra;
        [Tooltip("Seeds in the launcher drum (one per round).")]
        public Transform[] items;

        bool initialized;
        Vector3 poseRestPos, handRestPos, partRestPos, boltRestPos, extraRestPos, extraRestScale;
        Quaternion poseRestRot, partRestRot, boltRestRot;
        Vector3[] itemScales;
        float prevT;
        float ammo01 = 1f;

        void Init()
        {
            if (initialized) return;
            initialized = true;
            if (pose != null) { poseRestPos = pose.localPosition; poseRestRot = pose.localRotation; }
            if (leftHand != null) handRestPos = leftHand.localPosition;
            if (part != null) { partRestPos = part.localPosition; partRestRot = part.localRotation; }
            if (bolt != null) { boltRestPos = bolt.localPosition; boltRestRot = bolt.localRotation; }
            if (extra != null) { extraRestPos = extra.localPosition; extraRestScale = extra.localScale; }
            itemScales = new Vector3[items != null ? items.Length : 0];
            for (int i = 0; i < itemScales.Length; i++) itemScales[i] = items[i].localScale;
            if (style == Style.ShellByShell && extra != null) extra.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- Helpers

        static float Ease(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }
        static float Range(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));
        /// <summary>0 → 1 between a..b, holds, 1 → 0 between c..d.</summary>
        static float Bump(float t, float a, float b, float c, float d) => Ease(Range(t, a, b)) * (1f - Ease(Range(t, c, d)));
        bool Crossed(float t, float mark) => prevT < mark && t >= mark;

        // Reload pose: the gun is lifted towards the centre of the screen and angled so the part
        // being reloaded (magazine well, loading port, drum...) faces the player.
        // Reload poses roll the gun hard so the part being reloaded faces the camera:
        // magazine well / loading port (underside) for the rifle and shotgun, drum and liquid
        // window (right side) for the launcher and sprayer.
        Vector3 TiltEuler => style switch
        {
            Style.Magazine => new Vector3(-10f, 0f, -62f),
            Style.ShellByShell => new Vector3(-12f, 0f, -58f),
            Style.Pump => new Vector3(-12f, 0f, 38f),
            _ => new Vector3(-8f, 0f, 66f),
        };

        Vector3 TiltOffset => style switch
        {
            Style.Magazine => new Vector3(-0.12f, 0.13f, 0f),
            Style.ShellByShell => new Vector3(-0.1f, 0.13f, 0f),
            Style.Pump => new Vector3(-0.1f, 0.1f, 0f),
            _ => new Vector3(-0.12f, 0.12f, 0f),
        };

        /// <summary>Tilts the gun towards the player for the reload (0 = rest, 1 = fully tilted).</summary>
        public void ApplyTilt(float amount, Vector3 kick = default)
        {
            Init();
            if (pose == null) return;
            pose.localRotation = poseRestRot * Quaternion.Euler(TiltEuler * amount + kick);
            pose.localPosition = poseRestPos + TiltOffset * amount;
        }

        /// <summary>Puts everything back (reload finished or cancelled).</summary>
        public void Rest()
        {
            Init();
            ApplyTilt(0f);
            if (leftHand != null) leftHand.localPosition = handRestPos;
            if (part != null) { part.localPosition = partRestPos; part.localRotation = partRestRot; part.localScale = Vector3.one; }
            if (bolt != null) { bolt.localPosition = boltRestPos; bolt.localRotation = boltRestRot; }
            if (style == Style.ShellByShell && extra != null) extra.gameObject.SetActive(false);
            prevT = 0f;
            ShowAmmo();
        }

        /// <summary>Visual ammo state: seeds left in the drum, liquid left in the sprayer.</summary>
        public void SetAmmo(int ammo, int max)
        {
            Init();
            ammo01 = max > 0 ? Mathf.Clamp01(ammo / (float)max) : 1f;
            ShowAmmo(ammo);
        }

        void ShowAmmo(int ammo = -1)
        {
            if (style == Style.Drum && items != null)
            {
                int visible = ammo >= 0 ? ammo : Mathf.RoundToInt(ammo01 * items.Length);
                for (int i = 0; i < items.Length; i++) items[i].localScale = i < visible ? itemScales[i] : Vector3.zero;
            }
            if (style == Style.Pump)
            {
                SetLiquid(ammo01);
            }
        }

        void SetLiquid(float level)
        {
            if (extra != null) extra.localScale = new Vector3(extraRestScale.x, extraRestScale.y * Mathf.Max(0.08f, level), extraRestScale.z);
            if (bolt != null) bolt.localRotation = boltRestRot * Quaternion.Euler(0f, Mathf.Lerp(-70f, 50f, level), 0f);
        }

        // ---------------------------------------------------------------- Full reloads

        /// <summary>Magazine, Pump and Drum styles: t = 0..1 over the whole reload.</summary>
        public void SampleFull(float t)
        {
            Init();
            switch (style)
            {
                case Style.Magazine: SampleMagazine(t); break;
                case Style.Pump: SamplePumpTank(t); break;
                case Style.Drum: SampleDrum(t); break;
            }
            prevT = t;
        }

        void SampleMagazine(float t)
        {
            // Slap kick when the new mag goes in.
            float slap = Mathf.Sin(Range(t, 0.57f, 0.66f) * Mathf.PI) * 7f;
            ApplyTilt(Bump(t, 0f, 0.12f, 0.85f, 1f), new Vector3(slap, 0f, 0f));

            var down = Vector3.down;
            if (part != null)
            {
                if (t < 0.12f) { part.localPosition = partRestPos; part.localScale = Vector3.one; }
                else if (t < 0.28f) { float k = Range(t, 0.12f, 0.28f); part.localPosition = partRestPos + down * 0.4f * k * k; part.localScale = Vector3.one; }
                else if (t < 0.42f) part.localScale = Vector3.zero;
                else if (t < 0.58f) { part.localScale = Vector3.one; part.localPosition = partRestPos + down * 0.28f * (1f - Ease(Range(t, 0.42f, 0.58f))); }
                else { part.localPosition = partRestPos; part.localScale = Vector3.one; }
            }

            if (leftHand != null)
            {
                var magPoint = new Vector3(-0.01f, -0.19f, 0.19f);
                Vector3 p;
                if (t < 0.1f) p = handRestPos;
                else if (t < 0.22f) p = Vector3.Lerp(handRestPos, magPoint, Ease(Range(t, 0.1f, 0.22f)));
                else if (t < 0.34f) p = Vector3.Lerp(magPoint, magPoint + down * 0.4f, Ease(Range(t, 0.22f, 0.34f)));
                else if (t < 0.44f) p = Vector3.Lerp(magPoint + down * 0.4f, magPoint + down * 0.28f, Ease(Range(t, 0.34f, 0.44f)));
                else if (t < 0.58f) p = Vector3.Lerp(magPoint + down * 0.28f, magPoint, Ease(Range(t, 0.44f, 0.58f)));
                else if (t < 0.66f) p = magPoint;
                else if (t < 0.72f) p = Vector3.Lerp(magPoint, new Vector3(0.07f, 0.06f, 0.05f), Ease(Range(t, 0.66f, 0.72f)));
                else if (t < 0.86f) p = new Vector3(0.07f, 0.06f, 0.05f) + Vector3.back * 0.05f * Bump(t, 0.72f, 0.78f, 0.8f, 0.84f);
                else p = Vector3.Lerp(new Vector3(0.07f, 0.06f, 0.05f), handRestPos, Ease(Range(t, 0.86f, 0.98f)));
                leftHand.localPosition = p;
            }

            if (bolt != null) bolt.localPosition = boltRestPos + Vector3.back * 0.055f * Bump(t, 0.72f, 0.78f, 0.8f, 0.84f);

            if (Crossed(t, 0.14f)) SoundFX.PlayVaried(SoundFX.Sfx.MagOut, 0.8f);
            if (Crossed(t, 0.57f)) SoundFX.PlayVaried(SoundFX.Sfx.MagIn, 0.9f);
            if (Crossed(t, 0.76f)) SoundFX.PlayVaried(SoundFX.Sfx.Bolt, 0.9f);
        }

        void SamplePumpTank(float t)
        {
            ApplyTilt(Bump(t, 0f, 0.12f, 0.86f, 1f));
            var handleSpot = new Vector3(-0.12f, 0.2f, 0.06f);
            float stroke = 0f;
            float k = Range(t, 0.2f, 0.8f);
            if (t > 0.2f && t < 0.8f) stroke = 0.5f - 0.5f * Mathf.Cos(k * Mathf.PI * 2f * 3f);
            if (part != null) part.localPosition = partRestPos + Vector3.up * 0.07f * stroke;
            if (leftHand != null)
            {
                float reach = Bump(t, 0.08f, 0.2f, 0.82f, 0.94f);
                leftHand.localPosition = Vector3.Lerp(handRestPos, handleSpot + Vector3.up * 0.07f * stroke, reach);
            }
            SetLiquid(Mathf.Lerp(ammo01, 1f, Ease(Range(t, 0.2f, 0.85f))));
            for (int s = 0; s < 3; s++)
                if (Crossed(t, 0.2f + s * 0.2f + 0.1f)) SoundFX.PlayVaried(SoundFX.Sfx.Pump, 0.8f, 0.12f);
        }

        void SampleDrum(float t)
        {
            ApplyTilt(Bump(t, 0f, 0.12f, 0.86f, 1f));
            if (part != null) part.localRotation = partRestRot * Quaternion.Euler(720f * Ease(Range(t, 0.1f, 0.85f)), 0f, 0f);
            if (items != null)
                for (int i = 0; i < items.Length; i++)
                {
                    float start = 0.25f + i * 0.08f;
                    float pop = t < start ? 0f : PopIn.BackOut(Range(t, start, start + 0.08f), 3f);
                    items[i].localScale = itemScales[i] * pop;
                    if (Crossed(t, start)) SoundFX.PlayVaried(SoundFX.Sfx.ShellIn, 0.6f, 0.15f);
                }
            if (leftHand != null)
                leftHand.localPosition = Vector3.Lerp(handRestPos, new Vector3(0.1f, -0.08f, 0.1f), Bump(t, 0.06f, 0.18f, 0.82f, 0.95f));
            if (Crossed(t, 0.12f)) SoundFX.PlayVaried(SoundFX.Sfx.DrumSpin, 0.8f);
            if (Crossed(t, 0.88f)) SoundFX.PlayVaried(SoundFX.Sfx.Bolt, 0.8f);
        }

        // ---------------------------------------------------------------- Shotgun

        /// <summary>One shell going in (t = 0..1 per shell).</summary>
        public void SampleShell(float t)
        {
            Init();
            ApplyTilt(1f);
            var start = new Vector3(0f, -0.3f, 0.14f);
            var port = new Vector3(0f, -0.06f, 0.13f);
            var inside = new Vector3(0f, -0.03f, 0.18f);
            Vector3 shellPos;
            if (extra != null)
            {
                extra.gameObject.SetActive(t < 0.8f);
                if (t < 0.55f) shellPos = Vector3.Lerp(start, port, Ease(Range(t, 0f, 0.55f)));
                else shellPos = Vector3.Lerp(port, inside, Ease(Range(t, 0.55f, 0.8f)));
                extra.localPosition = shellPos;
            }
            else shellPos = port;
            if (leftHand != null)
                leftHand.localPosition = Vector3.Lerp(new Vector3(-0.02f, -0.12f, 0.2f), shellPos + new Vector3(-0.02f, -0.08f, -0.01f), Bump(t, 0f, 0.2f, 0.75f, 1f));
            if (Crossed(t, 0.6f)) SoundFX.PlayVaried(SoundFX.Sfx.ShellIn, 0.9f, 0.1f);
            prevT = t;
        }

        /// <summary>Final pump after loading shells (t = 0..1), tilting back to rest.</summary>
        public void SamplePumpAction(float t)
        {
            Init();
            if (extra != null) extra.gameObject.SetActive(false);
            ApplyTilt(1f - Ease(Range(t, 0.55f, 1f)), new Vector3(Mathf.Sin(Range(t, 0f, 0.4f) * Mathf.PI) * 6f, 0f, 0f));
            float pump = Bump(t, 0f, 0.3f, 0.35f, 0.6f);
            if (part != null) part.localPosition = partRestPos + Vector3.back * 0.09f * pump;
            if (leftHand != null)
            {
                var onPump = handRestPos + Vector3.back * 0.09f * pump;
                leftHand.localPosition = Vector3.Lerp(new Vector3(-0.02f, -0.12f, 0.2f), onPump, Ease(Range(t, 0f, 0.15f)));
            }
            if (Crossed(t, 0.05f)) SoundFX.PlayVaried(SoundFX.Sfx.Pump, 0.9f);
            if (Crossed(t, 0.4f)) SoundFX.PlayVaried(SoundFX.Sfx.Bolt, 0.8f);
            prevT = t;
        }

        /// <summary>Shotgun: tilt in before the first shell (t = 0..1).</summary>
        public void SampleShellEnter(float t)
        {
            Init();
            ApplyTilt(Ease(t));
            if (leftHand != null) leftHand.localPosition = Vector3.Lerp(handRestPos, new Vector3(-0.02f, -0.12f, 0.2f), Ease(t));
            prevT = 0f;
        }
    }
}

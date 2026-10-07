using UnityEngine;

namespace MutantPlants
{
    /// <summary>
    /// Procedurally synthesized sound effects and music loops, so the game has full audio
    /// without imported assets. Replace with real AudioClips whenever you like.
    /// </summary>
    public class SoundFX : MonoBehaviour
    {
        public enum Sfx
        {
            Rifle, Shotgun, Sprayer, Launcher, Hit, EnemyDeath, PlayerHurt, Pickup, Checkpoint, Click,
            Footstep, Explosion, Spit, Splat, BossRoar, BossSlam, WaveStart, Combo, Land, AutoRifle,
            MagOut, MagIn, Bolt, ShellIn, Pump, DrumSpin, DryFire,
        }

        public enum Track { None, Menu, Game }

        static SoundFX instance;
        AudioSource sfxSource, musicSource;
        AudioClip[] clips;
        AudioClip menuMusic, gameMusic;
        Track currentTrack;

        const int SampleRate = 44100;

        static SoundFX Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("SoundFX");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<SoundFX>();
                }
                return instance;
            }
        }

        public static void Play(Sfx sfx, float volume = 1f)
        {
            var s = Instance;
            s.sfxSource.pitch = 1f;
            s.sfxSource.PlayOneShot(s.clips[(int)sfx], volume);
        }

        /// <summary>Plays with a random pitch variation, so repeated sounds don't get tiring.</summary>
        public static void PlayVaried(Sfx sfx, float volume = 1f, float pitchRange = 0.1f)
        {
            var s = Instance;
            var src = s.GetPooledSource();
            src.pitch = 1f + Random.Range(-pitchRange, pitchRange);
            src.PlayOneShot(s.clips[(int)sfx], volume);
        }

        /// <summary>3D positional sound (enemies, explosions).</summary>
        public static void PlayAt(Sfx sfx, Vector3 position, float volume = 1f)
        {
            var s = Instance;
            var go = new GameObject("Sfx3D");
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = s.clips[(int)sfx];
            src.spatialBlend = 0.85f;
            src.minDistance = 4f;
            src.maxDistance = 60f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.pitch = 1f + Random.Range(-0.08f, 0.08f);
            src.volume = volume * GameSettings.EffectsVolume;
            src.Play();
            Destroy(go, src.clip.length + 0.1f);
        }

        public static void PlayMusic(Track track)
        {
            var s = Instance;
            if (s.currentTrack == track) return;
            s.currentTrack = track;
            s.musicSource.Stop();
            if (track == Track.None) return;
            s.musicSource.clip = track == Track.Menu ? s.menuMusic : s.gameMusic;
            s.musicSource.Play();
        }

        public static void ApplyVolumes()
        {
            if (instance == null) return;
            instance.sfxSource.volume = GameSettings.EffectsVolume;
            foreach (var p in instance.pool) p.volume = GameSettings.EffectsVolume;
            instance.musicSource.volume = GameSettings.MusicVolume * 0.6f;
        }

        AudioSource[] pool;
        int poolIndex;

        AudioSource GetPooledSource()
        {
            poolIndex = (poolIndex + 1) % pool.Length;
            return pool[poolIndex];
        }

        void Awake()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            pool = new AudioSource[8];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;

            clips = new AudioClip[System.Enum.GetValues(typeof(Sfx)).Length];
            clips[(int)Sfx.Rifle] = Gunshot("Rifle", 0.28f, 16f, 0.65f, 140f);
            clips[(int)Sfx.Shotgun] = Gunshot("Shotgun", 0.5f, 7f, 0.85f, 90f);
            clips[(int)Sfx.Sprayer] = Noise("Sprayer", 0.09f, 28f, 0.22f, 0.6f);
            clips[(int)Sfx.Launcher] = Tone("Launcher", 0.25f, 240f, 90f, 0.5f, 0.25f);
            clips[(int)Sfx.Hit] = Tone("Hit", 0.07f, 1100f, 700f, 0.25f);
            clips[(int)Sfx.EnemyDeath] = Squelch("Death", 0.45f);
            clips[(int)Sfx.PlayerHurt] = Tone("Hurt", 0.3f, 180f, 55f, 0.75f, 0.3f);
            clips[(int)Sfx.Pickup] = Arpeggio("Pickup", new[] { 523f, 659f, 784f, 1046f }, 0.06f);
            clips[(int)Sfx.Checkpoint] = Arpeggio("Checkpoint", new[] { 392f, 523f, 659f, 784f }, 0.11f);
            clips[(int)Sfx.Click] = Tone("Click", 0.035f, 1400f, 1100f, 0.2f);
            clips[(int)Sfx.Footstep] = Noise("Footstep", 0.09f, 45f, 0.18f, 0.12f);
            clips[(int)Sfx.Explosion] = Gunshot("Explosion", 1.2f, 3.2f, 1f, 55f);
            clips[(int)Sfx.Spit] = Tone("Spit", 0.18f, 600f, 1400f, 0.3f, 0.5f);
            clips[(int)Sfx.Splat] = Squelch("Splat", 0.25f);
            clips[(int)Sfx.BossRoar] = Roar("Roar", 1.6f);
            clips[(int)Sfx.BossSlam] = Gunshot("Slam", 0.9f, 4f, 1f, 40f);
            clips[(int)Sfx.WaveStart] = Arpeggio("Wave", new[] { 220f, 277f, 330f, 440f }, 0.13f);
            clips[(int)Sfx.Combo] = Tone("Combo", 0.12f, 880f, 1320f, 0.25f);
            clips[(int)Sfx.Land] = Noise("Land", 0.15f, 25f, 0.3f, 0.08f);
            clips[(int)Sfx.AutoRifle] = Gunshot("AutoRifle", 0.16f, 26f, 0.55f, 180f);
            clips[(int)Sfx.MagOut] = Clicks("MagOut", new[] { 0f, 0.05f }, new[] { 2200f, 900f }, 0.35f);
            clips[(int)Sfx.MagIn] = Clicks("MagIn", new[] { 0f, 0.035f }, new[] { 700f, 1600f }, 0.5f);
            clips[(int)Sfx.Bolt] = Clicks("Bolt", new[] { 0f, 0.09f, 0.12f }, new[] { 1800f, 2600f, 1200f }, 0.45f);
            clips[(int)Sfx.ShellIn] = Clicks("ShellIn", new[] { 0f, 0.025f }, new[] { 2400f, 1300f }, 0.35f);
            clips[(int)Sfx.Pump] = Noise("Pump", 0.28f, 9f, 0.3f, 0.2f);
            clips[(int)Sfx.DrumSpin] = Clicks("DrumSpin", new[] { 0f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.3f, 0.35f }, new[] { 2000f, 1900f, 2000f, 1900f, 2000f, 1900f, 2000f, 1900f }, 0.25f);
            clips[(int)Sfx.DryFire] = Clicks("DryFire", new[] { 0f }, new[] { 3000f }, 0.3f);

            menuMusic = Music("MenuMusic", 92f, false);
            gameMusic = Music("GameMusic", 124f, true);

            ApplyVolumes();
        }

        // ---------- Synthesis helpers ----------

        static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.5453f, 1f) * 2f - 1f;

        /// <summary>Lowpassed noise burst. smooth: 0 = very muffled, 1 = bright.</summary>
        static AudioClip Noise(string name, float length, float decay, float volume, float brightness)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), brightness);
                data[i] = lp * Mathf.Exp(-t * decay) * volume;
            }
            return Make(name, data);
        }

        /// <summary>Noise crack + low thump.</summary>
        static AudioClip Gunshot(string name, float length, float decay, float volume, float thumpHz)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), Mathf.Lerp(0.9f, 0.08f, Mathf.Clamp01(t * 8f)));
                float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t * (1f - t)) * Mathf.Exp(-t * decay * 1.4f);
                data[i] = Mathf.Clamp((lp * 0.8f + thump * 0.9f) * Mathf.Exp(-t * decay) * volume * 1.3f, -1f, 1f);
            }
            return Make(name, data);
        }

        static AudioClip Tone(string name, float length, float startHz, float endHz, float volume, float noise = 0f)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                phase += 2f * Mathf.PI * Mathf.Lerp(startHz, endHz, k) / SampleRate;
                data[i] = (Mathf.Sin(phase) + Random.Range(-noise, noise)) * (1f - k) * (1f - k) * volume;
            }
            return Make(name, data);
        }

        /// <summary>Wet, wobbly squish for plants getting splattered.</summary>
        static AudioClip Squelch(string name, float length)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)n;
                float hz = Mathf.Lerp(380f, 70f, k) + Mathf.Sin(t * 70f) * 60f;
                phase += 2f * Mathf.PI * hz / SampleRate;
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.25f);
                data[i] = (Mathf.Sin(phase) * 0.6f + lp * 0.5f) * Mathf.Pow(1f - k, 1.5f) * 0.6f;
            }
            return Make(name, data);
        }

        static AudioClip Roar(string name, float length)
        {
            int n = (int)(SampleRate * length);
            var data = new float[n];
            float p1 = 0f, p2 = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.6f));
                float hz = 70f + Mathf.Sin(k * 9f) * 12f;
                p1 += 2f * Mathf.PI * hz / SampleRate;
                p2 += 2f * Mathf.PI * hz * 1.51f / SampleRate;
                lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.15f);
                float saw = (Mathf.Repeat(p1 / (2f * Mathf.PI), 1f) * 2f - 1f);
                data[i] = Mathf.Clamp((saw * 0.5f + Mathf.Sin(p2) * 0.3f + lp * 0.6f) * env * 0.8f, -1f, 1f);
            }
            return Make(name, data);
        }

        static AudioClip Arpeggio(string name, float[] notes, float noteLength)
        {
            int per = (int)(SampleRate * noteLength);
            var data = new float[per * notes.Length + per * 2];
            for (int j = 0; j < notes.Length; j++)
                for (int i = 0; i < per * 3 && j * per + i < data.Length; i++)
                {
                    float k = i / (float)(per * 3);
                    float t = i / (float)SampleRate;
                    data[j * per + i] += (Mathf.Sin(2f * Mathf.PI * notes[j] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[j] * t)) * (1f - k) * 0.22f;
                }
            return Make(name, data);
        }

        /// <summary>
        /// Generates a looping 8-bar country/chiptune track: kick, snare, hats, bass and a
        /// Karplus-Strong plucked "banjo" melody.
        /// </summary>
        static AudioClip Music(string name, float bpm, bool intense)
        {
            float beat = 60f / bpm;
            int bars = 8;
            int n = (int)(SampleRate * beat * 4 * bars);
            var data = new float[n];
            var rng = new System.Random(intense ? 11 : 5);

            // A minor pentatonic-ish progression: Am - F - C - G
            float[] roots = { 110f, 87.31f, 130.81f, 98f };
            float[] scale = { 1f, 1.189f, 1.335f, 1.498f, 1.782f, 2f, 2.378f, 2.67f };
            int sixteenth = (int)(SampleRate * beat / 4f);

            for (int step = 0; step < bars * 16; step++)
            {
                int start = step * sixteenth;
                int bar = step / 16;
                int inBar = step % 16;
                float root = roots[(bar / 2) % roots.Length];

                // Kick
                if (inBar % 4 == 0 || (intense && inBar == 10))
                    AddKick(data, start);
                // Snare
                if (inBar == 4 || inBar == 12)
                    AddNoiseHit(data, start, 0.18f, 18f, 0.28f, 0.5f);
                // Hats
                if (intense || inBar % 2 == 0)
                    AddNoiseHit(data, start, 0.04f, 80f, inBar % 4 == 2 ? 0.08f : 0.05f, 0.95f);
                // Bass: root on beats, fifth on offbeats
                if (inBar % 4 == 0 || (intense && inBar % 4 == 2))
                    AddBass(data, start, sixteenth * (intense ? 2 : 4), inBar % 8 == 6 ? root * 1.5f : root);
                // Banjo melody
                bool play = intense ? rng.NextDouble() < 0.55 : (inBar % 2 == 0 && rng.NextDouble() < 0.6);
                if (play)
                {
                    float hz = root * 2f * scale[rng.Next(scale.Length)];
                    AddPluck(data, start, hz, (int)(SampleRate * 0.6f), intense ? 0.16f : 0.2f, rng);
                }
            }

            // Soft limiter
            for (int i = 0; i < n; i++) data[i] = (float)System.Math.Tanh(data[i] * 1.2f) * 0.8f;
            return Make(name, data);
        }

        static void AddKick(float[] d, int start)
        {
            int len = (int)(SampleRate * 0.25f);
            float phase = 0f;
            for (int i = 0; i < len && start + i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                phase += 2f * Mathf.PI * Mathf.Lerp(120f, 45f, t * 6f) / SampleRate;
                d[start + i] += Mathf.Sin(phase) * Mathf.Exp(-t * 14f) * 0.55f;
            }
        }

        static void AddNoiseHit(float[] d, int start, float length, float decay, float vol, float bright)
        {
            int len = (int)(SampleRate * length);
            float lp = 0f, prev = 0f;
            for (int i = 0; i < len && start + i < d.Length; i++)
            {
                float t = i / (float)SampleRate;
                float x = Hash(start + i);
                lp = Mathf.Lerp(lp, x, bright);
                float hp = lp - prev; // crude highpass for hats
                prev = lp;
                d[start + i] += (bright > 0.9f ? hp : lp) * Mathf.Exp(-t * decay) * vol;
            }
        }

        static void AddBass(float[] d, int start, int len, float hz)
        {
            float phase = 0f;
            for (int i = 0; i < len && start + i < d.Length; i++)
            {
                float k = i / (float)len;
                phase += 2f * Mathf.PI * hz / SampleRate;
                float tri = Mathf.Asin(Mathf.Sin(phase)) * 0.64f;
                d[start + i] += tri * 0.32f * (1f - k * 0.7f) * Mathf.Min(1f, i / 200f);
            }
        }

        static void AddPluck(float[] d, int start, float hz, int len, float vol, System.Random rng)
        {
            int period = Mathf.Max(2, (int)(SampleRate / hz));
            var buf = new float[period];
            for (int i = 0; i < period; i++) buf[i] = (float)(rng.NextDouble() * 2 - 1);
            int idx = 0;
            for (int i = 0; i < len && start + i < d.Length; i++)
            {
                int next = (idx + 1) % period;
                float v = buf[idx];
                buf[idx] = 0.5f * (buf[idx] + buf[next]) * 0.994f;
                idx = next;
                d[start + i] += v * vol;
            }
        }

        /// <summary>Short metallic clicks (mechanical gun sounds).</summary>
        static AudioClip Clicks(string name, float[] times, float[] pitches, float volume)
        {
            float length = times[times.Length - 1] + 0.08f;
            int n = (int)(SampleRate * length);
            var data = new float[n];
            for (int c = 0; c < times.Length; c++)
            {
                int start = (int)(times[c] * SampleRate);
                float lp = 0f;
                for (int i = 0; i < SampleRate * 0.06f && start + i < n; i++)
                {
                    float t = i / (float)SampleRate;
                    lp = Mathf.Lerp(lp, Random.Range(-1f, 1f), 0.7f);
                    float ring = Mathf.Sin(2f * Mathf.PI * pitches[c] * t);
                    data[start + i] += (ring * 0.6f + lp * 0.5f) * Mathf.Exp(-t * 90f) * volume;
                }
            }
            return Make(name, data);
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

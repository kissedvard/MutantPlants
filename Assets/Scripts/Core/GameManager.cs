using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MutantPlants
{
    public enum GameState { Playing, Paused, Dead, Won }

    [Serializable]
    public class RoundStats
    {
        public int kills;
        public int shotsFired;
        public int shotsHit;
        public int bestCombo = 1;
        public float time;

        public int Accuracy => shotsFired > 0 ? Mathf.RoundToInt(100f * shotsHit / shotsFired) : 0;
        public string TimeText => $"{(int)(time / 60f)}:{(int)(time % 60f):00}";
    }

    /// <summary>
    /// Owns the round: score, combo, stats, win/lose, pause, checkpoints and scene flow.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Rules")]
        [Tooltip("Score needed to win the round (GDD: győzelmi célpontszám).")]
        public int targetScore = 2500;
        [Tooltip("Seconds you have to chain the next kill to keep the combo.")]
        public float comboWindow = 2.5f;
        public int maxCombo = 5;

        [Header("References")]
        public PlayerHealth player;
        public WeaponInventory weapons;
        public EnemySpawner waves;
        public NavMeshSurface navMeshSurface;
        public GameUI ui;
        public string mainMenuScene = "MainMenu";

        public GameState State { get; private set; } = GameState.Playing;
        public int Score { get; private set; }
        public int LastCheckpointScore { get; private set; } = -1;
        public int Combo { get; private set; } = 1;
        public float ComboTimeLeft { get; private set; }
        public bool NewHighScore { get; private set; }
        public RoundStats Stats { get; } = new RoundStats();

        /// <summary>0 at the start of the round, 1 when the target score is reached.</summary>
        public float Progress => Mathf.Clamp01(Score / (float)targetScore);

        public event Action<int> ScoreChanged;
        public event Action<GameState> StateChanged;
        /// <summary>(killed enemy, base points, combo multiplier). Raised before the enemy is destroyed.</summary>
        public event Action<EnemyPlant, int, int> PointsAwarded;
        public event Action<int> ComboChanged;

        int killsInCombo;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            GameSettings.EnsureLoaded();
            EnemyPlant.Alive.Clear();

            // Built at runtime so the arena can be edited freely without re-baking.
            if (navMeshSurface != null) navMeshSurface.BuildNavMesh();
        }

        void Start()
        {
            if (SaveSystem.ContinueFromCheckpoint)
            {
                SaveSystem.ContinueFromCheckpoint = false;
                var cp = SaveSystem.LoadCheckpoint();
                if (cp != null)
                {
                    Score = cp.score;
                    LastCheckpointScore = cp.score;
                    weapons.SetUnlockedMask(cp.unlockedWeaponsMask);
                    player.SetHealth(cp.health);
                    if (waves != null) waves.SetStartWave(Mathf.Max(1, cp.wave));
                }
            }
            ScoreChanged?.Invoke(Score);
            SoundFX.PlayMusic(SoundFX.Track.Game);
            SceneFader.FadeIn();
            SetState(GameState.Playing);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == GameState.Playing) SetState(GameState.Paused);
                else if (State == GameState.Paused) Resume();
            }

            if (State != GameState.Playing) return;
            Stats.time += Time.deltaTime;

            if (Combo > 1)
            {
                ComboTimeLeft -= Time.deltaTime;
                if (ComboTimeLeft <= 0f)
                {
                    Combo = 1;
                    killsInCombo = 0;
                    ComboChanged?.Invoke(Combo);
                }
            }
        }

        public void EnemyKilled(EnemyPlant enemy)
        {
            if (State != GameState.Playing) return;
            Stats.kills++;

            // Every 2 quick kills raise the multiplier.
            killsInCombo++;
            ComboTimeLeft = comboWindow;
            int newCombo = Mathf.Clamp(1 + killsInCombo / 2, 1, maxCombo);
            if (newCombo != Combo)
            {
                Combo = newCombo;
                Stats.bestCombo = Mathf.Max(Stats.bestCombo, Combo);
                SoundFX.Play(SoundFX.Sfx.Combo, 0.6f);
                ComboChanged?.Invoke(Combo);
            }

            int points = enemy.scoreValue * Combo;
            PointsAwarded?.Invoke(enemy, enemy.scoreValue, Combo);
            AddScore(points);
        }

        public void AddScore(int amount)
        {
            if (State != GameState.Playing) return;
            Score += amount;
            ScoreChanged?.Invoke(Score);
            if (Score >= targetScore) Win();
        }

        public void PlayerDied()
        {
            if (State == GameState.Dead) return;
            NewHighScore = SaveSystem.RecordScore(Score, CurrentWave, false);
            SetState(GameState.Dead);
        }

        void Win()
        {
            NewHighScore = SaveSystem.RecordScore(Score, CurrentWave, true);
            SaveSystem.ClearCheckpoint();
            SetState(GameState.Won);
        }

        int CurrentWave => waves != null ? waves.Wave : 0;

        /// <summary>Called by the barn. Returns true if a new checkpoint was written.</summary>
        public bool TrySaveCheckpoint()
        {
            if (State != GameState.Playing || Score <= LastCheckpointScore) return false;
            LastCheckpointScore = Score;
            SaveSystem.SaveCheckpoint(new CheckpointData
            {
                score = Score,
                wave = Mathf.Max(1, CurrentWave),
                unlockedWeaponsMask = weapons.UnlockedMask,
                health = player.Current,
            });
            return true;
        }

        public void Resume() => SetState(GameState.Playing);

        public void RestartRound(bool fromCheckpoint)
        {
            SaveSystem.ContinueFromCheckpoint = fromCheckpoint && SaveSystem.HasCheckpoint;
            if (!fromCheckpoint) SaveSystem.ClearCheckpoint();
            SceneFader.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void GoToMainMenu()
        {
            SceneFader.LoadScene(mainMenuScene);
        }

        void SetState(GameState state)
        {
            State = state;
            bool playing = state == GameState.Playing;
            Time.timeScale = playing ? 1f : 0f;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
            StateChanged?.Invoke(state);
        }
    }
}

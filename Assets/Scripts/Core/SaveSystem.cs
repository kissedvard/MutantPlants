using System.Collections.Generic;
using UnityEngine;

namespace MutantPlants
{
    [System.Serializable]
    public class CheckpointData
    {
        public int score;
        public int wave;
        public int unlockedWeaponsMask;
        public float health;
    }

    [System.Serializable]
    public class ScoreEntry
    {
        public int score;
        public int wave;
        public bool won;
        public string date;
    }

    [System.Serializable]
    class Leaderboard
    {
        public List<ScoreEntry> entries = new List<ScoreEntry>();
    }

    /// <summary>
    /// Leaderboard and checkpoint persistence (GDD: "Checkpoint", "Pontszám mentése").
    /// </summary>
    public static class SaveSystem
    {
        const string KeyLeaderboard = "save.leaderboard";
        const string KeyCheckpoint = "save.checkpoint";
        const int LeaderboardSize = 5;

        /// <summary>Set by the main menu: should the Farm scene resume from the saved checkpoint?</summary>
        public static bool ContinueFromCheckpoint;

        public static List<ScoreEntry> TopScores => LoadLeaderboard().entries;

        public static int HighScore
        {
            get
            {
                var top = TopScores;
                return top.Count > 0 ? top[0].score : 0;
            }
        }

        /// <summary>Records a finished round. Returns true if it is a new best score.</summary>
        public static bool RecordScore(int score, int wave, bool won)
        {
            if (score <= 0) return false;
            bool best = score > HighScore;
            var board = LoadLeaderboard();
            board.entries.Add(new ScoreEntry { score = score, wave = wave, won = won, date = System.DateTime.Now.ToString("yyyy-MM-dd") });
            board.entries.Sort((a, b) => b.score.CompareTo(a.score));
            if (board.entries.Count > LeaderboardSize) board.entries.RemoveRange(LeaderboardSize, board.entries.Count - LeaderboardSize);
            PlayerPrefs.SetString(KeyLeaderboard, JsonUtility.ToJson(board));
            PlayerPrefs.Save();
            return best;
        }

        static Leaderboard LoadLeaderboard()
        {
            var json = PlayerPrefs.GetString(KeyLeaderboard, "");
            return string.IsNullOrEmpty(json) ? new Leaderboard() : JsonUtility.FromJson<Leaderboard>(json);
        }

        public static bool HasCheckpoint => PlayerPrefs.HasKey(KeyCheckpoint);

        public static void SaveCheckpoint(CheckpointData data)
        {
            PlayerPrefs.SetString(KeyCheckpoint, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static CheckpointData LoadCheckpoint()
        {
            return HasCheckpoint ? JsonUtility.FromJson<CheckpointData>(PlayerPrefs.GetString(KeyCheckpoint)) : null;
        }

        public static void ClearCheckpoint()
        {
            PlayerPrefs.DeleteKey(KeyCheckpoint);
            PlayerPrefs.Save();
        }
    }
}

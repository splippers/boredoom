using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Chorewars.Core
{
    /// <summary>
    /// Singleton player profile with full persistence to disk (JSON).
    /// Handles ghost run data, lifetime stats, and achievements.
    /// </summary>
    public class PlayerProfile : MonoBehaviour
    {
        public static PlayerProfile Instance { get; private set; }

        [Serializable]
        public class GhostData
        {
            public string choreMode;
            public List<SerializableVector3> positions = new();
            public float durationSeconds;
            public int pointsScored;
            public string dateUtc;
        }

        [Serializable]
        public class Achievement
        {
            public string id;
            public string title;
            public string description;
            public bool unlocked;
            public string unlockedDateUtc;
        }

        [Serializable]
        private class SaveData
        {
            public string playerId;
            public string displayName = "ChoreHero";
            public int lifetimePoints;
            public int totalSessions;
            public int currentStreak;
            public string lastSessionDateUtc;
            public List<GhostData> bestGhosts = new();
            public List<Achievement> achievements = new();
        }

        [Serializable]
        public struct SerializableVector3
        {
            public float x, y, z;
            public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
            public Vector3 ToVector3() => new(x, y, z);
        }

        private SaveData _data = new();
        private static readonly string SavePath =
            Path.Combine(Application.persistentDataPath, "player_profile.json");

        // Public read-only accessors
        public string DisplayName => _data.displayName;
        public int LifetimePoints => _data.lifetimePoints;
        public int TotalSessions => _data.totalSessions;
        public int CurrentStreak => _data.currentStreak;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
            if (string.IsNullOrEmpty(_data.playerId))
                _data.playerId = Guid.NewGuid().ToString();
        }

        // ── Ghost Data ────────────────────────────────────────────────────────

        public bool TryGetBestGhost(string choreMode, out List<Vector3> path, out float duration)
        {
            path = null; duration = 0f;
            var ghost = _data.bestGhosts.Find(g => g.choreMode == choreMode);
            if (ghost == null || ghost.positions == null || ghost.positions.Count < 2) return false;

            path = new List<Vector3>(ghost.positions.Count);
            foreach (var sv in ghost.positions) path.Add(sv.ToVector3());
            duration = ghost.durationSeconds;
            return true;
        }

        /// <summary>
        /// Best recorded score for a chore mode, or 0 when the mode has never been played. Used by
        /// the end-of-session summary to compare the run just finished against a personal best.
        /// </summary>
        public int GetBestPoints(string choreMode)
        {
            var ghost = _data.bestGhosts.Find(g => g.choreMode == choreMode);
            return ghost?.pointsScored ?? 0;
        }

        public void SaveGhost(string choreMode, List<Vector3> path, float duration, int points)
        {
            var existing = _data.bestGhosts.Find(g => g.choreMode == choreMode);
            if (existing != null && existing.pointsScored >= points) return; // not a PB

            var ghost = existing ?? new GhostData { choreMode = choreMode };
            ghost.positions = new List<SerializableVector3>(path.Count);
            foreach (var v in path) ghost.positions.Add(new SerializableVector3(v));
            ghost.durationSeconds = duration;
            ghost.pointsScored = points;
            ghost.dateUtc = DateTime.UtcNow.ToString("o");

            if (existing == null) _data.bestGhosts.Add(ghost);
            Save();
        }

        // ── Session Recording ─────────────────────────────────────────────────

        public void RecordSession(ChoreResult result)
        {
            _data.lifetimePoints += result.totalPoints;
            _data.totalSessions++;

            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (_data.lastSessionDateUtc == today)
            {
                // same day — no streak change
            }
            else if (_data.lastSessionDateUtc ==
                     DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"))
            {
                _data.currentStreak++;
            }
            else
            {
                _data.currentStreak = 1;
            }
            _data.lastSessionDateUtc = today;

            CheckAchievements(result);
            Save();
        }

        // ── Achievements ──────────────────────────────────────────────────────

        private void CheckAchievements(ChoreResult result)
        {
            TryUnlock("first_clean",    "First Clean",          "Complete your first session",
                      _data.totalSessions >= 1);
            TryUnlock("streak_3",       "Hat Trick",            "3-day cleaning streak",
                      _data.currentStreak >= 3);
            TryUnlock("streak_7",       "Week Warrior",         "7-day cleaning streak",
                      _data.currentStreak >= 7);
            TryUnlock("lifetime_10k",   "10K Club",             "Earn 10,000 lifetime points",
                      _data.lifetimePoints >= 10000);
            TryUnlock("grade_s",        "S-Class Cleaner",      "Score S grade in any session",
                      result.grade is "S" or "S+");
            TryUnlock("perfect_cover",  "No Spot Left Behind",  "Achieve 100% coverage",
                      result.session?.coveragePercent >= 99.9f);
        }

        private void TryUnlock(string id, string title, string description, bool condition)
        {
            if (!condition) return;
            var existing = _data.achievements.Find(a => a.id == id);
            if (existing != null && existing.unlocked) return;

            if (existing == null)
            {
                existing = new Achievement { id = id, title = title, description = description };
                _data.achievements.Add(existing);
            }
            existing.unlocked = true;
            existing.unlockedDateUtc = DateTime.UtcNow.ToString("o");
            Debug.Log($"[BoreDOOM] Achievement unlocked: {title}");
        }

        public List<Achievement> GetAchievements() => new(_data.achievements);

        // ── Persistence ───────────────────────────────────────────────────────

        private void Load()
        {
            try
            {
                if (File.Exists(SavePath))
                    _data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) ?? new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerProfile] Load failed: {e.Message}");
                _data = new SaveData();
            }
        }

        public void Save()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(_data, true)); }
            catch (Exception e) { Debug.LogWarning($"[PlayerProfile] Save failed: {e.Message}"); }
        }
    }
}

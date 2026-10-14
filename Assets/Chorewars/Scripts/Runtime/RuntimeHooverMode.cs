using System.Collections.Generic;
using UnityEngine;
using Chorewars.Core;
using Chorewars.Dollhouse;
using Chorewars.Modes;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Runtime-wired Hoover mode. Extends the existing <see cref="HooverModeController"/> and
    /// forwards the base class's virtual hooks to the placeholder visuals/HUD, so the full loop
    /// runs without any scene or prefab authoring.
    /// </summary>
    public sealed class RuntimeHooverMode : HooverModeController
    {
        private const float EndAtCoveragePercent = 100f;

        /// <summary>Profile key for this mode's personal best / ghost run. Named apart from the
        /// base class's serialized <c>ModeId</c> (which is empty for a runtime-added component).</summary>
        private const string ProfileModeId = "hoover";

        /// <summary>Ghost trail sampling interval, in seconds, and a hard cap on saved points.</summary>
        private const float TrailSampleInterval = 0.1f;
        private const int MaxTrailPoints = 3000;

        private PlaceholderHud _hud;
        private CoverageFloorVisualizer _floorVisualizer;
        private PlaceholderToolDriver _toolDriver;
        private readonly List<Vector3> _trail = new();
        private float _lastTrailSampleAt;
        private float _startTime;
        private float _timeLimitSeconds = 90f;
        private bool _begun;
        private bool _ended;

        /// <summary>Supplies the per-room bonus. Optional; without it the bonus is zero.</summary>
        public RoomCleanCelebration celebration;

        /// <summary>Source of the per-room breakdown for the summary. Optional.</summary>
        public DollhouseManager dollhouse;

        public void ConfigureRuntime(
            PlaceholderHud hud,
            CoverageFloorVisualizer floorVisualizer,
            PlaceholderToolDriver toolDriver,
            float timeLimitSeconds)
        {
            _hud = hud;
            _floorVisualizer = floorVisualizer;
            _toolDriver = toolDriver;
            _timeLimitSeconds = Mathf.Max(5f, timeLimitSeconds);
        }

        protected override void OnModeBegun()
        {
            _begun = true;
            _startTime = Time.time;
        }

        protected override void OnTrackedPositionSampled(Vector3 worldPos)
        {
            _floorVisualizer?.Mark(worldPos);

            // Ghost trail: sampled sparsely and capped, so the saved path stays small.
            if (Time.time - _lastTrailSampleAt >= TrailSampleInterval)
            {
                _lastTrailSampleAt = Time.time;
                if (_trail.Count < MaxTrailPoints) _trail.Add(worldPos);
            }
        }

        protected override void Update()
        {
            if (!_begun || _ended) return;

            base.Update();

            // One key flips the whole juice layer. Scoring never reads it, so this can be toggled
            // mid-run without changing the result.
            if (_toolDriver != null && _toolDriver.ConsumeJuiceToggle())
            {
                bool on = JuiceSettings.Toggle();
                Debug.Log($"[BoreDOOM] Juice {(on ? "ON" : "OFF")}");
            }

            float elapsed = Time.time - _startTime;
            float coverage = coverageMap != null ? coverageMap.ComputeCoveragePercent() : 0f;

            // Live score uses the same formula as ScoringEngine (coverage x10 + room bonus). The
            // session's efficiency/movement terms are never assigned anywhere, so this is not an
            // estimate — it is exactly what the summary screen will show.
            int points = (int)(coverage * 10f) + (celebration != null ? celebration.BonusPoints : 0);

            _hud?.SetCoverage(coverage);
            _hud?.SetPoints(points);
            _hud?.SetTimer(_timeLimitSeconds - elapsed);
            _hud?.SetGrade(EstimateGrade(points));

            bool manualEnd = _toolDriver != null && _toolDriver.EndRequested;
            if (coverage >= EndAtCoveragePercent || elapsed >= _timeLimitSeconds || manualEnd)
                End();
        }

        /// <summary>
        /// Ending is idempotent. <see cref="BaseCoverageModeController.End"/> scores the session and
        /// records it against the profile, so calling it twice would double-count the score, the
        /// lifetime points and the ghost. End can be reached from more than one place (the timer, the
        /// end key, or a UI button), so it guards itself rather than trusting its callers.
        /// </summary>
        public override void End()
        {
            if (_ended) return;

            _ended = true;
            base.End();
        }

        protected override void OnModeEnded(ChoreResult result)
        {
            int previousBest = -1;
            bool isNewBest = false;

            var profile = PlayerProfile.Instance;

            if (result != null)
            {
                float duration = Mathf.Max(0f, Time.time - _startTime);

                if (profile != null)
                {
                    // Read the old best before recording, so the compare is against the previous run.
                    previousBest = profile.GetBestPoints(ProfileModeId);
                    isNewBest = previousBest > 0 && result.totalPoints > previousBest;

                    profile.RecordSession(result);

                    if (_trail.Count >= 2) profile.SaveGhost(ProfileModeId, _trail, duration, result.totalPoints);
                }

                Debug.Log(
                    $"[BoreDOOM] Hoover ended — grade {result.grade}, points {result.totalPoints}, " +
                    $"coverage {result.session?.coveragePercent:0}%, duration {duration:0}s, " +
                    $"bestBefore {previousBest}, newBest {isNewBest}, trail {_trail.Count}");
            }

            _hud?.ShowSummary(
                result,
                dollhouse != null ? dollhouse.RoomFinalCoverage : null,
                previousBest,
                isNewBest);
        }

        /// <summary>
        /// Per-room bonus, sourced from the celebration so the score and the "+points" popups agree.
        /// </summary>
        protected override float GetBonusPoints()
        {
            return celebration != null ? celebration.BonusPoints : 0f;
        }

        /// <summary>
        /// Same thresholds as the base controller's re-grade, applied to the live score, so the grade
        /// on the HUD during play matches the grade on the summary screen.
        /// </summary>
        private static string EstimateGrade(int points)
        {
            return points switch
            {
                >= 1100 => "S+",
                >= 900 => "S",
                >= 750 => "A",
                >= 600 => "B",
                >= 400 => "C",
                _ => "D"
            };
        }
    }
}

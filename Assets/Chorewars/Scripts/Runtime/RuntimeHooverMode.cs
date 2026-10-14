using UnityEngine;
using Chorewars.Core;
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

        private PlaceholderHud _hud;
        private CoverageFloorVisualizer _floorVisualizer;
        private PlaceholderToolDriver _toolDriver;
        private float _startTime;
        private float _timeLimitSeconds = 90f;
        private bool _begun;
        private bool _ended;

        /// <summary>Supplies the per-room bonus. Optional; without it the bonus is zero.</summary>
        public RoomCleanCelebration celebration;

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
        }

        protected override void Update()
        {
            if (!_begun || _ended) return;

            base.Update();

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
            {
                _ended = true;
                End();
            }
        }

        protected override void OnModeEnded(ChoreResult result)
        {
            Debug.Log(
                $"[BoreDOOM] Hoover ended — grade {result?.grade}, points {result?.totalPoints}, " +
                $"coverage {result?.session?.coveragePercent:0}%");
            _hud?.ShowSummary(result);
        }

        /// <summary>
        /// Per-room bonus, sourced from the celebration so the score and the "+points" popups agree.
        /// </summary>
        protected override float GetBonusPoints()
        {
            return celebration != null ? celebration.BonusPoints : 0f;
        }

        /// <summary>
        /// Same thresholds as <see cref="ScoringEngine"/>, applied to the live score, so the grade on
        /// the HUD during play matches the grade on the summary screen.
        /// </summary>
        private static string EstimateGrade(int points)
        {
            return points switch
            {
                >= 900 => "S",
                >= 750 => "A",
                >= 600 => "B",
                >= 400 => "C",
                _ => "D"
            };
        }
    }
}

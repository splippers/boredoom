using System.Collections.Generic;
using UnityEngine;
using Chorewars.AR;
using Chorewars.Core;
using Chorewars.Dollhouse;
using Chorewars.Modes;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Shared runtime plumbing for every chore mode that the bootstrapper authors by hand.
    /// </summary>
    /// <remarks>
    /// The seven modes all share the same frame: a running clock, a coverage read-out, a points
    /// ticker, a live grade, a ghost trail, the personal-best compare and the session summary. Only
    /// the mechanic differs, so the mechanic lives in a subclass and everything else lives here.
    /// The modes in <c>Chorewars.Modes</c> are the Inspector-authored versions and need prefabs and
    /// scene wiring that this project does not ship; these Runtime versions build the same idea
    /// procedurally instead.
    ///
    /// The one invariant a subclass can break, and must not: the live score shown during play has to
    /// equal the score on the summary. That means the live formula must be the same arithmetic the
    /// base <see cref="BaseCoverageModeController.End"/> performs — coverage x10 plus the truncated
    /// bonus — so <see cref="GetBonusPoints"/> is the single source of bonus truth for both.
    /// </remarks>
    public abstract class RuntimeCoverageMode : BaseCoverageModeController
    {
        private const float EndAtCoveragePercent = 100f;

        /// <summary>Ghost trail sampling: a minimum distance between saved points, and a hard cap.</summary>
        /// <remarks>
        /// Distance, not time. A time-based sample spaces points by speed, so a fast sweep leaves gaps
        /// wider than a coverage cell and the saved ghost under-represents the run it recorded. A fixed
        /// spacing keeps the ghost faithful no matter how fast the player moves, and drops the long run
        /// of identical points a time-based sample records while the tool sits still.
        /// </remarks>
        private const float TrailSampleDistance = 0.08f;
        private const int MaxTrailPoints = 3000;

        private readonly List<Vector3> _trail = new();
        private float _startTime;
        private float _timeLimitSeconds = 90f;
        private bool _begun;
        private bool _ended;

        /// <summary>Supplies the per-room bonus. Optional; without it the bonus is zero.</summary>
        public RoomCleanCelebration celebration;

        /// <summary>Source of the per-room breakdown for the summary. Optional.</summary>
        public DollhouseManager dollhouse;

        /// <summary>Stable key for this mode's personal best / ghost in <see cref="PlayerProfile"/>.</summary>
        public abstract string ProfileModeId { get; }

        /// <summary>Human-readable name, used in logs and on the summary.</summary>
        public abstract string ModeDisplayName { get; }

        /// <summary>Floor-level extent of the play area, as supplied by the scan or room provider.</summary>
        protected Bounds PlayBounds { get; private set; }

        protected PlaceholderHud Hud { get; private set; }
        protected CoverageFloorVisualizer FloorVisualizer { get; private set; }
        protected PlaceholderToolDriver ToolDriver { get; private set; }

        protected bool IsRunning => _begun && !_ended;
        protected float ElapsedSeconds => Time.time - _startTime;
        protected float TimeRemainingSeconds => _timeLimitSeconds - ElapsedSeconds;

        public void ConfigureRuntime(
            PlaceholderHud hud,
            CoverageFloorVisualizer floorVisualizer,
            PlaceholderToolDriver toolDriver,
            Bounds playBounds,
            float timeLimitSeconds)
        {
            Hud = hud;
            FloorVisualizer = floorVisualizer;
            ToolDriver = toolDriver;
            PlayBounds = playBounds;
            _timeLimitSeconds = Mathf.Max(5f, timeLimitSeconds);
        }

        protected override void OnModeBegun()
        {
            _begun = true;
            _startTime = Time.time;
        }

        protected override void OnTrackedPositionSampled(Vector3 worldPos)
        {
            FloorVisualizer?.Mark(worldPos);

            // Save the trail at a fixed spacing: enough points to reproduce the path, few enough to
            // keep the profile small, and no runs of identical points while the tool is parked.
            if (_trail.Count == 0 || Vector3.Distance(_trail[_trail.Count - 1], worldPos) >= TrailSampleDistance)
            {
                if (_trail.Count < MaxTrailPoints) _trail.Add(worldPos);
            }
        }

        protected override void Update()
        {
            if (!_begun || _ended) return;

            base.Update();

            // One key flips the whole juice layer. Scoring never reads it, so this can be toggled
            // mid-run without changing the result.
            if (ToolDriver != null && ToolDriver.ConsumeJuiceToggle())
            {
                bool on = JuiceSettings.Toggle();
                Debug.Log($"[BoreDOOM] Juice {(on ? "ON" : "OFF")}");
            }

            float elapsed = ElapsedSeconds;
            float coverage = coverageMap != null ? coverageMap.ComputeCoveragePercent() : 0f;

            // Let the mode act first — collect an item, resample its grid, advance its ghost — so the
            // score computed below already reflects what happened on this very frame. A mode that scores
            // from its own state would otherwise leave the HUD a frame behind the summary.
            OnModeTick(elapsed, coverage);
            if (_ended) return;

            // Live score uses the same formula the base End() uses (coverage x10 + truncated bonus),
            // so the number on the HUD during play is exactly the number the summary will show.
            int points = (int)(coverage * 10f) + (int)GetBonusPoints();

            Hud?.SetCoverage(coverage);
            Hud?.SetPoints(points);
            Hud?.SetTimer(_timeLimitSeconds - elapsed);
            Hud?.SetGrade(EstimatedGrade(points));

            bool manualEnd = ToolDriver != null && ToolDriver.EndRequested;
            if (coverage >= EndAtCoveragePercent || elapsed >= _timeLimitSeconds || manualEnd)
                End();
        }

        /// <summary>Per-frame hook for mode-specific presentation. Runs after the HUD is updated.</summary>
        protected virtual void OnModeTick(float elapsedSeconds, float coveragePercent) { }

        /// <summary>
        /// Ending is idempotent. <see cref="BaseCoverageModeController.End"/> scores the session and
        /// records it against the profile, so calling it twice would double-count the score, the
        /// lifetime points and the ghost. End can be reached from more than one place (the timer, the
        /// end key, the summary or a UI button), so it guards itself rather than trusting its callers.
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
                float duration = Mathf.Max(0f, ElapsedSeconds);

                if (profile != null)
                {
                    // Read the old best before recording, so the compare is against the previous run.
                    previousBest = profile.GetBestPoints(ProfileModeId);
                    isNewBest = previousBest > 0 && result.totalPoints > previousBest;

                    profile.RecordSession(result);

                    if (_trail.Count >= 2) profile.SaveGhost(ProfileModeId, _trail, duration, result.totalPoints);
                }

                Debug.Log(
                    $"[BoreDOOM] {ModeDisplayName} ended — grade {result.grade}, points {result.totalPoints}, " +
                    $"coverage {result.session?.coveragePercent:0}%, duration {duration:0}s, " +
                    $"bestBefore {previousBest}, newBest {isNewBest}, trail {_trail.Count}");
            }

            Hud?.ShowSummary(
                result,
                dollhouse != null ? dollhouse.RoomFinalCoverage : null,
                previousBest,
                isNewBest);
        }

        /// <summary>
        /// Per-room bonus, sourced from the celebration so the score and the "+points" popups agree.
        /// Subclasses with their own bonus add to this rather than replacing it.
        /// </summary>
        protected override float GetBonusPoints()
        {
            return celebration != null ? celebration.BonusPoints : 0f;
        }

        /// <summary>
        /// Same thresholds as the base controller's re-grade, applied to the live score, so the grade
        /// on the HUD during play matches the grade on the summary screen.
        /// </summary>
        public static string EstimatedGrade(int points)
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

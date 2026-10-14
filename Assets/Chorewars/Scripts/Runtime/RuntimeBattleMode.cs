using UnityEngine;
using Chorewars.Core;
using Chorewars.Integration;

namespace Chorewars.Runtime
{
    /// <summary>
    /// ChoreWars Battle: two people clean their own rooms at the same time and race to the higher
    /// coverage. With an opponent present the scores cross the LAN over <see cref="LocalNetworkAPI"/>;
    /// on your own, a practice rival sets the pace so the mode is still playable and testable.
    /// </summary>
    /// <remarks>
    /// The Inspector-authored version has a bug this one is written to avoid: its local coverage is
    /// stored by a <c>SetLocalCoverage</c> hook that nothing ever calls, so it always broadcast 0% and
    /// its win bonus could never trigger. Here the local figure is read straight from the shared base
    /// every tick, so there is no second copy to fall out of sync.
    ///
    /// The practice rival is labelled as such on the HUD. It is not an opponent and does not pretend
    /// to be: it sets a floor derived from your own Standard Coverage best, so a solo run has something
    /// to beat rather than an empty scoreboard.
    /// </remarks>
    public sealed class RuntimeBattleMode : RuntimeCoverageMode
    {
        private const float BroadcastIntervalSeconds = 1.5f;
        private const float MaxWinBonus = 200f;
        private const float DefaultRivalRatePerSecond = 2f;
        private const float ObjectiveIntervalSeconds = 0.25f;

        private LocalNetworkAPI _network;
        private bool _hasOpponent;
        private float _opponentCoverage;
        private float _localCoverage;
        private float _rivalRatePerSecond;
        private float _nextBroadcastAt;
        private float _nextObjectiveAt;

        public override string ProfileModeId => "chorewars-battle";

        public override string ModeDisplayName => "ChoreWars Battle";

        protected override void OnModeBegun()
        {
            base.OnModeBegun();

            _network = FindAnyObjectByType<LocalNetworkAPI>();
            _hasOpponent = _network != null;

            if (_hasOpponent)
            {
                _network.OnCoverageBroadcastReceived += OnOpponentCoverage;
                Debug.Log("[BoreDOOM] ChoreWars Battle: opponent link found, racing live over the LAN.");
            }
            else
            {
                _rivalRatePerSecond = DeriveRivalRate();
                Debug.Log($"[BoreDOOM] ChoreWars Battle: no opponent found — practice rival at {_rivalRatePerSecond:0.0}%/s (from your Standard Coverage best).");
            }

            UpdateObjective();
        }

        protected override void OnModeEnded(ChoreResult result)
        {
            if (_network != null) _network.OnCoverageBroadcastReceived -= OnOpponentCoverage;

            bool won = _localCoverage >= _opponentCoverage;
            Debug.Log($"[BoreDOOM] ChoreWars Battle {(won ? "WON" : "LOST")} — you {_localCoverage:0.0}% vs {_opponentCoverage:0.0}%.");
            base.OnModeEnded(result);
        }

        protected override void OnModeTick(float elapsedSeconds, float coveragePercent)
        {
            _localCoverage = coveragePercent;

            if (!_hasOpponent) _opponentCoverage = Mathf.Min(100f, elapsedSeconds * _rivalRatePerSecond);

            if (_network != null && Time.time >= _nextBroadcastAt)
            {
                _nextBroadcastAt = Time.time + BroadcastIntervalSeconds;
                _network.BroadcastCoverageUpdate(_localCoverage);
            }

            if (Time.time >= _nextObjectiveAt) UpdateObjective();
        }

        /// <summary>Beating your opponent is worth up to 200, in proportion to the winning margin.</summary>
        protected override float GetBonusPoints()
        {
            float margin = _localCoverage - _opponentCoverage;
            if (margin <= 0f) return base.GetBonusPoints();
            return base.GetBonusPoints() + Mathf.Clamp(margin * 4f, 0f, MaxWinBonus);
        }

        private void OnOpponentCoverage(float percent)
        {
            _opponentCoverage = percent;
            _hasOpponent = true;
        }

        private void UpdateObjective()
        {
            _nextObjectiveAt = Time.time + ObjectiveIntervalSeconds;

            float margin = _localCoverage - _opponentCoverage;
            string lead = margin >= 0f ? $"AHEAD +{margin:0}" : $"BEHIND {margin:0}";
            string rival = _hasOpponent ? "RIVAL" : "PRACTICE RIVAL";

            Hud?.SetObjective($"{rival} {_opponentCoverage:0}%   YOU {_localCoverage:0}%   {lead}");
        }

        /// <summary>
        /// Sets the practice rival's pace from the player's own Standard Coverage best — its final
        /// coverage over its duration. Without a best to go on, a modest default keeps the mode moving.
        /// </summary>
        private static float DeriveRivalRate()
        {
            var profile = PlayerProfile.Instance;
            if (profile != null
                && profile.TryGetBestGhost("hoover", out var path, out float duration)
                && duration > 1f)
            {
                float bestCoverage = profile.GetBestPoints("hoover") / 10f;
                if (bestCoverage > 1f) return Mathf.Clamp(bestCoverage / duration, 0.5f, 6f);
            }

            return DefaultRivalRatePerSecond;
        }
    }
}

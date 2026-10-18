namespace Chorewars.Voice
{
    /// <summary>
    /// The timing of "look at it to press it", with no Unity in it so it can be tested.
    /// </summary>
    /// <remarks>
    /// Looking for <see cref="Seconds"/> presses once. It cannot press again until the player has looked
    /// away, so a long stare is one press, not a stream of them — which matters for a button that plays
    /// audio. A short glance away does not lose progress; the progress drains instead of snapping to zero.
    /// </remarks>
    public sealed class DwellButtonLogic
    {
        public float Seconds = 1.0f;

        /// <summary>How long a full dwell takes to drain away once the player looks elsewhere.</summary>
        public float DrainSeconds = 0.4f;

        float _progress;
        bool _spent;

        /// <summary>0 to 1: how close the button is to firing. Drives the fill bar.</summary>
        public float Progress01 => Seconds <= 0f ? 0f : _progress / Seconds;

        /// <summary>Advance by one frame. Returns true on the single frame the button fires.</summary>
        public bool Update(bool looking, float dt)
        {
            if (dt < 0f) dt = 0f;

            if (!looking)
            {
                _progress -= DrainSeconds <= 0f ? _progress : dt * (Seconds / DrainSeconds);
                if (_progress <= 0f)
                {
                    _progress = 0f;
                    _spent = false; // looked away long enough: armed again
                }
                return false;
            }

            if (_spent) return false;

            _progress += dt;
            if (_progress < Seconds) return false;

            _progress = 0f;
            _spent = true;
            return true;
        }
    }
}

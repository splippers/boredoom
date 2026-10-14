using System;

namespace Chorewars.Runtime
{
    /// <summary>
    /// One switch for every optional bit of "juice": particles, popups, procedural ticks, haptics,
    /// slow motion and the missed-spot hint.
    /// </summary>
    /// <remarks>
    /// This is presentation only, and gameplay code must never read it. Turning the juice off has to
    /// leave the core loop and the score completely untouched — the celebration still counts the
    /// rooms it awards bonus points for, it just does not draw or play anything. Keeping the switch
    /// in one place means a single toggle can silence the whole layer for a performance check or for
    /// a player who does not want the noise.
    /// </remarks>
    public static class JuiceSettings
    {
        private static bool _enabled = true;

        /// <summary>Raised with the new value whenever the switch flips, so live effects can settle.</summary>
        public static event Action<bool> Changed;

        /// <summary>Whether the juice layer is on. Defaults to on.</summary>
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;

                _enabled = value;
                Changed?.Invoke(value);
            }
        }

        /// <summary>Flips the switch and reports the new state.</summary>
        public static bool Toggle()
        {
            Enabled = !Enabled;
            return Enabled;
        }
    }
}

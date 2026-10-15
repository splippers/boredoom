namespace Chorewars.Runtime
{
    /// <summary>
    /// One switch for the pseudo-photogrammetry layer: whether a cleaned part of the floor shows
    /// captured imagery instead of a flat tint.
    /// </summary>
    /// <remarks>
    /// Presentation, like <see cref="JuiceSettings"/>, and it fails safe the same way: with the
    /// switch off the bake never runs and the floor behaves exactly as it did before this layer
    /// existed. It defaults to on because the effect is the point, but the switch exists because the
    /// camera path is the one piece that cannot be exercised in the editor — a device whose capture
    /// is not working yet should be able to drop the whole layer without touching the chore.
    /// </remarks>
    public static class PhotogrammetrySettings
    {
        private static bool _enabled = true;
        private static bool _verboseLogging;

        /// <summary>Whether the bake layer is on. Defaults to on.</summary>
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
            }
        }

        /// <summary>Flips the switch and reports the new state.</summary>
        public static bool Toggle()
        {
            Enabled = !Enabled;
            return Enabled;
        }

        /// <summary>Shortest gap between two captures, however fast the tool moves.</summary>
        public static float CaptureIntervalSeconds { get; set; } = 0.20f;

        /// <summary>
        /// How far the tool must travel before another capture is worth taking. A capture that only
        /// re-photographs ground we already hold costs real time on Quest and adds nothing, so
        /// standing still or shuffling in place is free.
        /// </summary>
        public static float CaptureDistanceMeters { get; set; } = 0.25f;

        /// <summary>
        /// How far a cleaned texel with no photograph of its own looks for a photographed neighbour.
        /// This is the "blur" that stops the reveal having a jagged, unfilled edge.
        /// </summary>
        public static int FillRadiusTexels { get; set; } = 3;

        /// <summary>Log every capture. Noisy; for chasing a device problem.</summary>
        public static bool VerboseLogging
        {
            get => _verboseLogging;
            set => _verboseLogging = value;
        }
    }
}

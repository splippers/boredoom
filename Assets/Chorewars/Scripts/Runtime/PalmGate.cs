namespace Chorewars.Runtime
{
    /// <summary>
    /// Decides when the palm-held dollhouse should be showing, with no flicker.
    /// </summary>
    /// <remarks>
    /// A hand held "palm up" is never perfectly still: the palm's tilt wobbles around any threshold, and a
    /// tracker loses a hand for a frame now and then. So showing needs the palm to be up for a moment, and
    /// hiding needs it to be down (or lost) for a moment, and the two thresholds differ. No Unity in here.
    /// </remarks>
    public sealed class PalmGate
    {
        /// <summary>Palm normal's upward component (1 = flat and facing up) that counts as "up" for showing.</summary>
        public float ShowAbove = 0.6f;

        /// <summary>Below this the palm counts as lowered. Lower than <see cref="ShowAbove"/> on purpose.</summary>
        public float HideBelow = 0.3f;

        public float ShowAfterSeconds = 0.25f;
        public float HideAfterSeconds = 0.5f;

        float _upFor;
        float _downFor;

        public bool Visible { get; private set; }

        /// <param name="palmNormalUp">The upward component of the palm's outward normal, -1 to 1.</param>
        /// <param name="tracked">False when the tracker has lost the hand.</param>
        public bool Update(float palmNormalUp, bool tracked, float dt)
        {
            if (dt < 0f) dt = 0f;

            bool up = tracked && palmNormalUp >= ShowAbove;
            bool down = !tracked || palmNormalUp <= HideBelow;

            if (up) { _upFor += dt; _downFor = 0f; }
            else if (down) { _downFor += dt; _upFor = 0f; }
            else { _upFor = 0f; _downFor = 0f; } // in between: neither clock runs, the state holds

            if (!Visible && _upFor >= ShowAfterSeconds) Visible = true;
            else if (Visible && _downFor >= HideAfterSeconds) Visible = false;

            return Visible;
        }

        public void Reset()
        {
            _upFor = 0f;
            _downFor = 0f;
            Visible = false;
        }
    }
}

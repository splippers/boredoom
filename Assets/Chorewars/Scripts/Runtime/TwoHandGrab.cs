using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Both hands pinch a hologram and stretch, turn and carry it, the way it is done in the films.
    /// </summary>
    /// <remarks>
    /// While both hands are pinching, everything is measured from the moment the second pinch closed:
    /// the gap between the hands gives a scale <see cref="Factor"/>, the turn of the line between them
    /// gives a <see cref="YawDegrees"/>, and the move of their midpoint gives <see cref="Move"/>. The
    /// caller applies those to the pose it had at that moment. Opening either hand ends the grab.
    /// No Unity objects, so the maths is tested directly.
    /// </remarks>
    public sealed class TwoHandGrab
    {
        /// <summary>Hands closer than this cannot give a trustworthy ratio (the tracker's noise is a few millimetres).</summary>
        const float MinGap = 0.04f;

        bool _active;
        float _startGap;
        Vector3 _startAxis;
        Vector3 _startMid;

        public bool Active => _active;

        /// <summary>The current gap divided by the gap when the grab began: 2 means the hands have moved twice as far apart.</summary>
        public float Factor { get; private set; } = 1f;

        /// <summary>How far the line between the hands has turned about the vertical axis, in degrees (positive is anticlockwise from above).</summary>
        public float YawDegrees { get; private set; }

        /// <summary>How far the midpoint between the hands has moved since the grab began.</summary>
        public Vector3 Move { get; private set; }

        /// <summary>Call every frame. Returns true while a two-handed grab is in progress.</summary>
        public bool Update(bool leftPinching, bool rightPinching, Vector3 leftPoint, Vector3 rightPoint)
        {
            if (!(leftPinching && rightPinching))
            {
                _active = false;
                Factor = 1f;
                YawDegrees = 0f;
                Move = Vector3.zero;
                return false;
            }

            var axis = rightPoint - leftPoint;
            float gap = axis.magnitude;
            var mid = (leftPoint + rightPoint) * 0.5f;

            if (!_active)
            {
                _active = true;
                _startGap = Mathf.Max(gap, MinGap);
                _startAxis = axis;
                _startMid = mid;
                Factor = 1f;
                YawDegrees = 0f;
                Move = Vector3.zero;
                return true;
            }

            Factor = Mathf.Max(gap, MinGap) / _startGap;
            Move = mid - _startMid;

            var a = Vector3.ProjectOnPlane(_startAxis, Vector3.up);
            var b = Vector3.ProjectOnPlane(axis, Vector3.up);
            YawDegrees = (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) ? 0f : Vector3.SignedAngle(a, b, Vector3.up);
            return true;
        }
    }
}

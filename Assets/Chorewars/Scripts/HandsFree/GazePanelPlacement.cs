using UnityEngine;

namespace Chorewars.HandsFree
{
    /// <summary>
    /// Keeps a panel still while it can be selected by looking at it.
    /// </summary>
    /// <remarks>
    /// Head-gaze selects whatever is at the middle of the view. A panel that is re-placed in front of
    /// the head every frame is at the same place in the view every frame, so the middle of the view
    /// always lands on the same spot of it and every other control is unreachable. To pick something
    /// the panel has to hold still so that turning the head moves the cursor across it.
    ///
    /// It is placed once, in front of where the player is looking, and held. If the player turns far
    /// enough away that they can no longer see it, it is brought back in front of them, so a panel is
    /// never lost behind the player.
    /// </remarks>
    public sealed class GazePanelPlacement
    {
        /// <summary>How far off the panel can drift from the view centre before it is brought back, in degrees.</summary>
        public float RecenterAngle = 55f;

        /// <summary>Walking away from a held panel is the other way to lose it: past this, it is brought back.</summary>
        public float RecenterDistance = 2.4f;

        /// <summary>Gap kept between a panel's lower edge and the floor, in metres.</summary>
        public float FloorClearance = 0.12f;

        /// <summary>
        /// A world-anchored panel stays where it was opened, however far the player walks or however they turn,
        /// so they can roam the house and come back to it. Until <see cref="WorldAnchoredPanels.Recall"/> is called,
        /// it is never pulled back in front of them.
        /// </summary>
        public bool Anchored;

        int _seenRecall;
        bool _held;
        Vector3 _position;
        Quaternion _rotation;

        /// <summary>Forget the held pose, so the next <see cref="Hold"/> places it afresh in front of the player.</summary>
        public void Release()
        {
            _held = false;
        }

        /// <param name="floorY">World height of the floor. The panel is lifted if its lower edge would dip below it.</param>
        /// <param name="belowCentre">How far the panel's lower edge hangs below the point it is positioned by.</param>
        /// <param name="yawOffsetDegrees">Turns the panel's spot to the right of where the player is facing (negative: left). Lets two panels sit side by side instead of on top of each other.</param>
        public void Hold(Transform panel, Transform head, float distance, float heightOffset,
                         float floorY = float.NegativeInfinity, float belowCentre = 0f, float yawOffsetDegrees = 0f)
        {
            if (panel == null || head == null) return;

            if (Anchored && _seenRecall != WorldAnchoredPanels.RecallCount)
            {
                _seenRecall = WorldAnchoredPanels.RecallCount;
                _held = false; // recalled: place it afresh in front of the player
            }

            if (_held)
            {
                float distanceLimit = Anchored ? float.PositiveInfinity : RecenterDistance;
                float angleLimit = Anchored ? 360f : RecenterAngle;

                var toPanel = _position - head.position;
                if (toPanel.sqrMagnitude < 1e-4f
                    || toPanel.magnitude > distanceLimit
                    || Vector3.Angle(head.forward, toPanel) > angleLimit)
                    _held = false;
            }

            if (!_held)
            {
                // Level, so the panel is upright however the head happens to be tilted when it appears.
                var flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (flat.sqrMagnitude < 1e-4f) flat = Vector3.ProjectOnPlane(head.up, Vector3.up);
                flat.Normalize();
                if (yawOffsetDegrees != 0f) flat = Quaternion.AngleAxis(yawOffsetDegrees, Vector3.up) * flat;

                _position = head.position + flat * distance + Vector3.up * heightOffset;

                // A seated player's head is not far above the floor, and a tall panel centred on the view
                // then reaches through it - the floor slices the bottom controls off. Keep the lower edge
                // clear, even if that lifts the panel above eye level.
                if (!float.IsNegativeInfinity(floorY))
                    _position.y = Mathf.Max(_position.y, floorY + FloorClearance + belowCentre);

                // A world-space canvas reads correctly when its +Z points away from the viewer.
                var away = Vector3.ProjectOnPlane(_position - head.position, Vector3.up);
                _rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
                _held = true;
            }

            panel.SetPositionAndRotation(_position, _rotation);
        }
    }
}

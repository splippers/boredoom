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

        bool _held;
        Vector3 _position;
        Quaternion _rotation;

        /// <summary>Forget the held pose, so the next <see cref="Hold"/> places it afresh in front of the player.</summary>
        public void Release()
        {
            _held = false;
        }

        public void Hold(Transform panel, Transform head, float distance, float heightOffset)
        {
            if (panel == null || head == null) return;

            if (_held)
            {
                var toPanel = _position - head.position;
                if (toPanel.sqrMagnitude < 1e-4f
                    || toPanel.magnitude > RecenterDistance
                    || Vector3.Angle(head.forward, toPanel) > RecenterAngle)
                    _held = false;
            }

            if (!_held)
            {
                // Level, so the panel is upright however the head happens to be tilted when it appears.
                var flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (flat.sqrMagnitude < 1e-4f) flat = Vector3.ProjectOnPlane(head.up, Vector3.up);
                flat.Normalize();

                _position = head.position + flat * distance + Vector3.up * heightOffset;

                // A world-space canvas reads correctly when its +Z points away from the viewer.
                var away = Vector3.ProjectOnPlane(_position - head.position, Vector3.up);
                _rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
                _held = true;
            }

            panel.SetPositionAndRotation(_position, _rotation);
        }
    }
}

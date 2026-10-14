using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Chorewars.Runtime
{
    /// <summary>
    /// Moves the placeholder tool. Follows an optional XR controller transform when one is
    /// present; otherwise steers with WASD / arrow keys so the whole loop is testable in the
    /// editor without a headset. The project uses the New Input System exclusively.
    /// </summary>
    public sealed class PlaceholderToolDriver : MonoBehaviour
    {
        private Transform _followTarget;
        private Bounds _bounds;
        private float _speed = 1.6f;
        private float _height = 0.9f;

        /// <summary>Set when the player asks to end the session (E or Enter).</summary>
        public bool EndRequested { get; private set; }

        public void Configure(Transform followTarget, Bounds bounds, float speed = 1.6f, float height = 0.9f)
        {
            _followTarget = followTarget;
            _bounds = bounds;
            _speed = Mathf.Max(0.1f, speed);
            _height = height;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                EndRequested = true;
#endif

            if (_followTarget != null)
            {
                var followed = _followTarget.position;
                SetPosition(followed.x, followed.z);
                return;
            }

            var move = ReadMove();
            var next = transform.position + new Vector3(move.x, 0f, move.y) * (_speed * Time.deltaTime);
            SetPosition(next.x, next.z);
        }

        private void SetPosition(float x, float z)
        {
            transform.position = new Vector3(
                Mathf.Clamp(x, _bounds.min.x, _bounds.max.x),
                _height,
                Mathf.Clamp(z, _bounds.min.z, _bounds.max.z));
        }

        private static Vector2 ReadMove()
        {
            float horizontal = 0f;
            float vertical = 0f;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f))
            {
                horizontal = Input.GetAxisRaw("Horizontal");
                vertical = Input.GetAxisRaw("Vertical");
            }
#endif

            var move = new Vector2(horizontal, vertical);
            return move.sqrMagnitude > 1f ? move.normalized : move;
        }
    }
}

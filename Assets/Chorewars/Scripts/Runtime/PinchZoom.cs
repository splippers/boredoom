using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Turns "pinch, then move the hand up or down" into a zoom factor. No Unity objects, so it is tested directly.
    /// </summary>
    /// <remarks>
    /// While the pinch is held, the zoom follows the hand's height from where the pinch began: up zooms in,
    /// down zooms out, on a curve where every equal move multiplies the size by the same amount (so there is no
    /// point at which it runs out of room). Letting go keeps the size. The next pinch carries on from there.
    /// </remarks>
    public sealed class PinchZoom
    {
        public float MinZoom = 0.4f;
        public float MaxZoom = 4f;

        /// <summary>How many times larger per metre the hand moves up (4 per metre: 20 cm up is about 2.2x).</summary>
        public float GainPerMetre = 4f;

        float _zoom = 1f;
        float _baseZoom = 1f;
        float _startHeight;
        bool _held;

        public float Zoom => _zoom;
        public bool Zooming => _held;

        public float Update(bool pinching, float handHeight)
        {
            if (!pinching)
            {
                _held = false;
                return _zoom;
            }

            if (!_held)
            {
                _held = true;
                _baseZoom = _zoom;
                _startHeight = handHeight;
                return _zoom;
            }

            _zoom = Mathf.Clamp(_baseZoom * Mathf.Exp(GainPerMetre * (handHeight - _startHeight)), MinZoom, MaxZoom);
            return _zoom;
        }

        /// <summary>Sets the zoom directly (a two-handed grab decides its own), keeping it inside the limits.</summary>
        public void Set(float zoom)
        {
            _zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            _baseZoom = _zoom;
            _held = false;
        }

        public void Reset()
        {
            _zoom = 1f;
            _baseZoom = 1f;
            _held = false;
        }
    }
}

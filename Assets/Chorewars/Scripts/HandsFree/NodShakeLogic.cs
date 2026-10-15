using UnityEngine;

namespace Chorewars.HandsFree
{
    public enum HeadGesture
    {
        None = 0,
        Nod = 1,
        Shake = 2
    }

    /// <summary>
    /// Gesture recognition over head pitch and yaw, as a pure state machine so it can be
    /// exercised with synthetic angles instead of by nodding at a headset.
    ///
    /// A nod is repeated pitch oscillation; a shake is repeated yaw oscillation. Slow
    /// drift is deliberately *not* a gesture - turning to look at the next room must not
    /// be read as "yes", or the player will agree to things by walking about.
    /// </summary>
    public class NodShakeLogic
    {
        public float Window = 1.0f;
        public float MinExcursion = 14f;
        public int MinReversals = 2;
        public float MinRate = 40f;
        public float Dominance = 1.3f;
        public float Cooldown = 0.9f;

        float _windowTime;
        float _lastPitch;
        float _lastYaw;
        bool _seeded;

        float _pitchRel;
        float _yawRel;
        float _minPitch;
        float _maxPitch;
        float _minYaw;
        float _maxYaw;

        int _pitchSign;
        int _yawSign;
        int _pitchReversals;
        int _yawReversals;

        float _lastFire = -99f;

        public void Reset()
        {
            _seeded = false;
            _windowTime = 0f;
            _pitchRel = _yawRel = 0f;
            _minPitch = _maxPitch = _minYaw = _maxYaw = 0f;
            _pitchSign = _yawSign = 0;
            _pitchReversals = _yawReversals = 0;
            _lastFire = -99f;
        }

        /// <summary>Feed one head sample. Returns a gesture only on the frame one completes.</summary>
        public HeadGesture Feed(float pitch, float yaw, float dt, float now)
        {
            if (dt <= 0f) return HeadGesture.None;

            if (!_seeded)
            {
                _seeded = true;
                _lastPitch = pitch;
                _lastYaw = yaw;
                return HeadGesture.None;
            }

            float dp = Mathf.DeltaAngle(_lastPitch, pitch);
            float dy = Mathf.DeltaAngle(_lastYaw, yaw);
            _lastPitch = pitch;
            _lastYaw = yaw;

            // Track excursion relative to where the window started, so a wrap through
            // +/-180 degrees cannot invent a 360-degree swing.
            _pitchRel += dp;
            _yawRel += dy;
            if (_pitchRel < _minPitch) _minPitch = _pitchRel;
            if (_pitchRel > _maxPitch) _maxPitch = _pitchRel;
            if (_yawRel < _minYaw) _minYaw = _yawRel;
            if (_yawRel > _maxYaw) _maxYaw = _yawRel;

            Track(dp / dt, ref _pitchSign, ref _pitchReversals);
            Track(dy / dt, ref _yawSign, ref _yawReversals);

            _windowTime += dt;
            if (_windowTime < Window) return HeadGesture.None;

            var gesture = Evaluate(now);
            ResetWindow(now);
            return gesture;
        }

        void Track(float rate, ref int sign, ref int reversals)
        {
            if (Mathf.Abs(rate) < MinRate) return;
            int s = rate > 0f ? 1 : -1;
            if (sign != 0 && s != sign) reversals++;
            sign = s;
        }

        HeadGesture Evaluate(float now)
        {
            if (now - _lastFire < Cooldown) return HeadGesture.None;

            float pitchSpan = _maxPitch - _minPitch;
            float yawSpan = _maxYaw - _minYaw;

            bool nod = pitchSpan >= MinExcursion && _pitchReversals >= MinReversals &&
                       pitchSpan >= yawSpan * Dominance;
            bool shake = yawSpan >= MinExcursion && _yawReversals >= MinReversals &&
                         yawSpan >= pitchSpan * Dominance;

            // A nod wins a tie: "yes" is the safer mistake.
            if (nod)
            {
                _lastFire = now;
                return HeadGesture.Nod;
            }

            if (shake)
            {
                _lastFire = now;
                return HeadGesture.Shake;
            }

            return HeadGesture.None;
        }

        void ResetWindow(float now)
        {
            _windowTime = 0f;
            _pitchRel = _yawRel = 0f;
            _minPitch = _maxPitch = _minYaw = _maxYaw = 0f;
            _pitchSign = _yawSign = 0;
            _pitchReversals = _yawReversals = 0;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Chorewars.HandsFree
{
    /// <summary>
    /// Head-gaze selection by dwell: look at a target, keep looking, it fires. The second
    /// line of defence after voice, and the one that still works when a hoover is running.
    ///
    /// The player is never cursor-locked - the reticle is not a pointer, it is the middle
    /// of where they are facing - so this deliberately does nothing until a target is
    /// genuinely inside the player's view.
    /// </summary>
    public class GazeDwellSelector : MonoBehaviour
    {
        public bool Enabled = true;

        /// <summary>How long to hold a look before it counts, in seconds.</summary>
        public float DwellSeconds = 1.2f;

        /// <summary>A brief glance away does not reset the dwell; a long one does.</summary>
        public float ReleaseGrace = 0.4f;

        /// <summary>After firing, ignore the same target for this long.</summary>
        public float RefractorySeconds = 1.5f;

        public HandsFreeInput Input;

        readonly List<GazeDwellTarget> _targets = new List<GazeDwellTarget>();

        Transform _head;
        GazeDwellTarget _focused;
        GazeDwellTarget _refractory;
        float _dwell;
        float _refractoryUntil;

        public GazeDwellTarget Focused { get { return _focused; } }

        public float Dwell01
        {
            get { return DwellSeconds <= 0f ? 0f : Mathf.Clamp01(_dwell / DwellSeconds); }
        }

        public void Register(GazeDwellTarget target)
        {
            if (target != null && !_targets.Contains(target)) _targets.Add(target);
        }

        public void Unregister(GazeDwellTarget target)
        {
            _targets.Remove(target);
            if (_focused == target) _focused = null;
        }

        void Update()
        {
            if (!Enabled) return;

            if (_head == null)
            {
                var camera = Camera.main;
                if (camera == null) return;
                _head = camera.transform;
            }

            float now = Time.unscaledTime;
            var best = PickTarget(out _);
            if (best == null)
            {
                // Let the dwell decay rather than snapping to zero, so a small head
                // movement - or a hoover jolt - does not lose the player's place.
                if (_focused == null || _dwell <= 0f) return;
                _dwell -= Time.unscaledDeltaTime / Mathf.Max(0.05f, ReleaseGrace);
                if (_dwell <= 0f)
                {
                    _dwell = 0f;
                    _focused = null;
                }

                ApplyFocus();
                return;
            }

            if (best != _focused)
            {
                _focused = best;
                _dwell = 0f;
            }

            bool suppressed = best == _refractory && now < _refractoryUntil;
            if (suppressed)
            {
                ApplyFocus();
                return;
            }

            _dwell += Time.unscaledDeltaTime;
            if (_dwell < DwellSeconds)
            {
                ApplyFocus();
                return;
            }

            Fire(best, now);
        }

        GazeDwellTarget PickTarget(out float bestAngle)
        {
            GazeDwellTarget best = null;
            bestAngle = float.MaxValue;

            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                if (target == null || !target.Enabled) continue;

                var direction = target.transform.position - _head.position;
                if (direction.sqrMagnitude < 1e-6f) continue;

                float angle = Vector3.Angle(_head.forward, direction);
                if (angle > target.AngularRadius) continue;
                if (angle >= bestAngle) continue;

                bestAngle = angle;
                best = target;
            }

            return best;
        }

        void Fire(GazeDwellTarget target, float now)
        {
            _dwell = 0f;
            _refractory = target;
            _refractoryUntil = now + RefractorySeconds;

            if (Input != null && target.Intent != ChorewarsIntent.None)
            {
                Input.Submit(new IntentRequest
                {
                    Intent = target.Intent,
                    Source = IntentSource.Gaze,
                    Argument = target.Argument,
                    Confidence = 0.8f
                });
            }

            ApplyFocus();
        }

        void ApplyFocus()
        {
            float dwell01 = Dwell01;
            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                if (target == null) continue;
                target.SetFocus(target == _focused && _dwell > 0f, target == _focused ? dwell01 : 0f);
            }
        }
    }
}

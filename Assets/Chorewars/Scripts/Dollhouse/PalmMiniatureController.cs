using System.Collections;
using System.Collections.Generic;
using Chorewars.Diagnostics;
using Chorewars.Runtime;
using UnityEngine;

namespace Chorewars.Dollhouse
{
    /// <summary>Pure state using the existing tested palm and pinch mathematics.</summary>
    public sealed class PalmMiniatureGestures
    {
        private readonly PalmGate _gate = new PalmGate();
        private readonly PinchZoom _zoom = new PinchZoom { MinZoom = 0.4f, MaxZoom = 2.5f };
        private readonly TwoHandGrab _grab = new TwoHandGrab();
        private bool _twoHeld, _singleHeld;
        private float _startZoom, _startYaw;
        private Vector3 _singlePoint, _pullAxis, _turnAxis;
        public float RotationDegreesPerMeter { get; set; } = 240f;
        public bool Visible => _gate.Visible;
        public float Zoom => _zoom.Zoom;
        public float Yaw { get; private set; }

        public void Update(bool palmTracked, float normalUp, Vector3 palm, bool leftPinch, bool rightPinch,
            Vector3 leftPoint, Vector3 rightPoint, Vector3 forward, Vector3 right, float dt)
        {
            bool holdForGesture = Visible && palmTracked && rightPinch;
            _gate.Update(holdForGesture ? 1f : normalUp, palmTracked, dt);
            if (!Visible || !palmTracked)
            {
                Release();
                return;
            }
            if (leftPinch && rightPinch)
            {
                bool first = !_twoHeld;
                _grab.Update(true, true, leftPoint, rightPoint);
                if (first) { _startZoom = Zoom; _startYaw = Yaw; }
                _twoHeld = true; _singleHeld = false;
                _zoom.Set(_startZoom * _grab.Factor);
                Yaw = _startYaw + _grab.YawDegrees;
                return;
            }
            _grab.Update(false, false, leftPoint, rightPoint);
            _twoHeld = false;
            if (rightPinch)
            {
                if (!_singleHeld)
                {
                    _singleHeld = true; _singlePoint = rightPoint; _startYaw = Yaw;
                    _pullAxis = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
                    if (_pullAxis.sqrMagnitude < 0.1f) _pullAxis = Vector3.forward;
                    _turnAxis = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
                    if (_turnAxis.sqrMagnitude < 0.1f) _turnAxis = Vector3.right;
                    _zoom.Set(Zoom);
                }
                _zoom.Update(true, -Vector3.Dot(rightPoint, _pullAxis));
                Yaw = _startYaw + Vector3.Dot(rightPoint - _singlePoint, _turnAxis) * RotationDegreesPerMeter;
            }
            else { _zoom.Update(false, 0f); _singleHeld = false; }
        }
        private void Release()
        {
            _grab.Update(false, false, Vector3.zero, Vector3.zero);
            _zoom.Update(false, 0f); _twoHeld = false; _singleHeld = false;
        }
        public void ResetView() { Release(); _zoom.Reset(); Yaw = 0f; }
        public void LoseTracking(float dt) => Update(false, -1f, Vector3.zero, false, false, Vector3.zero, Vector3.zero, Vector3.forward, Vector3.right, dt);
    }

    [RequireComponent(typeof(MetaRoomMiniature))]
    public sealed class PalmMiniatureController : MonoBehaviour
    {
        [SerializeField] private float _liftAbovePalm = 0.035f;
        [SerializeField] private float _rotationDegreesPerMeter = 240f;
        [SerializeField] private float _sampleExpirySeconds = 0.15f;
        private MetaRoomMiniature _miniature;
        private Transform _head, _trackingSpace;
        private UnityEngine.XR.Hands.XRHandSubsystem _hands;
        private readonly List<UnityEngine.XR.Hands.XRHandSubsystem> _subsystems = new List<UnityEngine.XR.Hands.XRHandSubsystem>();
        private readonly PalmMiniatureGestures _gestures = new PalmMiniatureGestures();
        private Vector3 _palm, _drawnPalm;
        private bool _placed, _leftHeld, _rightHeld, _wasVisible;
        private float _lastSample = -100f;
        private Coroutine _connect;
        public bool Revealed => _miniature != null && _miniature.IsVisible;

        private void Awake()
        {
            _miniature = GetComponent<MetaRoomMiniature>();
            _miniature.UsePalmPresentation();
            _gestures.RotationDegreesPerMeter = _rotationDegreesPerMeter;
        }
        private float _lastHandLog;
        private void OnEnable() { _connect = StartCoroutine(ConnectHands()); }
        private IEnumerator ConnectHands()
        {
            while (enabled)
            {
                if (_head == null && Camera.main != null)
                {
                    _head = Camera.main.transform;
                    var origin = _head.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
                    _trackingSpace = origin != null ? (origin.Origin != null ? origin.Origin.transform : origin.transform) : null;
                }
                if (_hands == null || !_hands.running)
                {
                    if (_hands != null) _hands.updatedHands -= OnHandsUpdated;
                    _hands = null; _subsystems.Clear(); SubsystemManager.GetSubsystems(_subsystems);
                    foreach (var subsystem in _subsystems) if (subsystem.running) { _hands = subsystem; break; }
                    if (_hands != null)
                    {
                        _hands.updatedHands += OnHandsUpdated;
                        TelemetryLogger.Info("PalmMiniature", "XR Hands connected; left palm reveals, right pinch/pull or two-hand stretch/turn controls the house.");
                    }
                }
                yield return new WaitForSecondsRealtime(1f);
            }
        }
        private void OnHandsUpdated(UnityEngine.XR.Hands.XRHandSubsystem subsystem,
            UnityEngine.XR.Hands.XRHandSubsystem.UpdateSuccessFlags flags,
            UnityEngine.XR.Hands.XRHandSubsystem.UpdateType updateType)
        {
            if (updateType != UnityEngine.XR.Hands.XRHandSubsystem.UpdateType.Dynamic || _head == null) return;
            _lastSample = Time.unscaledTime;
            var left = subsystem.leftHand;
            bool palmTracked = left.isTracked && (flags & UnityEngine.XR.Hands.XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0;
            float normalUp = -1f;
            if (palmTracked && left.GetJoint(UnityEngine.XR.Hands.XRHandJointID.Palm).TryGetPose(out var localPalm))
            {
                var worldPalm = ToWorldPose(_trackingSpace, localPalm);
                _palm = worldPalm.position;
                // XR Hands palm +Y is dorsal; the outward palm normal is -Y.
                normalUp = -(worldPalm.rotation * Vector3.up).y;
            }
            else palmTracked = false;
            if (Time.unscaledTime - _lastHandLog > 1f)
            {
                _lastHandLog = Time.unscaledTime;
                TelemetryLogger.Info("PalmMiniature", "hands: leftTracked=" + left.isTracked + " flags=" + flags + " palmTracked=" + palmTracked +
                    " normalUp=" + normalUp.ToString("0.00") + " visible=" + _gestures.Visible + " space=" + (_trackingSpace != null));
            }
            bool haveLeftJoints = (flags & UnityEngine.XR.Hands.XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints) != 0;
            bool haveRightJoints = (flags & UnityEngine.XR.Hands.XRHandSubsystem.UpdateSuccessFlags.RightHandJoints) != 0;
            bool lp = Pinch(left, haveLeftJoints, ref _leftHeld, out var leftPoint);
            bool rp = Pinch(subsystem.rightHand, haveRightJoints, ref _rightHeld, out var rightPoint);
            _gestures.Update(palmTracked, normalUp, _palm, lp, rp, leftPoint, rightPoint, _head.forward, _head.right, Time.unscaledDeltaTime);
        }
        private bool Pinch(UnityEngine.XR.Hands.XRHand hand, bool fresh, ref bool held, out Vector3 point)
        {
            point = default;
            if (!fresh || !hand.isTracked || !hand.GetJoint(UnityEngine.XR.Hands.XRHandJointID.ThumbTip).TryGetPose(out var thumb)
                || !hand.GetJoint(UnityEngine.XR.Hands.XRHandJointID.IndexTip).TryGetPose(out var index)) { held = false; return false; }
            Vector3 a = ToWorldPose(_trackingSpace, thumb).position, b = ToWorldPose(_trackingSpace, index).position;
            held = Vector3.Distance(a, b) < (held ? 0.035f : 0.02f);
            point = (a + b) * 0.5f;
            return held;
        }
        public static Pose ToWorldPose(Transform space, Pose local)
            => space != null ? new Pose(space.TransformPoint(local.position), space.rotation * local.rotation) : local;
        private void LateUpdate()
        {
            if (Time.unscaledTime - _lastSample > _sampleExpirySeconds) _gestures.LoseTracking(Time.unscaledDeltaTime);
            bool visible = _gestures.Visible;
            if (visible)
            {
                _drawnPalm = _placed ? Vector3.Lerp(_drawnPalm, _palm, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)) : _palm;
                _placed = true;
                _miniature.SetPalmPose(_drawnPalm + Vector3.up * _liftAbovePalm, _gestures.Yaw, _gestures.Zoom);
            }
            else _placed = false;
            _miniature.SetPalmVisible(visible);
            bool shown = _miniature.IsVisible;
            if (shown != _wasVisible)
            {
                _wasVisible = shown;
                TelemetryLogger.Info("PalmMiniature", shown ? "left palm raised: revealed" : "palm lowered/tracking/data lost: hidden");
            }
        }
        public void ResetView() { _gestures.ResetView(); _placed = false; }
        private void OnDisable()
        {
            if (_connect != null) StopCoroutine(_connect);
            if (_hands != null) _hands.updatedHands -= OnHandsUpdated;
            _hands = null; _leftHeld = _rightHeld = false;
            _gestures.LoseTracking(1f); _placed = false;
            if (_miniature != null) _miniature.SetPalmVisible(false);
        }
        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            _gestures.LoseTracking(1f); _leftHeld = _rightHeld = false; _lastSample = -100f;
            _miniature.SetPalmVisible(false);
        }
    }
}

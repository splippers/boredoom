using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace Chorewars.Diagnostics
{
    /// <summary>
    /// Finds out what the headset reports for the player's hands, so a palm-held tool can be built on facts.
    /// </summary>
    /// <remarks>
    /// Off by default. Create <c>hand-probe.request</c> in the app's files folder to run it for ten seconds:
    ///
    ///   adb shell "touch /sdcard/Android/data/&lt;package&gt;/files/hand-probe.request"
    ///
    /// Once a second it logs, for each hand, whether a device exists, whether it is tracked, where it is
    /// relative to the head, and which way its "up" points (a palm held flat faces up). Positions only:
    /// no camera images are involved.
    /// </remarks>
    public sealed class HandPoseProbe : MonoBehaviour
    {
        public const string RequestFileName = "hand-probe.request";
        const float RunSeconds = 10f;

        float _nextPollAt, _endsAt, _nextLogAt;
        bool _running;
        readonly List<InputDevice> _devices = new List<InputDevice>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!AndroidPermissions.IsDevice) return;
            if (FindFirstObjectByType<HandPoseProbe>() != null) return;

            var go = new GameObject(nameof(HandPoseProbe));
            DontDestroyOnLoad(go);
            go.AddComponent<HandPoseProbe>();
        }

        void Update()
        {
            if (!_running)
            {
                if (Time.unscaledTime < _nextPollAt) return;
                _nextPollAt = Time.unscaledTime + 1f;

                var request = Path.Combine(Application.persistentDataPath, RequestFileName);
                if (!File.Exists(request)) return;
                try { File.Delete(request); } catch { /* the probe still runs */ }

                _running = true;
                _endsAt = Time.unscaledTime + RunSeconds;
                _nextLogAt = 0f;
                TelemetryLogger.Info("HandProbe", "started");
                return;
            }

            if (Time.unscaledTime >= _endsAt)
            {
                _running = false;
                TelemetryLogger.Info("HandProbe", "finished");
                return;
            }

            if (Time.unscaledTime < _nextLogAt) return;
            _nextLogAt = Time.unscaledTime + 1f;

            Report(XRNode.LeftHand);
            Report(XRNode.RightHand);
            ReportJoints();
        }

        readonly List<XRHandSubsystem> _handSubsystems = new List<XRHandSubsystem>();

        /// <summary>What the hand-tracking system itself says: is it running, is each hand tracked, where is the palm.</summary>
        void ReportJoints()
        {
            _handSubsystems.Clear();
            SubsystemManager.GetSubsystems(_handSubsystems);

            if (_handSubsystems.Count == 0)
            {
                TelemetryLogger.Info("HandProbe", "XRHandSubsystem: none (feature off or no hand tracking support)");
                return;
            }

            var sub = _handSubsystems[0];
            var sb = new StringBuilder("XRHandSubsystem running=" + sub.running + "; ");
            AppendHand(sb, "left", sub.leftHand);
            AppendHand(sb, "right", sub.rightHand);
            TelemetryLogger.Info("HandProbe", sb.ToString());
        }

        static void AppendHand(StringBuilder sb, string name, XRHand hand)
        {
            sb.Append(name).Append(" tracked=").Append(hand.isTracked);
            if (hand.isTracked && hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var pose))
            {
                var up = pose.rotation * Vector3.up;
                // The palm's +Y points out of the back of the hand, so a palm held flat and facing up reads as pointing down here.
                sb.Append(" palm=").Append(pose.position.ToString("F2")).Append(" palmNormalUp=").Append((-up.y).ToString("F2"));
            }
            sb.Append("; ");
        }

        void Report(XRNode node)
        {
            _devices.Clear();
            InputDevices.GetDevicesAtXRNode(node, _devices);

            if (_devices.Count == 0)
            {
                TelemetryLogger.Info("HandProbe", node + ": no device");
                return;
            }

            var sb = new StringBuilder();
            foreach (var device in _devices)
            {
                bool tracked = device.TryGetFeatureValue(CommonUsages.isTracked, out var t) && t;
                bool hasPos = device.TryGetFeatureValue(CommonUsages.devicePosition, out var pos);
                bool hasRot = device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rot);

                sb.Append(node).Append(": '").Append(device.name).Append("' chars=").Append(device.characteristics)
                  .Append(" tracked=").Append(tracked);

                if (hasPos && hasRot)
                {
                    var head = Camera.main != null ? Camera.main.transform : null;
                    var up = rot * Vector3.up;
                    sb.Append(" pos=").Append(pos.ToString("F2")).Append(" up=").Append(up.ToString("F2"));
                    if (head != null)
                    {
                        // Tracking-space pos is only comparable to the head's local pose when the rig is at the origin.
                        sb.Append(" headLocal=").Append(head.localPosition.ToString("F2"));
                    }
                }
                else
                {
                    sb.Append(" pose=unavailable");
                }
                sb.Append("; ");
            }

            TelemetryLogger.Info("HandProbe", sb.ToString());
        }
    }
}

using System;
using System.IO;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Chorewars.Diagnostics
{
    /// <summary>
    /// Finds out, on the headset, whether the game can read the passthrough camera at all — which decides
    /// whether tracking a real-world object (the vacuum) is possible.
    /// </summary>
    /// <remarks>
    /// Off by default. Create <c>camera-probe.request</c> in the app's files folder to run it for a few
    /// seconds; only then is the headset-camera permission asked for, so the player is never prompted for
    /// the cameras by a normal run:
    ///
    ///   adb shell "touch /sdcard/Android/data/&lt;package&gt;/files/camera-probe.request"
    ///
    /// Privacy: each frame is acquired and disposed in the same call. No pixel is read, copied, written to
    /// disk or sent anywhere. The log gets the frame size, format and rate, and the reason when it fails.
    /// </remarks>
    public sealed class CameraFrameProbe : MonoBehaviour
    {
        public const string RequestFileName = "camera-probe.request";
        const float RunSeconds = 10f;

        float _nextPollAt;
        float _endsAt;
        bool _running;
        ARCameraManager _manager;
        CameraProbeStats _stats;
        int _width, _height;
        string _format = "?";
        string _lastFailure;
        int _frameEvents;
        int _acquireFalse;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!AndroidPermissions.IsDevice) return;
            if (FindFirstObjectByType<CameraFrameProbe>() != null) return;

            var go = new GameObject(nameof(CameraFrameProbe));
            DontDestroyOnLoad(go);
            go.AddComponent<CameraFrameProbe>();
        }

        void Update()
        {
            if (_running) { Tick(); return; }

            if (Time.unscaledTime < _nextPollAt) return;
            _nextPollAt = Time.unscaledTime + 1f;

            var request = Path.Combine(Application.persistentDataPath, RequestFileName);
            if (!File.Exists(request)) return;

            try { File.Delete(request); }
            catch (Exception e) { TelemetryLogger.Warn("CameraProbe", "could not consume request: " + e.Message); }

            Begin();
        }

        void Begin()
        {
            _manager = FindFirstObjectByType<ARCameraManager>();
            _stats = new CameraProbeStats();
            _lastFailure = null;
            _endsAt = Time.unscaledTime + RunSeconds;
            _running = true;

            _frameEvents = 0;
            _acquireFalse = 0;
            if (_manager != null) _manager.frameReceived += OnFrameReceived;

            TelemetryLogger.Info("CameraProbe", "started; arCameraManager=" + (_manager != null) + " " + Descriptor());

            // Asked only now, because someone asked for the probe.
            AndroidPermissions.Request(AndroidPermissions.HeadsetCamera);
        }

        void Tick()
        {
            if (Time.unscaledTime >= _endsAt) { Finish(); return; }

            if (_manager == null) { _lastFailure = "no ARCameraManager in the scene"; return; }

            if (!AndroidPermissions.IsGranted(AndroidPermissions.HeadsetCamera)) { _lastFailure = "headset camera permission not granted yet"; return; }

            try
            {
                if (_manager.TryAcquireLatestCpuImage(out var image))
                {
                    _width = image.width;
                    _height = image.height;
                    _format = image.format.ToString();
                    _stats.Record(Time.realtimeSinceStartupAsDouble);
                    image.Dispose(); // never read: the frame is released straight away
                }
                else
                {
                    _acquireFalse++;
                    _lastFailure = "TryAcquireLatestCpuImage returned false";
                }
            }
            catch (Exception e)
            {
                _lastFailure = e.GetType().Name + ": " + e.Message;
            }
        }

        void OnFrameReceived(ARCameraFrameEventArgs args) => _frameEvents++;

        void Finish()
        {
            _running = false;
            if (_manager != null) _manager.frameReceived -= OnFrameReceived;
            TelemetryLogger.Info("CameraProbe", "state: " + State());

            var summary = _stats.Describe(_width, _height, _format);
            if (_stats.Frames == 0)
                TelemetryLogger.Warn("CameraProbe", "finished: " + summary + " — " + (_lastFailure ?? "unknown reason") +
                                                    " permission=" + AndroidPermissions.Summary() + " " + Descriptor());
            else
                TelemetryLogger.Info("CameraProbe", "finished: " + summary + " (frames were released unread)");
        }

        /// <summary>What the camera system says about itself, to tell "never started" from "started, no images".</summary>
        string State()
        {
            try
            {
                var sub = _manager != null ? _manager.subsystem : null;
                return "managerEnabled=" + (_manager != null && _manager.enabled) +
                       " subsystemRunning=" + (sub != null && sub.running) +
                       " permissionGranted=" + (_manager != null && _manager.permissionGranted) +
                       " frameEvents=" + _frameEvents + " acquireFalse=" + _acquireFalse +
                       " currentFacing=" + (_manager != null ? _manager.currentFacingDirection.ToString() : "?") +
                       " autoFocus=" + (_manager != null ? _manager.autoFocusEnabled.ToString() : "?");
            }
            catch (Exception e)
            {
                return "state error: " + e.Message;
            }
        }

        string Descriptor()
        {
            try
            {
                var subsystem = _manager != null ? _manager.subsystem : null;
                if (subsystem == null) return "subsystem=none";

                return "subsystem=" + subsystem.GetType().Name +
                       " supportsCameraImage=" + subsystem.subsystemDescriptor.supportsCameraImage;
            }
            catch (Exception e)
            {
                return "subsystem=error(" + e.Message + ")";
            }
        }
    }
}

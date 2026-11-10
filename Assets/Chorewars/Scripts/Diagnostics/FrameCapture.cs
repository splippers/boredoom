using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Chorewars.Diagnostics
{
    /// <summary>
    /// Saves passthrough camera frames so a recogniser for the player's own hoover (and later dog toys, shoes...)
    /// can be trained. Off by default: create <c>frame-capture.request</c> in the app's files folder to run it.
    /// </summary>
    /// <remarks>
    /// The request file may hold <c>seconds=90 every=1.0 size=864</c> (all optional; capped at 600 s and 600 frames).
    /// Frames go to <c>files/captures/&lt;timestamp&gt;/frame_0001.jpg</c> with an <c>index.jsonl</c> giving each frame's
    /// time and head pose. They stay on the headset until pulled over adb to the player's own PC; nothing is uploaded.
    ///
    ///   adb shell "touch /sdcard/Android/data/&lt;package&gt;/files/frame-capture.request"
    /// </remarks>
    public sealed class FrameCapture : MonoBehaviour
    {
        public const string RequestFileName = "frame-capture.request";

        float _nextPollAt, _nextShotAt, _endsAt, _every = 1f;
        int _size = 864, _count, _failures;
        bool _running;
        string _dir;
        ARCameraManager _manager;
        Transform _head;
        string _label = "";

        public static FrameCapture Instance { get; private set; }
        public bool IsRunning => _running;
        public int Count => _count;
        public string Label => _label;

        void Awake() { Instance = this; }

        /// <summary>Starts a scan from inside the game. Returns false if one is already running or the folder cannot be made.</summary>
        public bool StartScan(string label, float seconds = 600f, float every = 1f, int size = 864)
        {
            if (_running) return false;
            return BeginScan(label, seconds, every, size);
        }

        /// <summary>Ends the scan now; the frames so far are kept.</summary>
        public void StopScan() { if (_running) Finish(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!AndroidPermissions.IsDevice || FindFirstObjectByType<FrameCapture>() != null) return;
            var go = new GameObject(nameof(FrameCapture));
            DontDestroyOnLoad(go);
            go.AddComponent<FrameCapture>();
        }

        void Update()
        {
            if (_running) { Tick(); return; }
            if (Time.unscaledTime < _nextPollAt) return;
            _nextPollAt = Time.unscaledTime + 1f;
            var request = Path.Combine(Application.persistentDataPath, RequestFileName);
            if (!File.Exists(request)) return;
            string text = "";
            try { text = File.ReadAllText(request); File.Delete(request); }
            catch (Exception e) { TelemetryLogger.Warn("FrameCapture", "could not consume request: " + e.Message); }
            Begin(text);
        }

        static float Setting(string text, string key, float fallback)
        {
            foreach (var part in text.Split(new[] { ' ', '\n', '\r', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && kv[0] == key && float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
            }
            return fallback;
        }

        void Begin(string text)
        {
            float seconds = Mathf.Clamp(Setting(text, "seconds", 90f), 5f, 600f);
            float every = Mathf.Clamp(Setting(text, "every", 1f), 0.2f, 10f);
            int size = (int)Setting(text, "size", 864f);
            BeginScan("", seconds, every, size);
        }

        bool BeginScan(string label, float seconds, float every, int size)
        {
            seconds = Mathf.Clamp(seconds, 5f, 600f);
            _every = Mathf.Clamp(every, 0.2f, 10f);
            _size = Mathf.Clamp(size, 256, 1728);
            _label = string.IsNullOrEmpty(label) ? "" : label;
            _manager = FindFirstObjectByType<ARCameraManager>();
            _head = Camera.main != null ? Camera.main.transform : null;
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            _dir = Path.Combine(Application.persistentDataPath, "captures", _label.Length > 0 ? stamp + "-" + _label : stamp);
            try { Directory.CreateDirectory(_dir); }
            catch (Exception e) { TelemetryLogger.Warn("FrameCapture", "cannot create " + _dir + ": " + e.Message); return false; }
            AndroidPermissions.Request(AndroidPermissions.HeadsetCamera);
            _count = 0; _failures = 0;
            _nextShotAt = Time.unscaledTime + 1f;
            _endsAt = Time.unscaledTime + seconds;
            _running = true;
            TelemetryLogger.Info("FrameCapture", "started" + (_label.Length > 0 ? " [" + _label + "]" : "") + ": " + seconds + " s, every " + _every + " s, " + _size + " px, into " + _dir);
            return true;
        }

        void Tick()
        {
            if (Time.unscaledTime >= _endsAt || _count >= 600) { Finish(); return; }
            if (Time.unscaledTime < _nextShotAt) return;
            _nextShotAt = Time.unscaledTime + _every;
            if (_manager == null || !AndroidPermissions.IsGranted(AndroidPermissions.HeadsetCamera)) { _failures++; return; }
            try
            {
                if (!_manager.TryAcquireLatestCpuImage(out var image)) { _failures++; return; }
                using (image) Save(image);
            }
            catch (Exception e)
            {
                _failures++;
                if (_failures < 4) TelemetryLogger.Warn("FrameCapture", e.GetType().Name + ": " + e.Message);
            }
        }

        void Save(XRCpuImage image)
        {
            int side = Mathf.Min(_size, Mathf.Min(image.width, image.height));
            var dims = new Vector2Int(side, Mathf.Max(1, side * image.height / image.width));
            var conversion = new XRCpuImage.ConversionParams(image, TextureFormat.RGBA32, XRCpuImage.Transformation.MirrorY)
            { outputDimensions = dims };
            var buffer = new NativeArray<byte>(image.GetConvertedDataSize(conversion), Allocator.Temp);
            try
            {
                image.Convert(conversion, buffer);
                var texture = new Texture2D(dims.x, dims.y, TextureFormat.RGBA32, false);
                texture.LoadRawTextureData(buffer);
                texture.Apply(false);
                var jpg = texture.EncodeToJPG(88);
                Destroy(texture);
                _count++;
                string name = "frame_" + _count.ToString("0000", CultureInfo.InvariantCulture) + ".jpg";
                File.WriteAllBytes(Path.Combine(_dir, name), jpg);
                var p = _head != null ? _head.position : Vector3.zero;
                var r = _head != null ? _head.rotation : Quaternion.identity;
                var line = new StringBuilder();
                line.Append("{\"label\":\"").Append(_label).Append("\",\"file\":\"").Append(name).Append("\",\"t\":").Append(Time.realtimeSinceStartup.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(",\"w\":").Append(dims.x).Append(",\"h\":").Append(dims.y)
                    .Append(",\"pos\":[").Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(']')
                    .Append(",\"rot\":[").Append(F(r.x)).Append(',').Append(F(r.y)).Append(',').Append(F(r.z)).Append(',').Append(F(r.w)).Append("]}\n");
                File.AppendAllText(Path.Combine(_dir, "index.jsonl"), line.ToString());
            }
            finally { buffer.Dispose(); }
        }

        static string F(float v) => v.ToString("0.0000", CultureInfo.InvariantCulture);

        void Finish()
        {
            _running = false;
            TelemetryLogger.Info("FrameCapture", "finished: " + _count + " frames saved, " + _failures + " misses, in " + _dir);
        }
    }
}

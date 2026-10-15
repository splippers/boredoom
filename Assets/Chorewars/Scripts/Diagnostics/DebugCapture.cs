using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace Chorewars.Diagnostics
{
    /// <summary>
    /// Takes a picture of what the player is looking at, on request, so a person or an agent who is not
    /// wearing the headset can see the current state of play.
    /// </summary>
    /// <remarks>
    /// The Quest compositor does not hand frames to <c>adb screencap</c>, so the game renders its own view:
    /// a spare camera placed at the head pose draws the scene into a texture, which is written as a PNG.
    ///
    /// Ask for one by creating <c>capture.request</c> in the app's files folder; the file's text, if any,
    /// becomes a label in the picture's name. Pull the result from <c>files/shots/</c>:
    ///
    ///   adb shell "echo menu > /sdcard/Android/data/&lt;package&gt;/files/capture.request"
    ///   adb pull /sdcard/Android/data/&lt;package&gt;/files/shots/
    ///
    /// Privacy: this draws the game's own content only. Passthrough is composited by the system outside the
    /// app, so the real room is never in the picture, and nothing is written unless the request file exists.
    /// A line describing every world-space canvas is logged with each capture, because "where is the menu
    /// relative to the head" is often the real answer to "why can't I select it".
    /// </remarks>
    public sealed class DebugCapture : MonoBehaviour
    {
        public const string RequestFileName = "capture.request";
        const int Width = 1280;
        const int Height = 960;
        const float VerticalFov = 75f;

        float _nextPollAt;
        string _directory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindFirstObjectByType<DebugCapture>() != null) return;
            var go = new GameObject("DebugCapture");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugCapture>();
        }

        void Awake()
        {
            _directory = Application.persistentDataPath;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextPollAt) return;
            _nextPollAt = Time.unscaledTime + 0.5f;

            var request = Path.Combine(_directory, RequestFileName);
            if (!File.Exists(request)) return;

            string label = string.Empty;
            try
            {
                label = File.ReadAllText(request).Trim();
                File.Delete(request);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[capture] could not consume request: " + e.Message);
            }

            StartCoroutine(Capture(label));
        }

        IEnumerator Capture(string label)
        {
            // After the frame is drawn, so panels have settled into this frame's positions.
            yield return new WaitForEndOfFrame();

            var head = Camera.main;
            if (head == null) head = FindFirstObjectByType<Camera>();
            if (head == null)
            {
                Debug.LogWarning("[capture] no camera to capture from");
                yield break;
            }

            RenderTexture target = null;
            GameObject eyeGo = null;
            Texture2D picture = null;

            try
            {
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

                eyeGo = new GameObject("DebugCaptureCamera");
                var eye = eyeGo.AddComponent<Camera>();
                eye.CopyFrom(head);
                eye.stereoTargetEye = StereoTargetEyeMask.None;
                eye.enabled = false;
                eye.targetTexture = target;
                eye.ResetProjectionMatrix();
                eye.fieldOfView = VerticalFov;
                eye.aspect = (float)Width / Height;
                eye.transform.SetPositionAndRotation(head.transform.position, head.transform.rotation);
                eye.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = target;
                picture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                picture.Apply(false);
                RenderTexture.active = previous;

                var folder = Path.Combine(_directory, "shots");
                Directory.CreateDirectory(folder);
                string name = "shot_" + DateTime.Now.ToString("HHmmss") + (string.IsNullOrEmpty(label) ? string.Empty : "_" + Sanitise(label)) + ".png";
                string path = Path.Combine(folder, name);
                File.WriteAllBytes(path, picture.EncodeToPNG());

                Debug.Log("[capture] saved " + path);
                Debug.Log("[capture] " + DescribeCanvases(head.transform));
                Debug.Log("[capture] " + DescribeSpace(head.transform));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[capture] failed: " + e);
            }
            finally
            {
                if (picture != null) Destroy(picture);
                if (target != null) { target.Release(); Destroy(target); }
                if (eyeGo != null) Destroy(eyeGo);
            }
        }

        static string Sanitise(string label)
        {
            var sb = new StringBuilder();
            foreach (var c in label)
            {
                if (sb.Length >= 24) break;
                sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Where the head is, what the headset thinks its tracking origin is, and how high each floor-like
        /// object sits - "the floor is cutting through the menu" is a height problem and needs the numbers.
        /// </summary>
        static string DescribeSpace(Transform head)
        {
            var sb = new StringBuilder("space: head=").Append(head.position.ToString("0.00"));

            var subsystems = new System.Collections.Generic.List<UnityEngine.XR.XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var input in subsystems)
                sb.Append(" origin=").Append(input.GetTrackingOriginMode()).Append("/supports=").Append(input.GetSupportedTrackingOriginModes());

            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                var n = r.name;
                if (n.IndexOf("floor", StringComparison.OrdinalIgnoreCase) < 0) continue;
                sb.Append(" | ").Append(n).Append(" top=").Append(r.bounds.max.y.ToString("0.00"))
                  .Append(" size=").Append(r.bounds.size.ToString("0.0"));
            }

            return sb.ToString();
        }

        /// <summary>Each world-space canvas as seen from the head: distance and the angle off the view centre.</summary>
        static string DescribeCanvases(Transform head)
        {
            var sb = new StringBuilder("canvases:");
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (canvas.renderMode != RenderMode.WorldSpace) continue;

                var toCanvas = canvas.transform.position - head.position;
                sb.Append(' ').Append(canvas.name)
                  .Append(canvas.gameObject.activeInHierarchy ? "[on" : "[OFF")
                  .Append(" d=").Append(toCanvas.magnitude.ToString("0.00"))
                  .Append(" off=").Append(Vector3.Angle(head.forward, toCanvas).ToString("0"))
                  .Append("deg]");
            }
            return sb.ToString();
        }
    }
}

using System.IO;
using Chorewars.Runtime;
using UnityEngine;

namespace Chorewars.AR
{
    /// <summary>
    /// A small panel in the headset showing the state of the room scan: permission, surfaces found, rooms and
    /// doorways. Shown automatically until the headset has handed over a room, then hidden; <see cref="Toggle"/>
    /// (or a <c>scan-readout.request</c> file) brings it back, and the hands-free trigger will call the same thing.
    /// </summary>
    public sealed class ScanReadout : MonoBehaviour
    {
        const float RefreshSeconds = 0.5f;

        SceneSurfaceReader _reader;
        TextMesh _text;
        Transform _head;
        float _nextRefresh;
        float _nextFilePoll;
        bool _forced;

        public void Configure(SceneSurfaceReader reader) => _reader = reader;

        public void Toggle() => _forced = !_forced;

        void Awake()
        {
            var go = new GameObject("ScanReadoutText");
            go.transform.SetParent(transform, false);
            _text = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _text.font = font;
            _text.fontSize = 48;
            _text.characterSize = 0.006f;
            _text.anchor = TextAnchor.MiddleLeft;
            _text.color = new Color(0.9f, 1f, 0.9f, 1f);
            var r = go.GetComponent<MeshRenderer>();
            if (r != null && font != null) r.sharedMaterial = font.material;
            go.SetActive(false);
        }

        void Update()
        {
            if (_reader == null) return;

            if (Time.unscaledTime >= _nextFilePoll)
            {
                _nextFilePoll = Time.unscaledTime + 0.5f;
                var request = Path.Combine(Application.persistentDataPath, "scan-readout.request");
                if (File.Exists(request))
                {
                    try { File.Delete(request); } catch { }
                    Toggle();
                }
            }

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;

            int rooms = SceneRoomBuilder.Known.Count;
            bool show = _forced || rooms == 0;
            _text.gameObject.SetActive(show);
            if (!show) return;

            _text.text = ScanReadoutText.Format(_reader.PermissionGranted, _reader.Counts, rooms,
                SceneRoomBuilder.PublishedDoors.Count, _reader.Restarts);
        }

        void LateUpdate()
        {
            if (!_text.gameObject.activeSelf) return;
            if (_head == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                _head = cam.transform;
            }

            // Hangs in front and a little to the left, easing round after the player's gaze.
            var flat = Vector3.ProjectOnPlane(_head.forward, Vector3.up).normalized;
            if (flat.sqrMagnitude < 0.01f) return;
            var target = _head.position + flat * 1.2f + Vector3.Cross(Vector3.up, flat) * 0.35f + Vector3.up * -0.1f;
            var t = _text.transform;
            t.position = Vector3.Lerp(t.position, target, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            t.rotation = Quaternion.LookRotation(t.position - _head.position, Vector3.up);
        }
    }
}

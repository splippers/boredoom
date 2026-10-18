using System.Collections.Generic;
using System.IO;
using Chorewars.Diagnostics;
using Chorewars.Dollhouse;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Shows the dollhouse as a hologram: it floats above the open left palm, and both hands can grab it,
    /// stretch it bigger or smaller, turn it and carry it.
    /// </summary>
    /// <remarks>
    /// The real miniature is never moved, scaled or hidden by anything here. Its pieces are placed in world
    /// coordinates but sized in local ones, which only agree while their parent is at normal scale, so a
    /// parent that is shrunk to hide it or moved to carry it corrupts everything built while it is moved.
    /// So the original stays exactly as it is (its renderers are switched off for drawing only, which
    /// nothing else touches), and this component keeps a live copy of what it draws under a parent of its own.
    /// The copy follows the original every frame: colours as rooms are cleaned, names as they are said.
    ///
    /// Hands: palm up shows it above the left hand. Right thumb-and-finger pinch, moved up or down, zooms.
    /// Both hands pinching grabs it: spread or squeeze to resize, turn to rotate, move to carry. Let go and
    /// it floats where it was left for a minute, or until the left palm is raised again.
    ///
    /// A fallback for when both hands are on the hoover: create <c>show-house.request</c> in the app's files
    /// folder (its text, if a number, is how many seconds) and the house is held out in front of the player.
    /// </remarks>
    public sealed class PalmDollhouse : MonoBehaviour
    {
        public const string RequestFileName = "show-house.request";
        const float TargetSize = 0.30f;
        const float LiftAbovePalm = 0.06f;
        const float DefaultPinSeconds = 20f;
        const float FloatSeconds = 60f;

        enum Mode { Hidden, Palm, Free }

        sealed class Copy
        {
            public Renderer source;
            public Transform transform;
            public MeshRenderer renderer;
            public TextMesh sourceText;
            public TextMesh text;
            public MeshFilter sourceFilter;
        }

        readonly PalmGate _gate = new PalmGate();
        readonly PinchZoom _zoom = new PinchZoom();
        readonly TwoHandGrab _grab = new TwoHandGrab();
        readonly List<XRHandSubsystem> _subsystems = new List<XRHandSubsystem>();
        readonly Dictionary<Renderer, Copy> _copies = new Dictionary<Renderer, Copy>();
        readonly List<Renderer> _gone = new List<Renderer>();
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        DollhouseManager _manager;
        XROrigin _origin;
        Transform _holo;

        Mode _mode = Mode.Hidden;
        Vector3 _centre;             // middle of the original miniature, in its own (authored) space
        float _extent = 0.5f;        // its width there
        float _halfHeight = 0.05f;
        float _scale = 0.6f;         // metres per authored unit, as drawn now
        float _fitScale = 0.6f;
        float _yaw;
        Vector3 _target;             // where the miniature's centre is drawn
        Vector3 _drawn;
        bool _placed;

        bool _leftHeld, _rightHeld;
        bool _grabWasActive;
        Vector3 _grabTarget0;
        float _grabYaw0, _grabZoom0;

        float _freeUntil, _pinnedUntil;
        float _nextFindAt, _nextRefreshAt, _nextPollAt, _nextColourAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!AndroidPermissions.IsDevice) return;
            if (FindFirstObjectByType<PalmDollhouse>() != null) return;

            var go = new GameObject(nameof(PalmDollhouse));
            DontDestroyOnLoad(go);
            go.AddComponent<PalmDollhouse>();
        }

        void LateUpdate()
        {
            if (_manager == null && !Find()) return;

            float now = Time.unscaledTime;
            PollRequest(now);

            if (now >= _nextRefreshAt)
            {
                _nextRefreshAt = now + 0.5f;
                Refresh();
            }

            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;

            bool havePalm = TryGetPalm(out var palm, out float normalUp, out bool tracked);
            bool palmUp = _gate.Update(havePalm ? normalUp : -1f, havePalm && tracked, Time.unscaledDeltaTime);

            bool left = TryGetPinch(false, ref _leftHeld, out var leftPoint);
            bool right = TryGetPinch(true, ref _rightHeld, out var rightPoint);
            bool grabbing = _grab.Update(left, right, leftPoint, rightPoint);

            DecideMode(now, head, palmUp, grabbing);

            if (_mode == Mode.Hidden)
            {
                if (_holo.gameObject.activeSelf) _holo.gameObject.SetActive(false);
                return;
            }
            if (!_holo.gameObject.activeSelf) _holo.gameObject.SetActive(true);

            if (!grabbing && !_grabWasActive || _mode == Mode.Palm)
            {
                // Right hand alone: pinch and drag up or down to zoom.
                _zoom.Update(right && !left, rightPoint.y);
            }

            _fitScale = Mathf.Clamp(TargetSize / Mathf.Max(0.05f, _extent), 0.1f, 2f);
            _scale = Mathf.Lerp(_scale, _fitScale * _zoom.Zoom, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));

            if (_mode == Mode.Palm)
            {
                _yaw = Mathf.LerpAngle(_yaw, 0f, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime)); // back to the compass direction
                _target = palm + Vector3.up * (LiftAbovePalm + _halfHeight * _scale);
            }

            _drawn = _placed ? Vector3.Lerp(_drawn, _target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)) : _target;
            _placed = true;

            _holo.SetPositionAndRotation(_drawn, Quaternion.Euler(0f, _yaw, 0f));
            _holo.localScale = Vector3.one * _scale;

            SyncCopies(now);
        }

        void DecideMode(float now, Transform head, bool palmUp, bool grabbing)
        {
            if (grabbing)
            {
                if (!_grabWasActive)
                {
                    _grabWasActive = true;
                    if (_mode == Mode.Hidden) { _target = PinnedSpot(head); _drawn = _target; _placed = true; }
                    _mode = Mode.Free;
                    _grabTarget0 = _target;
                    _grabYaw0 = _yaw;
                    _grabZoom0 = _zoom.Zoom;
                    TelemetryLogger.Info("PalmDollhouse", "grabbed with both hands");
                }

                _zoom.Set(_grabZoom0 * _grab.Factor);
                _yaw = _grabYaw0 + _grab.YawDegrees;
                _target = _grabTarget0 + _grab.Move;
                _freeUntil = now + FloatSeconds;
                return;
            }

            if (_grabWasActive)
            {
                _grabWasActive = false;
                TelemetryLogger.Info("PalmDollhouse", "released; floating");
            }

            if (now < _pinnedUntil && _mode == Mode.Hidden)
            {
                _mode = Mode.Free;
                _target = PinnedSpot(head);
                _drawn = _target;
                _placed = true;
                _freeUntil = _pinnedUntil;
                TelemetryLogger.Info("PalmDollhouse", "shown in front of you");
            }

            if (palmUp)
            {
                if (_mode != Mode.Palm)
                {
                    _mode = Mode.Palm;
                    _placed = _drawn != Vector3.zero; // glide from wherever it was to the hand
                    TelemetryLogger.Info("PalmDollhouse", "shown above the left palm");
                }
                return;
            }

            if (_mode == Mode.Palm) _mode = Mode.Hidden;                  // palm lowered
            else if (_mode == Mode.Free && now >= _freeUntil) _mode = Mode.Hidden;
        }

        // -- Finding and copying ---------------------------------------------------------------------

        bool Find()
        {
            if (Time.unscaledTime < _nextFindAt) return false;
            _nextFindAt = Time.unscaledTime + 1f;

            _manager = FindFirstObjectByType<DollhouseManager>();
            if (_manager == null) return false;

            _origin = FindFirstObjectByType<XROrigin>();
            _holo = new GameObject("DollhouseHologram").transform;
            _holo.SetParent(transform, false);
            _holo.gameObject.SetActive(false);
            TelemetryLogger.Info("PalmDollhouse", "found the dollhouse; hidden until a left palm is raised");
            return true;
        }

        /// <summary>Notices new and removed pieces of the original, and re-measures it.</summary>
        void Refresh()
        {
            var sources = _manager.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            var bounds = new Bounds();

            foreach (var source in sources)
            {
                if (source == null) continue;

                if (!_copies.ContainsKey(source)) TryMakeCopy(source);

                if (source.enabled && source.gameObject.activeInHierarchy && _copies.ContainsKey(source))
                {
                    if (!any) { bounds = source.bounds; any = true; }
                    else bounds.Encapsulate(source.bounds);
                }
            }

            _gone.Clear();
            foreach (var pair in _copies)
                if (pair.Key == null) _gone.Add(pair.Key);
            foreach (var key in _gone)
            {
                if (_copies[key].transform != null) Destroy(_copies[key].transform.gameObject);
                _copies.Remove(key);
            }

            if (!any) return;
            _centre = bounds.center;
            _extent = Mathf.Max(bounds.size.x, bounds.size.z);
            _halfHeight = bounds.extents.y;
        }

        void TryMakeCopy(Renderer source)
        {
            var filter = source.GetComponent<MeshFilter>();
            var text = source.GetComponent<TextMesh>();
            if (!(source is MeshRenderer) || (filter == null && text == null)) return;

            var go = new GameObject("Copy-" + source.name);
            go.transform.SetParent(_holo, false);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var copy = new Copy { source = source, transform = go.transform, renderer = renderer, sourceFilter = filter, sourceText = text };

            if (filter != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                renderer.sharedMaterials = source.sharedMaterials;
            }
            else
            {
                var mine = go.AddComponent<TextMesh>();
                mine.font = text.font;
                mine.fontSize = text.fontSize;
                mine.characterSize = text.characterSize;
                mine.anchor = text.anchor;
                mine.alignment = text.alignment;
                copy.text = mine;
                renderer.sharedMaterial = source.sharedMaterial;
            }

            // Drawn by the copy now. Only this flag is used, so nothing else's idea of "enabled" is disturbed.
            source.forceRenderingOff = true;
            _copies[source] = copy;
        }

        /// <summary>Every frame: put each copy where its original is, relative to the middle of the original miniature.</summary>
        void SyncCopies(float now)
        {
            bool colours = now >= _nextColourAt;
            if (colours) _nextColourAt = now + 0.2f;

            foreach (var pair in _copies)
            {
                var copy = pair.Value;
                var source = pair.Key;
                if (source == null || copy.transform == null) continue;

                bool show = source.enabled && source.gameObject.activeInHierarchy;
                if (copy.renderer.enabled != show) copy.renderer.enabled = show;
                if (!show) continue;

                var t = source.transform;
                copy.transform.localPosition = t.position - _centre;
                copy.transform.localRotation = t.rotation;
                copy.transform.localScale = t.lossyScale;

                if (copy.text != null)
                {
                    copy.text.text = copy.sourceText.text;
                    copy.text.color = copy.sourceText.color;
                }
                else if (colours)
                {
                    // Cleaning a room changes its colour through a per-renderer override, not a new material.
                    source.GetPropertyBlock(_block);
                    copy.renderer.SetPropertyBlock(_block);
                    if (copy.renderer.sharedMaterial != source.sharedMaterial) copy.renderer.sharedMaterial = source.sharedMaterial;
                }
            }
        }

        static Vector3 PinnedSpot(Transform head)
        {
            var flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
            return head.position + flat.normalized * 0.7f + Vector3.down * 0.15f;
        }

        void PollRequest(float now)
        {
            if (now < _nextPollAt) return;
            _nextPollAt = now + 0.5f;

            var path = Path.Combine(Application.persistentDataPath, RequestFileName);
            if (!File.Exists(path)) return;

            float seconds = DefaultPinSeconds;
            try
            {
                var text = File.ReadAllText(path).Trim();
                if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0f)
                    seconds = Mathf.Min(s, 300f);
                File.Delete(path);
            }
            catch { /* still show it */ }

            _pinnedUntil = now + seconds;
            _mode = Mode.Hidden; // so the next frame places it in front of the player afresh
            TelemetryLogger.Info("PalmDollhouse", "asked to hold the house in front of you for " + seconds + " s");
        }

        // -- Hands ----------------------------------------------------------------------------------

        Transform TrackingSpace()
            => _origin != null ? (_origin.Origin != null ? _origin.Origin.transform : _origin.transform) : null;

        XRHandSubsystem Hands()
        {
            _subsystems.Clear();
            SubsystemManager.GetSubsystems(_subsystems);
            return _subsystems.Count > 0 && _subsystems[0].running ? _subsystems[0] : null;
        }

        /// <summary>Left palm centre in world space, how much its outward normal points up, and whether it is tracked.</summary>
        bool TryGetPalm(out Vector3 world, out float normalUp, out bool tracked)
        {
            world = default;
            normalUp = -1f;
            tracked = false;

            var hands = Hands();
            if (hands == null) return false;

            var hand = hands.leftHand;
            tracked = hand.isTracked;
            if (!tracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var pose)) return false;

            var space = TrackingSpace();
            world = space != null ? space.TransformPoint(pose.position) : pose.position;
            var rotation = space != null ? space.rotation * pose.rotation : pose.rotation;

            // The palm joint's +Y points out of the back of the hand, so the palm's own normal is the opposite way.
            normalUp = -(rotation * Vector3.up).y;
            return true;
        }

        /// <summary>Thumb tip and index tip meeting, with a looser release than grab so it does not flicker.</summary>
        bool TryGetPinch(bool rightHand, ref bool held, out Vector3 world)
        {
            world = default;

            var hands = Hands();
            if (hands == null) { held = false; return false; }

            var hand = rightHand ? hands.rightHand : hands.leftHand;
            if (!hand.isTracked
                || !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb)
                || !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index))
            {
                held = false;
                return false;
            }

            var space = TrackingSpace();
            var a = space != null ? space.TransformPoint(thumb.position) : thumb.position;
            var b = space != null ? space.TransformPoint(index.position) : index.position;

            float gap = Vector3.Distance(a, b);
            held = held ? gap < 0.035f : gap < 0.02f;

            world = (a + b) * 0.5f;
            return held;
        }
    }
}

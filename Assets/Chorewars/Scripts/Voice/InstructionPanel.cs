using Chorewars.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Chorewars.HandsFree;

namespace Chorewars.Voice
{
    /// <summary>
    /// The panel Claude uses to tell the player what to do: the words on screen, spoken aloud, a
    /// "repeat" button, and a "talk" button to reply.
    /// </summary>
    /// <remarks>
    /// Everything is hands-free: the two buttons are pressed by looking at them for a moment. The
    /// instruction is spoken once when it arrives; "Repeat" speaks it once more, and never loops.
    /// The microphone is open only while the red "LISTENING" dot is showing.
    ///
    /// Built from plain quads and TextMesh so it needs no scene, prefab or canvas, and it exists only on
    /// a device where the PC's voice server is reachable: it does nothing until a line arrives.
    /// </remarks>
    public sealed class InstructionPanel : MonoBehaviour
    {
        const float Distance = 1.4f;
        const float HeightOffset = -0.05f;
        const int WrapWidth = 30;
        const int MaxLines = 6;
        const float VisibleSeconds = 150f;
        const float ButtonAngle = 5.5f;

        VoiceLinkClient _link;
        MicStreamer _mic;
        AudioSource _audio;
        AudioClip _lastClip;

        Transform _root;
        TextMesh _body;
        TextMesh _status;
        TextMesh _talkLabel;
        Transform _repeatFill, _talkFill;
        Transform _repeatButton, _talkButton;
        GameObject _listeningDot;

        readonly GazePanelPlacement _placement = new GazePanelPlacement();
        readonly DwellButtonLogic _repeatDwell = new DwellButtonLogic();
        readonly DwellButtonLogic _talkDwell = new DwellButtonLogic();
        float _lastLineAt = -1000f;
        bool _wasVisible;
        bool _wasConnected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!AndroidPermissions.IsDevice) return;
            if (FindFirstObjectByType<InstructionPanel>() != null) return;

            var go = new GameObject(nameof(InstructionPanel));
            DontDestroyOnLoad(go);
            go.AddComponent<InstructionPanel>();
        }

        void Awake()
        {
            _link = gameObject.AddComponent<VoiceLinkClient>();
            _mic = gameObject.AddComponent<MicStreamer>();
            _mic.Link = _link;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _audio.loop = false;
            _audio.playOnAwake = false;

            Build();
            _root.gameObject.SetActive(false);
        }

        void Update()
        {
            bool connected = _link.Connected;
            if (connected != _wasConnected)
            {
                _wasConnected = connected;
                TelemetryLogger.Info("Voice", connected ? "link to the PC is up" : "link to the PC is down");
            }

            while (_link.TryDequeue(out var frame)) Handle(frame);

            bool visible = _mic.Recording || Time.unscaledTime - _lastLineAt < VisibleSeconds;
            if (visible != _wasVisible)
            {
                _wasVisible = visible;
                _root.gameObject.SetActive(visible);
                if (visible) _placement.Release();
            }
            if (!visible) return;

            var camera = Camera.main;
            if (camera == null) return;
            var head = camera.transform;

            _placement.Hold(_root, head, Distance, HeightOffset);

            _status.text = connected ? "PC link: connected" : "PC link: waiting...";
            _listeningDot.SetActive(_mic.Recording);
            _talkLabel.text = _mic.Recording ? "STOP" : "TALK";

            float dt = Time.unscaledDeltaTime;

            if (_repeatDwell.Update(IsLooking(head, _repeatButton), dt)) Repeat();
            if (_talkDwell.Update(IsLooking(head, _talkButton), dt)) ToggleTalk();

            SetFill(_repeatFill, _repeatDwell.Progress01);
            SetFill(_talkFill, _talkDwell.Progress01);
        }

        void Handle(VoiceFrame frame)
        {
            if (frame.kind == VoiceLinkProtocol.Text)
            {
                var text = System.Text.Encoding.UTF8.GetString(frame.payload);
                var lines = TextWrap.Lines(text, WrapWidth, MaxLines);
                _body.text = string.Join("\n", lines);
                _lastLineAt = Time.unscaledTime;
                TelemetryLogger.Info("Voice", "instruction shown: " + text);
            }
            else if (frame.kind == VoiceLinkProtocol.Wav)
            {
                if (!PcmAudio.TryDecodeWav(frame.payload, out var samples, out int rate, out int channels) || samples.Length == 0)
                {
                    TelemetryLogger.Warn("Voice", "could not decode the spoken instruction");
                    return;
                }

                _lastClip = AudioClip.Create("instruction", samples.Length / channels, channels, rate, false);
                _lastClip.SetData(samples, 0);
                _lastLineAt = Time.unscaledTime;
                Speak();
            }
        }

        void Speak()
        {
            if (_lastClip == null) return;

            _audio.Stop();
            _audio.clip = _lastClip;
            _audio.loop = false;
            _audio.Play();
        }

        void Repeat()
        {
            TelemetryLogger.Info("Voice", "repeat pressed");
            Speak();
        }

        void ToggleTalk()
        {
            if (_mic.Recording)
            {
                _mic.Stop("you looked at stop");
                return;
            }

            if (_audio.isPlaying) _audio.Stop(); // do not record the instruction itself
            _mic.Begin();
        }

        static bool IsLooking(Transform head, Transform target)
        {
            var direction = target.position - head.position;
            return direction.sqrMagnitude > 1e-6f && Vector3.Angle(head.forward, direction) < ButtonAngle;
        }

        static void SetFill(Transform fill, float progress01)
        {
            var scale = fill.localScale;
            scale.x = Mathf.Max(0.0001f, progress01) * 0.34f;
            fill.localScale = scale;
            var position = fill.localPosition;
            position.x = -0.17f + scale.x * 0.5f;
            fill.localPosition = position;
        }

        // -- Building -------------------------------------------------------------------------------

        void Build()
        {
            _root = new GameObject("InstructionPanelRoot").transform;
            _root.SetParent(transform, false);

            Quad("Background", _root, new Vector3(0f, -0.04f, 0.02f), new Vector2(0.92f, 0.56f), new Color(0.05f, 0.07f, 0.10f));

            _body = Label("Instruction", _root, new Vector3(-0.42f, 0.22f, 0f), 64, 0.0055f, TextAnchor.UpperLeft, Color.white);
            _status = Label("Status", _root, new Vector3(-0.42f, -0.29f, 0f), 64, 0.0028f, TextAnchor.LowerLeft, new Color(0.6f, 0.7f, 0.8f));
            _status.text = "PC link: waiting...";

            _repeatButton = Button("Repeat", new Vector3(-0.2f, -0.17f, 0f), "REPEAT", new Color(0.12f, 0.35f, 0.55f), out _repeatFill, out _);
            _talkButton = Button("Talk", new Vector3(0.2f, -0.17f, 0f), "TALK", new Color(0.12f, 0.45f, 0.25f), out _talkFill, out _talkLabel);

            _listeningDot = new GameObject("ListeningDot");
            _listeningDot.transform.SetParent(_root, false);
            Quad("Dot", _listeningDot.transform, new Vector3(0.30f, 0.23f, 0f), new Vector2(0.05f, 0.05f), new Color(1f, 0.1f, 0.1f));
            var listening = Label("ListeningText", _listeningDot.transform, new Vector3(0.34f, 0.23f, 0f), 64, 0.0035f, TextAnchor.MiddleLeft, new Color(1f, 0.35f, 0.35f));
            listening.text = "LISTENING";
            _listeningDot.SetActive(false);
        }

        Transform Button(string name, Vector3 position, string label, Color colour, out Transform fill, out TextMesh text)
        {
            var button = new GameObject(name).transform;
            button.SetParent(_root, false);
            button.localPosition = position;

            Quad("Face", button, Vector3.zero, new Vector2(0.36f, 0.12f), colour);
            Quad("Track", button, new Vector3(0f, -0.045f, -0.002f), new Vector2(0.34f, 0.012f), new Color(0.2f, 0.2f, 0.2f));
            fill = Quad("Fill", button, new Vector3(-0.17f, -0.045f, -0.004f), new Vector2(1f, 0.012f), Color.white).transform;
            text = Label("Label", button, new Vector3(0f, 0.012f, -0.006f), 64, 0.0055f, TextAnchor.MiddleCenter, Color.white);
            text.text = label;
            SetFill(fill, 0f);
            return button;
        }

        static GameObject Quad(string name, Transform parent, Vector3 localPosition, Vector2 size, Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var shader = Shader.Find("Unlit/Color");
            var renderer = go.GetComponent<MeshRenderer>();
            if (shader != null)
            {
                var material = new Material(shader) { color = colour };
                renderer.sharedMaterial = material;
            }
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        static TextMesh Label(string name, Transform parent, Vector3 localPosition, int fontSize, float characterSize,
                              TextAnchor anchor, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var text = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.font = font;
            text.fontSize = fontSize;
            text.characterSize = characterSize;
            text.anchor = anchor;
            text.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
            text.color = colour;
            text.text = string.Empty;

            var renderer = go.GetComponent<MeshRenderer>();
            if (font != null) renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return text;
        }
    }
}

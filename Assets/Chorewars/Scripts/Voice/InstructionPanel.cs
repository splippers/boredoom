using Chorewars.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Chorewars.HandsFree;

namespace Chorewars.Voice
{
    /// <summary>
    /// The panel Claude uses to tell the player what to do: the words on screen, spoken aloud, a
    /// "repeat" button, and a "mute" button: the microphone is open for his replies.
    /// </summary>
    /// <remarks>
    /// Everything is hands-free: the two buttons are pressed by looking at them for a moment. The
    /// instruction is spoken once when it arrives; "Repeat" speaks it once more, and never loops.
    /// The microphone is open whenever the red "LISTENING" dot is showing, and the Mute button closes it.
    ///
    /// Built from plain quads and TextMesh so it needs no scene, prefab or canvas, and it exists only on
    /// a device where the PC's voice server is reachable: it does nothing until a line arrives.
    /// </remarks>
    public sealed class InstructionPanel : MonoBehaviour
    {
        const float Distance = 1.4f;
        // To the right of where the player faces, not in front: the game's own panels (score, room scan, chore menu)
        // are centred in front of them, and this panel is 36 degrees wide, so 40 degrees of turn leaves a clear gap.
        const float HeightOffset = 0.05f;
        const float YawOffsetDegrees = 40f;
        const int WrapWidth = 30;
        const int MaxLines = 5;
        const float ButtonAngle = 5.5f;

        VoiceLinkClient _link;
        MicStreamer _mic;
        AudioSource _audio;
        AudioClip _lastClip;
        readonly System.Collections.Generic.Queue<AudioClip> _toPlay = new System.Collections.Generic.Queue<AudioClip>();

        Transform _root;
        TextMesh _body;
        TextMesh _status;
        TextMesh _thinking;
        const int BarCount = 48;
        const float BarMaxHeight = 0.075f; // inside WaveBars: 0.005 below, 0.005 above
        Transform[] _bars;
        Material[] _barMaterials;
        int _barState = -1;
        ClaudeStatus _claude;
        float _claudeSince;
        TextMesh _talkLabel;
        Transform _repeatFill, _talkFill;
        Transform _repeatButton, _talkButton;
        GameObject _listeningDot;

        readonly GazePanelPlacement _placement = new GazePanelPlacement();

        // The floating head sits on the left; the panel is on the right. She turns to face the player.
        const float AvatarYawOffsetDegrees = -34f;
        const float AvatarDistance = 1.25f;
        const float AvatarScale = 1.5f;
        readonly GazePanelPlacement _avatarPlacement = new GazePanelPlacement();
        Transform _avatarAnchor;
        VoiceAvatar _avatar;
        readonly float[] _outputSamples = new float[256];
        readonly DwellButtonLogic _repeatDwell = new DwellButtonLogic();
        readonly DwellButtonLogic _talkDwell = new DwellButtonLogic();
        float _lastLineAt = -1000f;
        bool _wasVisible;
        bool _wasConnected;
        bool _micStarted;

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

            _avatarAnchor = new GameObject("VoiceAvatarAnchor").transform;
            _avatarAnchor.SetParent(transform, false);
            _avatar = new VoiceAvatar(_avatarAnchor);
            _avatar.Root.localScale = Vector3.one * AvatarScale;
            _avatarAnchor.gameObject.SetActive(false);
            _avatarPlacement.Anchored = true;
        }

        void Update()
        {
            bool connected = _link.Connected;
            if (connected != _wasConnected)
            {
                _wasConnected = connected;
                TelemetryLogger.Info("Voice", connected ? "link to the PC is up" : "link to the PC is down");
            }

            // Open mic by default, as Jon asked: the first time the link is up, listen. Muting is his to do.
            if (connected && !_micStarted)
            {
                _micStarted = true;
                _mic.Begin();
            }

            while (_link.TryDequeue(out var frame)) Handle(frame);

            // Up for as long as the link to the PC is: a panel that hides itself takes the Talk button with it,
            // and then the player cannot answer. It is small and held still, so leaving it up costs little.
            bool visible = connected || _mic.Listening;
            if (visible != _wasVisible)
            {
                _wasVisible = visible;
                _root.gameObject.SetActive(visible);
                _avatarAnchor.gameObject.SetActive(visible);
                if (visible) { _placement.Release(); _avatarPlacement.Release(); }
            }
            if (!visible) return;

            var camera = Camera.main;
            if (camera == null) return;
            var head = camera.transform;

            _placement.Hold(_root, head, Distance, HeightOffset, float.NegativeInfinity, 0f, YawOffsetDegrees);

            _avatarPlacement.Hold(_avatarAnchor, head, AvatarDistance, HeightOffset + 0.02f, float.NegativeInfinity, 0f, AvatarYawOffsetDegrees);
            _avatarAnchor.rotation = Quaternion.LookRotation(_avatarAnchor.position - head.position, Vector3.up) * Quaternion.Euler(0, 180f, 0);
            float voice = 0f;
            if (_audio.isPlaying)
            {
                _audio.GetOutputData(_outputSamples, 0);
                float sum = 0f;
                for (int i = 0; i < _outputSamples.Length; i++) sum += _outputSamples[i] * _outputSamples[i];
                voice = Mathf.Sqrt(sum / _outputSamples.Length);
            }
            _avatar.Tick(Time.unscaledDeltaTime, voice, _mic.Hearing, _claude.Busy);

            _status.text = connected ? "PC link: connected" : "PC link: waiting...";

            // A busy Claude with nothing said for two minutes is more likely a lost message than real work.
            if (_claude.Busy && Time.unscaledTime - _claudeSince > 120f) _claude = default;
            _thinking.text = _claude.Describe(1 + (int)(Time.unscaledTime * 2f) % 3);
            DrawWaveform();
            _listeningDot.SetActive(_mic.Listening);
            _talkLabel.text = _mic.Listening ? "MUTE" : "UNMUTE";
            PlayNext();
            _mic.Suppressed = _audio.isPlaying || _toPlay.Count > 0; // never record our own voice

            float dt = Time.unscaledDeltaTime;

            if (_repeatDwell.Update(IsLooking(head, _repeatButton), dt)) Repeat();
            if (_talkDwell.Update(IsLooking(head, _talkButton), dt)) ToggleMute();

            SetFill(_repeatFill, PanelLayout.RepeatProgress, _repeatDwell.Progress01);
            SetFill(_talkFill, PanelLayout.TalkProgress, _talkDwell.Progress01);
        }

        void Handle(VoiceFrame frame)
        {
            if (frame.kind == VoiceLinkProtocol.Status)
            {
                _claude = ClaudeStatus.Parse(System.Text.Encoding.UTF8.GetString(frame.payload));
                _claudeSince = Time.unscaledTime;
            }
            else if (frame.kind == VoiceLinkProtocol.Text)
            {
                _claude = default; // the answer has arrived
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

                _claude = default;
                _lastClip = AudioClip.Create("instruction", samples.Length / channels, channels, rate, false);
                _lastClip.SetData(samples, 0);
                _lastLineAt = Time.unscaledTime;
                Speak();
            }
        }

        void Speak()
        {
            if (_lastClip == null) return;

            // Queued, never cut in on: two things to say are said one after the other.
            _toPlay.Enqueue(_lastClip);
        }

        void PlayNext()
        {
            if (_audio.isPlaying || _toPlay.Count == 0) return;

            _audio.clip = _toPlay.Dequeue();
            _audio.loop = false;
            _audio.Play();
        }

        void Repeat()
        {
            TelemetryLogger.Info("Voice", "repeat pressed");
            Speak();
        }

        void ToggleMute()
        {
            if (_mic.Listening) _mic.Stop("you looked at mute");
            else _mic.Begin();
        }

        /// <summary>
        /// The scrolling waveform: 0 = oldest on the left, newest on the right. Green while a sentence is being
        /// sent, blue while the mic is open and quiet, grey while muted, so the strip shows the mic's true state.
        /// </summary>
        void DrawWaveform()
        {
            int state = !_mic.Listening ? 0 : _mic.Hearing ? 2 : 1;
            if (state != _barState)
            {
                _barState = state;
                var colour = state == 2 ? new Color(0.3f, 1f, 0.45f)
                           : state == 1 ? new Color(0.35f, 0.65f, 1f)
                           : new Color(0.35f, 0.35f, 0.38f);
                for (int i = 0; i < BarCount; i++) _barMaterials[i].color = colour;
            }

            for (int i = 0; i < BarCount; i++)
            {
                float h = Mathf.Max(0.004f, _mic.Levels.Height(i) * BarMaxHeight); // never vanishes: a flat line still shows the strip
                var scale = _bars[i].localScale;
                scale.y = h;
                _bars[i].localScale = scale;
                var position = _bars[i].localPosition;
                position.y = PanelLayout.WaveBars.yMin + 0.005f + h * 0.5f;
                _bars[i].localPosition = position;
            }
        }

        static bool IsLooking(Transform head, Transform target)
        {
            var direction = target.position - head.position;
            return direction.sqrMagnitude > 1e-6f && Vector3.Angle(head.forward, direction) < ButtonAngle;
        }

        static void SetFill(Transform fill, Rect row, float progress01)
        {
            float width = Mathf.Max(0.0001f, progress01) * row.width;
            var scale = fill.localScale;
            scale.x = width;
            fill.localScale = scale;
            var position = fill.localPosition;
            position.x = row.xMin + width * 0.5f;
            fill.localPosition = position;
        }

        // -- Building -------------------------------------------------------------------------------

        void Build()
        {
            _root = new GameObject("InstructionPanelRoot").transform;
            _root.SetParent(transform, false);

            // Behind everything by two centimetres; nothing else shares a depth with anything it touches.
            Place(Quad("Background", _root, Vector3.zero, Vector2.one, new Color(0.05f, 0.07f, 0.10f)), PanelLayout.Background, 0.02f);

            _body = Label("Instruction", _root, TopLeftOf(PanelLayout.Body), 64, 0.0055f, TextAnchor.UpperLeft, Color.white);
            _body.text = "Waiting for Claude...";
            _thinking = Label("Thinking", _root, LeftOf(PanelLayout.Thinking), 64, 0.0033f, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.3f));
            _status = Label("Status", _root, LeftOf(PanelLayout.Status), 64, 0.0028f, TextAnchor.MiddleLeft, new Color(0.6f, 0.7f, 0.8f));
            _status.text = "PC link: waiting...";

            BuildWaveform();

            _repeatButton = Button("Repeat", PanelLayout.RepeatButton, PanelLayout.RepeatProgress, "REPEAT",
                                   new Color(0.12f, 0.35f, 0.55f), out _repeatFill, out _);
            _talkButton = Button("Talk", PanelLayout.TalkButton, PanelLayout.TalkProgress, "MUTE",
                                 new Color(0.12f, 0.45f, 0.25f), out _talkFill, out _talkLabel);

            _listeningDot = new GameObject("ListeningDot");
            _listeningDot.transform.SetParent(_root, false);
            Place(Quad("Dot", _listeningDot.transform, Vector3.zero, Vector2.one, new Color(1f, 0.1f, 0.1f)), PanelLayout.HeaderDot, 0f);
            var listening = Label("ListeningText", _listeningDot.transform, LeftOf(PanelLayout.HeaderText), 64, 0.0035f, TextAnchor.MiddleLeft, new Color(1f, 0.35f, 0.35f));
            listening.text = "LISTENING";
            _listeningDot.SetActive(false);
        }

        static Vector3 LeftOf(Rect r) => new Vector3(r.xMin, r.center.y, 0f);
        static Vector3 TopLeftOf(Rect r) => new Vector3(r.xMin, r.yMax, 0f);

        /// <summary>Puts a unit quad exactly over a layout region, at depth <paramref name="z"/>.</summary>
        static void Place(GameObject quad, Rect region, float z)
        {
            quad.transform.localPosition = new Vector3(region.center.x, region.center.y, z);
            quad.transform.localScale = new Vector3(region.width, region.height, 1f);
        }

        void BuildWaveform()
        {
            _bars = new Transform[BarCount];
            _barMaterials = new Material[BarCount];

            var area = PanelLayout.WaveBars;
            float pitch = area.width / BarCount;
            for (int i = 0; i < BarCount; i++)
            {
                var bar = Quad("Bar" + i, _root, new Vector3(area.xMin + pitch * (i + 0.5f), area.yMin, 0f),
                               new Vector2(pitch * 0.7f, 0.004f), new Color(0.35f, 0.35f, 0.38f));
                _bars[i] = bar.transform;
                _barMaterials[i] = bar.GetComponent<MeshRenderer>().sharedMaterial;
            }

            // A small tick in its own column, level with the loudness at which speech starts to count.
            // Beside the bars rather than across them, so no line is drawn through a bar.
            var marker = PanelLayout.WaveMarker;
            float tickY = area.yMin + 0.005f + LevelHistory.ToHeight(0.02f) * BarMaxHeight;
            Quad("SpeechTick", _root, new Vector3(marker.center.x, tickY, 0f), new Vector2(marker.width, 0.003f), Color.white);
        }

        Transform Button(string name, Rect face, Rect progress, string label, Color colour, out Transform fill, out TextMesh text)
        {
            var button = new GameObject(name).transform;
            button.SetParent(_root, false);
            button.localPosition = new Vector3(face.center.x, face.center.y, 0f);

            // The face fills its region; the button's word floats a centimetre in front so the two never fight.
            Quad("Face", button, Vector3.zero, new Vector2(face.width, face.height), colour);
            text = Label("Label", button, new Vector3(0f, 0f, -0.01f), 64, 0.0055f, TextAnchor.MiddleCenter, Color.white);
            text.text = label;

            // The progress bar is its own strip under the button: nothing sits beneath it.
            fill = Quad("Fill", _root, new Vector3(progress.xMin, progress.center.y, 0f), new Vector2(0.0001f, progress.height), Color.white).transform;
            SetFill(fill, progress, 0f);
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

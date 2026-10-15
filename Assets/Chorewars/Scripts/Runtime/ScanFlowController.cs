using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Chorewars.Runtime
{
    /// <summary>
    /// Phase-based controller for Option B: present a room scan flow with a start button,
    /// live progress, "DONE" transition, and only then begin the chore mode.
    /// </summary>
    public sealed class ScanFlowController : MonoBehaviour
    {
        private enum Phase
        {
            Ready,       // waiting for user to start scan
            Scanning,    // scan + miniature construction underway
            Presenting,  // scan complete, playing "lift + frame" reveal
            Playing      // dollhouse docked, chore mode running
        }

        private PlaceholderHud _hud;
        private IScanSource _source;
        private ScanToDollhousePipeline _pipeline;
        private DollhousePresenter _presenter;
        private Transform _dollhouseRoot;
        private Camera _camera;
        private RuntimeCoverageMode _mode;

        private Phase _phase = Phase.Ready;
        private float _presentUntil = -1f;

        /// <summary>
        /// Raised once the scan has finished and before the chore begins, with the world extent of
        /// everything that was found. Until the scan runs, the play area can only be guessed at; this
        /// is the first moment it is known, so it is where the guess is replaced by the real room.
        /// </summary>
        public Action<Bounds> ScanAreaResolved { get; set; }

        public void Configure(PlaceholderHud hud,
                              IScanSource source,
                              ScanToDollhousePipeline pipeline,
                              DollhousePresenter presenter,
                              Transform dollhouseRoot,
                              RuntimeCoverageMode mode,
                              Camera camera)
        {
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _source = source;
            _pipeline = pipeline;
            _presenter = presenter;
            _dollhouseRoot = dollhouseRoot;
            _mode = mode;
            _camera = camera;

            _hud.StartScanRequested -= OnStartRequested;
            _hud.StartScanRequested += OnStartRequested;

            _phase = Phase.Ready;
            _presentUntil = -1f;

            _hud.ShowScanPanel("ROOM SCAN", "Stand in the room and press START SCAN to map it.");
            _hud.SetScanButtonLabel("START SCAN");
            _hud.SetScanProgress(0f, "Ready to scan");
            _hud.SetChorePanelVisible(false);
        }

        private void OnDestroy()
        {
            if (_hud != null) _hud.StartScanRequested -= OnStartRequested;
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Ready:
                    if (StartButtonPressed()) BeginScan();
                    break;

                case Phase.Scanning:
                    UpdateScanning();
                    break;

                case Phase.Presenting:
                    if (_presentUntil > 0f && Time.unscaledTime >= _presentUntil) BeginPlaying();
                    break;

                case Phase.Playing:
                    break;
            }
        }

        private void OnStartRequested() => BeginScan();

        private void BeginScan()
        {
            if (_phase != Phase.Ready) return;
            _phase = Phase.Scanning;

            _hud.ShowScanPanel("SCANNING THE ROOM", "Move around slowly while we map surfaces…");
            _hud.SetScanButtonLabel("SCANNING");
            _hud.SetScanProgress(0f, "Locating rooms…");

            if (_pipeline != null) _pipeline.StartScan(_source);
        }

        private void UpdateScanning()
        {
            float progress = _source != null ? _source.Progress : 0f;

            string status;
            if (_pipeline != null)
                status = $"Mapping rooms — {_pipeline.RoomCount} found";
            else
                status = "Mapping rooms…";

            _hud.SetScanProgress(progress, status);

            if (_pipeline != null && _pipeline.ScanComplete)
            {
                BeginPresent();
            }
            else if (_source != null && !_source.IsScanning && progress >= 0.999f)
            {
                // Fallback in case ScanCompleted didn't fire.
                BeginPresent();
            }
        }

        private void BeginPresent()
        {
            if (_phase == Phase.Presenting || _phase == Phase.Playing) return;
            _phase = Phase.Presenting;

            _hud.SetScanProgress(1f, "SCAN COMPLETE");
            _hud.SetScanButtonLabel("DONE");
            _hud.ShowScanPanel("SCAN COMPLETE", "Your home model is being framed in front of you.");

            // Hand over the real extent before the reveal frames it, so the reveal frames the room
            // that was actually scanned rather than the placeholder the game started with.
            if (_pipeline != null)
            {
                var scanned = _pipeline.ScannedBounds;
                if (scanned != null) ScanAreaResolved?.Invoke(scanned.Value);
            }

            _presentUntil = Time.unscaledTime + 2.4f;

            if (_presenter != null)
                _presenter.Present(_camera != null ? _camera.transform : null, _dollhouseRoot, 1.2f);
        }

        private void BeginPlaying()
        {
            _phase = Phase.Playing;
            _presentUntil = -1f;

            if (_presenter != null)
                _presenter.Dock(_camera != null ? _camera.transform : null, _dollhouseRoot, 1f);

            _hud.HideScanPanel();
            _hud.SetChorePanelVisible(true);

            if (_mode != null)
            {
                try { _mode.Begin(); }
                catch (Exception e) { Debug.LogError($"ScanFlowController: mode.Begin() failed: {e}"); }
            }
        }

        private static bool StartButtonPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }
    }
}

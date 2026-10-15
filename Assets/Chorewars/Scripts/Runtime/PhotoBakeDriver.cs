using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Photographs the floor and hands the pixels to <see cref="CoverageFloorVisualizer"/>, so a
    /// cleaned texel shows what the camera actually saw there rather than a flat tint.
    /// </summary>
    /// <remarks>
    /// The whole bake is one projection: for each texel in the camera's footprint, where does that
    /// world point land in the image, and what colour is the image there. It runs on the CPU, which
    /// sounds wrong for a renderer but is what makes the result a *texture* rather than an effect —
    /// the same texture feeds the live floor and the dollhouse miniature, and it survives the frame
    /// being dropped, because nothing re-renders it.
    ///
    /// Nothing here spends time on ground the player has already been photographed over. A capture is
    /// taken at most every <see cref="PhotogrammetrySettings.CaptureIntervalSeconds"/> and only after
    /// the tool has travelled <see cref="PhotogrammetrySettings.CaptureDistanceMeters"/>, so standing
    /// still is free.
    ///
    /// Capture is gated on cleaned texels being revealed, not on rooms: the reveal and the photograph
    /// are separate — see CoverageFloorVisualizer, which decides what a texel shows.
    /// </remarks>
    public sealed class PhotoBakeDriver : MonoBehaviour
    {
        private ISurfaceFrameSource _source;
        private CoverageFloorVisualizer _floor;
        private Transform _trackedOrigin;

        private float _nextCaptureTime;
        private Vector3 _lastCaptureAround = new(float.MaxValue, float.MaxValue, float.MaxValue);
        private bool _wasEnabled = true;

        /// <summary>Captures taken this session. Zero on a device whose camera never answered.</summary>
        public int CaptureCount { get; private set; }

        /// <summary>Texel writes performed, counted so "it did something" is not a guess.</summary>
        public int TexelsPainted { get; private set; }

        /// <summary>What the frames are coming from, for the log when they are not.</summary>
        public string SourceDescription => _source != null ? _source.Describe : "none";

        public void Configure(ISurfaceFrameSource source, CoverageFloorVisualizer floor, Transform trackedOrigin)
        {
            _source = source;
            _floor = floor;
            _trackedOrigin = trackedOrigin;

            // The first capture must not be gated on movement, or a player who never moves never sees
            // anything. MaxValue is far from any real tool position, so it always clears the gate once.
            _nextCaptureTime = 0f;
            _lastCaptureAround = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _wasEnabled = PhotogrammetrySettings.Enabled;
            CaptureCount = 0;
            TexelsPainted = 0;
        }

        /// <summary>
        /// Brings the floor in step with the switch and reports whether the layer is running.
        /// </summary>
        /// <remarks>
        /// Called every frame, but public because the transition needs to be drivable without a
        /// frame: nothing in a test can run Update, and a switch whose whole job is "stop showing
        /// photographs" has to be checkable. It also keeps the decision in one place — the driver
        /// already reads the switch, so it, rather than the floor, notices it go off.
        /// </remarks>
        public bool SyncEnabledState()
        {
            bool on = PhotogrammetrySettings.Enabled;

            if (!on && _wasEnabled && _floor != null) _floor.ClearPhotoLayer();
            _wasEnabled = on;

            return on;
        }

        private void Update()
        {
            if (!SyncEnabledState()) return;
            if (_source == null || _floor == null) return;
            if (_floor.Texture == null) return;
            if (Time.time < _nextCaptureTime) return;

            var around = _trackedOrigin != null ? _trackedOrigin.position : _floor.Bounds.center;

            if (CaptureCount > 0 &&
                Vector3.Distance(around, _lastCaptureAround) < PhotogrammetrySettings.CaptureDistanceMeters)
                return;

            var frame = _source.Capture();
            if (!frame.Valid || frame.Image == null) return;

            _nextCaptureTime = Time.time + PhotogrammetrySettings.CaptureIntervalSeconds;
            _lastCaptureAround = around;
            CaptureCount++;

            int painted = Bake(frame);

            if (PhotogrammetrySettings.VerboseLogging)
                Debug.Log($"[PhotoBake] capture {CaptureCount} from {SourceDescription} " +
                          $"at ({around.x:0.00}, {around.z:0.00}) — {painted} texels.");
        }

        /// <summary>Projects one frame onto the floor. Returns how many texels it wrote.</summary>
        private int Bake(SurfaceFrame frame)
        {
            if (!TryGroundRectangle(frame, out var minXZ, out var maxXZ)) return 0;

            // Only the rectangle the image can possibly cover is visited. Without this the bake would
            // touch every texel in the play area for every capture.
            int resolution = _floor.Resolution;
            float tx0 = _floor.TexelX(minXZ.x), tx1 = _floor.TexelX(maxXZ.x);
            float ty0 = _floor.TexelY(minXZ.y), ty1 = _floor.TexelY(maxXZ.y);

            int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(tx0, tx1)) - 1, 0, resolution - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(tx0, tx1)) + 1, 0, resolution - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(ty0, ty1)) - 1, 0, resolution - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(ty0, ty1)) + 1, 0, resolution - 1);
            if (x1 < x0 || y1 < y0) return 0;

            int width = frame.Image.width;
            int height = frame.Image.height;
            int painted = 0;

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var world = _floor.TexelCentreWorld(x, y);

                    if (!PinholeCamera.TryWorldToViewport(
                            world, frame.Position, frame.Rotation,
                            frame.VerticalFieldOfViewDegrees, frame.Aspect, out float u, out float v))
                        continue;

                    // The texel the projection lands in, named explicitly rather than left to a
                    // filtering helper's coordinate convention. A single read also avoids
                    // GetPixels32, which would copy the whole image on every capture.
                    int ix = Mathf.Clamp(Mathf.FloorToInt(u * width), 0, width - 1);
                    int iy = Mathf.Clamp(Mathf.FloorToInt(v * height), 0, height - 1);

                    var colour = frame.Image.GetPixel(ix, iy);

                    // A transparent pixel is a ray that never met the floor; it must not be written,
                    // or it would punch a hole in imagery that is already there.
                    if (colour.a == 0f) continue;

                    _floor.SetPhoto(x, y, colour);
                    painted++;
                }
            }

            if (painted > 0)
            {
                TexelsPainted += painted;
                _floor.RecomputePhotoLayer();
            }

            return painted;
        }

        /// <summary>
        /// The rectangle the frame can see, from the ground hit of each image corner. All four
        /// corners must land: a camera tilted enough to send three of them over the horizon is not
        /// looking at the floor in any useful sense, and a partial footprint would silently bake a
        /// skewed band.
        /// </summary>
        private static bool TryGroundRectangle(SurfaceFrame frame, out Vector2 min, out Vector2 max)
        {
            min = default;
            max = default;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            int hits = 0;

            for (int corner = 0; corner < 4; corner++)
            {
                float u = (corner & 1) == 0 ? 0f : 1f;
                float v = (corner & 2) == 0 ? 0f : 1f;

                var direction = PinholeCamera.RayDirection(
                    frame.Rotation, frame.VerticalFieldOfViewDegrees, frame.Aspect, u, v);

                if (!PinholeCamera.TryGroundHit(frame.Position, direction, frame.FloorY, out var hit))
                    continue;

                hits++;
                minX = Mathf.Min(minX, hit.x); maxX = Mathf.Max(maxX, hit.x);
                minZ = Mathf.Min(minZ, hit.z); maxZ = Mathf.Max(maxZ, hit.z);
            }

            if (hits < 4) return false;

            min = new Vector2(minX, minZ);
            max = new Vector2(maxX, maxZ);
            return true;
        }
    }
}

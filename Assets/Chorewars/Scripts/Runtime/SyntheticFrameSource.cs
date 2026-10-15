using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// A stand-in for the headset's camera: it looks straight down from above the tool and paints the
    /// floor with a deterministic world-space pattern.
    /// </summary>
    /// <remarks>
    /// This exists so the whole photogrammetry path can be exercised without a headset — the bake,
    /// the progressive reveal, the neighbour fill and the dollhouse's shared texture all run against
    /// real projections rather than a stub. The pattern is a property of *world position* rather
    /// than of the image, so the same floor point looks identical from every pose. That is what makes
    /// "the floor now shows what the camera saw" checkable: a test can ask for the surface colour at
    /// a texel's world centre and demand the baked texel matches it.
    ///
    /// It is a placeholder. It is not a render of the room, it has no lighting, and on device the
    /// real frames come from the headset (see ISurfaceFrameSource).
    /// </remarks>
    public sealed class SyntheticFrameSource : ISurfaceFrameSource
    {
        /// <summary>Edge of the generated image. Small on purpose: this is a placeholder lens.</summary>
        public const int ImageSize = 96;

        private readonly Transform _tool;
        private readonly Vector3 _eyeOffset;
        private readonly float _verticalFovDegrees;
        private readonly float _aspect;
        private readonly float _floorY;
        private readonly float _yawOffsetDegrees;

        private Texture2D _image;
        private Color32[] _pixels;

        public SyntheticFrameSource(
            Transform tool, Vector3 eyeOffset, float verticalFovDegrees, float aspect, float floorY,
            float yawOffsetDegrees = 0f)
        {
            _tool = tool;
            _eyeOffset = eyeOffset;
            _verticalFovDegrees = verticalFovDegrees;
            _aspect = aspect;
            _floorY = floorY;
            _yawOffsetDegrees = yawOffsetDegrees;
        }

        public string Describe => "synthetic floor camera (placeholder)";

        /// <summary>
        /// The colour the stand-in surface has at a world point: a wood-grain ramp crossed with a
        /// two-tone tile grid. Deterministic, world-space, and high enough contrast that a wrong
        /// projection is obvious rather than plausible.
        /// </summary>
        public static Color32 SurfaceColour(Vector3 world)
        {
            float grain = Mathf.Repeat(world.x * 1.7f + world.z * 0.9f, 1f);
            var colour = Color.Lerp(
                new Color(0.72f, 0.62f, 0.48f),
                new Color(0.42f, 0.31f, 0.22f),
                grain);

            int gx = Mathf.FloorToInt(world.x / 0.6f);
            int gz = Mathf.FloorToInt(world.z / 0.6f);
            if (((gx + gz) & 1) == 0)
                colour = Color.Lerp(colour, new Color(0.26f, 0.48f, 0.58f), 0.22f);

            return (Color32)colour;
        }

        public SurfaceFrame Capture()
        {
            var origin = _tool != null ? _tool.position + _eyeOffset : _eyeOffset;

            // Straight down, yawed with the tool so the frame can be seen to rotate with the player.
            float yaw = (_tool != null ? _tool.eulerAngles.y : 0f) + _yawOffsetDegrees;
            var rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);

            RenderImage(origin, rotation);

            return new SurfaceFrame
            {
                Valid = true,
                Position = origin,
                Rotation = rotation,
                VerticalFieldOfViewDegrees = _verticalFovDegrees,
                Aspect = _aspect,
                Image = _image,
                FloorY = _floorY
            };
        }

        /// <summary>
        /// Fills the image by casting one ray per pixel at the floor plane. Every pixel of a
        /// straight-down camera hits; a pixel that misses is left transparent and the bake skips it.
        /// </summary>
        private void RenderImage(Vector3 origin, Quaternion rotation)
        {
            if (_image == null)
            {
                _image = new Texture2D(ImageSize, ImageSize, TextureFormat.RGBA32, false)
                {
                    name = "SyntheticSurfaceFrame",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                _pixels = new Color32[ImageSize * ImageSize];
            }

            for (int y = 0; y < ImageSize; y++)
            {
                float v = (y + 0.5f) / ImageSize;

                for (int x = 0; x < ImageSize; x++)
                {
                    float u = (x + 0.5f) / ImageSize;
                    var direction = PinholeCamera.RayDirection(rotation, _verticalFovDegrees, _aspect, u, v);

                    _pixels[y * ImageSize + x] =
                        PinholeCamera.TryGroundHit(origin, direction, _floorY, out var hit)
                            ? SurfaceColour(hit)
                            : new Color32(0, 0, 0, 0);
                }
            }

            _image.SetPixels32(_pixels);
            _image.Apply(false);
        }
    }
}

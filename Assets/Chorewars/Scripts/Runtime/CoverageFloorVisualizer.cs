using System;
using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Placeholder "cleaned area" feedback: paints visited cells onto a texture applied to the
    /// floor. Purely cosmetic — scoring is driven by <c>CoverageMap</c>. Freshly cleaned texels
    /// flash bright and then settle, so progress reads as a sparkle rather than a silently changing
    /// texture.
    /// </summary>
    /// <remarks>
    /// Two layers share one texture. The base layer is the flat clean tint, one texel at a time as
    /// the tool passes over it. The photo layer, written by <see cref="PhotoBakeDriver"/>, replaces
    /// that tint with what the camera saw at that spot — as soon as the texel is cleaned, not when
    /// the room is finished, so the floor is revealed progressively under the player.
    ///
    /// A cleaned texel with no photograph of its own is filled from the blurred average of the
    /// photographed texels around it rather than falling back to flat tint. A reveal edge made of
    /// hard-edged unphotographed texels reads as a bug; a soft one reads as depth of field.
    ///
    /// The mapping from world to texel is the one the visualiser has always used, which is why the
    /// photo lines up with the coverage map and with the dollhouse miniature that shares this texture.
    /// </remarks>
    public sealed class CoverageFloorVisualizer : MonoBehaviour
    {
        private const int BrushRadius = 2;
        private const float FlashDecayPerSecond = 4.5f;
        private static readonly Color32 Uncleaned = new Color32(38, 42, 48, 255);
        private static readonly Color32 Cleaned = new Color32(90, 220, 160, 255);
        private static readonly Color32 FlashTint = new Color32(235, 255, 245, 255);

        private Texture2D _texture;
        private Color32[] _pixels;
        private Color32[] _display;
        private float[] _flash;

        // The photo layer. Alpha 0 means "never photographed", which is a different thing from
        // "photographed as black", so the two cannot be conflated in one array.
        private Color32[] _photo;
        private bool[] _revealed;

        private Bounds _bounds;
        private int _resolution = 64;
        private bool _dirty;
        private bool _flashing;
        private bool _photoDirty;

        /// <summary>The floor texture. The dollhouse miniature assigns this same object — no copy.</summary>
        public Texture Texture => _texture;

        /// <summary>Edge length of the floor texture, in texels.</summary>
        public int Resolution => _resolution;

        /// <summary>The world rectangle the texture covers.</summary>
        public Bounds Bounds => _bounds;

        /// <summary>
        /// Drops every photograph and settles the revealed texels back to the flat clean tint, so
        /// turning the layer off part-way through leaves the floor as it would have looked if the
        /// layer had never run. Without this the switch would only stop new photographs, and the
        /// floor would keep showing the old ones — a switch that does not do what it says.
        /// </summary>
        public void ClearPhotoLayer()
        {
            if (_photo == null) return;

            Array.Clear(_photo, 0, _photo.Length);
            RecomputePhotoLayer();
        }

        public void Build(Bounds bounds, Renderer floorRenderer, int resolution = 64)
        {
            _bounds = bounds;
            _resolution = Mathf.Clamp(resolution, 8, 256);

            _texture = new Texture2D(_resolution, _resolution, TextureFormat.RGBA32, false)
            {
                name = "CoverageFloor",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            _pixels = new Color32[_resolution * _resolution];
            _display = new Color32[_pixels.Length];
            _flash = new float[_pixels.Length];
            _photo = new Color32[_pixels.Length];
            _revealed = new bool[_pixels.Length];
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = Uncleaned;
            Array.Copy(_pixels, _display, _pixels.Length);
            _texture.SetPixels32(_display);
            _texture.Apply(false);
            _photoDirty = false;

            if (floorRenderer == null) return;

            var shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = "CoverageFloorMat", mainTexture = _texture };
            floorRenderer.sharedMaterial = material;
        }

        public void Mark(Vector3 worldPosition)
        {
            if (_texture == null) return;

            float u = Mathf.InverseLerp(_bounds.min.x, _bounds.max.x, worldPosition.x);
            float v = Mathf.InverseLerp(_bounds.min.z, _bounds.max.z, worldPosition.z);
            if (u < 0f || u > 1f || v < 0f || v > 1f) return;

            int cx = Mathf.Clamp((int)(u * (_resolution - 1)), 0, _resolution - 1);
            int cy = Mathf.Clamp((int)(v * (_resolution - 1)), 0, _resolution - 1);

            bool paintedNew = false;

            for (int y = cy - BrushRadius; y <= cy + BrushRadius; y++)
            {
                if (y < 0 || y >= _resolution) continue;
                for (int x = cx - BrushRadius; x <= cx + BrushRadius; x++)
                {
                    if (x < 0 || x >= _resolution) continue;

                    int i = y * _resolution + x;

                    // Only genuinely new texels flash. Re-flashing already-clean ones would keep
                    // the whole brush hot and the texture re-uploading whenever the tool stands
                    // still — exactly the per-frame cost we want to avoid on Quest. The reveal flag
                    // is the guard rather than the colour, because a revealed texel no longer holds
                    // the plain clean tint once a photograph lands on it.
                    if (_revealed[i]) continue;

                    _revealed[i] = true;
                    _pixels[i] = Cleaned;
                    _flash[i] = 1f;
                    paintedNew = true;
                }
            }

            if (!paintedNew) return;

            // The reveal just grew, so any newly cleaned texel with no photograph of its own needs
            // its neighbour fill recomputed.
            _photoDirty = true;
            _flashing = true;
            _dirty = true;
        }

        /// <summary>Texel column for a world X, in the texture's 0..resolution-1 space.</summary>
        public float TexelX(float worldX)
            => Mathf.InverseLerp(_bounds.min.x, _bounds.max.x, worldX) * (_resolution - 1);

        /// <summary>Texel row for a world Z, in the texture's 0..resolution-1 space.</summary>
        public float TexelY(float worldZ)
            => Mathf.InverseLerp(_bounds.min.z, _bounds.max.z, worldZ) * (_resolution - 1);

        /// <summary>World centre of a texel — the same point <c>Mark</c> would light up for it.</summary>
        public Vector3 TexelCentreWorld(int x, int y)
        {
            float u = _resolution <= 1 ? 0f : (float)x / (_resolution - 1);
            float v = _resolution <= 1 ? 0f : (float)y / (_resolution - 1);

            return new Vector3(
                Mathf.Lerp(_bounds.min.x, _bounds.max.x, u),
                _bounds.min.y,
                Mathf.Lerp(_bounds.min.z, _bounds.max.z, v));
        }

        /// <summary>
        /// Records what the camera saw at a texel. Written whether or not the texel is cleaned yet:
        /// the photograph is taken when the camera can see the spot, and the reveal decides when it
        /// becomes visible, so cleaning ground the camera already passed over is instant.
        /// </summary>
        public void SetPhoto(int x, int y, Color32 colour)
        {
            if (_photo == null) return;
            if (x < 0 || y < 0 || x >= _resolution || y >= _resolution) return;

            colour.a = 255;
            _photo[y * _resolution + x] = colour;
            _photoDirty = true;
        }

        /// <summary>Whether a texel has been cleaned. Exposed so the bake and tests can read the reveal.</summary>
        public bool IsRevealed(int x, int y)
            => _revealed != null && x >= 0 && y >= 0 && x < _resolution && y < _resolution &&
               _revealed[y * _resolution + x];

        /// <summary>The photograph at a texel, or alpha 0 if there is none yet.</summary>
        public Color32 PhotoAt(int x, int y)
            => _photo != null && x >= 0 && y >= 0 && x < _resolution && y < _resolution
                ? _photo[y * _resolution + x]
                : default;

        /// <summary>
        /// The colour the texel settles to once it stops flashing: its photograph, the neighbour
        /// fill, the clean tint, or the uncleaned base. Exposed for diagnostics — the bake has to be
        /// checkable without waiting for a frame to be drawn.
        /// </summary>
        public Color32 SettledAt(int x, int y)
            => _pixels != null && x >= 0 && y >= 0 && x < _resolution && y < _resolution
                ? _pixels[y * _resolution + x]
                : default;

        /// <summary>
        /// Rebuilds the settled colour of every revealed texel from the photo layer: the photograph
        /// where there is one, the blurred average of the photographed texels around it where there
        /// is not. Batched — call it after a run of <see cref="SetPhoto"/>, not per texel.
        /// </summary>
        public void RecomputePhotoLayer()
        {
            if (_pixels == null) return;

            for (int i = 0; i < _pixels.Length; i++)
            {
                if (!_revealed[i]) continue;

                var photo = _photo[i];
                _pixels[i] = photo.a > 0
                    ? new Color32(photo.r, photo.g, photo.b, 255)
                    : NeighbourFill(i);
            }

            _dirty = true;
        }

        /// <summary>
        /// Average of the photographed texels within the fill radius, or the plain clean tint when
        /// there is genuinely nothing nearby to borrow from.
        /// </summary>
        private Color32 NeighbourFill(int index)
        {
            int radius = Mathf.Clamp(PhotogrammetrySettings.FillRadiusTexels, 0, 8);
            if (radius == 0) return Cleaned;

            int y0 = index / _resolution;
            int x0 = index % _resolution;
            int r = 0, g = 0, b = 0, n = 0;

            for (int dy = -radius; dy <= radius; dy++)
            {
                int y = y0 + dy;
                if (y < 0 || y >= _resolution) continue;

                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = x0 + dx;
                    if (x < 0 || x >= _resolution) continue;

                    var photo = _photo[y * _resolution + x];
                    if (photo.a == 0) continue;

                    r += photo.r;
                    g += photo.g;
                    b += photo.b;
                    n++;
                }
            }

            return n == 0 ? Cleaned : new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
        }

        private void LateUpdate()
        {
            if (_texture == null) return;

            // The photo pass runs before the flash so the sparkle sits on top of the settled colour
            // rather than being overwritten by it on the same frame.
            if (_photoDirty)
            {
                _photoDirty = false;
                RecomputePhotoLayer();
            }

            if (_flashing)
            {
                float decay = Time.deltaTime * FlashDecayPerSecond;
                bool stillFlashing = false;

                for (int i = 0; i < _pixels.Length; i++)
                {
                    float f = _flash[i];
                    if (f <= 0f) continue;

                    f = Mathf.Max(0f, f - decay);
                    _flash[i] = f;
                    if (f > 0f) stillFlashing = true;

                    _display[i] = f > 0f
                        ? (Color32)Color32.Lerp(_pixels[i], FlashTint, f)
                        : _pixels[i];
                }

                _flashing = stillFlashing;
                _dirty = true;
            }

            if (!_dirty) return;
            _dirty = false;
            _texture.SetPixels32(_display);
            _texture.Apply(false);
        }
    }
}

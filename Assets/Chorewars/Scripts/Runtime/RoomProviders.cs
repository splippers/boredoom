using UnityEngine;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Supplies the horizontal (XZ) play area used for coverage scoring.
    /// Two implementations: a fixed fake room (editor / no spatial data) and one derived
    /// from captured XR meshes (Quest), which falls back to a fixed box until meshes arrive.
    /// </summary>
    public interface IRoomProvider
    {
        bool IsReady { get; }

        /// <summary>World-space bounds of the play area. Y is not used by coverage.</summary>
        Bounds RoomBounds { get; }

        /// <summary>Human-readable source, for logs and the placeholder HUD.</summary>
        string Source { get; }
    }

    /// <summary>Fixed rectangular room. Always ready. Defaults to 4x4 m centred on the origin.</summary>
    public sealed class FakeRoomProvider : MonoBehaviour, IRoomProvider
    {
        [SerializeField] private Vector2 sizeMeters = new Vector2(4f, 4f);
        [SerializeField] private Vector2 centreXZ = Vector2.zero;
        [SerializeField] private float heightMeters = 2.6f;
        [SerializeField] private float floorY;

        public bool IsReady => true;
        public string Source => "FakeRoom";

        public Bounds RoomBounds
        {
            get
            {
                var size = new Vector3(
                    Mathf.Max(0.1f, sizeMeters.x),
                    Mathf.Max(0.1f, heightMeters),
                    Mathf.Max(0.1f, sizeMeters.y));
                var centre = new Vector3(centreXZ.x, floorY + heightMeters * 0.5f, centreXZ.y);
                return new Bounds(centre, size);
            }
        }

        public void Configure(Vector2 size, Vector2 centre, float floorHeight)
        {
            sizeMeters = size;
            centreXZ = centre;
            floorY = floorHeight;
        }
    }

    /// <summary>
    /// Derives the play area from captured XR meshes under a parent transform.
    /// Not ready until at least one mesh exists; reports a fixed fallback box until then.
    /// </summary>
    public sealed class SpatialRoomProvider : MonoBehaviour, IRoomProvider
    {
        private const float RefreshIntervalSeconds = 0.5f;

        [SerializeField] private Transform meshParent;
        [SerializeField] private Vector2 fallbackSizeMeters = new Vector2(4f, 4f);
        [SerializeField] private float fallbackHeightMeters = 2.6f;

        private Bounds _bounds;
        private bool _ready;
        private float _nextRefreshAt;

        public bool IsReady => _ready;
        public string Source => _ready ? "SpatialMesh" : "SpatialMesh(fallback)";
        public Bounds RoomBounds => _ready ? _bounds : FallbackBounds();

        public void Configure(Transform parent) => meshParent = parent;

        private Bounds FallbackBounds()
        {
            var size = new Vector3(
                Mathf.Max(0.1f, fallbackSizeMeters.x),
                Mathf.Max(0.1f, fallbackHeightMeters),
                Mathf.Max(0.1f, fallbackSizeMeters.y));
            return new Bounds(new Vector3(0f, fallbackHeightMeters * 0.5f, 0f), size);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshAt) return;
            _nextRefreshAt = Time.unscaledTime + RefreshIntervalSeconds;
            Refresh();
        }

        public void Refresh()
        {
            if (meshParent == null)
            {
                _ready = false;
                return;
            }

            bool any = false;
            var bounds = new Bounds();
            foreach (var renderer in meshParent.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null) continue;
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            _ready = any;
            if (any) _bounds = bounds;
        }
    }
}

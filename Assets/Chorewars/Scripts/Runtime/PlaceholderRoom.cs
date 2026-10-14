using UnityEngine;
using UnityEngine.Rendering;

namespace Chorewars.Runtime
{
    /// <summary>
    /// Creates simple placeholder geometry for a room: an opaque floor (replaced by the coverage
    /// visualiser) and translucent walls, so AR passthrough stays readable rather than being
    /// blocked by a solid box.
    /// </summary>
    public sealed class PlaceholderRoom : MonoBehaviour
    {
        [SerializeField] private float wallHeightMeters = 2.4f;
        [SerializeField] private float wallAlpha = 0.10f;

        private Transform _root;

        /// <summary>Renderer of the floor, so the coverage visualiser can swap in its texture.</summary>
        public Renderer FloorRenderer { get; private set; }

        public void Build(Bounds bounds)
        {
            if (_root != null) Destroy(_root.gameObject);

            _root = new GameObject("PlaceholderRoomGeometry").transform;
            _root.SetParent(transform, false);

            float width = Mathf.Max(0.2f, bounds.size.x);
            float depth = Mathf.Max(0.2f, bounds.size.z);
            float floorY = bounds.min.y;
            var centre = bounds.center;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(_root, false);
            floor.transform.position = new Vector3(centre.x, floorY - 0.01f, centre.z);
            floor.transform.localScale = new Vector3(width, 0.02f, depth);
            StripCollider(floor);
            FloorRenderer = floor.GetComponent<Renderer>();
            if (FloorRenderer != null)
                FloorRenderer.sharedMaterial = PlaceholderMaterials.CreateOpaque(new Color(0.06f, 0.07f, 0.09f));

            var wallMaterial = PlaceholderMaterials.CreateTransparent(new Color(0.25f, 0.75f, 1f), wallAlpha);
            float height = wallHeightMeters;
            float wallY = floorY + height * 0.5f;

            MakeWall(_root, new Vector3(centre.x - width * 0.5f, wallY, centre.z), new Vector3(0.02f, height, depth), wallMaterial);
            MakeWall(_root, new Vector3(centre.x + width * 0.5f, wallY, centre.z), new Vector3(0.02f, height, depth), wallMaterial);
            MakeWall(_root, new Vector3(centre.x, wallY, centre.z - depth * 0.5f), new Vector3(width, height, 0.02f), wallMaterial);
            MakeWall(_root, new Vector3(centre.x, wallY, centre.z + depth * 0.5f), new Vector3(width, height, 0.02f), wallMaterial);
        }

        private static void MakeWall(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            StripCollider(wall);
            var renderer = wall.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private static void StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
    }

    /// <summary>Runtime-only placeholder materials. Built-in Render Pipeline shaders only.</summary>
    internal static class PlaceholderMaterials
    {
        public static Material CreateOpaque(Color colour)
        {
            var shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "PlaceholderOpaque" };
            if (material.HasProperty("_Color")) material.color = colour;
            return material;
        }

        public static Material CreateTransparent(Color colour, float alpha)
        {
            colour.a = Mathf.Clamp01(alpha);

            var standard = Shader.Find("Standard");
            var shader = standard != null ? standard : Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = "PlaceholderTransparent" };
            if (material.HasProperty("_Color")) material.color = colour;

            if (standard != null && material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f); // Fade
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            return material;
        }
    }
}

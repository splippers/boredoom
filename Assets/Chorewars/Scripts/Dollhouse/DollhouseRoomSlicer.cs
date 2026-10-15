using System.Collections.Generic;
using UnityEngine;
using Chorewars.Dollhouse;

#if UNITY_ANDROID || UNITY_EDITOR
using Unity.XR.CoreUtils;
#endif

namespace Chorewars.Dollhouse
{
    /// <summary>
    /// Bridges Meta Quest OVRSceneRoom data into DollhouseManager.
    /// On Start, enumerates all OVRSceneRoom objects in the scene,
    /// creates a miniature room quad in the dollhouse, and registers each with DollhouseManager.
    ///
    /// Requires OVRSceneManager with scene loaded before Start fires.
    /// Usage: Add to the same GameObject as DollhouseManager (or assign via field).
    /// </summary>
    public class DollhouseRoomSlicer : MonoBehaviour
    {
        [SerializeField] private DollhouseManager dollhouseManager;
        [SerializeField] private Material dollhouseRoomMaterial;  // base material for room floor quads
        [SerializeField] private float dollhouseWallHeight = 0.04f; // wall height in dollhouse scale

        // Maps OVRSceneRoom anchor UUID → dollhouse floor GameObject
        private readonly Dictionary<string, GameObject> _roomFloors = new();

        private void Start()
        {
            if (dollhouseManager == null)
                dollhouseManager = GetComponent<DollhouseManager>();

#if CHOREWARS_META_XR
            var rooms = FindObjectsOfType<OVRSceneRoom>();
            foreach (var room in rooms)
                RegisterRoom(room);
#elif UNITY_EDITOR
            // Editor: create a placeholder 5x5m room for testing
            CreateEditorPlaceholder();
#endif
        }

        private void RegisterRoom(
#if CHOREWARS_META_XR
            OVRSceneRoom room
#else
            object room
#endif
        )
        {
#if CHOREWARS_META_XR
            string roomId = room.Space.Uuid.ToString();

            // Floor plane — use the room's floor anchor
            var floorAnchor = room.Floor;
            if (floorAnchor == null) return;

            var floorBounds = floorAnchor.GetComponent<OVRScenePlane>()?.Dimensions ?? Vector2.one * 3f;

            var floorObj = CreateRoomFloor(roomId, floorBounds,
                floorAnchor.transform.position, floorAnchor.transform.rotation);

            _roomFloors[roomId] = floorObj;
            dollhouseManager?.RegisterRoom(roomId, floorObj.GetComponentsInChildren<Renderer>());
#endif
        }

        private GameObject CreateRoomFloor(string roomId, Vector2 realDimensions,
                                            Vector3 realWorldPos, Quaternion realWorldRot)
        {
            // Convert real world extent to dollhouse space (1:20 scale)
            float scale = 0.05f;
            var floorObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floorObj.name = $"DollhouseRoom_{roomId}";

            var ds = dollhouseManager.WorldToDollhouse(realWorldPos);
            floorObj.transform.position = new Vector3(ds.x, 0f, ds.z);
            floorObj.transform.rotation = Quaternion.Euler(90f, realWorldRot.eulerAngles.y, 0f);
            floorObj.transform.localScale = new Vector3(
                realDimensions.x * scale,
                realDimensions.y * scale,
                1f
            );

            if (dollhouseRoomMaterial != null)
                floorObj.GetComponent<Renderer>().material = Instantiate(dollhouseRoomMaterial);

            return floorObj;
        }

        private void CreateEditorPlaceholder()
        {
            const string id = "editor-room-0";
            var floorObj = CreateRoomFloor(id, new Vector2(5f, 5f), Vector3.zero, Quaternion.identity);
            _roomFloors[id] = floorObj;
            dollhouseManager?.RegisterRoom(id, floorObj.GetComponentsInChildren<Renderer>());
        }

        /// <summary>
        /// Call this when a chore session ends to update the dollhouse room's clean state.
        /// </summary>
        public void ApplyCoverageToRoom(string roomId, float coveragePct)
        {
            dollhouseManager?.OnCoverageUpdate(roomId, coveragePct);
        }
    }
}

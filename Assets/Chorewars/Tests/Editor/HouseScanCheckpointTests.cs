using Chorewars.AR;
using Chorewars.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    public class HouseScanCheckpointTests
    {
        [Test]
        public void EmptySceneNeverReportsMeasuredMesh()
        {
            Assert.AreEqual(HouseScanController.Readiness.WaitingForGeometry,
                HouseScanController.Evaluate(true, true, true, 0, 0, 0));
        }

        [Test]
        public void ProviderMeshWithoutTrianglesIsNotMeasuredGeometry()
        {
            Assert.AreEqual(HouseScanController.Readiness.WaitingForGeometry,
                HouseScanController.Evaluate(true, true, true, 0, 1, 0));
        }

        [Test]
        public void SurfacesAreReportedSeparatelyFromTriangleMesh()
        {
            Assert.AreEqual(HouseScanController.Readiness.SurfacesOnly,
                HouseScanController.Evaluate(true, true, true, 12, 0, 0));
            Assert.AreEqual(HouseScanController.Readiness.MeshAvailable,
                HouseScanController.Evaluate(true, true, true, 12, 2, 1200));
        }

        [Test]
        public void PermissionAndSessionGateEvenPreviouslyReceivedMeshes()
        {
            Assert.AreEqual(HouseScanController.Readiness.PermissionNeeded,
                HouseScanController.Evaluate(true, false, true, 12, 2, 1200));
            Assert.AreEqual(HouseScanController.Readiness.SessionUnavailable,
                HouseScanController.Evaluate(true, true, false, 12, 2, 1200));
        }

        [Test]
        public void EditorSimulationIsNeverClaimedAsQuestEvidence()
        {
            Assert.AreEqual(HouseScanController.Readiness.EditorOnly,
                HouseScanController.Evaluate(false, true, true, 12, 2, 1200));
        }

        [Test]
        public void FallbackRoomsCannotCompleteAMeasuredScan()
        {
            Assert.IsFalse(ScanFlowController.HasMeasuredRooms(null));
            Assert.IsFalse(ScanFlowController.HasMeasuredRooms(new RoomScan[0]));
            var fallback = new RoomScan
            {
                roomId = "Play Space", assumed = true, boundsXZ = new Rect(-2f, -2f, 4f, 4f)
            };
            Assert.IsFalse(ScanFlowController.HasMeasuredRooms(new[] { fallback }));
            fallback.assumed = false;
            Assert.IsTrue(ScanFlowController.HasMeasuredRooms(new[] { fallback }));
            fallback.boundsXZ = new Rect();
            Assert.IsFalse(ScanFlowController.HasMeasuredRooms(new[] { fallback }));
        }

        [Test]
        public void RetryDoesNotClearTheSavedHouseOrRequireCameraPermission()
        {
            var go = new GameObject("HouseScanTest");
            try
            {
                var controller = go.AddComponent<HouseScanController>();
                controller.Configure(null, null, null);
                controller.RetryRead();
                Assert.AreEqual(0, controller.MeshCount);
                Assert.IsFalse(controller.CaptureBusy);
                CollectionAssert.DoesNotContain(Chorewars.Diagnostics.AndroidPermissions.Wanted,
                    Chorewars.Diagnostics.AndroidPermissions.HeadsetCamera);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PanelIsInitiallyCompactAndAllControlsHaveValidLayout()
        {
            var go = new GameObject("ScanPanelTest");
            var camGo = new GameObject("ScanPanelCamera");
            var previousEvents = UnityEngine.EventSystems.EventSystem.current;
            GameObject testEvents = null;
            try
            {
                if (previousEvents == null)
                {
                    testEvents = new GameObject("ScanPanelTestEvents", typeof(UnityEngine.EventSystems.EventSystem));
                    testEvents.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                }
                var camera = camGo.AddComponent<Camera>();
                var controller = go.AddComponent<HouseScanController>();
                controller.Configure(null, null, null);
                var panel = go.AddComponent<Chorewars.UI.HouseScanPanel>();
                panel.Configure(controller, camera);
                Assert.IsFalse(panel.IsExpanded);
                panel.Show();
                Assert.IsTrue(panel.IsExpanded);
                var canvas = go.GetComponentInChildren<Canvas>();
                Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode);
                Assert.AreSame(camera, canvas.worldCamera);
                Assert.IsNotNull(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>());
                Assert.IsNotNull(previousEvents != null ? previousEvents : testEvents.GetComponent<UnityEngine.EventSystems.EventSystem>());
                foreach (var button in go.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    var rect = button.GetComponent<RectTransform>();
                    Assert.Greater(rect.rect.width, 0f);
                    Assert.Greater(rect.rect.height, 0f);
                    Assert.AreEqual(rect.anchorMin, rect.anchorMax);
                    Assert.IsTrue(button.targetGraphic.raycastTarget);
                }
                panel.Hide();
                Assert.IsFalse(panel.IsExpanded);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(camGo);
                if (testEvents != null) Object.DestroyImmediate(testEvents);
            }
        }
    }
}

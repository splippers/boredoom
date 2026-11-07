using Chorewars.Dollhouse;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Chorewars.Tests
{
    public class PalmMiniatureGestureTests
    {
        private static void Sample(PalmMiniatureGestures state, bool leftPinch, bool rightPinch, Vector3 left, Vector3 right, float normal = 1f, float dt = 0.3f)
            => state.Update(true, normal, Vector3.zero, leftPinch, rightPinch, left, right, Vector3.forward, Vector3.right, dt);

        [Test]
        public void PinchesDoNotRevealAHiddenHouseWithoutAnUpturnedPalm()
        {
            var state = new PalmMiniatureGestures();
            Sample(state, true, true, Vector3.zero, Vector3.right, -1f, 1f);
            Assert.IsFalse(state.Visible);
        }
        [Test]
        public void PalmMustRemainUpLongEnoughBeforeRevealing()
        {
            var state = new PalmMiniatureGestures();
            Sample(state, false, false, Vector3.zero, Vector3.right, 1f, 0.1f);
            Assert.IsFalse(state.Visible);
            Sample(state, false, false, Vector3.zero, Vector3.right, 1f, 0.2f);
            Assert.IsTrue(state.Visible);
        }
        [Test]
        public void RightPinchPullTowardTheBodyZoomsAndKeepsScaleAfterRelease()
        {
            var state = new PalmMiniatureGestures();
            Sample(state, false, false, Vector3.zero, Vector3.forward * 0.5f);
            Sample(state, false, true, Vector3.zero, Vector3.forward * 0.5f);
            Sample(state, false, true, Vector3.zero, Vector3.forward * 0.3f);
            float zoom = state.Zoom;
            Assert.Greater(zoom, 1f);
            Sample(state, false, false, Vector3.zero, Vector3.forward * 0.3f);
            Assert.AreEqual(zoom, state.Zoom, 0.0001f);
        }
        [Test]
        public void SidewaysPinchDragRotatesWithoutResettingOnRelease()
        {
            var state = new PalmMiniatureGestures();
            Sample(state, false, false, Vector3.zero, Vector3.forward * 0.5f);
            Sample(state, false, true, Vector3.zero, Vector3.forward * 0.5f);
            Sample(state, false, true, Vector3.zero, new Vector3(0.1f, 0f, 0.5f));
            Assert.AreEqual(24f, state.Yaw, 0.001f);
            Sample(state, false, false, Vector3.zero, Vector3.zero);
            Assert.AreEqual(24f, state.Yaw, 0.001f);
        }
        [Test]
        public void TwoHandStretchZoomsAndTurningTheAxisRotates()
        {
            var state = new PalmMiniatureGestures();
            Sample(state, false, false, Vector3.zero, Vector3.right * 0.2f);
            Sample(state, true, true, Vector3.zero, Vector3.right * 0.2f);
            Sample(state, true, true, Vector3.zero, Vector3.forward * 0.4f);
            Assert.AreEqual(2f, state.Zoom, 0.001f);
            Assert.AreEqual(90f, Mathf.Abs(state.Yaw), 0.001f);
        }
        [Test]
        public void TrackingLossHidesWithoutDestroyingTheChosenView()
        {
            var state = new PalmMiniatureGestures();
            Sample(state, false, false, Vector3.zero, Vector3.right);
            state.LoseTracking(0.6f);
            Assert.IsFalse(state.Visible);
        }
        [Test]
        public void ZoomIsBoundedAndResetRestoresViewWithoutClosingAnOpenPalm()
        {
            var state = new PalmMiniatureGestures();
            Sample(state, false, false, Vector3.zero, Vector3.forward);
            Sample(state, false, true, Vector3.zero, Vector3.forward);
            Sample(state, false, true, Vector3.zero, -Vector3.forward * 10f);
            Assert.AreEqual(2.5f, state.Zoom);
            state.ResetView();
            Assert.AreEqual(1f, state.Zoom);
            Assert.AreEqual(0f, state.Yaw);
            Assert.IsTrue(state.Visible);
        }
        [Test]
        public void LocalHandPoseUsesTheRealTrackingOriginTransform()
        {
            var go = new GameObject("HandSpaceTest");
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(10f, 0f, -4f), Quaternion.Euler(0f, 90f, 0f));
                var pose = PalmMiniatureController.ToWorldPose(go.transform, new Pose(new Vector3(1f, 2f, 3f), Quaternion.identity));
                Assert.Less(Vector3.Distance(new Vector3(13f, 2f, -5f), pose.position), 0.0001f);
                Assert.Less(Quaternion.Angle(go.transform.rotation, pose.rotation), 0.0001f);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void SelectedIntroTrackPreloadsAndRemainsStereo()
        {
            const string path = "Assets/Chorewars/ImportedMedia/June/named/audio/BoreDOOM_CombatTheme.wav";
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            Assert.IsTrue(importer.defaultSampleSettings.preloadAudioData);
            Assert.AreEqual(2, AssetDatabase.LoadAssetAtPath<AudioClip>(path).channels);
        }
    }
}

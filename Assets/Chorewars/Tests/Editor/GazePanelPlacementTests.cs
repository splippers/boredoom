using Chorewars.HandsFree;
using NUnit.Framework;
using UnityEngine;

namespace Chorewars.Tests
{
    public class GazePanelPlacementTests
    {
        GameObject _head, _panel;

        [SetUp]
        public void SetUp()
        {
            _head = new GameObject("head");
            _head.transform.position = new Vector3(0f, 1.6f, 0f);
            _head.transform.rotation = Quaternion.identity; // facing +Z
            _panel = new GameObject("panel");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_head);
            Object.DestroyImmediate(_panel);
        }

        [Test]
        public void WithNoOffsetThePanelIsStraightAhead()
        {
            new GazePanelPlacement().Hold(_panel.transform, _head.transform, 1.4f, 0f);
            var toPanel = _panel.transform.position - _head.transform.position;
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, new Vector3(toPanel.x, 0f, toPanel.z)), 0.01f);
            Assert.AreEqual(1.4f, new Vector2(toPanel.x, toPanel.z).magnitude, 0.001f);
        }

        [Test]
        public void APositiveOffsetPutsItToTheRightAtTheSameDistance()
        {
            new GazePanelPlacement().Hold(_panel.transform, _head.transform, 1.4f, 0f, float.NegativeInfinity, 0f, 40f);
            var toPanel = _panel.transform.position - _head.transform.position;
            Assert.Greater(toPanel.x, 0f, "right of the player is +x when facing +z");
            Assert.AreEqual(40f, Vector3.Angle(Vector3.forward, new Vector3(toPanel.x, 0f, toPanel.z)), 0.01f);
            Assert.AreEqual(1.4f, new Vector2(toPanel.x, toPanel.z).magnitude, 0.001f);
        }

        [Test]
        public void ANegativeOffsetPutsItToTheLeft()
        {
            new GazePanelPlacement().Hold(_panel.transform, _head.transform, 1.4f, 0f, float.NegativeInfinity, 0f, -40f);
            Assert.Less(_panel.transform.position.x, 0f);
        }

        [Test]
        public void ThePanelStillFacesThePlayerWhenOffsetToTheSide()
        {
            new GazePanelPlacement().Hold(_panel.transform, _head.transform, 1.4f, 0f, float.NegativeInfinity, 0f, 40f);
            // A panel reads correctly when its +z points away from the viewer.
            var away = _panel.transform.position - _head.transform.position;
            away.y = 0f;
            Assert.AreEqual(0f, Vector3.Angle(_panel.transform.forward, away), 0.1f);
        }

        [Test]
        public void AnUnanchoredPanelIsBroughtBackWhenThePlayerWalksAway()
        {
            var placement = new GazePanelPlacement();
            placement.Hold(_panel.transform, _head.transform, 1.4f, 0f);
            var first = _panel.transform.position;

            _head.transform.position += new Vector3(0f, 0f, -5f); // walked 5 m away
            placement.Hold(_panel.transform, _head.transform, 1.4f, 0f);
            Assert.AreNotEqual(first, _panel.transform.position, "followed the player");
        }

        [Test]
        public void AnAnchoredPanelStaysPutHoweverFarThePlayerWalksOrTurns()
        {
            var placement = new GazePanelPlacement { Anchored = true };
            placement.Hold(_panel.transform, _head.transform, 1.4f, 0f);
            var first = _panel.transform.position;

            _head.transform.position += new Vector3(0f, 0f, -8f);
            _head.transform.rotation = Quaternion.Euler(0f, 170f, 0f);
            placement.Hold(_panel.transform, _head.transform, 1.4f, 0f);
            Assert.AreEqual(first, _panel.transform.position);
        }

        [Test]
        public void RecallBringsAnAnchoredPanelBackInFrontOfThePlayer()
        {
            var placement = new GazePanelPlacement { Anchored = true };
            placement.Hold(_panel.transform, _head.transform, 1.4f, 0f);
            var first = _panel.transform.position;

            _head.transform.position += new Vector3(0f, 0f, -8f);
            WorldAnchoredPanels.Recall();
            placement.Hold(_panel.transform, _head.transform, 1.4f, 0f);

            var toPanel = _panel.transform.position - _head.transform.position;
            Assert.AreNotEqual(first, _panel.transform.position);
            Assert.AreEqual(1.4f, new Vector2(toPanel.x, toPanel.z).magnitude, 0.001f, "in front of where the player now is");
        }

        [Test]
        public void ARecallIsOnlyActedOnOnce()
        {
            var placement = new GazePanelPlacement { Anchored = true };
            placement.Hold(_panel.transform, _head.transform, 1.4f, 0f);
            WorldAnchoredPanels.Recall();
            placement.Hold(_panel.transform, _head.transform, 1.4f, 0f);
            var placed = _panel.transform.position;

            _head.transform.position += new Vector3(0f, 0f, -8f);
            placement.Hold(_panel.transform, _head.transform, 1.4f, 0f);
            Assert.AreEqual(placed, _panel.transform.position, "no second recall, so it stays where it was put");
        }

        [Test]
        public void ItHoldsStillWhileThePlayerLooksAround()
        {
            var placement = new GazePanelPlacement();
            placement.Hold(_panel.transform, _head.transform, 1.4f, 0f, float.NegativeInfinity, 0f, 40f);
            var first = _panel.transform.position;

            _head.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
            placement.Hold(_panel.transform, _head.transform, 1.4f, 0f, float.NegativeInfinity, 0f, 40f);
            Assert.AreEqual(first, _panel.transform.position, "a small turn of the head must not drag the panel with it");
        }
    }
}

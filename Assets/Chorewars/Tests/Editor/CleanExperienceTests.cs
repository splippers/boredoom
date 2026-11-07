using Chorewars.Core;
using Chorewars.Dollhouse;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Chorewars.Tests
{
    public class CleanExperienceTests
    {
        [Test]
        public void OnlyCleanScenesSuppressLegacyStartup()
        {
            Assert.IsTrue(CleanExperience.IsCleanScene("Intro"));
            Assert.IsTrue(CleanExperience.IsCleanScene("DollhouseSetup"));
            Assert.IsFalse(CleanExperience.IsCleanScene("HooverMode"));
            Assert.IsFalse(CleanExperience.IsCleanScene("Bootstrap"));
            Assert.IsFalse(CleanExperience.IsCleanScene(null));
        }
        [Test]
        public void IntroReferencesTheChosenImportedMedia()
        {
            var dependencies = AssetDatabase.GetDependencies(CleanExperience.IntroPath);
            CollectionAssert.Contains(dependencies, "Assets/Chorewars/ImportedMedia/June/named/images/BoreDOOM_Logo.png");
            CollectionAssert.Contains(dependencies, "Assets/Chorewars/ImportedMedia/June/named/audio/BoreDOOM_CombatTheme.wav");
        }
        [Test]
        public void StackedFloorsRemainSeparateLevels()
        {
            var levels = MetaRoomMiniature.GroupLevels(new[] { 3.1f, 0.05f, 3f, 0f, 0.02f, 3.08f }, 0.5f);
            Assert.AreEqual(2, levels.Count);
            Assert.AreEqual(0f, levels[0]);
            Assert.AreEqual(3f, levels[1]);
        }
        [Test]
        public void SourcePolygonsAreCopiedAndNotChangedByPresentation()
        {
            var points = new[] { Vector3.zero, Vector3.right, Vector3.forward };
            var surface = new MetaRoomMiniature.Surface("floor-source", true, points);
            points[0] = Vector3.up * 10f;
            Assert.AreEqual(Vector3.zero, surface.WorldCorners[0]);
            Assert.AreEqual("floor-source", surface.Id);
        }
        [Test]
        public void ConcaveFloorTriangulationPreservesArea()
        {
            var p = new[] { new Vector3(0,0,0), new Vector3(2,0,0), new Vector3(2,0,1), new Vector3(1,0,1), new Vector3(1,0,2), new Vector3(0,0,2) };
            var indices = MetaRoomMiniature.Triangulate(p);
            Assert.AreEqual(12, indices.Length);
            float area = 0f;
            for (int i = 0; i < indices.Length; i += 3)
                area += Vector3.Cross(p[indices[i+1]] - p[indices[i]], p[indices[i+2]] - p[indices[i]]).magnitude * 0.5f;
            Assert.AreEqual(3f, area, 0.0001f);
        }
        [Test]
        public void VerticalWallIsTriangulatedInItsOwnPlane()
        {
            var wall = new[] { Vector3.zero, new Vector3(3,0,0), new Vector3(3,2.5f,0), new Vector3(0,2.5f,0) };
            Assert.AreEqual(6, MetaRoomMiniature.Triangulate(wall).Length);
            Assert.AreEqual(0, MetaRoomMiniature.Triangulate(new[] { Vector3.zero, Vector3.right, Vector3.right * 2f }).Length);
        }
        [Test]
        public void SetupReferencesExplicitStereoMaterials()
        {
            var dependencies = AssetDatabase.GetDependencies(CleanExperience.SetupPath);
            CollectionAssert.Contains(dependencies, "Assets/Chorewars/Materials/CleanMiniatureFloor.mat");
            CollectionAssert.Contains(dependencies, "Assets/Chorewars/Materials/CleanMiniatureWall.mat");
            CollectionAssert.Contains(dependencies, "Assets/Chorewars/Shaders/CleanMiniature.shader");
        }
    }
}

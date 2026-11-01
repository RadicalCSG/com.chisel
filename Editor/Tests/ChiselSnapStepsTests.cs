using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Chisel snaps at the steps set in Unity's snap settings
    public sealed class ChiselSnapStepsTests
    {
        Vector3 m_GridSize;
        float   m_Rotate;
        float   m_Scale;

        [SetUp]
        public void SetUp()
        {
            m_GridSize = EditorSnapSettings.gridSize;
            m_Rotate   = EditorSnapSettings.rotate;
            m_Scale    = EditorSnapSettings.scale;
        }

        [TearDown]
        public void TearDown()
        {
            EditorSnapSettings.gridSize = m_GridSize;
            EditorSnapSettings.rotate   = m_Rotate;
            EditorSnapSettings.scale    = m_Scale;
        }

        [Test]
        public void UnitysSteps_AreTheStepsChiselSnapsAt()
        {
            EditorSnapSettings.gridSize = new Vector3(0.25f, 0.5f, 0.125f);
            EditorSnapSettings.rotate   = 7.0f;
            EditorSnapSettings.scale    = 0.3f;

            Assert.That(Grid.DefaultGrid.Spacing, Is.EqualTo(new Vector3(0.25f, 0.5f, 0.125f)));
            Assert.That(Snapping.MoveSnappingSteps, Is.EqualTo(new Vector3(0.25f, 0.5f, 0.125f)));
            Assert.That(ChiselEditorSettings.RotateSnap, Is.EqualTo(7.0f));
            Assert.That(Snapping.RotateSnappingStep, Is.EqualTo(7.0f));
            Assert.That(ChiselEditorSettings.ScaleSnap, Is.EqualTo(0.3f));
            Assert.That(Snapping.ScaleSnappingStep, Is.EqualTo(0.3f));
        }

        [Test]
        public void ChangingChiselsGridSpacing_ChangesUnitysGridSize()
        {
            Grid.DefaultGrid.Spacing = new Vector3(2, 4, 8);

            Assert.That(EditorSnapSettings.gridSize, Is.EqualTo(new Vector3(2, 4, 8)));
        }
    }
}

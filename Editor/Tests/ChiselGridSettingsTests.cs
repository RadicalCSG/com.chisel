using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Unity's grid is drawn where Chisel's grid is
    public sealed class ChiselGridSettingsTests
    {
        Vector3    m_Position;
        Quaternion m_Rotation;

        [SetUp]
        public void SetUp()
        {
            m_Position = EditorSnapSettings.gridPosition;
            m_Rotation = EditorSnapSettings.gridRotation;
        }

        [TearDown]
        public void TearDown()
        {
            EditorSnapSettings.gridPosition = m_Position;
            EditorSnapSettings.gridRotation = m_Rotation;
        }

        [Test]
        public void ARotatedGrid_MovesAndRotatesUnitysGrid()
        {
            var position = new Vector3(1, 2, 3);
            var rotation = Quaternion.Euler(30, 45, 10);
            var grid = new Grid(Matrix4x4.TRS(position, rotation, Vector3.one));

            ChiselGridSettings.ShowInUnityGrid(grid);

            Assert.That(Vector3.Distance(EditorSnapSettings.gridPosition, position), Is.LessThan(1e-5f));
            Assert.That(Quaternion.Angle(EditorSnapSettings.gridRotation, rotation), Is.LessThan(0.01f));
        }
    }
}

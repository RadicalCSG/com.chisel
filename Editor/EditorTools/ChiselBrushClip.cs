using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // What the clip tool keeps of the brushes it cuts, by the side of its plane (in front is the side its normal points to)
    public enum ChiselClipMode { KeepBehind, KeepInFront, Split }

    // Cuts brushes with a plane in the world, undoably
    static class ChiselBrushClip
    {
        internal const string kUndoName = "Clip brushes";

        /// <summary>The brushes cut by the plane as the mode says; the brushes left afterwards, the halves split off
        /// included. A brush wholly on the side that is removed is destroyed, one wholly on the side that is kept is left
        /// as it was; the face a cut makes gets the active material.</summary>
        public static List<ChiselBrushComponent> Apply(IEnumerable<ChiselBrushComponent> brushes, Plane worldPlane, ChiselClipMode mode)
        {
            var left = new List<ChiselBrushComponent>();
            foreach (var brush in brushes)
            {
                if (!brush || brush.definition?.BrushOutline == null)
                    continue;

                var plane = LocalPlane(brush, worldPlane);
                if (mode == ChiselClipMode.KeepInFront)
                    plane = -plane;

                var side = brush.definition.BrushOutline.SideOf(plane);
                if (side == BrushMesh.PlaneSide.Behind)
                {
                    left.Add(brush);
                    continue;
                }
                if (side == BrushMesh.PlaneSide.InFront)
                {
                    if (mode != ChiselClipMode.Split)
                        Undo.DestroyObjectImmediate(brush.gameObject);
                    else
                        left.Add(brush);
                    continue;
                }

                if (mode == ChiselClipMode.Split)
                {
                    var other = Duplicate(brush);
                    Clip(other, -plane);
                    left.Add(other);
                }
                Clip(brush, plane);
                left.Add(brush);
            }
            return left;
        }

        // the plane in the space the brush's outline is in
        static float4 LocalPlane(ChiselBrushComponent brush, Plane worldPlane)
        {
            var local = brush.GlobalTransformation.inverse.TransformPlane(worldPlane);
            return new float4(local.normal, local.distance);
        }

        static ChiselBrushComponent Duplicate(ChiselBrushComponent brush)
        {
            var source = brush.gameObject;
            var copy   = Object.Instantiate(source, source.transform.parent);
            copy.name  = source.name;
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(copy, kUndoName);
            return copy.GetComponent<ChiselBrushComponent>();
        }

        // A brush's surfaces are one per face, in the order of its faces: they are put in the order of the faces the cut
        // leaves, each face keeping its own, the cut's a new one with the active material
        static void Clip(ChiselBrushComponent brush, float4 plane)
        {
            Undo.RecordObject(brush, kUndoName);
            var surfaceArray = brush.SurfaceDefinition;
            var surfaces     = surfaceArray.surfaces ?? System.Array.Empty<ChiselSurface>();
            var cutIndex     = surfaces.Length;

            var outline = new BrushMesh(brush.definition.BrushOutline);
            if (!outline.Clip(plane, cutIndex))
                return;

            var polygons    = outline.polygons;
            var newSurfaces = new ChiselSurface[polygons.Length];
            for (int p = 0; p < polygons.Length; p++)
            {
                var from = polygons[p].descriptionIndex;
                newSurfaces[p] = from >= 0 && from < cutIndex ? surfaces[from] : CutSurface();
                polygons[p].descriptionIndex = p;
            }
            surfaceArray.surfaces = newSurfaces;
            brush.definition.BrushOutline = outline;
            // the indices the brush's vertex, edge and face selection holds are of the outline it had
            ChiselTopologySelectionManager.DeselectAll(brush);
            brush.OnValidate();
        }

        static ChiselSurface CutSurface()
        {
            var surface  = ChiselSurfaceArray.CreateNewSurface();
            var material = ChiselSurfaceBrowserPrefs.ActiveMaterial;
            if (material != null)
                surface.SetMaterial(material);
            return surface;
        }
    }
}

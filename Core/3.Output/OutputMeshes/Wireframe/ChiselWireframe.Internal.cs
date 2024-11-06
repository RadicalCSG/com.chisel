using System;

using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    sealed partial class ChiselWireframe
    {
        private static uint GetOutlineHash(CSGTreeBrush brush)
        {
            if (!brush.Outline.IsCreated)
                return 0;
            return brush.Outline.Value.hash;
        }

        private static bool GetOutlineValues(CSGTreeBrush      brush,
                                             ref float3[]      vertices,
                                             ref Int32[]       visibleOuterLines)
        {
            if (brush.Outline == BlobAssetReference<NativeWireframeBlob>.Null ||
				!brush.Outline.IsCreated)
                return false;
            ref var brushOutline = ref brush.Outline.Value;
            if (brushOutline.vertices.Length < BrushMesh.kMinimumVertices)
            {
                //UnityEngine.Debug.Log($"{brushOutline.vertices.Length} {brushOutline.surfaceVisibleOuterLines.Length} {brushOutline.visibleOuterLines.Length} {brushOutline.hash}");
                return false;
            }

            // TODO: once we switch to managed code, remove need to copy outlines
            vertices          = brushOutline.vertices.ToArray();
            visibleOuterLines = brushOutline.visibleOuterLines.ToArray();
            return true;
        }

        private static bool GetSurfaceOutlineValues(CSGTreeBrush    brush,
                                                    Int32           surfaceIndex,
                                                    ref float3[]    vertices,
                                                    ref Int32[]     visibleOuterLines)
		{
			if (brush.Outline == BlobAssetReference<NativeWireframeBlob>.Null ||
				!brush.Outline.IsCreated)
				return false;
			ref var brushOutline = ref brush.Outline.Value;
			if (brushOutline.vertices.Length < BrushMesh.kMinimumVertices)
                return false;

            ref var surfaceOutlineRanges = ref brushOutline.surfaceVisibleOuterLineRanges;
            if (surfaceIndex < 0 || surfaceIndex >= surfaceOutlineRanges.Length)
                return false;

            var startIndex  = surfaceIndex == 0 ? 0 : surfaceOutlineRanges[surfaceIndex - 1];
            var lastIndex   = surfaceOutlineRanges[surfaceIndex];
            var count       = lastIndex - startIndex;
            if (count <= 0)
                return false;

            ref var surfaceOutlines = ref brushOutline.surfaceVisibleOuterLines;
            if (startIndex < 0 || lastIndex > surfaceOutlines.Length)
                return false;

            visibleOuterLines = new int[count];
            for (int i = startIndex; i < lastIndex; i++)
                visibleOuterLines[i - startIndex] = surfaceOutlines[i];

            vertices = brushOutline.vertices.ToArray();
            return true;
        }

        private static ChiselWireframe CreateSurfaceWireframe(CSGTreeBrush brush, Int32 surfaceID)
        {
            var wireframe = new ChiselWireframe() { originBrush = brush, originSurfaceID = surfaceID };
            bool success = GetSurfaceOutlineValues(brush, surfaceID,
                                                   ref wireframe.vertices,
                                                   ref wireframe.visibleOuterLines);

            if (!success)
                return null;

            wireframe.outlineHash = GetOutlineHash(brush);
            return wireframe;
        }

        private static bool UpdateSurfaceWireframe(ChiselWireframe wireframe)
        {
            bool success = GetSurfaceOutlineValues(wireframe.originBrush, 
                                                   wireframe.originSurfaceID,
                                                   ref wireframe.vertices,
                                                   ref wireframe.visibleOuterLines);

            if (!success)
                return false;

            wireframe.outlineHash = GetOutlineHash(wireframe.originBrush);
            return true;
        }

        private static ChiselWireframe CreateBrushWireframe(CSGTreeBrush brush)
        {
            var wireframe = new ChiselWireframe { originBrush = brush };
            bool success = GetOutlineValues(brush,
                                                 ref wireframe.vertices,
                                                 ref wireframe.visibleOuterLines);

            if (!success)
                return null;

            wireframe.outlineHash = GetOutlineHash(brush);
            return wireframe;
        }

        private static bool UpdateBrushWireframe(ChiselWireframe wireframe)
        {
            bool success = GetOutlineValues(wireframe.originBrush,
                                                 ref wireframe.vertices,
                                                 ref wireframe.visibleOuterLines);

            if (!success)
                return false;

            wireframe.outlineHash = GetOutlineHash(wireframe.originBrush);
            return true;
        }
    }
}

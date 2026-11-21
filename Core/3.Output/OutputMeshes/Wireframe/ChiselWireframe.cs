using System;
using Unity.Mathematics;

namespace Chisel.Core
{
    [Serializable]
    public sealed partial class ChiselWireframe
    {
        internal float3[]	  vertices          = null;
        internal Int32[]	  visibleOuterLines = null;
        
        internal uint		  outlineHash		= 0;
        internal CSGTreeBrush originBrush		= default;
        internal int		  originSurfaceID	= 0;
        // the outline of a node's whole shape (ChiselShapeOutline), in the node's space
        internal CSGTreeNode  originNode		= default;

        public float3[] Vertices            { get { return vertices; } }
        public Int32[]	VisibleOuterLines   { get { return visibleOuterLines; } }

        public bool	Dirty { get { return originNode.Valid ? outlineHash != ChiselShapeOutline.Hash(originNode) : outlineHash != GetOutlineHash(originBrush); }  }

        public static ChiselWireframe CreateWireframe(CSGTreeBrush brush) { if (!brush.Valid) return null; return CreateBrushWireframe(brush); }
        public static ChiselWireframe CreateWireframe(CSGTreeBrush brush, int surfaceIndex) { if (!brush.Valid) return null; return CreateSurfaceWireframe(brush, surfaceIndex); }
        public static ChiselWireframe CreateShapeWireframe(CSGTreeNode node) { if (!node.Valid) return null; var wireframe = new ChiselWireframe { originNode = node }; return UpdateShapeWireframe(wireframe) ? wireframe : null; }
        public bool UpdateWireframe() { if (originNode.Valid) return UpdateShapeWireframe(this); if (originSurfaceID == 0) return UpdateBrushWireframe(this); else  return UpdateSurfaceWireframe(this);  }

        // worked out only when the shape changed
        static bool UpdateShapeWireframe(ChiselWireframe wireframe)
        {
            var shapeVertices = new System.Collections.Generic.List<float3>();
            var shapeLines    = new System.Collections.Generic.List<int>();
            ChiselShapeOutline.Build(wireframe.originNode, shapeVertices, shapeLines);
            wireframe.vertices          = shapeVertices.ToArray();
            wireframe.visibleOuterLines = shapeLines.ToArray();
            wireframe.outlineHash       = ChiselShapeOutline.Hash(wireframe.originNode);
            return wireframe.vertices.Length > 0;
        }
    }
}

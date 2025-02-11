using System;
using System.Runtime.InteropServices;

namespace Chisel.Core
{
    /// <summary>
    /// This class defines an intersection into a specific brush
    /// </summary>
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CSGTreeBrushIntersection
    {
        public CSGTree		tree;
        public CSGTreeBrush	brush;
        
        public Int32        surfaceIndex;
        
        public ChiselSurfaceIntersection surfaceIntersection;

        readonly static CSGTreeBrushIntersection kNone = new()
        {
            tree				= (CSGTree)CSGTreeNode.Invalid,
            brush				= (CSGTreeBrush)CSGTreeNode.Invalid,
            surfaceIndex		= -1,
            surfaceIntersection	= ChiselSurfaceIntersection.None
        };
        public static ref readonly CSGTreeBrushIntersection None => ref kNone;

	};
}

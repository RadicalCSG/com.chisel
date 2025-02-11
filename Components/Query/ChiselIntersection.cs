using System;
using System.Runtime.InteropServices;
using Chisel.Core;
using Vector3 = UnityEngine.Vector3;
using Plane = UnityEngine.Plane;
using UnityEngine;
using System.Runtime.CompilerServices;

namespace Chisel.Components
{
    /// <summary>
    /// This class defines an intersection into a specific brush
    /// </summary>
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ChiselIntersection
    {
        public ChiselModelComponent	model;
        public ChiselNodeComponent	treeNode;

        public Plane        worldPlane;
        public Vector3      worldPlaneIntersection;

        public CSGTreeBrushIntersection brushIntersection;
        public GameObject gameObject;

        readonly static ChiselIntersection kNone = new()
        {
            model                   = null,
            treeNode                = null,
            worldPlane			    = default,
            worldPlaneIntersection	= Vector3.zero,
            brushIntersection       = CSGTreeBrushIntersection.None,
            gameObject              = null
        };
        
        public static ref readonly ChiselIntersection None => ref kNone;
    };
}

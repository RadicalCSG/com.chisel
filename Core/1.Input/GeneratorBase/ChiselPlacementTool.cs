using UnityEngine;

namespace Chisel.Core
{
    /// Default group names for ChiselPlacementTools such as <see cref="Chisel.Core.ChiselBoundsPlacementTool"/> 
    /// and <see cref="Chisel.Core.ChiselShapePlacementTool"/>
    public static class ChiselToolGroups
    {
        public const string kBasePrimitives = "Basic Primitives";
        public const string kFreeForm       = "FreeForm";
        public const string kStairs         = "Stairs";

        // When a generator doesn't have a group (no attribute set), it'll use this name
        public const string kDefault        = "Default";
    }


    /// A placement tool that is placed by dragging a box like shape, and fitting the generator inside it
    /// Use <see cref="Chisel.Core.ChiselPlacementToolAttribute"/> to give it a name and group
    public abstract class ChiselBoundsPlacementTool<PlacementToolType> : ScriptableObject
        where PlacementToolType : IChiselNodeGenerator, new()
    {
        public virtual bool CreateAsBrush { get; set; } = false;
        /// <summary>Thinner than this along any axis, the generator isn't built: it is hidden while drawn so thin, and
        /// finishing then makes nothing</summary>
        public virtual float MinimumSize => ChiselGeneratorLimits.kMinSize;
        /// <summary>The generator runs along its depth (z): drawn wider than deep, it is turned to run along the long side</summary>
        public virtual bool AlongLongSide => false;
        /// <summary>The generator is built around its origin along y, not upwards from it: it is lifted by half its height</summary>
        public virtual bool CenteredOnY => false;
        public abstract PlacementFlags PlacementFlags { get; }
        public virtual void OnCreate(ref PlacementToolType definition) { }
        public abstract void OnUpdate(ref PlacementToolType definition, Bounds bounds);
        public abstract void OnPaint(IChiselHandleRenderer renderer, Bounds bounds);
    }


    /// A placement tool that is placed by drawing a 2D shape and extruding it
    /// Use <see cref="Chisel.Core.ChiselPlacementToolAttribute"/> to give it a name and group
    public abstract class ChiselShapePlacementTool<PlacementToolType> : ScriptableObject
        where PlacementToolType : IChiselNodeGenerator, new()
    {
        public virtual bool CreateAsBrush { get; set; } = false;
        public virtual void OnCreate(ref PlacementToolType definition, Curve2D shape) { }
        public abstract void OnUpdate(ref PlacementToolType definition, float height);
        public abstract void OnPaint(IChiselHandleRenderer renderer, Curve2D shape, float height);
    }
}

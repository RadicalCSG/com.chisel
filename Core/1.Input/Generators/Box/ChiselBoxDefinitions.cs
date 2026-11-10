using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselBox : IBrushGenerator, IChiselSurfaceNames
    {
        readonly static ChiselBox kDefaultSettings = new()
        {
            bounds = new MinMaxAABB { Min = float3.zero, Max = new float3(1) }
        };
		public static ref readonly ChiselBox DefaultSettings => ref kDefaultSettings;


		public MinMaxAABB bounds;


        #region Properties
        public float3 Min { readonly get { return bounds.Min; } set { bounds.SetMin(value); } }
        public float3 Max { readonly get { return bounds.Max; } set { bounds.SetMax(value); } }
        public float3 Size
        {
			readonly get { return bounds.Max - bounds.Min; }
            set { bounds.SetExtents(math.abs(value) * 0.5f); }
        }

        public float3 Center
        {
			readonly get { return (bounds.Max + bounds.Min) * 0.5f; }
            set { bounds.SetCenter(value); }
        }
        #endregion

        #region Generate
        public readonly BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob,
                                                                       Allocator allocator = Allocator.Persistent)// Indirect
		{
            // a negative size is the box with its sides swapped (CreateBox swaps them), so it is the right way out
            if (!BrushMeshFactory.CreateBox(bounds.Min, bounds.Max,
                                            in surfaceDefinitionBlob,
                                            out var newBrushMesh,
                                            allocator))// Indirect
				return default;
            return newBrushMesh;
        }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 6; } }

        [BurstDiscard]
        public readonly bool IsSurfaceUsed(int index) { return true; }

        [BurstDiscard]
        public readonly string GetSurfaceName(int index)
        {
            switch ((BrushMeshFactory.BoxSides)index)
            {
                case BrushMeshFactory.BoxSides.Top:    return ChiselSurfaceNames.Top;
                case BrushMeshFactory.BoxSides.Bottom: return ChiselSurfaceNames.Bottom;
                case BrushMeshFactory.BoxSides.Left:   return ChiselSurfaceNames.Left;
                case BrushMeshFactory.BoxSides.Right:  return ChiselSurfaceNames.Right;
                case BrushMeshFactory.BoxSides.Front:  return ChiselSurfaceNames.Front;
                case BrushMeshFactory.BoxSides.Back:   return ChiselSurfaceNames.Back;
                default:                               return null;
            }
        }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition) { }
        #endregion

        #region Validation
        /// <summary>The smallest a box can be along an axis, either way: thinner than the other generators (a sheet of a
        /// few millimetres is a box)</summary>
        public const float kMinSize = 0.0001f;

        // A negative size stays as it was given (the sides being dragged stay the ones dragged), a size of 0 becomes
        // kMinSize
        public bool Validate()
        {
            var size = bounds.Max - bounds.Min;
            var max  = bounds.Max;
            for (int axis = 0; axis < 3; axis++)
                max[axis] = bounds.Min[axis] + ChiselGeneratorLimits.NotZero(size[axis], kMinSize);
            bounds.Max = max;
            return true;
        }

        const string kDimensionCannotBeZero = "One or more dimensions of the box is zero, which is not allowed";

		[BurstDiscard]
        public void GetMessages(IChiselMessageHandler messages)
        {
            var size = Size;
            if (size.x == 0 || size.y == 0 || size.z == 0)
                messages.Warning(kDimensionCannotBeZero);
        }
        #endregion

        #region Reset
        public void Reset() { this = DefaultSettings; }
        #endregion
    }

    [Serializable]
    public class ChiselBoxDefinition : SerializedBrushGenerator<ChiselBox>
    {
        public const string kNodeTypeName = "Box";

        //[NamedItems("Top", "Bottom", "Right", "Left", "Back", "Front", fixedSize = 6)]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        #region OnEdit
        public override void OnEdit(IChiselHandles handles)
        {
            // a drag that would make the box flat isn't taken
            var before = settings.bounds;
            handles.DoBoundsHandle(ref settings.bounds);
            if (math.any(math.abs(settings.bounds.Max - settings.bounds.Min) < ChiselBox.kMinSize))
                settings.bounds = before;
            before = settings.bounds;
            if (handles.DoBoxMeasurements(ref settings.bounds) &&
                math.any(math.abs(settings.bounds.Max - settings.bounds.Min) < ChiselBox.kMinSize))
                settings.bounds = before;
        }
        #endregion
    }
}
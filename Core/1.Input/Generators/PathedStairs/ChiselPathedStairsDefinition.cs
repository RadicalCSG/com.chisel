using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselPathedStairs : IBranchGenerator
    {
        readonly static ChiselPathedStairs kDefaultSettings = new()
		{
            curveSegments   = 8,
            closed          = true,
            stairs          = ChiselLinearStairs.DefaultSettings
		};
		public static ref readonly ChiselPathedStairs DefaultSettings => ref kDefaultSettings;

		[NonSerialized] public bool closed;

        public int curveSegments;

        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        [HideFoldout] public ChiselLinearStairs stairs;

        [UnityEngine.HideInInspector, NonSerialized] public BlobAssetReference<ChiselCurve2DBlob> curveBlob;
        [UnityEngine.HideInInspector, NonSerialized] internal UnsafeList<SegmentVertex> shapeVertices;

        #region Generate
        public int PrepareAndCountRequiredBrushMeshes()
        {
            ref var curve = ref curveBlob.Value;
            var bounds = stairs.bounds;
            if (shapeVertices.IsCreated) shapeVertices.Dispose();
			shapeVertices = curve.GetPathVertices(curveSegments, Allocator.Persistent);
            if (shapeVertices.Length < 2)
                return 0;

            return BrushMeshFactory.CountPathedStairBrushes(shapeVertices, closed, bounds,
                                                            stairs.stepHeight, stairs.stepDepth, stairs.treadHeight, stairs.nosingDepth,
                                                            stairs.plateauHeight, stairs.riserType, stairs.riserDepth,
                                                            stairs.leftSide, stairs.rightSide,
                                                            stairs.sideWidth, stairs.sideHeight, stairs.sideDepth);
        }

        public bool GenerateNodes(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, NativeList<GeneratedNode> nodes, Allocator allocator)
        {
            if (!shapeVertices.IsCreated)
                return false;
            NativeList<BlobAssetReference<BrushMeshBlob>> generatedBrushMeshes;
			using var _generatedBrushMeshes = generatedBrushMeshes = new NativeList<BlobAssetReference<BrushMeshBlob>>(nodes.Length, Allocator.Temp);
            generatedBrushMeshes.Resize(nodes.Length, NativeArrayOptions.ClearMemory);
                
            ref var curve = ref curveBlob.Value;
            var bounds = stairs.bounds;

            if (!BrushMeshFactory.GeneratePathedStairs(generatedBrushMeshes,
                                                        shapeVertices, closed, bounds,
                                                        stairs.stepHeight, stairs.stepDepth, stairs.treadHeight, stairs.nosingDepth,
                                                        stairs.plateauHeight, stairs.riserType, stairs.riserDepth,
                                                        stairs.leftSide, stairs.rightSide,
                                                        stairs.sideWidth, stairs.sideHeight, stairs.sideDepth,
                                                        in surfaceDefinitionBlob, allocator))
            {
                for (int i = 0; i < generatedBrushMeshes.Length; i++)
                {
                    if (generatedBrushMeshes[i].IsCreated)
                        generatedBrushMeshes[i].Dispose();
                    generatedBrushMeshes[i] = default;
                }
                return false;
            }
            for (int i = 0; i < generatedBrushMeshes.Length; i++)
                nodes[i] = GeneratedNode.GenerateBrush(generatedBrushMeshes[i]);
            return true;
        }

        public void Dispose()
        {
            if (shapeVertices.IsCreated) shapeVertices.Dispose(); shapeVertices = default;
			if (curveBlob.IsCreated) curveBlob.Dispose(); curveBlob = default;
        }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return stairs.RequiredSurfaceCount; } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition)
        {
            stairs.UpdateSurfaces(ref surfaceDefinition);
        }
        #endregion

        #region Validation
        public const int kMinCurveSegments = 2;

        public bool Validate()
        {
            curveSegments = math.max(curveSegments, kMinCurveSegments);
            return stairs.Validate();
        }

        [BurstDiscard]
        public void GetWarningMessages(IChiselMessageHandler messages) { }
        #endregion

        #region Reset
        public void Reset() { this = DefaultSettings; }
        #endregion
    }

    [Serializable]
    public class ChiselPathedStairsDefinition : SerializedBranchGenerator<ChiselPathedStairs>
    {
        public const string kNodeTypeName = "Pathed Stairs";

        readonly static Curve2D	kDefaultShape = new(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });
        public static ref readonly Curve2D DefaultShape => ref kDefaultShape;


		public Curve2D shape;
        
        // TODO: do not use this data structure, find common stuff and share between the definitions ...
        //public ChiselLinearStairsDefinition stairs;

        public override void Reset()
        {
            shape = DefaultShape;
            base.Reset();
        }

        public override bool Validate() 
        {
            shape ??= DefaultShape;
            return base.Validate(); 
        }

        const Allocator defaultAllocator = Allocator.TempJob;
        public override ChiselPathedStairs GetBranchGenerator()
        {
            settings.curveBlob = ChiselCurve2DBlob.Convert(shape, defaultAllocator);
            settings.closed = shape.closed;
            return base.GetBranchGenerator();
        }

        public override void OnEdit(IChiselHandles handles)
        {
            handles.DoShapeHandle(ref shape);
        }
    }
}
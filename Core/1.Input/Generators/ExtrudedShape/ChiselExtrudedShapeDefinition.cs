using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselExtrudedShape : IBranchGenerator
    {
        readonly static ChiselExtrudedShape kDefaultSettings = new()
        {
            curveSegments = 8
        };
		public static ref readonly ChiselExtrudedShape DefaultSettings => ref kDefaultSettings;

		public int curveSegments;

        [UnityEngine.HideInInspector, NonSerialized] public BlobAssetReference<ChiselPathBlob>     pathBlob;
        [UnityEngine.HideInInspector, NonSerialized] public BlobAssetReference<ChiselCurve2DBlob>  curveBlob;

        [UnityEngine.HideInInspector, NonSerialized] internal UnsafeList<SegmentVertex>            polygonVerticesList;
        [UnityEngine.HideInInspector, NonSerialized] internal UnsafeList<int>                      polygonVerticesSegments;
         
        #region Generate
        public int PrepareAndCountRequiredBrushMeshes()
        {
            if (!curveBlob.IsCreated)
                return 0; 

            ref var curve = ref curveBlob.Value;
            if (!curve.ConvexPartition(curveSegments, out polygonVerticesList, out polygonVerticesSegments))
                return 0;

            return polygonVerticesSegments.Length;
        }

        public bool GenerateNodes(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, 
                                  NativeList<GeneratedNode> nodes, Allocator allocator = Allocator.Persistent)// Indirect
		{
			NativeList<BlobAssetReference<BrushMeshBlob>> generatedBrushMeshes;
			using var _generatedBrushMeshes = generatedBrushMeshes = new NativeList<BlobAssetReference<BrushMeshBlob>>(nodes.Length, Allocator.Temp);
			generatedBrushMeshes.Resize(nodes.Length, NativeArrayOptions.ClearMemory);

			using var pathMatrices = pathBlob.Value.GetUnsafeMatrices(Allocator.Temp);

			if (!BrushMeshFactory.GenerateExtrudedShape(generatedBrushMeshes,
														in polygonVerticesList,
														in polygonVerticesSegments,
														in pathMatrices,
														in surfaceDefinitionBlob,
														allocator))// Indirect
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
				nodes[i] = GeneratedNode.GenerateBrush(generatedBrushMeshes[i]); // Confirmed to dispose
			return true;
		}

        public void Dispose()
		{
			// Confirmed to be called
			if (pathBlob.IsCreated) pathBlob.Dispose();
            if (curveBlob.IsCreated) curveBlob.Dispose();
            if (polygonVerticesList.IsCreated) polygonVerticesList.Dispose();
            if (polygonVerticesSegments.IsCreated) polygonVerticesSegments.Dispose();
            pathBlob = default;
            curveBlob = default;
            polygonVerticesList = default;
            polygonVerticesSegments = default;
        }
        #endregion
        
        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 2 + (curveBlob.IsCreated ? curveBlob.Value.controlPoints.Length : 3); } }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition) { }
        #endregion
        
        #region Validation
        public bool Validate() { return true; }

        [BurstDiscard]
        public void GetWarningMessages(IChiselMessageHandler messages) { }
        #endregion

        #region Reset
        public void Reset() { this = DefaultSettings; }
        #endregion
    }

    [Serializable]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class ChiselExtrudedShapeDefinition : SerializedBranchGenerator<ChiselExtrudedShape>
    {
        public const string kNodeTypeName = "Extruded Shape";

        public readonly static Curve2D  kDefaultShape           = new Curve2D(new[]{ new CurveControlPoint2D(-1,-1), new CurveControlPoint2D( 1,-1), new CurveControlPoint2D( 1, 1), new CurveControlPoint2D(-1, 1) });

        public Curve2D                  shape;
        public ChiselPath               path;

        //[NamedItems(overflow = "Surface {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        public override void Reset()
        {
            shape = new Curve2D(kDefaultShape);
            path  = new ChiselPath(ChiselPath.Default);
            base.Reset();
        }

        public override int RequiredSurfaceCount { get { return 2 + shape.controlPoints.Length; } }

        // as a cylinder: where it starts and ends along its path, and a side for each point of its shape, along the span
        // from that point to the next
        public override string GetSurfaceName(int index)
        {
            if (index == 0) return ChiselSurfaceNames.Bottom;
            if (index == 1) return ChiselSurfaceNames.Top;
            return (index < RequiredSurfaceCount) ? ChiselSurfaceNames.Side(index - 1) : null;
        }

        public override bool IsSurfaceUsed(int index) { return true; }


        public override bool Validate()
        {
            shape ??= new Curve2D(kDefaultShape);
            path  ??= new ChiselPath(ChiselPath.Default);
            // never flat: a point of the path on the one before it is moved out by the smallest size, the way it faces
            for (int i = 1; path.segments != null && i < path.segments.Length; i++)
            {
                var point = path.segments[i];
                if (Vector3.Distance(point.position, path.segments[i - 1].position) >= ChiselGeneratorLimits.kMinSize)
                    continue;
                point.position = path.segments[i - 1].position + (point.rotation * Vector3.forward) * ChiselGeneratorLimits.kMinSize;
                path.segments[i] = point;
            }
            return base.Validate();
        }

        // Whether a point of the path moved to position would be on the one next to it
        bool OnItsNeighbour(int i, Vector3 position)
        {
            return (i > 0 && Vector3.Distance(position, path.segments[i - 1].position) < ChiselGeneratorLimits.kMinSize) ||
                   (i + 1 < path.segments.Length && Vector3.Distance(position, path.segments[i + 1].position) < ChiselGeneratorLimits.kMinSize);
        }

        const Allocator defaultAllocator = Allocator.TempJob;
        public override ChiselExtrudedShape GetBranchGenerator()
        {
            settings.pathBlob = ChiselPathBlob.Convert(path, defaultAllocator);
            settings.curveBlob = ChiselCurve2DBlob.Convert(shape, defaultAllocator);
            return base.GetBranchGenerator();
        }

        #region OnEdit
        //
        //


        const float kLineDash					= 2.0f;
        const float kCapLineThickness			= 1.0f;

        public override void OnEdit(IChiselHandles handles)
        {
            var baseColor		= handles.color;            
            var noZTestcolor	= handles.GetStateColor(baseColor, false, true);
            var zTestcolor		= handles.GetStateColor(baseColor, false, false);

            path.UpgradeIfNecessary();

            for (int i = 0; i < path.segments.Length; i++)
            {
                var pathPoint	= path.segments[i];
                var currMatrix	= pathPoint.ToMatrix();

                handles.color = baseColor;
                handles.DoShapeHandle(ref shape, currMatrix);

                if (i == 0)
                {
                    if (handles.DoDirectionHandle(ref pathPoint.position, -(pathPoint.rotation * Vector3.forward)) &&
                        !OnItsNeighbour(i, pathPoint.position))   // a drag that would make it flat isn't taken
                    {
                        path.segments[i] = pathPoint;
                        path = new ChiselPath(path);
                    }
                } else
                if (i == path.segments.Length - 1)
                {
                    if (handles.DoDirectionHandle(ref pathPoint.position, (pathPoint.rotation * Vector3.forward)) &&
                        !OnItsNeighbour(i, pathPoint.position))
                    {
                        path.segments[i] = pathPoint;
                        path = new ChiselPath(path);
                    }
                }


                // Draw lines between different segments
                if (i + 1 < path.segments.Length)
                {
                    var nextPoint		= path.segments[i + 1];
                    var nextMatrix		= nextPoint.ToMatrix();
                    var controlPoints	= shape.controlPoints;

                    for (int c = 0; c < controlPoints.Length; c++)
                    {
                        var controlPoint = controlPoints[c].position;
                        var pointA		 = currMatrix.MultiplyPoint(controlPoint);
                        var pointB		 = nextMatrix.MultiplyPoint(controlPoint);
                        handles.color = noZTestcolor;
                        handles.DrawLine(pointA, pointB, lineMode: LineMode.NoZTest, thickness: kCapLineThickness, dashSize: kLineDash);

                        handles.color = zTestcolor;
                        handles.DrawLine(pointA, pointB, lineMode: LineMode.ZTest,   thickness: kCapLineThickness, dashSize: kLineDash);
                    }

                    {
                        var pointA = currMatrix.MultiplyPoint(Vector3.zero);
                        var pointB = nextMatrix.MultiplyPoint(Vector3.zero);
                        handles.color = zTestcolor;
                        handles.DrawLine(pointA, pointB, lineMode: LineMode.NoZTest, thickness: kCapLineThickness, dashSize: kLineDash);

                        handles.color = zTestcolor;
                        handles.DrawLine(pointA, pointB, lineMode: LineMode.ZTest,   thickness: kCapLineThickness, dashSize: kLineDash);
                    }

                    handles.color = baseColor;
                }

            }

            for (int i = 0; i < path.segments.Length; i++)
            {
                var pathPoint = path.segments[i];
                if (handles.DoPathPointHandle(ref pathPoint))
                {
                    var originalSegments = path.segments;
                    var newPath = new ChiselPath(new ChiselPathPoint[originalSegments.Length]);
                    System.Array.Copy(originalSegments, newPath.segments, originalSegments.Length);
                    newPath.segments[i] = pathPoint;
                    path = newPath;
                }
            }

        }
        #endregion
    }
}

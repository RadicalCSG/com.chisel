using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselSphere : IBrushGenerator, IChiselSurfaceNames
    {
        readonly static ChiselSphere kDefaultSettings = new()
		{
            diameterXYZ		    = new float3(1),
            offsetY             = 0,
            rotation		    = 0.0f,
            horizontalSegments  = 12,
            verticalSegments    = 12,
            generateFromCenter  = false,
            smoothingGroup      = 1
        };
		public static ref readonly ChiselSphere DefaultSettings => ref kDefaultSettings;

		[DistanceValue] public float3	diameterXYZ;
        public float    offsetY;
        public bool     generateFromCenter;
        public float    rotation;
        public int	    horizontalSegments;
        public int	    verticalSegments;
        // the smoothing group of all its faces (0 for none)
        public uint     smoothingGroup;


        #region Generate
        public readonly BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, 
                                                                       Allocator allocator = Allocator.Persistent)// Indirect
		{
            if (!BrushMeshFactory.GenerateSphere(diameterXYZ,
                                                 offsetY,
                                                 rotation,
                                                 generateFromCenter,
                                                 horizontalSegments,
                                                 verticalSegments,
                                                 smoothingGroup,
                                                 in surfaceDefinitionBlob,
                                                 out var newBrushMesh,
                                                 allocator))// Indirect
				return default;
            return newBrushMesh;
        }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 2; } }

        // its bottom half and its top half
        [BurstDiscard]
        public readonly string GetSurfaceName(int index) { return index == 0 ? ChiselSurfaceNames.Bottom : index == 1 ? ChiselSurfaceNames.Top : null; }

        [BurstDiscard]
        public readonly bool IsSurfaceUsed(int index) { return true; }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition) { }
        #endregion

        #region Validation
        public const float kMinSphereDiameter = 0.01f;

        public bool Validate()
        {
            diameterXYZ.x       = math.max(kMinSphereDiameter, math.abs(diameterXYZ.x));
            diameterXYZ.y       = ChiselGeneratorLimits.NotZero(diameterXYZ.y);   // a negative height stays, 0 is never kept
            diameterXYZ.z       = math.max(kMinSphereDiameter, math.abs(diameterXYZ.z));

            horizontalSegments  = math.max(horizontalSegments, 3);
            verticalSegments	= math.max(verticalSegments, 2);
            return true;
        }

        [BurstDiscard]
        public void GetMessages(IChiselMessageHandler messages)
        {
        }
        #endregion

        #region Reset
        public void Reset() { this = DefaultSettings; }
        #endregion
    }

    [Serializable]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class ChiselSphereDefinition : SerializedBrushGenerator<ChiselSphere>
    {
        public const string kNodeTypeName = "Sphere";

        //[NamedItems(overflow = "Side {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        #region OnEdit
        //
        //


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;

        static void DrawOutline(IChiselHandleRenderer renderer, ChiselSphereDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            var sides			= definition.settings.horizontalSegments;
            
            var extraVertices	= 2;
            var bottomVertex	= 1;
            var topVertex		= 0;
            
            var rings			= (vertices.Length - extraVertices) / sides;

            var prevColor = renderer.color;
            var color = prevColor;
            color.a *= 0.6f;

            renderer.color = color;
            for (int i = 0, j = extraVertices; i < rings; i++, j += sides)
            {
                renderer.DrawLineLoop(vertices, j, sides, lineMode: lineMode, thickness: kHorzLineThickness, dashSize: kLineDash);
            }

            for (int k = 0; k < sides; k++)
            {
                renderer.DrawLine(vertices[topVertex], vertices[extraVertices + k], lineMode: lineMode, thickness: kVertLineThickness);
                for (int i = 0, j = extraVertices; i < rings - 1; i++, j += sides)
                    renderer.DrawLine(vertices[j + k], vertices[j + k + sides], lineMode: lineMode, thickness: kVertLineThickness);
                renderer.DrawLine(vertices[bottomVertex], vertices[extraVertices + k + ((rings - 1) * sides)], lineMode: lineMode, thickness: kVertLineThickness);
            }
            renderer.color = prevColor;
        }

        static Vector3[] vertices = null;

        public override void OnEdit(IChiselHandles handles)
        {
            var baseColor		= handles.color;
            var normal			= Vector3.up;

            if (BrushMeshFactory.GenerateSphereVertices(this, ref vertices))
            {
                handles.color = handles.GetStateColor(baseColor, false, false);
                DrawOutline(handles, this, vertices, lineMode: LineMode.ZTest);

                handles.color = handles.GetStateColor(baseColor, false, true);
                DrawOutline(handles, this, vertices, lineMode: LineMode.NoZTest);
                handles.color = baseColor;
            }

            Vector3 center, topPoint, bottomPoint;
            if (!settings.generateFromCenter)
            {
                center      = normal * (settings.offsetY + (settings.diameterXYZ.y * 0.5f));
                topPoint    = normal * (settings.offsetY + settings.diameterXYZ.y);
                bottomPoint = normal * (settings.offsetY);
            } else
            {
                center      = normal * (settings.offsetY);
                topPoint    = normal * (settings.offsetY + (settings.diameterXYZ.y *  0.5f));
                bottomPoint = normal * (settings.offsetY + (settings.diameterXYZ.y * -0.5f));
            }

            if (settings.diameterXYZ.y < 0)
                normal = -normal;

            var radius2D = new float2(settings.diameterXYZ.x, settings.diameterXYZ.z) * 0.5f;

            {
                var radiusX = radius2D.x;
                handles.DoRadiusHandle(ref radiusX, normal, center);
                radius2D.x = radiusX;

                {
                    var isBottomBackfaced	= false;
                    
                    handles.backfaced = isBottomBackfaced;
                    handles.DoDirectionHandle(ref bottomPoint, -normal);
                    handles.backfaced = false;
                }

                {
                    var isTopBackfaced		= false;
                    
                    handles.backfaced = isTopBackfaced;
                    handles.DoDirectionHandle(ref topPoint, normal);
                    handles.backfaced = false;
                }
            }
            if (handles.modified)
            {
                var diameter = settings.diameterXYZ;
                diameter.x = radius2D.x * 2.0f;
                diameter.z = radius2D.x * 2.0f;
                // a drag that would make it flat isn't taken
                if (math.abs(topPoint.y - bottomPoint.y) >= ChiselGeneratorLimits.kMinSize)
                {
                    diameter.y       = topPoint.y - bottomPoint.y;
                    settings.offsetY = bottomPoint.y;
                }
                settings.diameterXYZ = diameter;
            }

            // the sizes of the box around it, as when it was placed
            var boxCenterY = settings.generateFromCenter ? settings.offsetY : settings.offsetY + settings.diameterXYZ.y * 0.5f;
            var box        = new Bounds(new Vector3(0, boxCenterY, 0), math.abs(settings.diameterXYZ));
            var sizeBefore = box.size;
            if (handles.DoBoxMeasurements(ref box))
            {
                // a typed size changes that size alone
                var size     = box.size;
                var diameter = settings.diameterXYZ;
                if (size.x != sizeBefore.x) diameter.x = size.x;
                if (size.z != sizeBefore.z) diameter.z = size.z;
                if (size.y != sizeBefore.y) diameter.y = (diameter.y < 0 ? -1 : 1) * size.y;
                settings.diameterXYZ = diameter;
            }
        }
        #endregion
    }
}
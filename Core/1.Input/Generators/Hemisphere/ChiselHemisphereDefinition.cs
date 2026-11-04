using System;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselHemisphere : IBrushGenerator, IChiselSurfaceNames
    {
        readonly static ChiselHemisphere kDefaultSettings = new()
		{
            diameterXYZ		    = new float3(1.0f, 0.5f, 1.0f),
            rotation			= 0.0f,
            horizontalSegments	= 8,
            verticalSegments	= 8
        };
		public static ref readonly ChiselHemisphere DefaultSettings => ref kDefaultSettings;


		[DistanceValue] public float3   diameterXYZ;
        public float                    rotation; // TODO: useless?
        public int                      horizontalSegments;
        public int                      verticalSegments;


        #region Generate
        public readonly BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, 
                                                                       Allocator allocator = Allocator.Persistent)// Indirect
		{
            if (!BrushMeshFactory.GenerateHemisphere(diameterXYZ,
                                                     rotation, // TODO: useless?
                                                     horizontalSegments,
                                                     verticalSegments,
                                                     in surfaceDefinitionBlob,
                                                     out var newBrushMesh,
                                                     allocator))// Indirect
				return default;
            return newBrushMesh;
        }
        #endregion

        #region Surfaces
        public int RequiredSurfaceCount { get { return 2; } }

        // its flat base and its rounded top
        [BurstDiscard]
        public readonly string GetSurfaceName(int index) { return index == 0 ? ChiselSurfaceNames.Bottom : index == 1 ? ChiselSurfaceNames.Top : null; }

        [BurstDiscard]
        public readonly bool IsSurfaceUsed(int index) { return true; }

        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition) { }
        #endregion

        #region Validation
        public const float kMinDiameter = 0.01f;

        public bool Validate()
        {
            diameterXYZ.x = math.max(kMinDiameter, math.abs(diameterXYZ.x));
            diameterXYZ.y = ChiselGeneratorLimits.NotZero(diameterXYZ.y);   // a negative height stays (built downwards), 0 is never kept
            diameterXYZ.z = math.max(kMinDiameter, math.abs(diameterXYZ.z));

            horizontalSegments	= math.max(horizontalSegments, 3);
            verticalSegments	= math.max(verticalSegments, 1);
            return true;
        }

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
    public class ChiselHemisphereDefinition : SerializedBrushGenerator<ChiselHemisphere>
    {
        public const string kNodeTypeName = "Hemisphere";

        //[NamedItems("Bottom", overflow = "Side {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        #region OnEdit
        //
        // TODO: code below needs to be cleaned up & simplified 
        //


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;

        static void DrawOutline(IChiselHandleRenderer renderer, ChiselHemisphereDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            var sides			= definition.settings.horizontalSegments;

            var topSegments		= math.max(definition.settings.verticalSegments,    0);
            var bottomCap		= false;
            var topCap			= (topSegments    != 0);
            var extraVertices	= ((topCap) ? 1 : 0) + ((bottomCap) ? 1 : 0);
            var bottomVertex	= 0;
            //var topVertex		= (bottomCap) ? 1 : 0;
            
            var rings			= (vertices.Length - extraVertices) / sides;
            var bottomRing		= 0;

            var prevColor = renderer.color;
            var color = prevColor;
            color.a *= 0.6f;

            for (int i = 0, j = extraVertices; i < rings; i++, j += sides)
            {
                renderer.color = ((i == bottomRing) ? prevColor : color);
                renderer.DrawLineLoop(vertices, j, sides, lineMode: lineMode, thickness: ((i == bottomRing) ? kCapLineThickness : kHorzLineThickness), dashSize: ((i == bottomRing) ? 0 : kLineDash));
            }

            renderer.color = color;
            for (int k = 0; k < sides; k++)
            {
                for (int i = 0, j = extraVertices; i < rings - 1; i++, j += sides)
                    renderer.DrawLine(vertices[j + k], vertices[j + k + sides], lineMode: lineMode, thickness: kVertLineThickness);
                if (topCap)
                    renderer.DrawLine(vertices[bottomVertex], vertices[extraVertices + k + ((rings - 1) * sides)], lineMode: lineMode, thickness: kVertLineThickness);
            }
            renderer.color = prevColor;
        }
        

        static Vector3[] vertices = null; // TODO: store this per instance? or just allocate every frame?

        public override void OnEdit(IChiselHandles handles)
        {
            var baseColor		= handles.color;
            var normal			= Vector3.up;

            if (BrushMeshFactory.GenerateHemisphereVertices(ref settings, ref vertices))
            {
                handles.color = handles.GetStateColor(baseColor, false, false);
                DrawOutline(handles, this, vertices, lineMode: LineMode.ZTest);

                handles.color = handles.GetStateColor(baseColor, false, true);
                DrawOutline(handles, this, vertices, lineMode: LineMode.NoZTest);
                handles.color = baseColor;
            }
            

            var topPoint	= normal * settings.diameterXYZ.y;
            var radius2D	= new float2(settings.diameterXYZ.x, settings.diameterXYZ.z) * 0.5f;

            if (settings.diameterXYZ.y < 0)
                normal = -normal;
            bool previousModified;
            previousModified = handles.modified;
            {
                handles.color = baseColor;
                // TODO: make it possible to (optionally) size differently in x & z
                handles.DoRadiusHandle(ref radius2D.x, normal, Vector3.zero);

                {
                    var isTopBackfaced		= false; // TODO: how to do this?
                    
                    handles.backfaced = isTopBackfaced;
                    handles.DoDirectionHandle(ref topPoint, normal);
                    handles.backfaced = false;
                }
            }
            if (previousModified != handles.modified)
            {
                var diameter = settings.diameterXYZ;
                // a drag that would make it flat isn't taken
                if (math.abs(topPoint.y) >= ChiselGeneratorLimits.kMinSize)
                    diameter.y = topPoint.y;
                diameter.x = radius2D.x * 2.0f;
                diameter.z = radius2D.x * 2.0f;
                settings.diameterXYZ = diameter;
            }

            // the sizes of the box around it, as when it was placed
            var box        = new Bounds(new Vector3(0, settings.diameterXYZ.y * 0.5f, 0), math.abs(settings.diameterXYZ));
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
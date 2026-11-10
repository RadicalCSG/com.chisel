using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public enum CylinderShapeType : byte
    {
        Cylinder,
        Cone,
        ConicalFrustum
    }

    [Serializable]
    public struct ChiselCylinder : IBrushGenerator, IChiselSurfaceNames
    {
        readonly static ChiselCylinder kDefaultSettings = new()
        {
            topDiameterX        = 1.0f,
            topDiameterZ        = 1.0f,
            height              = 1.0f,

            bottomDiameterX     = 1.0f,
            bottomDiameterZ     = 1.0f,
            bottomOffset        = 0.0f,

            rotation            = 0.0f,
            isElliptical         = false,
            fitToBounds         = true,
            sides               = 16,
            smoothingGroup      = 1,
            type                = CylinderShapeType.Cylinder,
            surfaceUVVersion    = kSurfaceUVVersion
        };
		public static ref readonly ChiselCylinder DefaultSettings => ref kDefaultSettings;

		public CylinderShapeType type;
        [UnityEngine.Serialization.FormerlySerializedAs("isEllipsoid")]
        public bool     isElliptical;
        public bool     fitToBounds;
        public int      sides;

        // Show the name for the top Diameter X as "Top Diameter X" or "Top Diameter" depending on isElliptical being true or false
        [ConditionalNamePart("{ellipsoid}", " X", "", nameof(isElliptical)),
            ConditionalName("Top Diameter{ellipsoid}"),
            // Show this field depending on the type being a ConicalFrustum
            ConditionalHide(nameof(type), CylinderShapeType.Cone, CylinderShapeType.Cylinder), DistanceValue] 
        public float    topDiameterX;

        // Only show this field if it's a ConicalFrustum and isElliptical is true
        [ConditionalHide(nameof(type), CylinderShapeType.Cone, CylinderShapeType.Cylinder),
            ConditionalHide(nameof(isElliptical)), DistanceValue] // (z-diameter is only used for ellipsoids)
        public float    topDiameterZ;

        // Show the name for the bottom Diameter X as "Bottom Diameter X", "Bottom Diameter" or "Diameter" depending on isElliptical being true or false 
        // and if the type is a ConicalFrustum or not
        [ConditionalNamePart("{bottom}", "Bottom ", "", nameof(type), CylinderShapeType.Cone, CylinderShapeType.Cylinder),
            ConditionalNamePart("{ellipsoid}", " X", "", nameof(isElliptical)),
            ConditionalName("{bottom}Diameter{ellipsoid}"), DistanceValue] 
        public float    bottomDiameterX;

        // Show the name for the bottom Diameter Z as "Bottom Diameter X" or "Diameter Z" depending on if the type is a ConicalFrustum or not
        [ConditionalNamePart("{bottom}", "Bottom ", "", nameof(type), CylinderShapeType.Cone, CylinderShapeType.Cylinder),
            ConditionalName("{bottom}Diameter Z"),
            // Only show this field if isElliptical is true
            ConditionalHide(nameof(isElliptical)), DistanceValue] // (z-diameter is only used for ellipsoids)
        public float    bottomDiameterZ;

        [DistanceValue] public float height;
        [DistanceValue] public float bottomOffset;

        // the smoothing group of its sides, not its flat caps (0 for none)
        public uint smoothingGroup;

        [UnityEngine.HideInInspector, AngleValue]
        public float rotation;

        // 1: each surface holds its own uvs, and the cylinder lays out its caps and sides on top of them when it is
        // built. 0, saved before: the surfaces hold the layout itself
        public const int kSurfaceUVVersion = 1;
        [UnityEngine.HideInInspector]
        public int surfaceUVVersion;


        #region Properties
        public float TopDiameterX
        {
            get { return topDiameterX; }
            set
            {
                if (value == topDiameterX)
                    return;

                topDiameterX = value;
                if (!isElliptical)
                    topDiameterZ = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    bottomDiameterX = value;
                    if (!isElliptical)
                        bottomDiameterZ = value;
                }
            }
        }

        public float TopDiameterZ
        {
            get { return topDiameterZ; }
            set
            {
                if (value == topDiameterZ)
                    return;

                topDiameterZ = value;
                if (!isElliptical)
                    topDiameterX = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    bottomDiameterZ = value;
                    if (!isElliptical)
                        bottomDiameterX = value;
                }
            }
        }

        public float Diameter
        {
            get { return bottomDiameterX; }
            set
            {
                if (value == bottomDiameterX)
                    return;

                bottomDiameterX = value;
                topDiameterX = value;
                bottomDiameterZ = value;
                topDiameterZ = value;
            }
        }

        public float DiameterX
        {
            get { return bottomDiameterX; }
            set
            {
                if (value == bottomDiameterX)
                    return;

                bottomDiameterX = value;
                topDiameterX = value;
                if (!isElliptical)
                {
                    bottomDiameterZ = value;
                    topDiameterZ = value;
                }
            }
        }

        public float DiameterZ
        {
            get { return bottomDiameterZ; }
            set
            {
                if (value == bottomDiameterZ)
                    return;

                bottomDiameterZ = value;
                topDiameterZ = value;
                if (!isElliptical)
                {
                    bottomDiameterX = value;
                    topDiameterX = value;
                }
            }
        }

        public float BottomDiameterX
        {
            get { return bottomDiameterX; }
            set
            {
                if (value == bottomDiameterX)
                    return;

                bottomDiameterX = value;
                if (!isElliptical)
                    bottomDiameterZ = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    topDiameterX = value;
                    if (!isElliptical)
                        topDiameterZ = value;
                }
            }
        }

        public float BottomDiameterZ
        {
            get { return bottomDiameterZ; }
            set
            {
                if (value == bottomDiameterZ)
                    return;

                bottomDiameterZ = value;
                if (!isElliptical)
                    bottomDiameterX = value;
                if (type == CylinderShapeType.Cylinder)
                {
                    topDiameterZ = value;
                    if (!isElliptical)
                        topDiameterX = value;
                }
            }
        }
        #endregion

        #region Generate
        public readonly BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, 
                                                                       Allocator allocator = Allocator.Persistent)// Indirect
		{
            var topDiameter     = new float2(topDiameterX, topDiameterZ);
            var bottomDiameter  = new float2(bottomDiameterX, bottomDiameterZ);

            var topHeight = height + bottomOffset;
            var bottomHeight = bottomOffset;
            switch (type)
            {
                case CylinderShapeType.ConicalFrustum:  break;
                case CylinderShapeType.Cylinder:        topDiameter = bottomDiameter; break;
                case CylinderShapeType.Cone:            topDiameter = float2.zero; break;
                default: throw new NotImplementedException();
            }

            if (!isElliptical)
            {
                topDiameter.y = topDiameter.x;
                bottomDiameter.y = bottomDiameter.x;
            }

            if (!BrushMeshFactory.GenerateConicalFrustumSubMesh(topDiameter,    topHeight,
                                                                bottomDiameter, bottomHeight,
                                                                rotation, sides, fitToBounds,
                                                                in surfaceDefinitionBlob,
                                                                out var newBrushMesh,
                                                                allocator))// Indirect
				return default;

            // Each face's surface with the layout put on it (a surface mapped in world space keeps its own); the mesh is
            // only hashed when it is registered, after this
            ref var polygons = ref newBrushMesh.Value.polygons;
            for (int p = 0; p < polygons.Length; p++)
            {
                ref var details = ref polygons[p].surface.details;
                if ((details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) == 0)
                    details.UV0 = OnLayout(details.UV0, Layout(polygons[p].descriptionIndex));
            }
            return newBrushMesh;
        }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 2 + sides; } }

        [BurstDiscard]
        public readonly bool IsSurfaceUsed(int index) { return true; }

        [BurstDiscard]
        public readonly string GetSurfaceName(int index)
        {
            if (index == 0) return ChiselSurfaceNames.Bottom;
            if (index == 1) return ChiselSurfaceNames.Top;
            return (index < 2 + sides) ? ChiselSurfaceNames.Side(index - 1) : null;
        }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition)
        {
            var surfaces = surfaceDefinition.surfaces;

            // Saved before, every surface held the layout (written into it again on every change): they become
            // untouched surfaces, which the layout is put on when the cylinder is built, so it looks the same
            if (surfaceUVVersion < kSurfaceUVVersion)
            {
                for (int i = 0; i < surfaces.Length; i++)
                    surfaces[i].surfaceDetails.UV0 = SurfaceDetails.Default.UV0;
                surfaceUVVersion = kSurfaceUVVersion;
            }

            for (int i = 2; i < 2 + sides && i < surfaces.Length; i++)
                surfaces[i].surfaceDetails.smoothingGroup = smoothingGroup;
        }

        // Where a surface's texture starts: the caps centered, each side after the one before it round the cylinder, so
        // the texture runs on from side to side
        public readonly UVMatrix Layout(int descriptionIndex)
        {
            if (descriptionIndex < 2)
                return UVMatrix.Centered;
            float radius     = topDiameterX * 0.5f;
            float angle      = 360.0f / sides;
            float sideLength = (2 * math.sin(math.radians(angle / 2.0f))) * radius;
            var uv0 = UVMatrix.Identity;
            uv0.U.w = ((descriptionIndex - 2) + 0.5f) * sideLength;
            return uv0;
        }

        // The surface's own uvs applied to the layout (its u and v are the layout's), as the sphere does with its map. They
        // count from a new surface's uvs, so a surface left as it is gets the layout itself
        public static UVMatrix OnLayout(UVMatrix own, UVMatrix layout)
        {
            var neutral = SurfaceDetails.Default.UV0;
            float4 layoutU = layout.U, layoutV = layout.V, ownU = own.U, ownV = own.V;
            return new UVMatrix(ownU.x * layoutU + ownU.y * layoutV + new float4(0, 0, 0, ownU.w - neutral.U.w),
                                ownV.x * layoutU + ownV.y * layoutV + new float4(0, 0, 0, ownV.w - neutral.V.w));
        }
        #endregion

        #region Validation
        // A negative height stays (the factory builds it the right way out); a height or diameter of 0 becomes the smallest
        // (a frustum's top can be 0: then it is a cone)
        public bool Validate()
        {
            height = ChiselGeneratorLimits.NotZero(height);
            topDiameterX = math.abs(topDiameterX);
            topDiameterZ = math.abs(topDiameterZ);
            bottomDiameterX = math.max(math.abs(bottomDiameterX), ChiselGeneratorLimits.kMinSize);
            bottomDiameterZ = math.max(math.abs(bottomDiameterZ), ChiselGeneratorLimits.kMinSize);
            if (type == CylinderShapeType.Cylinder)
            {
                topDiameterX = math.max(topDiameterX, ChiselGeneratorLimits.kMinSize);
                topDiameterZ = math.max(topDiameterZ, ChiselGeneratorLimits.kMinSize);
            }

            sides = math.max(3, sides);
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
    public class ChiselCylinderDefinition : SerializedBrushGenerator<ChiselCylinder>
    {
        public const string kNodeTypeName = "Cylinder";

        //[NamedItems("Top", "Bottom", overflow = "Side {0}")]
        //public ChiselSurfaceDefinition surfaceDefinition;

        #region OnEdit
        //
        //

        static void DraggableRadius(IChiselHandles handles, ref float diameterX, ref float diameterZ, Vector3 center, Vector3 up, Vector3 xVector, Vector3 zVector, IChiselHandle[] radiusHandles, bool isElliptical)
        {
            if (isElliptical)
            {
                Debug.Assert(radiusHandles.Length == 4);
                diameterX = -diameterX; handles.DoSlider1DHandle(ref diameterX, center, xVector, radiusHandles[1]); // right
                if (handles.lastHandleHadFocus)
                {
                    var radiusX = diameterX * 0.5f;
                    var radiusZ = diameterZ * 0.5f;
                    var vecX = (xVector.normalized * radiusX);
                    var vecZ = (zVector.normalized * radiusZ);
                    handles.RenderDistanceMeasurement(center, center + vecX, radiusX, "Radius");
                    handles.RenderDistanceMeasurement(center, center + vecZ, radiusZ, "Radius");
                }
                diameterX = -diameterX; handles.DoSlider1DHandle(ref diameterX, center, xVector, radiusHandles[0]); // left
                if (handles.lastHandleHadFocus)
                {
                    var radiusX = diameterX * 0.5f;
                    var radiusZ = diameterZ * 0.5f;
                    var vecX = (xVector.normalized * radiusX);
                    var vecZ = (zVector.normalized * radiusZ);
                    handles.RenderDistanceMeasurement(center, center + vecX, radiusX, "Radius");
                    handles.RenderDistanceMeasurement(center, center + vecZ, radiusZ, "Radius");
                }
                diameterZ = -diameterZ; handles.DoSlider1DHandle(ref diameterZ, center, zVector, radiusHandles[3]); // back
                if (handles.lastHandleHadFocus)
                {
                    var radiusX = diameterX * 0.5f;
                    var radiusZ = diameterZ * 0.5f;
                    var vecX = (xVector.normalized * radiusX);
                    var vecZ = (zVector.normalized * radiusZ);
                    handles.RenderDistanceMeasurement(center, center + vecX, radiusX, "Radius");
                    handles.RenderDistanceMeasurement(center, center + vecZ, radiusZ, "Radius");
                }
                diameterZ = -diameterZ; handles.DoSlider1DHandle(ref diameterZ, center, zVector, radiusHandles[2]); // forward
                if (handles.lastHandleHadFocus)
                {
                    var radiusX = diameterX * 0.5f;
                    var radiusZ = diameterZ * 0.5f;
                    var vecX = (xVector.normalized * radiusX);
                    var vecZ = (zVector.normalized * radiusZ);
                    handles.RenderDistanceMeasurement(center, center + vecX, radiusX, "Radius");
                    handles.RenderDistanceMeasurement(center, center + vecZ, radiusZ, "Radius");
                }

                diameterX = math.abs(diameterX);
                diameterZ = math.abs(diameterZ);
            } else
            {
                Debug.Assert(radiusHandles.Length == 1);

                handles.DoDistanceHandle(ref diameterX, center, up, radiusHandles[0]); // left
                diameterX       = math.abs(diameterX);
                var radiusX     = diameterX * 0.5f;

                if (handles.lastHandleHadFocus)
                {
                    if (handles.TryGetClosestPoint(radiusHandles, out var closestPoint, interpolate: false))
                    {
                        var vec = (closestPoint - center);
                        handles.RenderDistanceMeasurement(center, center + vec, radiusX, "Radius");
                    }
                }
            }
        }

        class CylinderHandles
        {
            public Vector3 normal = Vector3.up;
            public IChiselEllipsoidHandle  fullBottomCircleHandle;
            public IChiselEllipsoidHandle fullTopCircleHandle;
            public IChiselLineHandle verticalHandle1;
            public IChiselLineHandle verticalHandle2;
            public IChiselNormalHandle topHandle;
            public IChiselNormalHandle bottomHandle;

            public IChiselHandle[] topHandles;
            public IChiselHandle[] bottomHandles;
            public IChiselEllipsoidHandle[] topRadiusHandles;
            public IChiselEllipsoidHandle[] bottomRadiusHandles;

            public float topY;
            public float bottomY;
            public Vector3 topPoint;
            public Vector3 bottomPoint;
            public Vector3 topXVector;
            public Vector3 topZVector;
            public Vector3 bottomXVector;
            public Vector3 bottomZVector;

            public void Init(IChiselHandleAllocation handles, ChiselCylinderDefinition definition)
            {   
                fullBottomCircleHandle  = handles.CreateEllipsoidHandle(Vector3.zero, Vector3.zero, 0, 0);
                fullTopCircleHandle     = handles.CreateEllipsoidHandle(Vector3.zero, Vector3.zero, 0, 0);

                verticalHandle1          = handles.CreateLineHandle(Vector3.zero, Vector3.zero);
                verticalHandle2          = handles.CreateLineHandle(Vector3.zero, Vector3.zero);

                topHandle               = handles.CreateNormalHandle(Vector3.zero, Vector3.zero);
                bottomHandle            = handles.CreateNormalHandle(Vector3.zero, Vector3.zero);
            }
        
            public void Update(IChiselHandles handles, ChiselCylinderDefinition definition)
            {
                var tempBottomDiameterX	= definition.settings.BottomDiameterX;
                var tempBottomDiameterZ = definition.settings.isElliptical ? definition.settings.BottomDiameterZ : definition.settings.BottomDiameterX;

                float tempTopDiameterX, tempTopDiameterZ;
                if (definition.settings.type == CylinderShapeType.Cone)
                {
                    tempTopDiameterX     = 0;
                    tempTopDiameterZ     = 0;
                } else
                if (definition.settings.type == CylinderShapeType.Cylinder)
                { 
                    tempTopDiameterX	= tempBottomDiameterX;
                    tempTopDiameterZ	= tempBottomDiameterZ;
                } else
                {
                    tempTopDiameterX	= definition.settings.TopDiameterX;
                    tempTopDiameterZ	= definition.settings.isElliptical ? definition.settings.TopDiameterZ : definition.settings.TopDiameterX;
                }
            
                topY            = (definition.settings.height + definition.settings.bottomOffset);
                bottomY         = definition.settings.bottomOffset;
                var rotate		= quaternion.AxisAngle(new float3(0,1,0), math.radians(definition.settings.rotation));
                topXVector		= math.mul(rotate, new float3(1, 0, 0)) * tempTopDiameterX * 0.5f;
                topZVector      = math.mul(rotate, new float3(0, 0, 1)) * tempTopDiameterZ * 0.5f;
                bottomXVector   = math.mul(rotate, new float3(1, 0, 0)) * tempBottomDiameterX * 0.5f;
                bottomZVector   = math.mul(rotate, new float3(0, 0, 1)) * tempBottomDiameterZ * 0.5f;
                // the top and bottom are where they are along up; only the way out of them turns over at a negative height
                normal          = (topY < bottomY) ? -Vector3.up : Vector3.up;
                topPoint        = Vector3.up * topY;
                bottomPoint     = Vector3.up * bottomY;
            
            
                // Render vertical horizon of cylinder
                Vector3 bottomPointA, topPointA;
                Vector3 bottomPointB, topPointB;
                var camera = UnityEngine.Camera.current;
                var cameraTransform = camera.transform;
                var cameraPosition = handles.inverseMatrix.MultiplyPoint(cameraTransform.position);
                const float degreeStep = 5;
                var pointA      = fullTopCircleHandle.GetPointAtDegree(360 - degreeStep);
                var pointB      = fullBottomCircleHandle.GetPointAtDegree(360 - degreeStep);
                var camOrtho    = camera.orthographic;
                var camForward  = handles.inverseMatrix.MultiplyVector(cameraTransform.forward).normalized;
                var camDir      = camOrtho ? camForward : (pointA - cameraPosition).normalized;

            
                var delta       = (pointA - pointB).normalized;
                var normal3     = -Vector3.Cross(delta, Vector3.Cross((pointB - bottomPoint).normalized, delta)).normalized;
                var prevDot     = Vector3.Dot(normal3, camDir) < 0;

                bool renderHorizon = false;
                //*
                bottomPointA    = Vector3.zero;
                topPointA       = Vector3.zero;
                bottomPointB    = Vector3.zero;
                topPointB       = Vector3.zero;
                var lineCount = 0;
                for (float degree = 0; degree < 360; degree += degreeStep)
                {
                    pointA = fullTopCircleHandle.GetPointAtDegree(degree);
                    pointB = fullBottomCircleHandle.GetPointAtDegree(degree);

                    delta   = (pointA - pointB).normalized;
                    normal3 = -Vector3.Cross(delta, Vector3.Cross((pointB - bottomPoint).normalized, delta)).normalized;

                    camDir = camOrtho ? camForward : (pointB - cameraPosition).normalized;
                    var currDot = Vector3.Dot(normal3, camDir) < 0;

                    if (prevDot != currDot)
                    {
                        lineCount++;
                        if (lineCount == 1)
                        {
                            topPointA = pointA;
                            bottomPointA = pointB;
                        } else
                        //if (lineCount == 2)
                        {
                            topPointB = pointA;
                            bottomPointB = pointB;
                            renderHorizon = true;
                            break;
                        }
                    }
                    prevDot = currDot;
                }


				/*/
                if (camera.orthographic)
                {
                    {
                        var radius = definition.bottomDiameterX * 0.5f;
                        var center = bottomPoint;
                        bottomPointA = center + (cameraTransform.right * radius);
                        bottomPointB = center - (cameraTransform.right * radius);
                    }
                    {
                        var radius = definition.topDiameterX * 0.5f;
                        var center = topPoint;
                        topPointA = center + (cameraTransform.right * radius);
                        topPointB = center - (cameraTransform.right * radius);
                    }
                } else
                {
                    var handleMatrix = handles.matrix;
                    renderHorizon = MathExtensions.FindCircleHorizon(handleMatrix, definition.bottomDiameterX, bottomPoint, -normal, out bottomPointB, out bottomPointA);
                    renderHorizon = MathExtensions.FindCircleHorizon(handleMatrix, definition.topDiameterX,    topPoint,     normal, out topPointA,    out topPointB) && renderHorizon;

                    if (renderHorizon && definition.bottomDiameterX != definition.topDiameterX)
                    {
                        renderHorizon = !(MathExtensions.PointInCameraCircle(handleMatrix, bottomPointA, definition.topDiameterX,    topPoint,     normal) ||
                                          MathExtensions.PointInCameraCircle(handleMatrix, topPointA,    definition.bottomDiameterX, bottomPoint, -normal) ||
                                          MathExtensions.PointInCameraCircle(handleMatrix, bottomPointB, definition.topDiameterX,    topPoint,     normal) ||
                                          MathExtensions.PointInCameraCircle(handleMatrix, topPointB,    definition.bottomDiameterX, bottomPoint, -normal));
                    }
                }
                //*/

				if (!renderHorizon)
                {
                    bottomPointA = Vector3.zero;
                    topPointA = Vector3.zero;
                    bottomPointB = Vector3.zero;
                    topPointB = Vector3.zero;
                }

                verticalHandle1.From = bottomPointA;
                verticalHandle1.To   = topPointA;
                verticalHandle2.From = bottomPointB;
                verticalHandle2.To   = topPointB;
                
                fullTopCircleHandle.Center       = topPoint;
                fullBottomCircleHandle.Center    = bottomPoint;
                
                fullTopCircleHandle.DiameterX    = tempTopDiameterX;
                fullTopCircleHandle.DiameterZ    = tempTopDiameterZ;
                fullBottomCircleHandle.DiameterX = tempBottomDiameterX;
                fullBottomCircleHandle.DiameterZ = tempBottomDiameterZ;

                topHandle   .Origin = topPoint;
                bottomHandle.Origin = bottomPoint;


                fullTopCircleHandle.Normal = normal;
                fullBottomCircleHandle.Normal = -normal;
                // out of the cylinder at its ends, so from inside the part behind is fainter
                fullTopCircleHandle.Outward = normal;
                fullBottomCircleHandle.Outward = -normal;

                topHandle.Normal = normal;
                bottomHandle.Normal = -normal;

                
                if (definition.settings.isElliptical)
                {
                    if (bottomRadiusHandles == null || bottomRadiusHandles.Length != 4)
                    {
                        bottomRadiusHandles = new IChiselEllipsoidHandle[]
                        {
                            handles.CreateEllipsoidHandle(Vector3.zero, -normal, 0, 0, startAngle: +45f, angles: 90),        // left
                            handles.CreateEllipsoidHandle(Vector3.zero, -normal, 0, 0, startAngle: +45f + 180f, angles: 90), // right
                            handles.CreateEllipsoidHandle(Vector3.zero, -normal, 0, 0, startAngle: -45f, angles: 90),        // forward
                            handles.CreateEllipsoidHandle(Vector3.zero, -normal, 0, 0, startAngle: -45f + 180f, angles: 90), // back
                        };
                    }

                    if (topRadiusHandles == null || topRadiusHandles.Length != 4)
                    {
                        topRadiusHandles = new IChiselEllipsoidHandle[]
                        {
                            handles.CreateEllipsoidHandle(Vector3.zero, normal, 0, 0, startAngle: +45f, angles: 90),          // left
                            handles.CreateEllipsoidHandle(Vector3.zero, normal, 0, 0, startAngle: +45f + 180f, angles: 90),   // right
                            handles.CreateEllipsoidHandle(Vector3.zero, normal, 0, 0, startAngle: -45f, angles: 90),          // forward
                            handles.CreateEllipsoidHandle(Vector3.zero, normal, 0, 0, startAngle: -45f + 180f, angles: 90),   // back
                        };
                    }

                    for (int i = 0; i < bottomRadiusHandles.Length; i++)
                    {
                        bottomRadiusHandles[i].Center = bottomPoint;
                        bottomRadiusHandles[i].Normal = -normal;
                        bottomRadiusHandles[i].Outward = -normal;
                        bottomRadiusHandles[i].DiameterX = tempBottomDiameterX;
                        bottomRadiusHandles[i].DiameterZ = tempBottomDiameterZ;
                        bottomRadiusHandles[i].Rotation = definition.settings.rotation;
                    }

                    for (int i = 0; i < topRadiusHandles.Length; i++)
                    {
                        topRadiusHandles[i].Center = topPoint;
                        topRadiusHandles[i].Normal = normal;
                        topRadiusHandles[i].Outward = normal;
                        topRadiusHandles[i].DiameterX = tempTopDiameterX;
                        topRadiusHandles[i].DiameterZ = tempTopDiameterZ;
                        topRadiusHandles[i].Rotation = definition.settings.rotation;
                    }

                    if (bottomHandles == null || bottomHandles.Length != 4)
                        bottomHandles   = new IChiselHandle[] { bottomHandle, bottomRadiusHandles[0], bottomRadiusHandles[1], bottomRadiusHandles[2], bottomRadiusHandles[3] };
                    if (definition.settings.type != CylinderShapeType.Cone)
                    {
                        if (topHandles == null || topHandles.Length != 5)
                            topHandles = new IChiselHandle[] { topHandle, topRadiusHandles[0], topRadiusHandles[1], topRadiusHandles[2], topRadiusHandles[3] };
                    } else
                    {
                        if (topHandles == null || topHandles.Length != 1)
                            topHandles = new IChiselHandle[] { topHandle };
                    }
                } else
                {
                    if (bottomRadiusHandles == null || bottomRadiusHandles.Length != 1) bottomRadiusHandles = new IChiselEllipsoidHandle[] { fullBottomCircleHandle };
                    if (topRadiusHandles == null || topRadiusHandles.Length    != 1) topRadiusHandles = new IChiselEllipsoidHandle[] { fullTopCircleHandle };

                    if (bottomHandles == null || bottomHandles.Length != 2) bottomHandles   = new IChiselHandle[] { bottomHandle, bottomRadiusHandles[0] };
                    if (definition.settings.type != CylinderShapeType.Cone)
                    {
                        if (topHandles == null || topHandles.Length != 2)
                            topHandles = new IChiselHandle[] { topHandle, topRadiusHandles[0] };
                    } else
                    {
                        if (topHandles == null || topHandles.Length != 1)
                            topHandles = new IChiselHandle[] { topHandle };
                    }
                }
            }
        }

        public override void OnEdit(IChiselHandles handles)
        {
            // Store our allocated handles in generatorState to avoid reallocating them every frame
            var cylinderHandles = handles.generatorState as CylinderHandles;
            if (cylinderHandles == null)
            {
                cylinderHandles = new CylinderHandles();
                cylinderHandles.Init(handles, this);
                handles.generatorState = cylinderHandles;
            }
            cylinderHandles.Update(handles, this);


            // Render vertical lines at the horizon of the cylinder
            handles.DoRenderHandles(new[] { cylinderHandles.verticalHandle1, cylinderHandles.verticalHandle2 });

            // round, they're dragged out or in to resize it: a frustum's two circles by as much
            if (!settings.isElliptical)
            {
                var change = 0.0f;
                foreach (var line in new[] { cylinderHandles.verticalHandle1, cylinderHandles.verticalHandle2 })
                {
                    if (line.From != line.To)
                        change += handles.DoHorizonLineHandle(line.From, line.To, Vector3.zero);
                }
                if (change != 0)
                {
                    settings.bottomDiameterX = math.max(0, settings.bottomDiameterX + change);
                    settings.bottomDiameterZ = settings.bottomDiameterX;
                    if (settings.type == CylinderShapeType.ConicalFrustum)
                    {
                        settings.topDiameterX = math.max(0, settings.topDiameterX + change);
                        settings.topDiameterZ = settings.topDiameterX;
                    }
                }
            }


            // Move the cylinder top/bottom up/down
            var prevModified = handles.modified;
            {
                handles.DoSlider1DHandle(ref cylinderHandles.bottomPoint, -cylinderHandles.normal, cylinderHandles.bottomHandles);
                var haveFocus = handles.lastHandleHadFocus;
                handles.DoSlider1DHandle(ref cylinderHandles.topPoint,     cylinderHandles.normal, cylinderHandles.topHandles);
                haveFocus = haveFocus || handles.lastHandleHadFocus;
                if (haveFocus)
                    handles.RenderDistanceMeasurement(cylinderHandles.topPoint, cylinderHandles.bottomPoint, math.abs(settings.height), "Height");
            }
            if (prevModified != handles.modified)
            {
                var topY    = Vector3.Dot(Vector3.up, cylinderHandles.topPoint);
                var bottomY = Vector3.Dot(Vector3.up, cylinderHandles.bottomPoint);
                // a drag that would make it flat isn't taken
                if (math.abs(topY - bottomY) >= ChiselGeneratorLimits.kMinSize)
                {
                    cylinderHandles.topY    = topY;
                    cylinderHandles.bottomY = bottomY;
                    settings.height         = topY - bottomY;
                    settings.bottomOffset   = bottomY;
                }
            }


            // Resize the top/bottom circle by grabbing and dragging it
            prevModified = handles.modified;
            {
                // Make the bottom circle draggable
                DraggableRadius(handles, ref settings.bottomDiameterX, ref settings.bottomDiameterZ, cylinderHandles.bottomPoint, -cylinderHandles.normal, cylinderHandles.bottomXVector, cylinderHandles.bottomZVector, cylinderHandles.bottomRadiusHandles, settings.isElliptical);
                // If we're a Cylinder, the top circle actually changes the bottom circle too
                if (settings.type == CylinderShapeType.Cylinder)
                    DraggableRadius(handles, ref settings.bottomDiameterX, ref settings.bottomDiameterZ, cylinderHandles.topPoint, cylinderHandles.normal, cylinderHandles.topXVector, cylinderHandles.topZVector, cylinderHandles.topRadiusHandles, settings.isElliptical);
                else
                // If we're a Conical Frustum, the top circle can be resized independently from the bottom circle
                if (settings.type == CylinderShapeType.ConicalFrustum)
                    DraggableRadius(handles, ref settings.topDiameterX, ref settings.topDiameterZ, cylinderHandles.topPoint, cylinderHandles.normal, cylinderHandles.topXVector, cylinderHandles.topZVector, cylinderHandles.topRadiusHandles, settings.isElliptical);
                // else; If we're a Cone, we ignore the top circle
            }
            if (prevModified != handles.modified)
            {
                // Ensure that when our shape is circular and we modify it, that when we convert back to an ellipsoid, it'll still be circular
                if (!settings.isElliptical)
                {
                    settings.topDiameterZ    = settings.topDiameterX;
                    settings.bottomDiameterZ = settings.bottomDiameterX;
                }
            }

            // round: its diameters, and its height just outside the horizon line on the right, as spiral stairs have them
            if (!settings.isElliptical)
            {
                // one for the two circles of the same size; a frustum's differ, so it shows both
                if (settings.type == CylinderShapeType.Cylinder)
                    handles.DoDiameterMeasurement(ref settings.bottomDiameterX, cylinderHandles.topPoint, cylinderHandles.bottomPoint);
                else
                {
                    handles.DoDiameterMeasurement(ref settings.bottomDiameterX, cylinderHandles.bottomPoint, isTop: false);
                    if (settings.type == CylinderShapeType.ConicalFrustum)
                        handles.DoDiameterMeasurement(ref settings.topDiameterX, cylinderHandles.topPoint, isTop: true);
                }
                settings.bottomDiameterZ = settings.bottomDiameterX;
                settings.topDiameterZ    = settings.topDiameterX;

                Vector3 from = cylinderHandles.verticalHandle1.From, to = cylinderHandles.verticalHandle1.To;
                var otherFrom = cylinderHandles.verticalHandle2.From;
                var otherTo   = cylinderHandles.verticalHandle2.To;
                var camera    = UnityEngine.Camera.current;
                if (camera != null && Vector3.Dot(handles.inverseMatrix.MultiplyVector(camera.transform.right), otherFrom - from) > 0)
                {
                    from = otherFrom;
                    to   = otherTo;
                }
                if (from != to)
                {
                    var typedHeight = math.abs(settings.height);
                    var outwards    = new Vector3(from.x, 0, from.z).normalized;
                    if (handles.DoDistanceMeasurement(ref typedHeight, from, new Vector3(from.x, to.y, from.z), "Change Height", offsetDirection: outwards, name: "Height"))
                        settings.height = (settings.height < 0 ? -1 : 1) * typedHeight;
                }
                return;
            }

            // the sizes of the box around it, as when it was placed
            var frustum  = settings.type == CylinderShapeType.ConicalFrustum;
            var sizeX    = math.max(math.abs(settings.bottomDiameterX), frustum ? math.abs(settings.topDiameterX) : 0);
            var sizeZ    = settings.isElliptical ? math.max(math.abs(settings.bottomDiameterZ), frustum ? math.abs(settings.topDiameterZ) : 0) : sizeX;
            var boxCenter = new Vector3(0, settings.bottomOffset + settings.height * 0.5f, 0);
            var box        = new Bounds(boxCenter, new Vector3(sizeX, math.abs(settings.height), sizeZ));
            var sizeBefore = box.size;
            if (handles.DoBoxMeasurements(ref box))
            {
                // a typed width scales both circles, so a cone or frustum keeps its shape; a typed height moves the top
                var size = box.size;
                if (size.x != sizeBefore.x)
                {
                    var scale = size.x / math.max(ChiselGeneratorLimits.kMinSize, sizeBefore.x);
                    settings.bottomDiameterX *= scale; settings.topDiameterX *= scale;
                    if (!settings.isElliptical) { settings.bottomDiameterZ = settings.bottomDiameterX; settings.topDiameterZ = settings.topDiameterX; }
                }
                if (size.z != sizeBefore.z)
                {
                    var scale = size.z / math.max(ChiselGeneratorLimits.kMinSize, sizeBefore.z);
                    settings.bottomDiameterZ *= scale; settings.topDiameterZ *= scale;
                    if (!settings.isElliptical) { settings.bottomDiameterX = settings.bottomDiameterZ; settings.topDiameterX = settings.topDiameterZ; }
                }
                if (size.y != sizeBefore.y)
                    settings.height = (settings.height < 0 ? -1 : 1) * size.y;
            }
        }
        #endregion
    }
}
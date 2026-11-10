using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using Unity.Entities;

namespace Chisel.Core
{
    [Serializable]
    public struct ChiselCapsule : IBrushGenerator, IChiselSurfaceNames
    {
        public const float	kDefaultDiameter			= 1.0f;
        public const float	kDefaultHemisphereRatio	    = 0.25f;
        public const float	kDefaultHemisphereHeight	= kDefaultDiameter * kDefaultHemisphereRatio;
        
        readonly static ChiselCapsule kDefaultSettings = new()
        {
            height				= 1.0f,
            topHeight			= kDefaultHemisphereHeight,
            bottomHeight		= kDefaultHemisphereHeight,
            offsetY             = 0,
            diameterX			= kDefaultDiameter,
            diameterZ			= kDefaultDiameter,
            rotation			= 0.0f,
            
            sides				= 8,
            topSegments			= 4,
            bottomSegments		= 4,
            smoothingGroup      = 1
        };
		public static ChiselCapsule DefaultSettings { get { return kDefaultSettings; } }

		public float    height;
        public float    topHeight;
        public float    bottomHeight;
        public float    offsetY;

        // across, as wide in both directions, or with a width and a depth of its own
        [Tooltip("Whether it can be wider one way than the other; otherwise its diameter is the same all round")]
        public bool     isElliptical;

        [ConditionalNamePart("{elliptical}", " X", "", nameof(isElliptical)),
            ConditionalName("Diameter{elliptical}"), DistanceValue]
        public float    diameterX;
        [ConditionalName("Diameter Z"), ConditionalHide(nameof(isElliptical)), DistanceValue]
        public float    diameterZ;
        public float    rotation;

        public int      sides;
        public int      topSegments;
        public int      bottomSegments;
        // the smoothing group of its rounded faces and sides, not its flat caps (0 for none)
        public uint     smoothingGroup;


        #region Properties
        const float kHeightEpsilon = 0.001f;

        public bool	        HaveRoundedTop		{ get { return topSegments > 0 && topHeight > kHeightEpsilon; } }
        public bool	        HaveRoundedBottom	{ get { return bottomSegments > 0 && bottomHeight > kHeightEpsilon; } }
        public bool	        HaveCylinder		{ get { return CylinderHeight > kHeightEpsilon; } }
        public float        CylinderHeight		{ get { return math.abs(height - (bottomHeight + topHeight)); } }


        internal int		BottomRingCount		{ get { return HaveRoundedBottom ? bottomSegments : 1; } }
        internal int		TopRingCount		{ get { return HaveRoundedTop    ? topSegments    : 1; } }
        internal int		RingCount			{ get { return BottomRingCount + TopRingCount - (HaveCylinder ? 0 : 1); } }
        internal int		Segments            { get { return math.max(1, RingCount); } }


        internal int		ExtraVertexCount	{ get { return ((HaveRoundedTop) ? 1 : 0) + ((HaveRoundedBottom) ? 1 : 0); } }
        internal int		BottomVertex		{ get { return (0); } }
        internal int		TopVertex			{ get { return (HaveRoundedBottom) ? 1 : 0; } }


        internal int		VertexCount			{ get { return (sides * RingCount) + ExtraVertexCount; } }


        internal float	    TopOffset			{ get { if (height < 0) return -topHeight; return height - topHeight; } }

        internal float	    BottomOffset		{ get { if (height < 0) return height + bottomHeight; return bottomHeight; } }

        internal int		TopVertexOffset		{ get { return ExtraVertexCount + ((TopRingCount - 1) * sides); } }
        internal int		BottomVertexOffset	{ get { return ExtraVertexCount + ((RingCount - BottomRingCount) * sides); } }
        #endregion

        #region Generate
        public readonly BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, 
                                                                       Allocator allocator = Allocator.Persistent)// Indirect
		{
            if (!BrushMeshFactory.GenerateCapsule(in this,
                                                  in surfaceDefinitionBlob,
                                                  out var newBrushMesh,
                                                  allocator))// Indirect
				return default;
            return newBrushMesh;
        }
        #endregion

        #region Surfaces
        [BurstDiscard]
        public int RequiredSurfaceCount { get { return 2 + sides; } }

        // as a cylinder: its bottom, its top (each rounded or flat) and the sides of the upright band between them
        [BurstDiscard]
        public readonly string GetSurfaceName(int index)
        {
            if (index == 0) return ChiselSurfaceNames.Bottom;
            if (index == 1) return ChiselSurfaceNames.Top;
            return (index < 2 + sides) ? ChiselSurfaceNames.Side(index - 1) : null;
        }

        [BurstDiscard]
        public bool IsSurfaceUsed(int index) { return index < 2 || HaveCylinder; }

        [BurstDiscard]
        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition) { }
        #endregion

        #region Validation

        public const float kMinDiameter = 0.01f;

        [BurstDiscard]
        public bool Validate()
        {
            topHeight		= math.max(topHeight, 0);
            bottomHeight	= math.max(bottomHeight, 0);
            // a negative height stays, 0 is the smallest; the rounded ends shrink to fit in it (growing it instead would
            // move the side across from the one dragged)
            height			= ChiselGeneratorLimits.NotZero(height);
            var ends        = topHeight + bottomHeight;
            if (ends > math.abs(height))
            {
                var scale     = math.abs(height) / ends;
                topHeight    *= scale;
                bottomHeight *= scale;
            }

            diameterX		= math.max(math.abs(diameterX), kMinDiameter);
            diameterZ		= isElliptical ? math.max(math.abs(diameterZ), kMinDiameter) : diameterX;

            topSegments	    = math.max(topSegments, 0);
            bottomSegments	= math.max(bottomSegments, 0);
            sides			= math.max(sides, 3);
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
    public class ChiselCapsuleDefinition : SerializedBrushGenerator<ChiselCapsule>
    {
        public const string kNodeTypeName = "Capsule";

        //[NamedItems(overflow = "Side {0}")]
        //public ChiselSurfaceDefinition  surfaceDefinition;

        
        #region OnEdit
        //
        //


        const float kLineDash					= 2.0f;
        const float kVertLineThickness			= 0.75f;
        const float kHorzLineThickness			= 1.0f;
        const float kCapLineThickness			= 2.0f;
        const float kCapLineThicknessSelected   = 2.5f;

        static Color Faint(Color color) { color.a *= 0.3f; return color; }

        // The loop at an end, side by side: seen from inside the capsule, the sides behind where its outline turns fainter
        static void DrawEndLoop(IChiselHandles handles, Vector3[] vertices, int offset, int sides, Vector3 center, Vector3 outward, Color baseColor, bool hasFocus, float thickness)
        {
            for (int j = sides - 1, i = 0; i < sides; j = i, i++)
            {
                var from   = vertices[offset + j];
                var to     = vertices[offset + i];
                var behind = handles.IsEdgeBehind(from, to, center, outward);
                var hidden = handles.GetStateColor(baseColor, hasFocus, true);
                var shown  = handles.GetStateColor(baseColor, hasFocus, false);
                handles.color = behind ? Faint(hidden) : hidden;
                handles.DrawLine(from, to, lineMode: LineMode.NoZTest, thickness: thickness);
                handles.color = behind ? Faint(shown) : shown;
                handles.DrawLine(from, to, lineMode: LineMode.ZTest, thickness: thickness);
            }
        }

        static void DrawOutline(IChiselHandleRenderer renderer, ChiselCapsuleDefinition definition, Vector3[] vertices, LineMode lineMode)
        {
            var sides			    = definition.settings.sides;
            
            var topHemisphere		= definition.settings.HaveRoundedTop;
            var bottomHemisphere	= definition.settings.HaveRoundedBottom;
            var topSegments			= topHemisphere    ? definition.settings.topSegments    : 0;
            var bottomSegments		= bottomHemisphere ? definition.settings.bottomSegments : 0;
            
            var extraVertices		= definition.settings.ExtraVertexCount;
            var bottomVertex		= definition.settings.BottomVertex;
            var topVertex			= definition.settings.TopVertex;
            
            var rings				= definition.settings.RingCount;
            var bottomRing			= (bottomHemisphere) ? (rings - bottomSegments) : rings - 1;
            var topRing				= (topHemisphere   ) ? (topSegments - 1) : 0;

            var prevColor = renderer.color;
            var color = prevColor;
            color.a *= 0.6f;

            for (int i = 0, j = extraVertices; i < rings; i++, j += sides)
            {
                if ((!definition.settings.HaveRoundedTop && i == topRing) ||
                    (!definition.settings.HaveRoundedBottom && i == bottomRing))
                    continue;
                bool isCapRing = (i == topRing || i == bottomRing);
                if (isCapRing)
                    continue;
                renderer.color = (isCapRing ? prevColor : color);
                renderer.DrawLineLoop(vertices, j, sides, lineMode: lineMode, thickness: (isCapRing ? kCapLineThickness : kHorzLineThickness), dashSize: (isCapRing ? 0 : kLineDash));
            }

            renderer.color = color;
            for (int k = 0; k < sides; k++)
            {
                if (topHemisphere)
                    renderer.DrawLine(vertices[topVertex], vertices[extraVertices + k], lineMode: lineMode, thickness: kVertLineThickness);
                for (int i = 0, j = extraVertices; i < rings - 1; i++, j += sides)
                    renderer.DrawLine(vertices[j + k], vertices[j + k + sides], lineMode: lineMode, thickness: kVertLineThickness);
                if (bottomHemisphere)
                    renderer.DrawLine(vertices[bottomVertex], vertices[extraVertices + k + ((rings - 1) * sides)], lineMode: lineMode, thickness: kVertLineThickness);
            }
            renderer.color = prevColor;
        }

        static Vector3[] vertices = null;
        
        public override void OnEdit(IChiselHandles handles)
        {
            var baseColor		= handles.color;
            var normal			= Vector3.up;


            if (BrushMeshFactory.GenerateCapsuleVertices(ref settings, ref vertices))
            {
                handles.color = handles.GetStateColor(baseColor, false, false);
                DrawOutline(handles, this, vertices, lineMode: LineMode.ZTest);

                handles.color = handles.GetStateColor(baseColor, false, true);
                DrawOutline(handles, this, vertices, lineMode: LineMode.NoZTest);

                handles.color = baseColor;
            }

            var topPoint	= normal * (settings.offsetY + settings.height);
            var bottomPoint = normal * (settings.offsetY);
            var middlePoint	= normal * (settings.offsetY + (settings.height * 0.5f));
            var radius2D	= new float2(settings.diameterX, settings.diameterZ) * 0.5f;

            var topHeight       = settings.topHeight;
            var bottomHeight    = settings.bottomHeight;

            // at a negative height the top is below the bottom: the ends are dragged the other way
            var heightSign      = settings.height < 0 ? -1.0f : 1.0f;
            var maxTopHeight    = math.abs(settings.height) - bottomHeight;
            var maxBottomHeight = math.abs(settings.height) - topHeight;

            if (settings.height < 0)
                normal = -normal;

            var prevModified = handles.modified;
            {
                handles.color = baseColor;
                // round, its radius is dragged; elliptical, its width and depth are, with the box around it below
                if (!settings.isElliptical)
                {
                    var radius2Dx = radius2D.x;
                    handles.DoRadiusHandle(ref radius2Dx, normal, middlePoint);

                    // the lines on the horizon of its round middle, from ring to ring, dragged out or in to resize it
                    var hasRings = vertices != null && vertices.Length > math.max(settings.TopVertexOffset, settings.BottomVertexOffset);
                    var lowY  = hasRings ? math.min(vertices[settings.TopVertexOffset].y, vertices[settings.BottomVertexOffset].y) : 0;
                    var highY = hasRings ? math.max(vertices[settings.TopVertexOffset].y, vertices[settings.BottomVertexOffset].y) : 0;
                    var axis  = new Vector3(middlePoint.x, 0, middlePoint.z);
                    if (highY > lowY &&
                        handles.TryGetHorizonPoints(axis, radius2Dx, out var left, out var right))
                    {
                        foreach (var point in new[] { left, right })
                        {
                            var change = handles.DoHorizonLineHandle(new Vector3(point.x, lowY, point.z), new Vector3(point.x, highY, point.z), axis);
                            radius2Dx = math.max(0, radius2Dx + change * 0.5f);
                        }
                    }
                    radius2D.x = radius2Dx;
                }

                {
                    var isTopBackfaced = handles.IsSufaceBackFaced(topPoint, normal);
                    var topLoopHasFocus = false;
                    handles.backfaced = isTopBackfaced;
                    for (int j = settings.sides - 1, i = 0; i < settings.sides; j = i, i++)
                    {
                        var from = vertices[j + settings.TopVertexOffset];
                        var to = vertices[i + settings.TopVertexOffset];

                        handles.color = handles.IsEdgeBehind(from, to, topPoint, normal) ? Faint(baseColor) : baseColor;
                        if (handles.DoEdgeHandle1DOffset(out var edgeOffset, Chisel.Core.Axis.Y, from, to, renderLine: false))
                            topHeight = math.clamp(topHeight - edgeOffset * heightSign, 0, maxTopHeight);
                        topLoopHasFocus = topLoopHasFocus || handles.lastHandleHadFocus;
                    }


                    handles.color = baseColor;
                    handles.DoDirectionHandle(ref topPoint, normal);
                    var topHasFocus = handles.lastHandleHadFocus;
                    handles.backfaced = false;

                    topLoopHasFocus = topLoopHasFocus || (topHasFocus && !settings.HaveRoundedTop);

                    var thickness = topLoopHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                    DrawEndLoop(handles, vertices, settings.TopVertexOffset, settings.sides, topPoint, normal, baseColor, topLoopHasFocus, thickness);
                }
                
                {
                    var isBottomBackfaced	= handles.IsSufaceBackFaced(bottomPoint, -normal);
                    var bottomLoopHasFocus = false;
                    handles.backfaced = isBottomBackfaced;
                    for (int j = settings.sides - 1, i = 0; i < settings.sides; j = i, i++)
                    {
                        var from    = vertices[j + settings.BottomVertexOffset];
                        var to      = vertices[i + settings.BottomVertexOffset];

                        handles.color = handles.IsEdgeBehind(from, to, bottomPoint, -normal) ? Faint(baseColor) : baseColor;
                        if (handles.DoEdgeHandle1DOffset(out var edgeOffset, Chisel.Core.Axis.Y, from, to, renderLine: false))
                            bottomHeight = math.clamp(bottomHeight + edgeOffset * heightSign, 0, maxBottomHeight);
                        bottomLoopHasFocus = bottomLoopHasFocus || handles.lastHandleHadFocus;
                    }

                    handles.color = baseColor;
                    handles.DoDirectionHandle(ref bottomPoint, -normal);
                    var bottomHasFocus = handles.lastHandleHadFocus;
                    handles.backfaced = false;

                    bottomLoopHasFocus = bottomLoopHasFocus || (bottomHasFocus && !settings.HaveRoundedBottom);

                    var thickness = bottomLoopHasFocus ? kCapLineThicknessSelected : kCapLineThickness;

                    DrawEndLoop(handles, vertices, settings.BottomVertexOffset, settings.sides, bottomPoint, -normal, baseColor, bottomLoopHasFocus, thickness);
                }
            }
            if (prevModified != handles.modified)
            {
                if (!settings.isElliptical)
                {
                    settings.diameterX  = radius2D.x * 2.0f;
                    settings.diameterZ  = radius2D.x * 2.0f;
                }
                // a drag that would make it flat isn't taken
                if (math.abs(topPoint.y - bottomPoint.y) >= ChiselGeneratorLimits.kMinSize)
                {
                    settings.height     = topPoint.y - bottomPoint.y;
                    settings.offsetY    = bottomPoint.y;
                }
                settings.topHeight      = topHeight;
                settings.bottomHeight   = bottomHeight;
            }

            // elliptical: the box around it, as a box's, its sides dragged and its sizes typed in
            if (settings.isElliptical)
            {
                var box = new MinMaxAABB
                {
                    Min = new float3(-math.abs(settings.diameterX) * 0.5f, math.min(settings.offsetY, settings.offsetY + settings.height), -math.abs(settings.diameterZ) * 0.5f),
                    Max = new float3( math.abs(settings.diameterX) * 0.5f, math.max(settings.offsetY, settings.offsetY + settings.height),  math.abs(settings.diameterZ) * 0.5f)
                };
                var boxBefore = box;
                var changed   = handles.DoBoundsHandle(ref box);
                changed       = handles.DoBoxMeasurements(ref box) || changed;
                var size      = box.Max - box.Min;
                if (changed && math.all(math.abs(size) >= ChiselGeneratorLimits.kMinSize))
                {
                    // it stays around its middle: a side moved is as far out on the other side
                    var sizeBefore = boxBefore.Max - boxBefore.Min;
                    if (size.x != sizeBefore.x) settings.diameterX = math.abs(size.x);
                    if (size.z != sizeBefore.z) settings.diameterZ = math.abs(size.z);
                    if (size.y != sizeBefore.y || box.Min.y != boxBefore.Min.y)
                    {
                        var sign = settings.height < 0 ? -1 : 1;
                        settings.height  = sign * math.abs(size.y);
                        settings.offsetY = sign < 0 ? math.max(box.Min.y, box.Max.y) : math.min(box.Min.y, box.Max.y);
                        // ends that no longer fit are made to
                        var scale = math.min(1, math.abs(size.y) / math.max(ChiselGeneratorLimits.kMinSize, settings.topHeight + settings.bottomHeight));
                        settings.topHeight    *= scale;
                        settings.bottomHeight *= scale;
                    }
                }
                return;
            }

            // as a cylinder: its diameter on one of its widest circles, where the rounded ends start, and its height along its
            // outline on the right; both can be typed in
            var low      = math.min(settings.offsetY, settings.offsetY + settings.height);
            var high      = math.max(settings.offsetY, settings.offsetY + settings.height);
            var diameter  = math.abs(settings.diameterX);
            var widestTop = Vector3.up * (high - (settings.height < 0 ? settings.bottomHeight : settings.topHeight));
            var widestLow = Vector3.up * (low  + (settings.height < 0 ? settings.topHeight : settings.bottomHeight));
            var diameterBefore = diameter;
            handles.DoDiameterMeasurement(ref diameter, widestTop, widestLow);
            if (diameter != diameterBefore)
            {
                settings.diameterX = diameter;
                settings.diameterZ = diameter;
            }

            var typedHeight = math.abs(settings.height);
            if (handles.DoHeightMeasurement(ref typedHeight, Vector3.up * low, Vector3.up * high, diameter * 0.5f))
            {
                settings.height = (settings.height < 0 ? -1 : 1) * typedHeight;
                // ends that no longer fit are made to
                var scale = math.min(1, typedHeight / math.max(ChiselGeneratorLimits.kMinSize, settings.topHeight + settings.bottomHeight));
                settings.topHeight    *= scale;
                settings.bottomHeight *= scale;
            }
        }
        #endregion
    }
}
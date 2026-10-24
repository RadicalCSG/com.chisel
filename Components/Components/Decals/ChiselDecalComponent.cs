using System;
using System.Collections.Generic;
using Chisel.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>A surface a decal is limited to: a surface of a brush, as its surface array numbers them.</summary>
    [Serializable]
    public struct ChiselDecalSurfaceTarget
    {
        public const int kAllSurfaces = ChiselDecalTarget.kAllSurfaces;

        /// <summary>The component that makes the brush, such as a <see cref="ChiselBrushComponent"/>.</summary>
        public ChiselNodeComponent brush;

        /// <summary>The brush's surface, or <see cref="kAllSurfaces"/> for all of them.</summary>
        public int surfaceIndex;
    }

    /// <summary>What a decal covers: a box it projects through, or the edge where two surfaces meet</summary>
    public enum ChiselDecalShape : byte
    {
        Box,
        /// <summary>On the edge between its first and second target: the image's lower half on the first, its upper half on the second</summary>
        Trim
    }

    /// <summary>How a trim decal fills its edge</summary>
    public enum ChiselTrimFill : byte
    {
        /// <summary>One image over the whole edge</summary>
        Stretch,
        /// <summary>The image repeated at its length from the edge's start, the last one cut short</summary>
        Repeat,
        /// <summary>The whole number of images nearest to what fits, stretched to fill the edge</summary>
        Fit,
        /// <summary>One image at its length, round the decal's position on the edge</summary>
        Single
    }

    /// <summary>
    /// A decal: a box that draws its material onto every surface of a Chisel model it reaches, except surfaces that
    /// face away from the box's center. The image is projected along the box's forward (+Z) axis. A transparent
    /// decal is drawn over the surfaces; an opaque one replaces them where it covers them.
    /// </summary>
    /// <remarks>
    /// The decal belongs to the model it is a child of. A decal outside any model draws on every model in its
    /// scene.
    /// </remarks>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("Chisel/Decal")]
    public sealed class ChiselDecalComponent : MonoBehaviour
    {
        public const string kNodeTypeName = "Decal";

        [SerializeField] Material material;
        [SerializeField] ChiselDecalSettings settings = ChiselDecalSettings.Default;
        // Empty: every surface the decal reaches. Otherwise only these, and nothing when none of them exists.
        [SerializeField] List<ChiselDecalSurfaceTarget> targets = new();
        [SerializeField] ChiselDecalShape shape = ChiselDecalShape.Box;
        [SerializeField] ChiselTrimFill trimFill = ChiselTrimFill.Repeat;
        [SerializeField] bool trimMirror = false;
        [SerializeField] int imageTile = 0;
        [SerializeField] Vector2 repeat = Vector2.one;

        public const string kMaterialName = nameof(material);
        public const string kSettingsName = nameof(settings);
        public const string kTargetsName  = nameof(targets);
        public const string kShapeName      = nameof(shape);
        public const string kTrimFillName   = nameof(trimFill);
        public const string kTrimMirrorName = nameof(trimMirror);
        public const string kImageTileName  = nameof(imageTile);
        public const string kRepeatName     = nameof(repeat);

        /// <summary>A box decal projects through its box; a trim decal lies on the edge between its first two targets
        /// (size.x: the image's length along the edge, size.y: its height across both surfaces), and its transform isn't used.</summary>
        public ChiselDecalShape Shape
        {
            get => shape;
            set { if (shape == value) return; shape = value; ChiselDecalManager.SetDirty(); }
        }

        /// <summary>How a trim decal fills its edge: the whole edge (Stretch, Repeat, Fit), or one image at the decal (Single)</summary>
        public ChiselTrimFill TrimFill
        {
            get => trimFill;
            set { if (trimFill == value) return; trimFill = value; ChiselDecalManager.SetDirty(); }
        }

        /// <summary>The tile of the material's hotspot layout the image is (a trim sheet's image), 0 for the whole texture</summary>
        public int ImageTile
        {
            get => imageTile;
            set { if (imageTile == value) return; imageTile = value; ChiselDecalManager.SetDirty(); }
        }

        /// <summary>The part of the texture the image is, in texture coordinates: all of it, or its tile's bounds</summary>
        public Rect ImageRect => ImageRectOf(material, imageTile);

        /// <summary>A tile's bounds in a material's hotspot layout, in texture coordinates; the whole texture without one</summary>
        public static Rect ImageRectOf(Material material, int tile)
        {
            var whole = new Rect(0, 0, 1, 1);
            if (tile == 0 || material == null)
                return whole;
            var metadata = material.GetMetadataOfType<ChiselSurfaceMetadata>();
            var layout   = metadata != null ? metadata.hotspotLayout : null;
            if (layout == null || layout.size.x <= 0 || layout.size.y <= 0)
                return whole;
            var index = layout.IndexOfTile(tile);
            var points = index >= 0 ? layout.tiles[index].points : null;
            if (points == null || points.Length == 0)
                return whole;
            var min = points[0];
            var max = points[0];
            foreach (var point in points)
            {
                min = Vector2Int.Min(min, point);
                max = Vector2Int.Max(max, point);
            }
            // the layout's rows run down from the top, texture coordinates up from the bottom
            var size = (Vector2)layout.size;
            return new Rect(min.x / size.x, (size.y - max.y) / size.y, (max.x - min.x) / size.x, (max.y - min.y) / size.y);
        }

        /// <summary>How many times a box decal's image repeats across its box, in x and y (an image in a trim sheet is a tile per image)</summary>
        public Vector2 Repeat
        {
            get => repeat;
            set { if (repeat == value) return; repeat = value; ChiselDecalManager.SetDirty(); }
        }

        /// <summary>The image's lower half on both surfaces, mirrored across the edge, instead of its upper half on the second</summary>
        public bool TrimMirror
        {
            get => trimMirror;
            set { if (trimMirror == value) return; trimMirror = value; ChiselDecalManager.SetDirty(); }
        }

        /// <summary>A box decal: the first is the surface it is limited to (none: every surface it reaches). A trim decal: pairs of surfaces, an edge each.</summary>
        public IReadOnlyList<ChiselDecalSurfaceTarget> Targets => targets;

        public void SetTargets(IEnumerable<ChiselDecalSurfaceTarget> newTargets)
        {
            targets.Clear();
            if (newTargets != null)
                targets.AddRange(newTargets);
            ChiselDecalManager.SetDirty();
        }

        public Material Material
        {
            get => material;
            set { if (material == value) return; material = value; ChiselDecalManager.SetDirty(); }
        }

        public ChiselDecalSettings Settings
        {
            get => settings;
            set { if (settings.Equals(value)) return; settings = value; ChiselDecalManager.SetDirty(); }
        }

        /// <summary>The box's size: the image's width and height, and how deep the decal reaches.</summary>
        public Vector3 Size
        {
            get => settings.size;
            set { var s = settings; s.size = value; Settings = s; }
        }

        /// <summary>The box's center, in the decal's own space.</summary>
        public Vector3 Center
        {
            get => settings.center;
            set { var s = settings; s.center = value; Settings = s; }
        }

        public ChiselDecalProjection Projection
        {
            get => settings.projection;
            set { var s = settings; s.projection = value; Settings = s; }
        }

        public float FieldOfView
        {
            get => settings.fieldOfView;
            set { var s = settings; s.fieldOfView = value; Settings = s; }
        }

        /// <summary>Whether the surfaces below stay (transparent) or are replaced where the decal covers them.</summary>
        public bool Transparent
        {
            get => settings.transparent;
            set { var s = settings; s.transparent = value; Settings = s; }
        }

        public float MaxAngle
        {
            get => settings.maxAngle;
            set { var s = settings; s.maxAngle = value; Settings = s; }
        }

        public float SurfaceOffset
        {
            get => settings.surfaceOffset;
            set { var s = settings; s.surfaceOffset = value; Settings = s; }
        }

        public Vector2 UVScale
        {
            get => settings.uvScale;
            set { var s = settings; s.uvScale = value; Settings = s; }
        }

        public Vector2 UVOffset
        {
            get => settings.uvOffset;
            set { var s = settings; s.uvOffset = value; Settings = s; }
        }

        void Reset()
        {
            settings = ChiselDecalSettings.Default;
        }

        void OnEnable()                 { ChiselDecalManager.Register(this); }
        void OnDisable()                { ChiselDecalManager.Unregister(this); }
        void OnValidate()               { ChiselDecalManager.SetDirty(); }
        void OnTransformParentChanged() { ChiselDecalManager.SetDirty(); }

        // The corners of the volume in the decal's own space: the near face, then the far face, each counter
        // clockwise from its lower left.
        public void GetCorners(Vector3[] corners)
        {
            var size   = (float3)math.abs(settings.size);
            var center = settings.center;
            var half   = size * 0.5f;
            var nearZ  = center.z - half.z;
            var farZ   = center.z + half.z;
            float2 nearExtent = half.xy, farExtent = half.xy;
            if (settings.projection == ChiselDecalProjection.Perspective &&
                settings.fieldOfView >= ChiselDecalSettings.kMinFieldOfView &&
                settings.fieldOfView <= ChiselDecalSettings.kMaxFieldOfView)
            {
                var focal = half.y / math.tan(math.radians(settings.fieldOfView) * 0.5f);
                var apexZ = center.z - focal;
                nearZ = math.max(nearZ, apexZ + focal * 1e-3f);
                nearExtent = half.xy * ((nearZ - apexZ) / focal);
                farExtent  = half.xy * ((farZ - apexZ) / focal);
            }
            corners[0] = new Vector3(center.x - nearExtent.x, center.y - nearExtent.y, nearZ);
            corners[1] = new Vector3(center.x + nearExtent.x, center.y - nearExtent.y, nearZ);
            corners[2] = new Vector3(center.x + nearExtent.x, center.y + nearExtent.y, nearZ);
            corners[3] = new Vector3(center.x - nearExtent.x, center.y + nearExtent.y, nearZ);
            corners[4] = new Vector3(center.x - farExtent.x,  center.y - farExtent.y,  farZ);
            corners[5] = new Vector3(center.x + farExtent.x,  center.y - farExtent.y,  farZ);
            corners[6] = new Vector3(center.x + farExtent.x,  center.y + farExtent.y,  farZ);
            corners[7] = new Vector3(center.x - farExtent.x,  center.y + farExtent.y,  farZ);
        }

#if UNITY_EDITOR
        static readonly Vector3[] s_Corners = new Vector3[8];
        static readonly Color kGizmoColor = new Color(1.0f, 0.6f, 0.1f, 1.0f);

        // Only when selected: an imported map has thousands of decals. A trim decal's box means nothing: its editor
        // draws its outline instead
        void OnDrawGizmosSelected()
        {
            if (shape == ChiselDecalShape.Box)
                DrawVolume();
        }

        void DrawVolume()
        {
            Gizmos.color  = kGizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            GetCorners(s_Corners);
            for (int i = 0; i < 4; i++)
            {
                var next = (i + 1) % 4;
                Gizmos.DrawLine(s_Corners[i], s_Corners[next]);
                Gizmos.DrawLine(s_Corners[4 + i], s_Corners[4 + next]);
                Gizmos.DrawLine(s_Corners[i], s_Corners[4 + i]);
            }
            // Which way the image goes, and which way is up in it
            var center = (Vector3)settings.center;
            var depth  = math.abs(settings.size.z) * 0.5f;
            Gizmos.DrawLine(center, center + new Vector3(0, 0, depth));
            var height = math.abs(settings.size.y) * 0.5f;
            Gizmos.DrawLine(center, center + new Vector3(0, height * 0.5f, 0));
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}

using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Editors
{
    public enum HotspotDiagnosticMode
    {
        Off,
        // texture pixels per unit against the tile's: stretched red, squeezed blue
        Density,
        // how much more one way than the other
        Anisotropy,
        // symmetric Dirichlet energy: both together
        Distortion
    }

    // The hotspot diagnostic view (diagnostics): every drawn triangle of
    // a hotspot surface coloured by how its texture lies on it against its tile's density (HotspotDiagnostics.Measure),
    // triangles mirrored against the rest of their surface in magenta; the outlines of surfaces repeated as a last resort
    // and of surfaces that are all border; optionally the drawn triangles' edges and the seams where the uvs jump.
    public static class ChiselHotspotDiagnosticsView
    {
        public struct Summary
        {
            public int    models, surfaces, triangles, lastResort, allBorder, pinned, mirrored;
            public double leastDensity, mostDensity, mostAnisotropy, mostEnergy;
        }

        public static readonly Color kLastResortColor = new(1.0f, 0.55f, 0.0f, 1);
        public static readonly Color kAllBorderColor  = new(0.0f, 0.9f, 1.0f, 1);
        public static readonly Color kSeamColor       = new(1, 1, 1, 1);
        public static readonly Color kEdgeColor       = new(0, 0, 0, 0.6f);
        public static readonly Color kMirroredColor   = new(1, 0, 1, 0.75f);
        const float kAlpha = 0.55f;

        const string kModeKey  = "ChiselHotspotDiagnostics.Mode";
        const string kEdgesKey = "ChiselHotspotDiagnostics.Edges";
        const string kSeamsKey = "ChiselHotspotDiagnostics.Seams";

        public static HotspotDiagnosticMode Mode
        {
            get => (HotspotDiagnosticMode)EditorPrefs.GetInt(kModeKey, 0);
            set { if (value != Mode) { EditorPrefs.SetInt(kModeKey, (int)value); s_Dirty = true; SceneView.RepaintAll(); } }
        }
        public static bool ShowEdges
        {
            get => EditorPrefs.GetBool(kEdgesKey, false);
            set { if (value != ShowEdges) { EditorPrefs.SetBool(kEdgesKey, value); s_Dirty = true; SceneView.RepaintAll(); } }
        }
        public static bool ShowSeams
        {
            get => EditorPrefs.GetBool(kSeamsKey, true);
            set { if (value != ShowSeams) { EditorPrefs.SetBool(kSeamsKey, value); s_Dirty = true; SceneView.RepaintAll(); } }
        }

        public static Summary LastSummary { get; private set; }

        sealed class View
        {
            public Matrix4x4 matrix;
            public Mesh      triangles;
            public Mesh      lines;
        }

        static readonly List<View> s_Views = new();
        static bool s_Dirty = true;

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            ChiselModelManager.Instance.PostUpdateModels -= OnPostUpdateModels;
            ChiselModelManager.Instance.PostUpdateModels += OnPostUpdateModels;
            AssemblyReloadEvents.beforeAssemblyReload -= Clear;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
        }

        static void OnPostUpdateModels() { s_Dirty = true; }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (Mode == HotspotDiagnosticMode.Off || Event.current.type != EventType.Repaint)
                return;
            if (s_Dirty)
                Rebuild();
            var material = SceneHandleMaterialManager.ColoredPolygonMaterial;
            if (!material || !material.SetPass(0))
                return;
            foreach (var view in s_Views)
            {
                if (view.triangles) Graphics.DrawMeshNow(view.triangles, view.matrix);
                if (view.lines)     Graphics.DrawMeshNow(view.lines, view.matrix);
            }
        }

        static void Clear()
        {
            foreach (var view in s_Views)
            {
                if (view.triangles) Object.DestroyImmediate(view.triangles);
                if (view.lines)     Object.DestroyImmediate(view.lines);
            }
            s_Views.Clear();
            s_Dirty = true;
        }

        public static Color ColorOf(HotspotDiagnosticMode mode, double s1, double s2)
        {
            switch (mode)
            {
                case HotspotDiagnosticMode.Density:
                {
                    var v = math.clamp(math.log2(math.sqrt(s1 * s2)), -1, 1);
                    var c = v < 0 ? Color.Lerp(Color.green, Color.red, (float)-v) : Color.Lerp(Color.green, Color.blue, (float)v);
                    c.a = kAlpha;
                    return c;
                }
                case HotspotDiagnosticMode.Anisotropy:
                    return Ramp(math.log2(s2 > 0 ? s1 / s2 : double.PositiveInfinity));
                default:
                    return Ramp(0.5 * math.log2(HotspotDiagnostics.Energy(s1, s2)));
            }
        }

        // 0 green, 0.5 yellow, 1 and beyond red
        static Color Ramp(double v)
        {
            v = double.IsNaN(v) ? 1 : math.clamp(v, 0, 1);
            var c = v < 0.5 ? Color.Lerp(Color.green, Color.yellow, (float)(v * 2)) : Color.Lerp(Color.yellow, Color.red, (float)(v * 2 - 1));
            c.a = kAlpha;
            return c;
        }

        struct Triangle
        {
            public int    surface;     // index into the surfaces of the view
            public int    i0, i1, i2;  // mesh vertices
            public double s1, s2, area;
            public bool   mirrored;
        }

        static void Rebuild()
        {
            Clear();
            s_Dirty = false;
            var summary = new Summary { leastDensity = double.PositiveInfinity };
            var mode = Mode;
            if (mode == HotspotDiagnosticMode.Off)
            {
                LastSummary = summary;
                return;
            }
            var lookups = ChiselTreeLookup.Value;
            foreach (var model in ChiselModelManager.Instance.Models)
            {
                if (!model || !model.isActiveAndEnabled || model.generated?.renderables == null || !lookups.HasTree(model.Node))
                    continue;
                var data = lookups[model.Node];
                data.lastJobHandle.Complete();
                var buffers = data.brushRenderBufferLookup;
                if (!buffers.IsCreated)
                    continue;
                var counted = false;
                foreach (var renderable in model.generated.renderables)
                {
                    if (renderable == null || !renderable.Valid || renderable.debugVisualizationRenderer || !renderable.HasGeometry || !renderable.container)
                        continue;
                    var view = Build(renderable, buffers, mode, ref summary);
                    if (view == null)
                        continue;
                    s_Views.Add(view);
                    if (!counted) { summary.models++; counted = true; }
                }
            }
            if (summary.triangles == 0)
                summary.leastDensity = 0;
            LastSummary = summary;
        }

        static View Build(ChiselRenderObjects renderable, Unity.Collections.NativeParallelHashMap<CompactNodeID, Unity.Entities.BlobAssetReference<ChiselBrushRenderBuffer>> buffers,
                          HotspotDiagnosticMode mode, ref Summary summary)
        {
            var lookup = renderable.triangleBrushes;
            var selectionIDs = lookup?.perTriangleSelectionIDLookup;
            var descriptions = lookup?.selectionIndexDescriptions;
            if (selectionIDs == null || descriptions == null || selectionIDs.Length == 0 || descriptions.Length == 0)
                return null;
            var mesh = renderable.sharedMesh;
            var positions = mesh.vertices;
            var uvs = mesh.uv;
            if (uvs.Length != positions.Length)
                return null;

            // the hotspot surface of each selection id (-1: none, or a decal)
            var surfaceOf = new int[descriptions.Length];
            var infos = new List<HotspotSurfaceInfo>();
            var keys = new Dictionary<(CompactNodeID, int), int>();
            for (int d = 0; d < descriptions.Length; d++)
            {
                surfaceOf[d] = -1;
                var description = descriptions[d];
                if (!buffers.TryGetValue(description.brushNodeID, out var buffer) || !buffer.IsCreated)
                    continue;
                ref var surfaces = ref buffer.Value.surfaces;
                if (description.surfaceIndex < 0 || description.surfaceIndex >= surfaces.Length)
                    continue;
                var isDecal = false;
                for (int s = 0; s < surfaces.Length && !isDecal; s++)
                    isDecal = surfaces[s].decalEntityID != 0 && surfaces[s].decalEntityID == description.entityID;
                var info = surfaces[description.surfaceIndex].hotspot;
                if (isDecal || !info.IsMapped)
                    continue;
                var key = (description.brushNodeID, description.surfaceIndex);
                if (!keys.TryGetValue(key, out var index))
                {
                    index = infos.Count;
                    infos.Add(info);
                    keys.Add(key, index);
                }
                surfaceOf[d] = index;
            }
            if (infos.Count == 0)
                return null;

            // the measured triangles
            var triangles = new List<Triangle>();
            var indices = new List<int>();
            for (int subMesh = 0, n = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                mesh.GetTriangles(indices, subMesh, applyBaseVertex: true);
                for (int i = 0; i + 2 < indices.Count; i += 3, n++)
                {
                    if (n >= selectionIDs.Length)
                        break;
                    var id = selectionIDs[n];
                    if (id < 0 || id >= surfaceOf.Length || surfaceOf[id] < 0)
                        continue;
                    var info = infos[surfaceOf[id]];
                    int i0 = indices[i], i1 = indices[i + 1], i2 = indices[i + 2];
                    if (!HotspotDiagnostics.Measure(positions[i0], positions[i1], positions[i2], uvs[i0], uvs[i1], uvs[i2],
                                                    info.textureSize, info.pixelsPerUnit, out var s1, out var s2, out var mirrored))
                        continue;
                    var area = 0.5 * math.length(math.cross((double3)(float3)(positions[i1] - positions[i0]), (double3)(float3)(positions[i2] - positions[i0])));
                    triangles.Add(new Triangle { surface = surfaceOf[id], i0 = i0, i1 = i1, i2 = i2, s1 = s1, s2 = s2, area = area, mirrored = mirrored });
                }
            }
            if (triangles.Count == 0)
                return null;

            // mirrored against the rest of its surface: the way round that covers less of it
            var mirroredArea = new double[infos.Count];
            var totalArea = new double[infos.Count];
            foreach (var t in triangles)
            {
                totalArea[t.surface] += t.area;
                if (t.mirrored) mirroredArea[t.surface] += t.area;
            }

            var vertices = new List<Vector3>(triangles.Count * 3);
            var colors   = new List<Color>(triangles.Count * 3);
            var lineVertices = new List<Vector3>();
            var lineColors   = new List<Color>();
            var edges = new Dictionary<(Vector3, Vector3), (int surface, Vector2 uvA, Vector2 uvB, int uses, bool seam)>();
            foreach (var t in triangles)
            {
                var flipped = t.mirrored != (mirroredArea[t.surface] * 2 > totalArea[t.surface]);
                var color = flipped ? kMirroredColor : ColorOf(mode, t.s1, t.s2);
                vertices.Add(positions[t.i0]); vertices.Add(positions[t.i1]); vertices.Add(positions[t.i2]);
                colors.Add(color); colors.Add(color); colors.Add(color);
                summary.triangles++;
                summary.mirrored += flipped ? 1 : 0;
                var density = math.sqrt(t.s1 * t.s2);
                summary.leastDensity   = math.min(summary.leastDensity, density);
                summary.mostDensity    = math.max(summary.mostDensity, density);
                summary.mostAnisotropy = math.max(summary.mostAnisotropy, t.s2 > 0 ? t.s1 / t.s2 : double.PositiveInfinity);
                summary.mostEnergy     = math.max(summary.mostEnergy, HotspotDiagnostics.Energy(t.s1, t.s2));
                AddEdge(t.i0, t.i1); AddEdge(t.i1, t.i2); AddEdge(t.i2, t.i0);

                void AddEdge(int a, int b)
                {
                    var pa = positions[a]; var pb = positions[b];
                    var forward = Less(pa, pb);
                    var key = forward ? (pa, pb) : (pb, pa);
                    var ua = forward ? uvs[a] : uvs[b]; var ub = forward ? uvs[b] : uvs[a];
                    if (edges.TryGetValue(key, out var seen))
                    {
                        var seam = seen.seam || seen.uvA != ua || seen.uvB != ub;
                        edges[key] = (seen.surface == t.surface ? seen.surface : -1, seen.uvA, seen.uvB, seen.uses + 1, seam);
                    } else
                        edges.Add(key, (t.surface, ua, ub, 1, false));
                }
            }

            for (int s = 0; s < infos.Count; s++)
            {
                summary.surfaces++;
                if ((infos[s].flags & HotspotSurfaceFlags.LastResort) != 0) summary.lastResort++;
                if ((infos[s].flags & HotspotSurfaceFlags.AllBorder) != 0)  summary.allBorder++;
                if ((infos[s].flags & HotspotSurfaceFlags.Pinned) != 0)     summary.pinned++;
            }

            var showEdges = ShowEdges; var showSeams = ShowSeams;
            foreach (var (key, edge) in edges)
            {
                Color color;
                if (edge.uses == 1 && edge.surface >= 0 && (infos[edge.surface].flags & HotspotSurfaceFlags.LastResort) != 0)
                    color = kLastResortColor;
                else if (edge.uses == 1 && edge.surface >= 0 && (infos[edge.surface].flags & HotspotSurfaceFlags.AllBorder) != 0)
                    color = kAllBorderColor;
                else if (showSeams && edge.seam)
                    color = kSeamColor;
                else if (showEdges)
                    color = kEdgeColor;
                else
                    continue;
                lineVertices.Add(key.Item1); lineVertices.Add(key.Item2);
                lineColors.Add(color); lineColors.Add(color);
            }

            var view = new View { matrix = renderable.container.transform.localToWorldMatrix };
            view.triangles = new Mesh { name = "Hotspot diagnostics", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            view.triangles.SetVertices(vertices);
            view.triangles.SetColors(colors);
            var order = new int[vertices.Count];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            view.triangles.SetIndices(order, MeshTopology.Triangles, 0);
            if (lineVertices.Count > 0)
            {
                view.lines = new Mesh { name = "Hotspot diagnostic lines", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
                view.lines.SetVertices(lineVertices);
                view.lines.SetColors(lineColors);
                var lineOrder = new int[lineVertices.Count];
                for (int i = 0; i < lineOrder.Length; i++)
                    lineOrder[i] = i;
                view.lines.SetIndices(lineOrder, MeshTopology.Lines, 0);
            }
            return view;
        }

        static bool Less(Vector3 a, Vector3 b)
        {
            if (a.x != b.x) return a.x < b.x;
            if (a.y != b.y) return a.y < b.y;
            return a.z < b.z;
        }
    }
}

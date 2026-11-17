using System;

namespace Chisel.Editors
{
    // An icon in Chisel's Editor Resources/Icons folder, by its name: lowercase words joined by underscores, as its file
    // is named (CONTRIBUTING.md, Icons): "<name>.png", "d_<name>.png" for the dark skin, "<name> on.png" for its active
    // look. Chisel asks for icons only by these, so every icon it uses is one ChiselIconTests checks: the fixed ones are
    // listed here, and the tests go through the generators and toggle flags that name the others.
    public readonly struct ChiselIcon : IEquatable<ChiselIcon>
    {
        public readonly string name;

        internal ChiselIcon(string name) { this.name = name; }

        // A generator's placement tool's icon, by the tool's name in lowercase, its spaces as underscores
        public static ChiselIcon OfGenerator(string toolName) => new(toolName.Replace(' ', '_').ToLowerInvariant());

        // The icon a toggle flag of an enum names (ToggleFlagAttribute), or its member's name
        public static ChiselIcon OfToggleFlag(string name) => new(name);

        public bool Equals(ChiselIcon other) => string.Equals(name, other.name, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(object obj) => obj is ChiselIcon other && Equals(other);
        public override int GetHashCode() => name == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(name);
        public override string ToString() => name;

        // Operations
        public static readonly ChiselIcon Additive          = new("csg_additive");
        public static readonly ChiselIcon Subtractive       = new("csg_subtractive");
        public static readonly ChiselIcon Intersecting      = new("csg_intersecting");
        public static readonly ChiselIcon Model             = new("csg_model");

        // Chisel Global
        public static readonly ChiselIcon ConvertToBrush                = new("convert_to_brush");
        public static readonly ChiselIcon CenterPivotOnSelectionCenter  = new("center_pivot_on_selection_center");
        public static readonly ChiselIcon CenterPivotOnEach             = new("center_pivot_on_each");
        public static readonly ChiselIcon Rebuild                       = new("rebuild");

        // Chisel Snap Settings
        public static readonly ChiselIcon BoundsSnap        = new("bounds_snap");
        public static readonly ChiselIcon PivotSnap         = new("pivot_snap");
        public static readonly ChiselIcon EdgeSnap          = new("edge_snap");
        public static readonly ChiselIcon VertexSnap        = new("vertex_snap");
        public static readonly ChiselIcon SurfaceSnap       = new("surface_snap");
        public static readonly ChiselIcon UVGridSnap        = new("uv_grid_snap");
        public static readonly ChiselIcon UVEdgeSnap        = new("uv_edge_snap");
        public static readonly ChiselIcon UVVertexSnap      = new("uv_vertex_snap");
        public static readonly ChiselIcon Snapping          = new("snapping");

        // Tools
        public static readonly ChiselIcon PaintTool         = new("paint_tool");
        public static readonly ChiselIcon PaintPaint        = new("paint_paint");
        public static readonly ChiselIcon PaintFill         = new("paint_fill");
        public static readonly ChiselIcon PaintAdd          = new("paint_add");
        public static readonly ChiselIcon PaintSubtract     = new("paint_subtract");
        public static readonly ChiselIcon PaintSmooth       = new("paint_smooth");
        public static readonly ChiselIcon PaintErase        = new("paint_erase");
        public static readonly ChiselIcon SurfaceShapeTool  = new("free_draw");

        // Windows
        public static readonly ChiselIcon SurfaceBrowser    = new("box_surfaces");
        public static readonly ChiselIcon Add               = new("add");
        public static readonly ChiselIcon Warning           = new("warning");
    }
}

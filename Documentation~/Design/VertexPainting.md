# Vertex painting and shader painting profiles

Status, 2026-09-19: **researched / proposed**. This document surveys established vertex-painting workflows and
proposes a Chisel-specific design. No implementation described here exists yet.

## Goal

Chisel should support painting data onto generated surfaces in the Scene view. The shader assigned to a surface
determines what the data means and how the painting tool behaves. One shader might expose ordinary vertex color,
another might expose a signed height value, and another might use four normalized channels as material-layer weights.

The intended abstraction is therefore not merely a vertex-color brush. It is a **shader-driven vertex-data painter**:

- A shader painting profile describes the paintable values, their storage channels, ranges and brush behavior.
- Paint data belongs to Chisel source surfaces, not to transient generated Unity meshes.
- Generated render vertices receive the evaluated data whenever the CSG output is rebuilt.

## Existing approaches

### Unity Polybrush

Polybrush is the closest existing Unity precedent. It supports sculpting, smoothing, vertex-color painting, texture
blending and prefab scattering. Polybrush 1.2.1 is released for Unity 6.0:

- [Unity 6 Polybrush package](https://docs.unity3d.com/6000.0/Manual/com.unity.polybrush.html)
- [Polybrush painting modes](https://docs.unity.cn/Packages/com.unity.polybrush%401.1/manual/modes.html)

Its most relevant idea is shader metadata. For texture blending, metadata maps a shader feature to a mesh attribute
such as Color, Tangent or a UV channel, selects one component, supplies a numeric range and optionally places related
attributes into a group. Painting one attribute in a group can set the other attributes to their minima. This allows a
shader to define the meaning of generic mesh data instead of making the paint tool understand a particular shader.

- [Polybrush texture painting and shader metadata](https://docs.unity.cn/Packages/com.unity.polybrush%401.1/manual/modes_texture.html)
- [Polybrush vertex-color painting](https://docs.unity.cn/Packages/com.unity.polybrush%401.1/manual/modes_color.html)

Polybrush is useful as a UI and metadata reference, but it is not a complete persistence solution for Chisel.
Polybrush commonly applies changes to a mesh or to `MeshRenderer.additionalVertexStreams`. Chisel regenerates its
output meshes as its CSG changes. Unity documents additional vertex streams as serialized overrides suitable for
vertex painting, but Polybrush also documents a lightmapping issue that can clear the stream from the renderer.

- [Unity `MeshRenderer.additionalVertexStreams`](https://docs.unity3d.com/2023.2/ScriptReference/MeshRenderer-additionalVertexStreams.html)
- [Polybrush troubleshooting](https://docs.unity.cn/Packages/com.unity.polybrush%401.1/manual/faq.html)

### Blender

Blender stores vertex painting as a color attribute. Materials and renderers decide how to consume that attribute.
This is a useful conceptual separation: the stored data need not represent the final visible base color.

- [Blender vertex painting](https://docs.blender.org/manual/en/latest/sculpt_paint/vertex_paint/introduction.html)

### Unreal Engine

Unreal separates ordinary vertex-color painting from vertex-weight painting. It also distinguishes instance data from
data saved to the shared mesh asset, and supplies channel filters, fill, smooth, copy/paste, LOD propagation and
repair after topology changes.

- [Unreal Mesh Paint overview](https://dev.epicgames.com/documentation/unreal-engine/activating-and-using-mesh-paint-mode-in-unreal-engine)
- [Unreal Mesh Paint tools and settings](https://dev.epicgames.com/documentation/unreal-engine/mesh-paint-tool-reference-in-unreal-engine)
- [Unreal Paint Vertex Colors tool](https://dev.epicgames.com/documentation/unreal-engine/paint-vertex-colors-tool-in-unreal-engine)

The reusable lessons are explicit value semantics, direct channel visualization, a clear shared-versus-instance data
model, and utilities for maintaining or transferring painted data.

## Paint semantics

The profile must distinguish the following operations because their editors and blending rules differ.

| Kind | Typical shader use | Brush behavior |
| --- | --- | --- |
| Color | Tint, ambient color, damage color | RGBA picker; replace or mix |
| Independent scalar | Wetness, moss, dirt, emissive, dissolve | One channel; add, subtract or replace |
| Layer weights | Rock/grass/snow/mud blending | Paint one layer while clearing or renormalizing its group |
| Packed masks | R=wetness, G=dirt, B=damage, A=selection | Channel-isolated painting |
| Direction/vector | Wind, flow or anisotropy | Direction sampling or a vector-oriented brush |
| Shader displacement | Rendered vertex offset along a normal or axis | Signed scalar with a neutral value and bounds padding |
| Height-aware material blend | Painted influence combined with material height maps | Weight painting; does not alter geometry |
| Geometry sculpting | Actual CSG vertex movement | A separate geometry tool, not a shader paint mode |

"Paint height" is otherwise ambiguous. It can mean:

1. **Shader displacement:** a channel encodes a signed rendered offset, often with 0.5 as zero.
2. **Height-aware material blending:** the painted value is a layer weight; height maps produce the detailed boundary.
3. **Geometry deformation:** source brush geometry moves, affecting CSG, collision and topology.

Only the first two belong in shader painting profiles. Geometry deformation should remain a separate Chisel editing
operation.

## Proposed profile model

Create a shared `ChiselShaderPaintProfile : ScriptableObject` that references a `Shader`. A project-level registry
maps shader asset references to profiles. Do not key profiles by shader name: names are mutable and are not guaranteed
to be unique.

Each profile contains one or more named modes. For example:

```text
Shader: Environment/LayeredRock

Mode: Rock / Moss
Kind: NormalizedWeights
Channels:
  Rock -> Color.R
  Moss -> Color.G
Defaults: (1, 0)
Group policy: SumToOne

Mode: Wetness
Kind: Scalar
Target: Color.B
Range: 0..1
Default: 0
Erase: 0

Mode: Surface Offset
Kind: SignedScalar
Target: Color.A
Encoded range: 0..1
Physical range: -0.20m..+0.20m
Neutral: 0.5
Operations: Add, Subtract, Smooth, Flatten
```

A mode should describe:

- Stable identifier, display name, tooltip and optional icon.
- Semantic kind: color, scalar, signed scalar, normalized weights or vector.
- Vertex attribute and component.
- Default, paint and erase values.
- Encoded and physical ranges.
- Clamp or wrap behavior.
- Operations: Replace, Add, Subtract, Multiply, Min, Max and Smooth as appropriate.
- Group behavior: independent, exclusive or normalized.
- Default brush radius, strength, hardness, falloff and spacing.
- Preview style: color, grayscale, signed heatmap or isolated layer.
- Surface, facing and connectivity filters.
- Maximum rendered displacement for mesh-bounds expansion.
- A schema version for migration.

Profile resolution should be:

```text
Material override profile
        -> registered profile for material.shader
        -> unsupported-shader warning
```

A material override allows the same shader to use different physical ranges or conventions. Chisel already attaches
custom metadata to materials through `ChiselSurfaceMetadata`, so a paint-profile override can use the same mechanism.
A separate registry remains preferable for the default per-shader association because package shaders can be
read-only and Shader Graph imports may regenerate assets.

The profile editor must reject unintended channel overlap. Two modes claiming `Color.R` is an error unless that
sharing is explicitly declared.

## Vertex storage

The current `RenderVertex` contains position, normal, tangent and UV0. Its render vertex descriptors likewise expose
no color attribute. The first implementation should add a universal `Color32` / `UNorm8x4` attribute to every render
vertex.

Benefits:

- Four independent normalized paint channels.
- Four bytes per vertex.
- A single layout for all submeshes and materials in a Unity mesh.
- Direct support in conventional shaders and Shader Graph.

The current render vertex is 48 bytes, so a four-byte color increases that stream by roughly 8.3%. Unity documents
that colors supplied as `Color32` use four bytes while `Color` uses sixteen:

- [Unity `Mesh.SetColors`](https://docs.unity3d.com/6000.0/ScriptReference/Mesh.SetColors.html)

Physical values should normally be encoded by the profile. For example, a byte value of 0..255 can represent
-0.2..+0.2 metres. Add UV1/UV2 or another stream only when four channels or eight-bit precision demonstrably prove
insufficient.

Do not use tangent components as general paint storage. Chisel recomputes tangents and shaders legitimately need the
complete tangent for normal mapping.

## Persistence across CSG rebuilds

Paint data must not be stored only in generated vertex indices. Boolean operations can add, remove and reorder output
vertices at any time.

Persist paint against this source identity:

```text
Chisel node/brush ID
+ source surface index
+ position in the source surface's local 2D plane
```

The generated triangle lookup already maps an output triangle back to both its Chisel node and source surface. A
Scene-view ray hit can therefore identify the source surface that should receive the stroke.

For an initial implementation:

1. Store brush strokes per source surface in surface-local plane coordinates.
2. Initialize every paint channel from the active profile's defaults.
3. Replay/evaluate the strokes when generating that surface's `RenderVertex` values.
4. Evaluate Boolean-created intersection vertices exactly like any other vertex on the source surface.
5. Periodically compact older strokes into a baked per-surface field to prevent unbounded scene-file growth.

This makes the result deterministic across CSG rebuilds. A later implementation could replace stroke replay with a
sparse field or another baked representation without changing the profile abstraction.

Changing a material or shader creates a policy question when its new profile gives channels different meanings. The
tool should retain the raw data, warn about the profile change, and offer explicit Reset or Convert actions. It should
not silently reinterpret and overwrite the data.

## Painting tool

Implement the Scene-view interaction as a component-specific Unity `EditorTool`:

- [Unity `EditorTool`](https://docs.unity3d.com/6000.0/ScriptReference/EditorTools.EditorTool.html)
- [Unity `EditorTool.OnToolGUI`](https://docs.unity3d.com/6000.0/ScriptReference/EditorTools.EditorTool.OnToolGUI.html)

Initial controls:

- Radius, strength/flow, hardness, falloff and stroke spacing.
- Replace, Add, Subtract and Smooth where supported by the selected mode.
- Sample the value under the cursor.
- Fill source surface and fill selected model.
- A paint/erase shortcut and swappable paint/erase values.
- Profile-provided channel or layer selection.
- Raw-data visualization independent of the material's final appearance.
- Front-facing-only filtering.
- The hit source surface as the default scope.
- Optional connected-surface and normal-angle filters.
- One Undo record per stroke, not per mouse event.

A simple world-space sphere must not affect every nearby vertex by default. That leaks paint through thin walls and
onto unrelated adjacent surfaces. Start with the hit source surface and make crossing a boundary explicit.

Useful follow-up features include symmetry, channel copy/swap/invert, surface flood, selection masks, pressure input,
multi-object painting, and copying paint between compatible surfaces.

## Resolution and topology limits

Vertex-paint resolution is mesh resolution. A large planar Chisel polygon may contain only boundary vertices, so a
small painted spot cannot be represented; interpolation spreads it across the polygon.

Profiles should be able to state a recommended minimum vertex density or warn that a mode is unsuitable for the
current surface. Fine detail requires one of:

- Surface subdivision/tessellation.
- A texture or virtual-texture painting path.
- A lower-frequency vertex mask combined with high-frequency procedural or height-map detail in the shader.

Hard paint boundaries require split vertices. Chisel already produces surface-local render vertices, which helps
prevent unintended interpolation between unrelated faces.

## Shader displacement requirements

A displacement profile needs additional validation:

- Expand generated mesh bounds by the profile's maximum displacement or displaced geometry may be culled.
- Apply the same displacement in forward, depth, shadow and motion-vector passes where applicable.
- Make it clear that the collider and CSG geometry remain undisplaced.
- Specify the displacement direction: surface normal, object axis or world axis.
- Supply a well-defined neutral value and physical unit range.

If collision or Boolean geometry must follow the painted shape, use a geometry-sculpting workflow instead.

## Color-space rules

Treat stored paint channels as raw linear numbers. Do not silently perform sRGB conversion for masks, weights or
encoded signed values. A mode explicitly representing visible color may provide color-friendly editing, but its
storage conversion must be declared by the profile.

## Implementation order

1. Add `Color32` to `RenderVertex`, its descriptor and all render-vertex construction/copy paths.
2. Add shader paint profiles, a registry and profile validation.
3. Add scalar and color painting on one source surface with Undo and raw-channel preview.
4. Persist surface-local strokes and replay them during CSG output generation.
5. Add normalized layer groups, fill, smooth, sample and channel utilities.
6. Add signed displacement profiles, bounds expansion and shader validation.
7. Add extra vertex channels only in response to concrete profile requirements.
8. Consider texture painting, symmetry and cross-surface/geodesic brushes after the core persistence model is stable.

The central design rule is: **paint data belongs to Chisel surfaces; shader profiles define how that data is
interpreted and edited**. This provides shader-specific behavior without coupling authored data to unstable generated
meshes.

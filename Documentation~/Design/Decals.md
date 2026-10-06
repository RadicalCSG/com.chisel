# Decals

Status, 2026-09-17: **built**: the decal (core, component, inspector, tests), target lists (a decal can be limited to
chosen brush surfaces), and the Source import, which draws `info_overlay`, `infodecal` and `info_projecteddecal` with
Chisel decals (below). Hand-placed decals are the goal; the importer drives the development (Sander, 2026-09-17).

## What a decal is

Sander, 2026-09-16:

> we want to support decals in chisel, where we define a box, and all the surfaces that intersect with that brush,
> and are not backfacing relatively to its center, will get a decal at the intersection polygon of the box and the
> surface. the underlying surface will be either removed or kept. depending on if the decal is transparent or not.
> (for now just have a state on the decal component). the decal itself can be a perspective or orthographic
> projection with size (or something). it needs to be compatible with what vmf's do.

As built:

- A decal is a box in its own space (`ChiselDecalSettings.center`, `.size`), placed by its GameObject. It projects
  its image along its local +Z axis.
- A surface of the model takes the decal where the surface lies inside the box, unless the surface faces away from
  the box's center: `dot(surfaceNormal, center - surfacePoint) >= 0`, with a 0.1 mm tolerance.
- **Orthographic**: the image is `size.x` by `size.y` at every depth.
- **Perspective**: the image is `size.x` by `size.y` at the box's center plane and comes from a point behind it,
  `fieldOfView` (vertical) wide. The volume is that frustum, cut to the box's depth. Texture coordinates are exact at
  every vertex and linear in between (see Known limits).
- **Transparent** (default): the decal is drawn over the surface, lifted `surfaceOffset` (1 mm) along the normal.
- **Opaque**: the decal replaces the surface in the rendered mesh. The collider and the shadow caster keep the
  surface's full shape, because the decal's pieces take over the surface's shadow casting and collision.
- `order`: higher decals are drawn over lower ones. An opaque decal hides everything below it, including lower
  decals. A transparent decal is drawn over whatever is below it, and hidden by opaque decals above it.
- `maxAngle` (default 180, no limit): a surface only takes the decal when its normal is within this angle of the
  direction back to the projector. Unity's decal projectors default to no limit too.
- `uvScale`, `uvOffset`: the texture coordinate is `uvOffset + uvScale * p`, with p from (0,0) at the image's lower
  left to (1,1) at its upper right.
- **Targets** (default none): a list of brush surfaces, each a brush component plus the index of its surface in the
  brush's surface array (-1: all of that brush's surfaces). With a list, the decal draws only on those surfaces.
  Another surface in the box is left alone by a transparent decal, and cut but kept by an opaque one (see
  Architecture). A decal whose targets are all gone draws nothing.
- The decal's material decides only whether it is rendered and receives shadows (its `ChiselSurfaceMetadata`).
- Surfaces the CSG removed have no decal: decals are cut from the model's output, not from the brushes.

A decal belongs to the model it is a child of. A decal outside any model draws on every model in its scene. The
imported maps keep their decals in a "Decals" group beside the model.

## How Source places decals (research, 2026-09-17)

Read in Source SDK 2013 (public) and noclip.website's Source renderer (MIT), counted over the 660 Black Mesa VMFs.

| | count | maps | placed by |
|---|---|---|---|
| info_overlay | 13,622 | 184 | vbsp + engine |
| infodecal | 13,607 | 132 | engine, at map load |
| info_projecteddecal | 35 | 7 | client, when fired (all 35 are named) |

**info_overlay** (vbsp `overlay.cpp`, noclip `buildOverlay`):
- A quad in its own basis: `BasisOrigin`, `BasisU`, `BasisNormal`. vbsp keeps only whether `BasisV` agrees with
  `BasisNormal x BasisU`; all 48 overlays of bm_c0a0a have `V = N x U`.
- `uv0`..`uv3` are the quad's corners along U and V. 13,246 of 13,622 are rectangles centred on the origin in the
  standard corner order; the other 376 are mostly parallelograms from transformed overlays.
- Corner texture coordinates: uv0 = (StartU, StartV), uv1 = (StartU, EndV), uv2 = (EndU, EndV), uv3 = (EndU, StartV).
  Most common: (0,1,1,0) 8,067 times, (1,0,0,1) 4,047.
- Applied only to the faces its `sides` list names (every BSP fragment of them), whatever their distance: on
  bm_c0a0a the basis origin is up to 46 units from its face. Each face triangle is projected onto the overlay plane
  along `BasisNormal`, the quad is clipped to it, and the result is projected back.
- Drawn over the surface. `RenderOrder` 0..3 (1,232 overlays set it); `fademindist`/`fademaxdist`.

**infodecal** (server `world.cpp` `CDecal::StaticDecal`; the engine's placement is not public, noclip `buildDecal`):
- The server traces origin ± (5,5,5) only to choose the entity (world or a brush entity), then the engine places the
  decal at the origin.
- Size: the base texture's size times `$decalscale`.
- Every surface near the origin gets its own copy, oriented by the surface's normal: on floors and ceilings the
  image's u runs along world X; on walls its v runs down world Z (so the image stands upright).
- bm_c0a0a: 131 of 141 infodecals lie within 1 unit of faces of a single orientation, 9 near faces of 2 or 3
  orientations (often tool faces that aren't drawn), 1 near none.
- `angles` (3,989 in the corpus) is Hammer's and unused. Named infodecals (2) are applied when fired.

**info_projecteddecal** (`c_te_projecteddecal.cpp`): traces from the origin along its angles for `Distance`; if the
trace hits, the engine places a decal like an infodecal, centred at the END of the ray, not at the hit point.

## Importing Source decals (built)

The Source importer keeps each decal entity as data (`VmfDecal`, tagged EditorOnly) and draws it with Chisel decals
(`VmfChiselDecals`), placed by `VmfDecalPlacement` and limited by target lists to the faces Source draws on:
- **info_overlay** → one decal, limited to the drawn faces its `sides` key lists. The frame is vbsp's: `BasisU` kept,
  V = N × U turned to agree with `BasisV`, corner i at origin + U x + V y of `uv`i with texture coordinates
  (StartU, StartV), (StartU, EndV), (EndU, EndV), (EndU, StartV). The image's up is where Source's v is lowest, so
  Hammer's default (StartV 1, EndV 0) is upright; `uvScale`/`uvOffset` carry the rest, with
  `v_unity = 1 - v_source`. `transparent`, `order = RenderOrder`.
- **infodecal** → one decal per surface normal among the drawn faces whose plane is within 4 units of the point
  (DECAL_DISTANCE) and whose bounds reach the image, limited to those faces. Each is laid on by the engine's basis for
  that normal (R_DecalComputeBasis: on floors and ceilings the image's s runs along world X, on walls its t runs down
  world Z), centred on the point, sized as the base texture's pixels times `$decalscale` (read by vpktools now).
- **info_projecteddecal** → the same, at origin + Distance along its angles, where the client centres it.
- A named infodecal or projected decal is applied when fired, so its decals start inactive; so do hidden entities'.
- **Depth.** A decal's box reaches every target face with a 1 unit margin, and its center lies in front of every
  target face's plane (`VmfDecalPlacement.FrontDistance`), so the facing test accepts them all. An orthographic image
  doesn't change along its axis, so the box can move freely along it.
- **Faces.** `VmfBrushIndex` (built by the world import) knows, per solid, the brush the importer made and, per side,
  the brush surface that shows it and whether Source draws it. Side k shows as the polygon `CreateFromPlanes` made with
  `descriptionIndex` k, because a `ChiselBrushComponent` numbers its surfaces by polygon
  (`ChiselBrushDefinition.UpdateSurfaces`); a side whose plane made no polygon shows nowhere, and a displaced side
  isn't drawn by the brush.
- The Chisel decals go in a "Decals" group under the model their target brushes are in (one decal per model).
- The import log reports the counts: `Source map import (decals): ...`.

Measured on bm_c0a0a (`scan_decal_reach.py`, brush polygons rather than CSG output, so it over-counts hidden faces):
- **29 of the 45 visible overlays** would also reach visible faces their `sides` list doesn't name. A box alone can't
  reproduce overlays, so stage 2 started with a **per-decal target list** (built: brush + surface; a surface that
  isn't listed is never drawn on). The importer maps a listed side to the brush it built for the solid and to the
  polygon `CreateFromPlanes` made for that side (its `descriptionIndex` is the side's index at that point): a
  `ChiselBrushComponent` numbers its surfaces by polygon (`ChiselBrushDefinition.UpdateSurfaces`), so the surface
  index is that polygon's index, and a side that made no polygon has no surface.
- **Infodecals**: 107 of 141 reach visible faces of one orientation within 4 units, 30 reach two, 3 reach three, 1 none.

Not covered yet, and possibly needed for exactness:
- Parallelogram overlays (376): an affine image frame instead of a rectangle. They are drawn as the rectangle on
  their first edge, and counted in the import log.
- Overlay fade distances, and sorting transparent decals by `order` across materials.
- Blending: Source decal materials multiply (`DecalModulate`) or blend with the surface; that is the material
  importer's to convert, and it doesn't yet.
- Decals on displacements (the brush face is replaced by a mesh) and on props.
- A per-surface-orientation mode, the way Source lays an infodecal on each surface by that surface's normal. Hand-
  placed decals across corners would want it; the importer would then need one decal per infodecal.

## Architecture

**Data.** `ChiselDecalInstance` (entity id, decal-to-tree matrix, settings, material id, material flags, target
range) is what a tree receives: `CSGTree.SetDecals(decals, targets)` replaces all of a tree's decals at once.
A decal's `targetStart`/`targetCount` pick its `ChiselDecalTarget`s (brush entity id, surface index or -1 for all)
from `targets`; a count of 0 means no target list. `ChiselDecalStore` keeps them in the tree's
`ChiselTreeLookup.Data`, builds a `DecalVolume` for each (six unit planes in tree space, bounds, projection, target
range), and compares the new set with the old one by entity id, targets included (where they sit in the array
doesn't matter). A decal whose target range lies outside the array keeps the targets inside it, and draws nothing
when none are left. The old and new bounds of every decal that was added, removed or changed are kept until the
next update, and the tree is marked dirty. The volumes are kept in stacking order, with their targets packed in the
same order (`decalVolumeTargets`).

**Invalidation.** `FindDecalAffectedBrushesJob` runs right after `FindModifiedBrushesJob` and adds every brush whose
bounds overlap a changed decal's bounds to the rebuild list, without `NeedAllTouchingUpdated`: a decal changes what a
brush looks like, not what touches it. Moving a decal rebuilds the brushes it left and reached, and nothing else
(`DecalPipelineTests.MovingADecal_RebuildsOnlyTheBrushesItReaches`).

**Output.** `GenerateSurfaceTrianglesJob` finds, per brush, the decals whose volume overlaps the brush (once per
brush), then per surface the ones that meet the surface's plane, and whether each may draw there: a decal with targets only draws on a surface whose brush entity id
and `BaseSurface.descriptionIndex` (the surface's index in its brush's surface array) one of them names. Elsewhere a
transparent decal is left out, and an opaque one only cuts the surface (see below). Each remaining (surface, decal)
pair gets a slot after the brush's own surfaces in
`ChiselBrushRenderBuffer.surfaces`; `ChiselSurfaceRenderBuffer.baseSurfaceIndex` names the surface a slot was cut
from. After a surface is triangulated, `DecalSurfaceBuilder` cuts it:
- Opaque decals cut each triangle top first (`DecalClipping.Split`). Pieces inside a decal the surface takes belong
  to that decal; pieces an upper decal already owns stay with it. Opaque decals the surface doesn't take still cut it,
  so its edges keep the points its neighbours get.
- Transparent decals then clip every piece they are above (`DecalClipping.AddInside`) and add it, lifted, to their
  slot.
- What remains of the surface replaces it; texture coordinates and tangents are computed again.

Decal slots are ordinary surfaces: their material id (parameter 1) makes them their own sub-mesh; opaque ones keep
the surface's physics material (parameter 2) and flags; transparent ones are only rendered and receive shadows.
Clicking a decal selects it: a decal slot carries the decal's entity id (`ChiselSurfaceRenderBuffer.decalEntityID`),
which `SubMeshTriangleLookup` puts in the slot's selection description, with the brush surface below as its surface.
Scene ray casts look through a surface's decal slots where an opaque decal took the surface (`ChiselSceneQuery`).

**Cutting without cracks.** `DecalClipping.Split` puts new points on a triangle's edges only where the edge enters or
leaves the volume, computed from the edge alone with its ends in a fixed order. The triangle on the other side of the
edge makes the same points bit for bit, whichever brush it belongs to, so opaque decals leave no T-junctions where
the surface had none. Inside, the part in the volume is fanned from its centroid, and the ring around it is
triangulated from the existing points only (`TriangulateRing`), with every triangle checked; a failed split keeps the
triangle and draws the decal over it.

**Components.** `ChiselDecalComponent` holds the material, settings and targets (`ChiselDecalSurfaceTarget`: a brush
component and a surface index), and draws the volume as a gizmo while it is selected (an imported map has thousands).
`ChiselDecalManager.Update` runs before every `CompactHierarchyManager.Flush` in `ChiselModelManager.UpdateModels`. It
compares every decal's decal-to-model matrix and targets with what the model last received, and calls `SetDecals`
once per model that changed. A model whose tree doesn't hold as many decals as it was sent gets them again: a full
rebuild recreates every tree, and the new tree can get the old one's handle. Targets are resolved to entity ids (a
generator's brushes carry its component's) when the manager is dirty; a decal whose target brushes are all gone is
left out. `ChiselDecalEditor` gives the inspector, a box handle and GameObject/Chisel/Create Decal.

## Known limits

- Perspective texture coordinates are linear between vertices, so a wide perspective decal on a tilted surface
  bends slightly within large triangles.
- Transparent decals with different materials are drawn in sub-mesh order (material id), not by `order`.
- Decals don't reach surfaces of other models unless they are outside any model.
- Surface indices are generator specific: a box's top is 0 (`BoxSides`), a `ChiselBrushComponent`'s surfaces are its
  polygons. A target names that surface on every brush its component makes.
- Targets are resolved again only when the decal manager is dirty (a decal changed, or `SetDirty`). A decal whose
  target brush was deleted draws nothing there, but an opaque one keeps cutting the other surfaces in its box until
  then.

## Tests

Results, 2026-09-17 (editor): `DecalClippingTests` 21/21, `DecalPipelineTests` 13/13, `Chisel.Core.Tests` 666 run
(663 passed, 3 explicit skipped), `Chisel.Components.Tests` 6/6. A scripted check with a model, a box and a
`ChiselDecalComponent` saw the opaque decal replace half a square unit of the box (collider area unchanged), the
transparent one move to the shadow-receiving renderer, and the decal follow its GameObject away, back and out when
deleted. bm_c0a0a has no Chisel decals yet and rebuilds with the same 58,983 vertices and 31,966 triangles
(render hash 115094847299898 after the change). (That check's box was the default 1 m cube: `ChiselBoxComponent`'s
`Min`/`Max` setters don't change the box, see below.)

Results with target lists, 2026-09-17 (editor): `DecalClippingTests` 21/21, `DecalPipelineTests` 18/18,
`Chisel.Core.Tests` 671 run (668 passed, 3 explicit skipped), `Chisel.Components.Tests` 6/6. bm_c0a0a rebuilt with
the same 58,983 vertices, 31,966 triangles and render hash 115094847299898 before the change, after it and after
reopening the scene. A scripted check of targets through `ChiselDecalComponent` is still to do: the first one placed
its boxes with `ChiselBoxComponent.Min`/`Max`, which change nothing (`BoundsExtensions`' `Set*` methods take the
bounds by value), so both boxes were the default cube and it measured nothing. That check is now a test
(`ChiselDecalComponentTests`), and the setters are fixed.

Results with the Source import, 2026-09-17 (editor): the importer's tests 48/48 (`VmfDecalPlacementTests` 13),
vpktools 150/150, `Chisel.Components.Tests` 11/11 (`ChiselDecalComponentTests` 3), `Chisel.Core.Tests` 676 run (673
passed, 3 explicit skipped). bm_c0a0a's brushes and decals imported into a scratch scene: 48 overlays and 141 point
decals made 228 Chisel decals (225 active: the rest are named or hidden), 1 reaches no drawn face, 3 overlays aren't
rectangles. They draw 3,202 triangles over 1,353 m2; most of that is 31 long stain overlays, up to 2,418 by 128 units
with their texture repeated 10 to 15 times. A full rebuild took 1,361 ms without them and 1,365 ms with them.

- `DecalClippingTests` (pure, runs offline): the volume, both projections, the angle and facing tests, edge points
  (identical in both directions, bit for bit), and splitting: area kept, winding kept, pieces on the right side,
  neighbours sharing their edge points; 400 random decals and triangles. A stress probe (not committed) ran 39,546
  cases, 17,507 of them real splits, with no failure.
- `DecalPipelineTests` (the whole update, `ContentsTreeHarness`): transparent and opaque decals by area and by
  point coverage, the facing test through a thin wall, the angle limit, a decal over two brushes with the same points
  on both sides of the seam, stacking order, perspective, incremental moves and removal, unchanged sets, decals in
  empty space or on faces the CSG removed, and colliders keeping their area. Targets: a transparent decal limited to
  one of two brushes; an opaque one that covers only its target but still cuts the other brush, with the same points
  on both sides of the seam; one surface of a block (its top, or its front) against both without targets; targets
  that don't exist or lie outside the array; changed targets, and the same targets elsewhere in the array (no
  change).
- `ChiselDecalComponentTests` (components): an opaque decal over two floors, a full rebuild keeping it, and targets
  (one brush, one surface, a surface out of reach, a deleted brush before and after the manager looks again).
- `VmfDecalPlacementTests` (importer): overlay texture coordinates at all four corners against vbsp's corner order,
  for Hammer's placement, other texture ranges, a flipped `BasisV`, other corner orders, walls and slopes;
  parallelograms and degenerate input; the engine's decal basis on floors, ceilings and walls; point decals against the
  engine's texture formula; depth and facing; the Chisel transform; and side to surface mapping, on a real
  `CreateFromPlanes` brush too.

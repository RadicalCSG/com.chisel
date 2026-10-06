# Brush contents: solid, glass, water

Design plan. Decided 14 and 15 September 2026, implemented in core, components, editor and the Source importer
on 16 September (see [As built](#as-built)); the map gates are still open. The same plan with figures is at
https://claude.ai/code/artifact/9cd3c828-dc39-46ca-a184-6711fc8b6bf0.

Let a brush say what it is made of, so the CSG stops deleting faces that sit against a different
material. This document is the plan and the tests it needs. It was researched from the code, the VMF
files and an offline scan of the Black Mesa maps; nothing was compiled or run for it. Line numbers
refer to com.chisel `main` at `7494058`.

- **Example:** bm_c0a0a, func_detail 247407
- **Brushes:** Solid 247411 (metal post), Solid 247412 (glass pane)
- **Units:** Hammer units, 52.46 u = 1 m
- **Touches:** com.chisel Core, Components and Editor, and the Source importer

## What breaks today

In bm_c0a0a a glass pane 4 units thick, Solid 247412, ends flush against a metal frame post,
Solid 247411, at y = 8668. Both are additive brushes in one model, so Chisel treats the contact as
interior and deletes the post's metal face wherever the pane covers it: a 4 × 64 u strip out of an
8 × 64 u face. Half the face is gone, and the gap shows through the glass.

| Brush | x | y | z | Textured faces |
|---|---|---|---|---|
| Solid 247411, metal post | −176 … −168 | 8668 … 8672 | 100 … 164 at y = 8668 | x = −176 and y = 8668, METAL/CSREV_METALBEAM_01 |
| Solid 247412, glass pane | −174 … −170 | 8548 … 8668 | 100 … 164 | x = −174, GLASS/UNBREAKABLE; five nodraw sides |

The post's face at y = 8668 is 8 × 64 u = 512 u². Today Chisel draws 256 u² of it, the two 2-unit
strips either side of the pane. With contents it draws all 512 u².

For every brush, `CreateRoutingTableJob` builds a routing table over the brushes that touch it. The
pane's end face is a *ReverseAligned* partner of the post's face. In the two-brush case, a
*SelfAligned* surface meeting a ReverseAligned partner routes to *Inside* in the Additive table, and
`CleanUp` keeps only SelfAligned and SelfReverseAligned loops, so the strip is dropped. Nothing along
the way knows the pane is glass.

## Contents and rules

Contents is a project-wide list of types, kept in a ScriptableObject that maps each index to a
name. Solid is the first entry, index 0, and can't be removed, renamed or moved. Each brush stores
the index of exactly one entry, Solid by default, so existing scenes keep their meaning and a map
where every brush is Solid produces byte-identical geometry. The inspector shows the names in a
dropdown. Composites don't carry contents; only brushes do.

An example list:

| Index | Name | Note |
|---|---|---|
| 0 | Solid | Built in. Always first, can't be removed or renamed. |
| 1 | Glass | What the importer calls Source's window contents. |
| 2 | Grate | Fences and grates; as common as glass in Black Mesa. |
| 3 | Water | Rare in Black Mesa: 278 brushes. |

Is a face removed where it lies inside another brush? Rows are the face's type, columns the type of
the brush it lies in:

| Face of ↓ · inside → | Solid | Glass | Grate | Water |
|---|---|---|---|---|
| **Solid** | Removed (same type, as today) | Kept (the fix for the post) | Kept | Kept (walls stay visible underwater) |
| **Glass** | Removed (inside Solid) | Removed (same type) | Kept | Kept (glass against water) |
| **Grate** | Removed (inside Solid) | Kept | Removed (same type) | Kept |
| **Water** | Removed (inside Solid) | Kept (water against glass) | Kept | Removed (same type) |

- **Two rules.** A face inside a brush of its own type is removed, as today. A face inside Solid is
  removed, whatever its type. Every other interface between two types is kept, so glass against
  water shows both faces and solid walls stay visible through glass and water. None of this is a
  setting; only the list of types is.
- **Touching faces.** Faces that meet back to back follow the same rules. The post's face against
  the pane is kept, and the pane's end face against the post is removed. Glass touching water keeps
  both faces.
- **Coplanar ties.** When brushes of different types overlap and share a face plane facing the same
  way, one face survives. Solid always wins; between two other types, the earlier entry in the list
  wins.
- **Carving.** Subtractive and intersecting brushes, and every brush inside a subtractive or
  intersecting composite, carve every type alike. The walls a carve leaves belong to the carving
  brush's own type, Solid by default. Carve a pool into a floor, fill it with a water brush, and the
  pool walls stay.
- **One per brush.** A brush has exactly one type, never a combination: nothing is glass and water
  at once. The list holds up to 32 entries, plenty for a handful of types. A brush whose index is
  past the end of the list builds as Solid, and its inspector says so.

### Decisions, 14 and 15 September 2026

- **D1:** every interface between two types is kept; only Solid removes other types' faces. Glass
  against water shows both.
- **D2:** glass inside Solid loses those faces, and so does every other type.
- **D3:** the types are a configurable list, with Solid hardcoded as entry 0 and up to 32 entries.
- **D4:** brushes only. Composites don't carry contents.
- **D5:** the list is a ScriptableObject that maps each index to a name. Each brush stores its own
  index, and the inspector shows the names in a dropdown.

### Where the list lives

The list is a ScriptableObject asset (D5). `ChiselProjectSettings` can't hold it: it is an
editor-only singleton saved under `ProjectSettings/`, and a player build creates a fresh default
instance instead. Generated meshes are saved with the scene, so builds would still look right, but
a rebuild at runtime would see every brush as Solid. So the project settings point to the asset, and
the build adds it to its preloaded assets.

Brushes store indices, so renaming an entry is safe, but moving or removing one would change the
type of every brush that uses a later index. Entries are only renamed, appended, or removed from the
end.

## Where it plugs in

Each brush pair already gives every surface piece a category relative to the other brush: *Inside*,
*Aligned* or *ReverseAligned*. Contents only changes which category a piece carries into routing,
based on the two brushes' types. `CategoryIndex`, `CategoryRoutingRow` and the operation tables stay
exactly as they are.

Selected jobs, in the order `CSGManager.UpdateTreeMeshes` schedules them:

1. FindAllBrushIntersectionPairs
2. StoreBrushIntersections
3. FindBrushPairs
4. PrepareBrushPairIntersections: categories are born here; leave it alone
5. CreateIntersectionLoops
6. CreateRoutingTable: **hook 2**
7. PerformCSG: **hook 1**
8. GenerateSurfaceTriangles

Hook 1: the category a piece of brush A carries relative to brush B, when their types differ and B
isn't a carving brush:

| Computed today | Carried into routing |
|---|---|
| Inside | Inside if B is Solid, otherwise Outside |
| ReverseAligned | ReverseAligned if B is Solid, otherwise Outside |
| Aligned | Inside if B wins the tie (B is Solid, or neither is and B comes first in the list), otherwise Outside |

- **Hook 1, `PerformCSGJob`.** Where the intersection loops are read in, around lines 1218–1250,
  compare the two brushes' types and rewrite each loop's `interiorCategory` by the rules. An Outside
  piece routes the same as the uncut remainder, so the existing same-category check at line 1427
  skips the cut entirely.
- **Hook 2, `CreateRoutingTableJob.GetStackNodes`.** A brush lying wholly inside a brush of another
  type that isn't Solid gets `AllOutside` instead of `AllInside` (line 262). Optionally, drop such
  brushes from the table altogether when they have no aligned contact, which also saves work.
- **Carving brushes.** A carving brush is never rewritten as B. When the brush being processed is
  itself carving, its own type stands in for A.

### Why this is safe

- `PerformCSGJob` already ignores loops from brushes that aren't in the routing table
  (lines 1296–1303), so a brush that stops mattering is a path the job already takes.
- An Outside-only node changes nothing in additive composition: every row of the Additive table maps
  Outside to itself.
- With every brush Solid nothing is rewritten, so an all-solid map's geometry hash can't move.

### Why not earlier

- `PrepareBrushPairIntersectionsJob` (lines 552–558) is where categories are born, but
  `AddFullSurfaceLoopsForAlignedFaces` keys on Aligned and ReverseAligned to give a coplanar face its
  own loop. Rewriting there starves that fix, and the back-to-back duplicates return.
- Filtering `BrushesTouchedByBrush` would also cut vertex merging, T-junction insertion and the
  touching-brush invalidation in `InvalidateBrushesJob`, which all need physical contact.
- `CSGOperationType.Copy` indexes the fourth operation block, which is all 255 and asserted that way
  by `CategoryRoutingRowTests`. It isn't a working "keep interior" operation.

## Work by area

### Core: data

- Brushes store a contents index, an int with Solid = 0, on `CompactNode` and `CSGTreeBrush`.
  Changing it raises `NeedAllTouchingUpdated`, as `SetState` does for operation
  (CompactHierarchy.Internal.cs, line 1474).
- `GeneratedNode` carries it too, for generators that emit several brushes.
- `CompactTreeBuilder` copies each index into one byte, turning an index past the end of the list
  into 0 (Solid), and sets the carving flag when the brush or any ancestor is subtractive or
  intersecting.
- The list's entry count is checked on each update, and a change rebuilds every tree. Names don't
  affect the geometry, so renaming rebuilds nothing.

### Core: CSG

- Hook 1 in `PerformCSGJob`, hook 2 in `CreateRoutingTableJob`.
- Pass both jobs each brush's contents index and carving flag, by node order, where
  `CSGManager.UpdateTreeMeshes` schedules them (lines 2023 and 2056).
- No change to `CategoryIndex`, `CategoryRoutingRow` or the operation tables.

### Settings and editor

- A contents ScriptableObject asset holding the list, with Solid pinned first. Its editor renames
  entries, appends them, and removes them only from the end.
- The project settings point to the asset, and the build adds it to its preloaded assets.
- An int `contents` field on `ChiselGeneratorComponent` beside `operation`, pushed to the node
  everywhere operation is.
- A dropdown of the list's names next to the operation control in `ChiselNodeEditor`, storing the
  index, with an "Edit contents…" entry, undo, multi-selection, and a warning when the index is past
  the end of the list.

### Source importer

- `VMT.cs` skips `%compilewater`, `%compileslime`, `%compilepassbullets`, `%compileinvisible`,
  `%compilenonsolid` (lines 223–253) and `$alpha` (line 1074). Parse them.
- Derive side contents the way VBSP's `FindMiptex` does, and brush contents the way `BrushContents`
  does. Keep its order: tool materials never become windows, or about 11,000 more brushes would.
- Map Source's window, grate, water and slime to list entries by name, adding any that are missing,
  and set each `ChiselBrushComponent`'s index in both `VmfWorldConverter` loops. A Source brush can
  union several contents; Chisel keeps one, the highest-ranked (window, then grate, slime, water).

As built (16 September): `VMTContents` in vpktools reads the keys and applies FindMiptex, LoadSideCallback,
BrushContents and VisibleContents. It is a reader of its own, next to `VMT`, which it leaves unchanged: it follows
"patch" materials for these keys only, so imported materials don't change. `GameResources.GetMaterialContents`
caches it per material. `VmfBrushContents` in the importer maps window to Glass and keeps grate, slime and water
as they are. `ChiselContentsList.FindOrAppendInProject` then gives the index, adding the name to the project's
list (or creating the list) when needed. A typed brush is named after its type, "Glass 247412"; every other
brush keeps "Solid <id>".

### Output follow-ups

- Water seen from below: back faces, or double-sided materials. Source gives those faces
  `$bottommaterial`.
- Keep water surfaces out of the collider.
- Brush entities other than func_detail join the world model and cut world faces; VBSP never does
  that. In Black Mesa that is 32,790 solid and 7,102 non-solid brushes. Same symptom, separate fix.

### Tooling

- `Chisel DEBUG/Validate Output Meshes` judges all patches as one closed surface. Judge each type
  instead.
- Re-baseline bm_c0a0a's duplicate and non-manifold counts once imported maps carry contents.
- Build the test harness before the feature: scene description, oracle, generators and seed
  shrinker, proven on all-Solid scenes first. See Testing.

## Testing

No Core test runs the whole CSG today, and most CSG verification so far has been measurements taken by
hand in the editor. Contents gets a suite that decides what is correct without trusting the pipeline,
runs on scenes nobody hand-picked, and fails when the feature is switched off.

- **An oracle.** A short piece of plain C# that classifies points against the brushes decides which
  faces should exist. It shares no code with the routing tables, so it can't share their mistakes.
- **Generated scenes.** Random scenes from fixed seeds, on top of the hand-written cases. Contents
  bugs live in contacts and overlaps nobody thinks to draw, and the core has to hold for any
  geometry, not only imported maps.
- **Properties.** Relations between two builds that must hold even without an oracle, such as every
  brush sharing one type reproducing today's geometry.
- **Controls.** Every case also runs with every brush Solid and must match today's output, and the
  suite runs once with the rewrite switched off and must fail. A test that passes either way proves
  nothing.

### The oracle

1. Describe each scene once, as brushes given by planes with an operation and a contents index, inside
   nested composites. Build both the Chisel tree and the oracle from that description.
2. To judge a face of brush X, set aside every non-carving brush of another type that isn't Solid; a
   carving X uses its own type. Evaluate the rest left to right the way Chisel does: additive adds,
   subtractive removes, intersecting keeps the overlap.
3. At a sample point on the face, test one point just behind it and one just in front. Filled behind
   and empty in front: the face is drawn facing out. Empty behind and filled in front: it is drawn
   facing in, as a carved wall. Anything else: no face.
4. Where several brushes qualify at one point, exactly one may draw. Between different types it must
   be the tie winner: Solid first, then list order. Between the same type any one will do, so the
   oracle doesn't pin today's choice.
5. Sample every face on a grid inset from its edges, and skip samples near another brush's plane that
   crosses the face, where welding decides rather than contents. Keep the front and behind offsets
   well above the weld distance.
6. At each sample, a triangle of the expected brush, facing the expected way, must cover the point,
   and no triangle may cover a point the oracle leaves empty. Triangles map to brushes through
   `SubMeshTriangleLookup.perTriangleNodeIDLookup`, reached inside the `FinishMeshUpdate` callback
   passed to `CompactHierarchyManager.Flush` as
   `meshUpdates.vertexBufferContents.subMeshTriangleLookups[meshUpdate.contentsIndex]`. That lookup
   is built for renderable and debug sections only, never for colliders, so a test brush's surfaces
   have to keep the default destination flags. (`GeneratedMeshContents.brushIndices` is dead code:
   nothing constructs or reads it, and the `CSGTree.GetGeneratedMesh` its comments point at does not
   exist.)

### Scene generators

| Generator | What it stresses | Default run |
|---|---|---|
| Grid boxes | 2 to 6 axis-aligned boxes with whole-unit corners inside a 6-unit cube, so faces touch and overlap on shared planes all the time. | 300 seeds |
| Rotated grid boxes | The same kind of scene under one random rotation and offset, so every shared plane is a float plane. | 100 seeds |
| Free brushes | Random convex brushes, boxes and wedges, each with its own rotation and scale. | 100 seeds |
| Trees and types | Applied to all of the above: mostly additive, some subtractive and intersecting brushes, composites nested two deep, and types drawn from a 4-entry list. | every seed |

A failing seed is shrunk by deleting brushes and composites for as long as it still fails, then
printed as C# for the fixed set. The long run, tens of thousands of seeds, sits in its own category.
Before any contents code lands, run the generators with every brush Solid against the plain oracle:
whatever fails there is an existing CSG defect, not a contents one. Shrink it, file it, and keep that
kind of scene out of the contents gate until it is fixed.

### What runs

**Rules, exhaustively** (Core/Tests)

| Test | What it proves |
|---|---|
| Rules and ties | Every ordered pair of the 32 indices, against a reference written straight from the two rules and the tie order. |
| Category rewrite | Every category × pair of indices × carving flags. Matching types change nothing. |
| List invariants | Solid stays at index 0, entries are only appended or removed from the end, and a 33rd entry is refused. |
| Index past the end | A brush whose index is past the end of the list builds as Solid. |
| Carving flag | A brush, an additive composite, a subtractive composite, and nesting. |
| Operation tables | CategoryRoutingRowTests still green and untouched. |

**Routing tables** (Core/Tests)

| Test | What it proves |
|---|---|
| GetStackNodes on built blobs | A brush wholly inside another non-solid type routes AllOutside, and carving brushes are never neutralized. Needs `GetStackNodes` made internal. |

**Scenes checked by the oracle** (Core/Tests)

| Test | What it proves |
|---|---|
| Fixed scenes | Frame and pane; solid inside water; same type; glass overlapping water with both interiors kept; a coplanar tie in both orders; carving through solid and glass; floor, pool, water; carving composites. Each asserts the drawn area per brush and plane for all-Solid and for contents, such as the post face going from 256 to 512 u². |
| Generated scenes | No oracle mismatch across the three generators. |
| Shrunk seeds | Every seed that ever failed, kept as a fixed scene. |

**Properties** (Core/Tests)

| Test | What it proves |
|---|---|
| Separation | Solid faces don't change when every non-solid brush is deleted, and one non-solid type's faces don't change when brushes of the other non-solid types are deleted, in scenes without coplanar overlaps between types. |
| One type | Giving every brush the same type, whichever it is, reproduces the all-Solid geometry exactly. |
| Swapped types | Swapping two non-solid types on every brush swaps their faces and changes nothing else, apart from ties. |
| Order and labels | Permuting additive siblings or renaming entries changes nothing; permuting the non-solid indices across all brushes changes only ties. |
| Determinism | Two builds match, and so do builds with Burst compilation on and off. |

**Incremental updates and settings**

| Test | What it proves | Where |
|---|---|---|
| Random edits | Move or resize a brush, change its operation or type, add or delete a brush: after every step the incremental result equals a fresh full build. | Core/Tests |
| List changes | Adding or removing an entry rebuilds every tree; renaming one, or an unchanged list, rebuilds nothing. | Core/Tests |
| Serialization | Defaults to Solid, scenes saved before the feature load unchanged, a brush keeps its index when entries are renamed, an index past the end builds as Solid with a warning, and undo restores. | Components/Tests |
| Runtime list | A player loads the same list the editor uses. | Components/Tests |

**Real data**

| Test | What it proves | Where |
|---|---|---|
| Map fixtures | Brushes copied out of the VMFs as planes with their Source contents: bm_c0a0a's frame and pane, water against window in bm_c2a3b, bm_c1a0b and bm_c3a2d, and a brush mixing window and grate sides. The oracle checks them, and no game install is needed. | Core/Tests |
| Importer vs scan | The C# contents rules agree brush for brush with the offline Python scan over all 80 maps. Skipped when the VMFs or VPKs aren't present. | vpktools Tests |
| VMT flags | The `%compile*` flags and `$alpha` are parsed, and tool materials never become windows. | vpktools Tests |

**The real map**

| Test | What it proves | Where |
|---|---|---|
| All-solid hash | bm_c0a0a's geometry hash is unchanged with the feature compiled in and every brush Solid. | Headless harness or editor eval |
| Imported contents | No new holes in the solid layer by sphere probe, manifold checks per type, and the post face at 512 u². | Editor eval after a reimport |
| Contents toggle | Solid 247412 going Solid → Glass → Solid: incremental equals full on the real map. | Headless harness |
| Rebuild time | Within noise of the all-Solid build, five runs each. | Headless harness |

### Running it

- Tests live in `com.chisel.core.tests` under `Tests/Contents`, category `Contents`; the long fuzz run
  is `Contents.Fuzz.Long`.
- The fixture builds and destroys only its own trees. `CompactHierarchyManager.Flush` updates every
  tree with pending changes, so run with no pending edits in an open map, or give tests a way to
  update one tree. **The callback owns the mesh data it is handed, for every tree, so a harness that
  disposes it strips the meshes off any model in the open scene and the level renders empty until its
  next rebuild.** Learned the hard way on 2026-09-16. The harness now refuses to flush while another
  tree is dirty rather than damaging it. `TreeUpdate.ScheduleTreeMeshJobs(callback, trees)` is the
  per-tree entry point, but using it means repeating the propagation-round loop, because only the
  final round builds meshes.
- **Recompile first, then open the empty scene.** An unsaved scene does not survive a domain reload:
  Unity restores the previous scene setup, so the map comes back on its own and the next run meets
  exactly the situation the empty scene was meant to avoid.
- **Match brushes by handle, not by id.** A brush is given a new `CompactNodeID` when it is parented
  into the tree, so an id captured at creation matches nothing in `perTriangleNodeIDLookup`, and every
  face looks undrawn. Keep the `CSGTreeBrush` and read its `CompactNodeID` when the triangles arrive.
- **Don't skip a sample because a neighbour's plane passes through it when that plane is parallel to
  the face.** A coplanar contact sits at distance zero from every sample on the face, so a naive
  "skip near another brush's plane" filter throws away the post and the pane, glass against water, and
  every other case the feature exists for. Skip only planes that genuinely cross the face.
- In the shared editor, run it by fixture name through the MCP test runner, announced like any other
  editor slot.
- With the editor closed:
  `Unity.exe -batchmode -projectPath D:\Unity\Chisel.Dev -runTests -testPlatform EditMode -testCategory Contents -testResults contents.xml`

### Done when

- The rules layer is exhaustive and green.
- The oracle finds no mismatch in the default seeds of all three generators, outside scene kinds the
  all-Solid baseline excluded.
- Every property holds, and incremental equals full over the random edits.
- bm_c0a0a's all-Solid hash is unchanged, and the solid layer has no new holes once contents are
  imported.
- With the rewrite switched off, the suite fails.

## As built

Where the implementation differs from the plan above, or fills in what it left open:

- **The rules** live in `ContentsRules` (Core/2.Processing/Categorization): `Rewrite` for hook 1,
  `RemovesWhenInside` for hook 2, `WinsTie` and `Resolve`. `ContentsRulesTests` checks every category, every
  ordered pair of the 32 indices and both carving flags.
- **Per brush in the CSG,** `CompactTree.brushIDValueToContents` holds one `BrushContentsInfo` byte per brush:
  the resolved type, plus bit 0x80 for carving. It is indexed like `brushIDValueToAncestorLegend`, so both hooks
  read it by brush id; `CompactTreeBuilder` fills it.
- **A contents change raises `HierarchyModified`,** not `NeedAllTouchingUpdated`. `FindModifiedBrushesJob`
  turns the one into the other, and `CSGTreeBrush.Dirty` then reports the brush too.
- **The list's entry count** is checked by `CompactHierarchyManager.ApplyContentsList()`, which `Flush` calls
  before every update. Code that changes the list calls it straight away, so other trees are marked before any
  flush rather than inside someone else's. `BuildCompactTreeJob.contentsCount` defaults to 0, which builds
  every brush as Solid.
- **`CompactHierarchyManager.ContentsEnabled`** (internal) builds everything as Solid. It exists for the control
  that shows the suite fails without the feature.
- **Generators:** a brush generator keeps the index on its brush. A branch generator hands its component's
  index to the job pool, which puts it on `GeneratedNodeDefinition` for every brush it emits, and
  `CompactHierarchy.SetState` applies it. `GeneratedNode` carries nothing yet, since no generator types its
  brushes individually.
- **Where the list lives:** `ChiselProjectSettings.ContentsList` points to the asset.
  `ChiselContentsList.Instance` reads that pointer the first time the list is needed in the editor, which is
  before the first tree is built, and a player gets the list from its preloaded assets.
  `ChiselContentsBuildPreprocess` adds the list to those if it is missing.
- **Editor:** Project Settings > Chisel > Contents creates or picks the list and edits it (rename, append,
  remove last), and the asset's own inspector shows the same narrow editor. Generators show a Contents
  dropdown under the operation, with an "Edit Contents…" entry and a warning for an index past the end.
- **A face shared with a carving brush.** Where two faces lie on one plane, the pipeline on its own lets
  one of them draw, and when one brush carves the other, that is the carving brush. With contents that is
  wrong whenever the types differ: the carving brush looks straight through the other brush, so it has
  nothing to draw there, and the face vanished (or the carving brush drew it where only Solid may). So
  `Rewrite` also takes whether A carves, and whether B's operation applies after A's (brushes are numbered
  depth first, which is the order their operations apply in):
  - Where A carves B (A carving, B earlier), A gives up the faces they share, coplanar or back to back:
    Aligned and ReverseAligned both become Outside.
  - Where B carves A (B carving, B later), A keeps them: its Aligned piece becomes Inside (its material is
    inside the carve, so an intersection keeps it and a subtraction removes it), and its ReverseAligned
    piece becomes Outside.
  - A carving brush that comes first carves nothing of the other brush, so the ordinary rules apply.
    Without that, water poured into a carved pool kept its faces against the floor. Between two carving
    brushes the pipeline's order still decides.

  - Against an intersecting carve, a back to back piece keeps its category: an intersection keeps that
    face only where material remains on the carve's side, and the pipeline works that out itself. The
    CompactTree records per brush whether it carves by intersection (it or a composite it sits in is
    intersecting) for this.

  The first version ignored carving. Only one of the suite's 8 grid seeds showed it; a probe over 162
  generated scenes and 45 fixed ones failed 36 of them. The final rule passes all of them except grid and
  rotated seed 7, a known limit (below). The routing table is not a usable source for the order: a brush
  doesn't reliably find itself in its own table.
- **The oracle's ties count only faces that cover the point.** `ContentsComparison` used to let every
  brush with a coplanar plane into a tie, even where its face doesn't reach the sample. A Solid face
  elsewhere on the plane then "won" and ruled out the brush that really draws there. With every brush Solid,
  the tie allows any brush, so this never showed before contents.
- **Tests:** `ContentsSceneTests` covers the fixed scenes, generated scenes with types, one type everywhere
  against all-Solid, incremental type changes, the list's length, and the switched-off control. The fixed
  scenes include a carving brush on the faces of, and back to back with, a brush of another type. The
  generated scenes run grid seeds 1–80, rotated seeds 1–30, and free and cut seeds 1–30.
  `ContentsListScope` installs a 4-entry list for them. `ContentsComparison` enforces the tie order and
  allows at most one brush per facing.
- **Existing CSG defects the probe found.** These seeds fail the oracle with every brush Solid, at canonical
  vertex stage 0, so they are left out of the typed runs: grid 11 and 37, rotated 11, free 14, 21, 23, 25
  and 26. They are not contents problems.

## What Source does

Checked against VBSP in the source-sdk-2013 code. It is the importer's target for which brushes get
which type.

- **Material to contents.** `FindMiptex`: `%compilewater` gives water, `%compileslime` slime,
  `%compilepassbullets` grate. Tool materials such as sky, hint, skip, clip and trigger leave an
  if/else chain first. Every other material whose shader isn't opaque, translucent or alpha-tested,
  becomes a window unless it is already grate or water. The order matters: toolsclip, toolstrigger,
  toolshint and toolsskip all have `$translucent 1` and are not windows.
- **Brush contents.** `BrushContents` takes the union of the sides. Any window, grate, water or slime
  side wins and clears solid. That is why 247412, one GLASS/UNBREAKABLE side and five nodraw sides,
  compiles as a window.
- **Which faces exist.** Faces come from portals between leaves whose visible contents differ, facing
  into the weaker side. Solid leaves get no faces, `FaceFromPortal` skips the insides of windows and
  grates, and water seen from inside gets its bottom material.
- **Rank.** `VisibleContents` takes the lowest set bit: SOLID 0x1, WINDOW 0x2, AUX 0x4, GRATE 0x8,
  SLIME 0x10, WATER 0x20. Source also skips faces inside windows and grates, so water seen from
  inside glass has no surface there. Chisel keeps both faces on purpose; the importer only uses this
  rank to pick one type for a brush with mixed sides.
- **Brush entities.** func_detail brushes are moved into the world. Chisel's importer puts every other
  brush entity into the world model too.

### How much of Black Mesa this touches

A scan of the 80 campaign maps with those rules. VBSP gets opacity from the closed material system, so
the scan reads it from VMT keys instead, and brushes inside func_instance files aren't counted. Treat
the numbers as close, not exact.

| Owner | Solid | Window | Grate | Water |
|---|---:|---:|---:|---:|
| world | 84,380 | 314 | 533 | 236 |
| func_detail | 159,362 | 3,588 | 2,054 | 3 |
| other brush entities | 32,790 | 2,888 | 4,175 | 39 |
| **all 80 maps** | **276,532** | **6,790** | **6,762** | **278** |

- 13,830 of 311,544 brushes are not solid, 4.4%. Leaving out hidden brushes and brushes with a
  displacement side, 11,559 remain. Clip, hint, skip and blocklight brushes are in neither figure.
- The biggest single material is `tools/toolsinvisible`: 3,641 grate brushes that are never drawn,
  which the importer already skips. The visible ones are led by `glass/unbreakable` (735 brushes),
  `metal/metalgrate013a2` (572), `metal/metaltruss011a` (568) and `lab/labglasswindowbreak070a` (476).
- bm_c0a0a has 71 non-solid brushes: 62 window and 9 grate. Solid 247412 comes out window and
  Solid 247411 solid, as predicted above.
- In world and func_detail, 5,812 of the 6,728 non-solid brushes have a bounding box touching a solid
  brush's. Those contacts are where faces would come back. Bounding boxes overstate contact, so treat
  this as an upper bound.
- Different see-through contents touch far less: grate and window in about 195 places, water and
  window in about 138, mostly in bm_c2a3b, bm_c1a0b and bm_c3a2d. Those are the maps where keeping
  both faces differs most from Source.
- Water is rare, 278 brushes. `gasworks/gassworks_river` uses a refraction shader without
  `%compilewater`, so Source compiles its 235 brushes as window, not water.

## Known limits

- Two brushes of one type that touch back to back exactly on the face of a later intersecting brush of
  another type: the intersection uncovers their shared face, which the pipeline leaves to the intersecting
  brush, and that brush may not draw it against their type, so the face is missing. Generated grid seed 7
  is this case; it is left out of the typed test runs.
- Up to 32 contents entries per project. Entries can't be moved, and only the last one can be
  removed, because brushes store their index.
- Carved walls belong to one contents. A hole carved into water shows no water walls unless the
  carving brush is Water, and a carving brush set to Water also leaves walls where it cuts solid.
- Nothing emits back faces yet, so a water surface is invisible from below unless its material is
  double-sided.
- Transparent surfaces are merged per material across the whole model, so overlapping panes won't
  sort. The planned chunking would help.

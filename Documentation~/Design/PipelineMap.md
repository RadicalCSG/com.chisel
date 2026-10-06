# The CSG pipeline, in schedule order

**Generated** by `scratchpad/pipeline_map.py` from `Core/2.Processing/Managers/CSGManager.UpdateTreeMeshes.cs`. Do not hand-edit — re-run it, and diff.

The regions and their nesting are the author's, read off the scheduler. The Read/Write columns are the job's own declarations. Nothing here is a taxonomy imposed on the code: an earlier attempt at this map invented eleven stages and then classified everything by them, which made the classification fiction even where the counts were right.

**53 job instantiations** of **49 distinct job types**, across **33 regions**.

## Scheduled more than once

A map with one row per job type would merge these. The indirect-update passes deliberately re-run earlier jobs over a different brush set, so the same code decides twice on different data.

- `CreateTreeSpaceVerticesAndBoundsJob` — 2×: line 1462 (Perform CSG / Prepare / Update brush tree space vertices and bounds); line 1611 (Perform CSG / Prepare / Fixup indirectly brush cache data order (when brush touches a brush that has changed))
- `FindTriangleBrushIndicesJob` — 2×: line 2614 (Store Results / Create Meshes); line 2633 (Store Results / Create Meshes)
- `OutputCopyJob` — 3×: line 2507 (Store Results / Create Meshes); line 2527 (Store Results / Create Meshes); line 2547 (Store Results / Create Meshes)

## Job types defined under Core/ that this scheduler never instantiates

Each is scheduled somewhere else, or is dead. Reported rather than dropped, because the point of this file is to say what it does not cover.

- `BranchAllocateBrushesJob` — 1.Input/GeneratorBase/GeneratorJobPool.cs:662
- `BranchCreateBrushesJob` — 1.Input/GeneratorBase/GeneratorJobPool.cs:687
- `BranchPrepareAndCountBrushesJob` — 1.Input/GeneratorBase/GeneratorJobPool.cs:645
- `ConvertToBrushMeshBlobJob` — 1.Input/BrushMesh/BrushMeshManager.Internal.cs:404
- `CreateBrushesJob` — 1.Input/GeneratorBase/GeneratorJobPool.cs:383
- `DisposeArrayChildrenJob` — ExtensionMethods/Jobs/Schedule.cs:337
- `DisposeBlobReferenceChildJob` — ExtensionMethods/Jobs/Schedule.cs:399
- `DisposeJob` — 2.Processing/Containers/InternedPlanes.cs:93
- `DisposeListChildrenBlobAssetReferenceJob` — ExtensionMethods/Jobs/Schedule.cs:358
- `DisposeListChildrenJob` — ExtensionMethods/Jobs/Schedule.cs:315
- `DisposeReferenceChildBlobAssetReferenceJob` — ExtensionMethods/Jobs/Schedule.cs:419
- `DisposeReferenceChildJob` — ExtensionMethods/Jobs/Schedule.cs:382
- `DualJobHandle` — ExtensionMethods/Jobs/JobExtensions.cs:125
- `EnsureCapacityListForEachCountFromListJob` — ExtensionMethods/Jobs/Schedule.cs:279
- `EnsureCapacityListReferenceJob` — ExtensionMethods/Jobs/Schedule.cs:297
- `HierarchySortJob` — 1.Input/GeneratorBase/GeneratorJobPool.cs:212
- `InitializeArraysJob` — 1.Input/GeneratorBase/GeneratorJobPool.cs:419
- `JobHandleAccumulator` — ExtensionMethods/Jobs/JobExtensions.cs:291
- `JobHandlesStruct` — 2.Processing/Managers/CSGManager.UpdateTreeMeshes.cs:387
- `MergeTouchingBrushVerticesJob` — 2.Processing/Jobs/MergeTouchingBrushVerticesJob.cs:15
- `ReadJobHandles` — ExtensionMethods/Jobs/JobExtensions.cs:192
- `RegisterBrushMeshesJob` — 1.Input/BrushMesh/BrushMeshManager.Internal.cs:789
- `ResizeTempListsJob` — 1.Input/GeneratorBase/GeneratorJobPool.cs:82
- `UnsafeDisposeJob` — 2.Processing/Containers/HashedVertices.cs:560
- `UpdateHierarchyJob` — 1.Input/GeneratorBase/GeneratorJobPool.cs:406
- `WriteJobHandles` — ExtensionMethods/Jobs/JobExtensions.cs:246

## The input stage — where brushes and their planes are born

Not scheduled from `CSGManager.UpdateTreeMeshes.cs`, so it is absent from the generated walk below. `GeneratorJobPool` dispatches through `IGeneratorJobPool`, and the explicit-BrushMesh path is entered from the Components layer. This section is hand-written from the two orchestrating functions and checked against them: every job instantiation in those files must appear here or the script refuses to write.

**There are two input paths, and they are not equivalent.** Path B is the one a hand-built brush and the VMF importer take.

### Path A - generators (box, cylinder, stairs, ...): GeneratorJobPool.ScheduleJobs

- **per pool: ScheduleGenerateJob - brush pools**
  - `CreateBrushesJob<Generator>` — 1.Input/GeneratorBase/GeneratorJobPool.cs:586
- **per pool: ScheduleGenerateJob - branch pools**
  - `BranchPrepareAndCountBrushesJob<Generator>` — 1.Input/GeneratorBase/GeneratorJobPool.cs:857
  - `BranchAllocateBrushesJob<Generator>` — 1.Input/GeneratorBase/GeneratorJobPool.cs:865
  - `BranchCreateBrushesJob<Generator>` — 1.Input/GeneratorBase/GeneratorJobPool.cs:873
- **per pool: ScheduleUpdateHierarchyJob**
  - `UpdateHierarchyJob (brush pool)` — 1.Input/GeneratorBase/GeneratorJobPool.cs:605
  - `UpdateHierarchyJob (branch pool)` — 1.Input/GeneratorBase/GeneratorJobPool.cs:961
- **allocate the shared output lists**
  - `ResizeTempListsJob` — 1.Input/GeneratorBase/GeneratorJobPool.cs:145
- **SYNC POINT: allocateJobHandle.Complete() - GeneratorJobPool.cs:158, marked 'TODO: get rid of this'**
- **per pool: ScheduleInitializeArraysJob**
  - `InitializeArraysJob (brush pool)` — 1.Input/GeneratorBase/GeneratorJobPool.cs:621
  - `InitializeArraysJob (branch pool)` — 1.Input/GeneratorBase/GeneratorJobPool.cs:1067
- **BrushMeshManager.ScheduleBrushRegistration**
  - `RegisterBrushMeshesJob` — 1.Input/BrushMesh/BrushMeshManager.Internal.cs:854
- **ScheduleAssignMeshesJob**
  - `HierarchySortJob` — 1.Input/GeneratorBase/GeneratorJobPool.cs:326
  - `AssignMeshesJob` — 1.Input/GeneratorBase/GeneratorJobPool.cs:329

### Path B - explicit BrushMesh (ChiselBrushComponent, the VMF importer, any hand-built brush): ChiselNodeHierarchyManager.cs:436 -> BrushMeshManager.ConvertBrushMeshesToBrushMeshInstances

- **convert managed BrushMesh to blob**
  - `ConvertToBrushMeshBlobJob (Schedule + immediate Complete)` — 1.Input/BrushMesh/BrushMeshManager.Internal.cs:366
- **register (synchronous, not a job)**

### Where a plane comes from

Traced for Path B, because that is the path exact input planes would arrive on — and the path the VMF importer uses: `VmfWorldConverter.cs:312` and `:502` call `BrushMeshFactory.CreateFromPlanes` with planes it computed from each side's three integer points.

**There is exactly one place the exact planes are lost.** That is worth stating precisely, because the loose version of this claim - 'planes get recomputed all over the place' - is wrong in a way that matters: `CalculatePlanes()` has 21 live call sites, but the ones on this path are guarded. `BrushMesh.Validate` recomputes only `if (planes == null)` (`BrushMesh.Validate.cs:14`), and `ValidateShape` only when the count disagrees (`:174`). So the exact planes survive validation intact. They die at a single boundary, and that makes the repair small and local rather than systemic.

1. `BrushFactory.CreateFromPlanes` cuts the brush with the caller's planes, then `AssignCuttingPlanes` (`BrushFactory.Utility.cs:1131`) writes those **exact** planes back onto `brushMesh.planes`, with a comment explaining that re-deriving them is wrong.
2. `ConvertBrushMeshesToBrushMeshInstances` packs each brush into `BrushMeshPointers` (`BrushMeshManager.Internal.cs:210`), which has fields for `vertices`, `polygons` and `halfEdges` and **no field for planes**. The exact planes cannot cross into the job; they are dropped here.
3. `ConvertToBrushMeshBlobJob` welds the vertices (`:453`), then re-fits every plane from the **welded** vertices with its own `CalculatePlane` (`:412`): Newell in double, `math.normalize`, and `d` set to the **average** of `-dot(normal, vertex)` over the polygon's vertices. That is a fit, not a plane through the geometry, so the plane does not pass exactly through any of its own vertices.
4. The non-job route `ConvertToBrushMeshBlob` (`:120`) reaches the same end differently: it calls `brushMesh.CalculatePlanes()` (`:132`) unconditionally, overwriting whatever was there, and then copies the result.
5. `CreateBrushTreeSpacePlanesJob` (`2.Processing/Jobs/CreateBrushTreeSpacePlanesJob.cs:45`) multiplies by the inverse-transpose of the brush transform and **normalizes again** (`treePlane /= math.length(treePlane.xyz)`).

So between a caller's exact plane and the predicate that consumes it there are two re-derivations and two normalizations, and the re-derivation is per-brush, from that brush's own vertex set.

Stated precisely, because the convenient version is wrong: two brushes meeting on one geometric plane get the same `float4` only when their Newell sums and their `d` averages agree bitwise. For an axis-aligned face that generally holds — every vertex shares the coordinate being averaged, and the normal is exact — which is why axis-aligned test scenes behave. It stops holding when the vertices are not exactly coplanar (any rotated brush, after rounding), when the two sides carry different vertex counts, or when the shared face is split differently on each side. So plane identity across brushes is **incidental** — it is a property of the input happening to be nice, not something the representation guarantees.

That is what a plan starting 'intern the planes so one plane has one representation' has to answer first. At the point interning would happen the exact planes no longer exist, and the planes that do exist are per-brush fits whose agreement is a coincidence of the geometry.

Both paths also weld at input, through `HashedVertices.AddNoResize` with no weld filter (`:453` and `:164`).

### Machinery for plane/vertex consistency that nothing calls

`BrushMesh.Optimize.cs` contains most of what a plane-exact representation would need, and these have no non-test callers:

- `SnapPolygonVerticesToItsPlanes` (`:28`) — would make the fit exact by moving vertices onto their own planes. Dead.
- `GetVertexFromIntersectingPlanes` (`:40`) — derives a vertex from the planes meeting at it, which is the plane-based vertex identity idea. Dead.
- `CenterAndSnapPlanes` (`:120`) — dead, and worth knowing because the comment justifying `AssignCuttingPlanes` (`BrushFactory.Utility.cs:1125`) cites it as the reason exact planes must be preserved. The justification outlived the function.
- `InvertWhenInsideOut` (`:386`) — dead.

`SplitNonPlanarPolygons` (`:430`) is the one that runs, but only through `ChiselBrushDefinition.EnsurePlanarPolygons`, whose only callers are `Editor/ComponentEditors/Generators/ChiselBrushEditor.cs:53` and `:94`. So polygons are made planar when a human edits a brush in the inspector, and never on the import path. Imported geometry reaches the plane fit with whatever non-planarity it has.

## Machinery that already exists, and is switched off

See also **[DeadMachinery.md](DeadMachinery.md)**, generated by `dead_machinery.py`: the same pattern — the careful implementation exists and nothing calls it — turned up four separate times on this walk, so the rest are enumerated there rather than found one at a time. It reports 11 methods with no production caller that tests still exercise (the dangerous class: the suite reads green over code the pipeline cannot run) and 47 with no caller at all. It is a regex, not a compiler, so those are candidates; it was validated by checking that it independently finds all six cases found by hand here, and its first version was corrected after it wrongly called `CSGManager.SkipUnchangedTrees`' API dead by searching only `Core/` for callers.

Found while walking `Determine Intersection Surfaces`. This matters more than any single defect below, because the exact-predicates plan proposed **building** most of it. Four toggles in `CSGManager.UpdateTreeMeshes.cs`, all off, several carrying measurements:

- **`kInternBrushPlanes`** (`:178`) — `InternBrushPlanesJob` gives every face a shared integer id for the plane it lies on, so two brushes meeting on one wall hold the same id instead of two `float4`s for an epsilon to reconcile. Built, serial by design, off.
- **`kUsePlaneIdsForAlignment`** (`:185`) — `FindAlignedPlanesByID` decides face alignment by comparing those ids. Its own comment: *"No epsilon is consulted here at all — it was spent once, when the planes were interned."* Measured against the current path: **they disagree on ~0.13% of face pairs, and where they do the id answer is the better one.**
- **`kUseIncidenceWeld`** (`:193`) — gates every positional weld on plane incidence, so a weld may only merge two vertices when each stays on the faces the other lies on. Measured on `bm_c0a0a`: of 34,354 real welds it refuses **13,106**, and those sit on **106 of 107** T-junction and 339 of 429 other uncovered-boundary stretches. Its own note records that `CreateBlobPolygonsBlobsJob` is not gated, because it runs before the tree-space planes exist (371 welds).
- **`kCanonicalVertexStage`** (`:199`) — canonical vertices in five stages, 0 = shipped behaviour through 4 = vertex identity used in the merge, the CSG re-weld and triangulation.

So the question this plan should have opened with is not *how would we build plane identity*. It is **why is the plane identity that exists switched off, and what does turning it on cost** — a question with measurements already attached. [CanonicalVertices.md](CanonicalVertices.md) and the plane-representation notes record that the payoffs were falsified on the real map, which is evidence about those specific payoffs, not a verdict on the representation.

One caution carried over from the section above: what `InternBrushPlanesJob` interns is the **fitted** plane — `localPlanes[p]` from `ConvertToBrushMeshBlobJob`, transformed and normalized (`:97`–`:104`). That is why `InternedPlanes.Matches` needs `kMaxNormalDeviation` and `kPlaneDAlignEpsilon` at all. Interning is tolerance-based *because* the planes it interns are per-brush refits; carrying the authored planes across `BrushMeshPointers` is what would let the match be exact.

**Do not trust the comments here.** `InternBrushPlanesJob:15` says "Nothing consumes these ids yet" and `CSGManager.UpdateTreeMeshes.cs:252` repeats it. Both are stale: `PrepareBrushPairIntersectionsJob` reads `brushPlaneIds` and `brushPlaneIdRange` at `:39`–`:40` and `:263`–`:268`.

## The shape of the whole thing

One sentence, after walking every geometric stage: **the exact machinery is not missing — it is deployed downstream as damage control instead of upstream as the decision.**

Every decision that determines topology is made with float geometry:

- which vertices are the same — a float distance, resolved to a non-nearest candidate (`HashedVerticesUtility`);
- which plane a face lies on — a per-brush least-squares refit, compared with an epsilon that does not transform;
- what order a loop's vertices go in — `math.atan2` about a float centroid;
- whether an edge survives a merge — a midpoint against a volume, known to be wrong exactly when the edge straddles;
- where two loops cross — five inline float copies of a double helper nothing calls.

And then a substantial, careful, **exact** layer cleans up afterwards. `LoopEdgeSplitter` holds six repairs and five of them contain no floating point at all — `RemoveAntiparallelEdgePairs`, `RemoveSimpleChords`, `RewindClosedLoop`, `RemoveTinyComponents` and `CloseSingleOpenChain` are union-find, degree counting and index walks. Only `SplitEdgesAtVertices`, which inserts T-junction vertices and genuinely needs geometry, uses floats. `CreateRoutingTableJob` — the boolean logic itself — is likewise entirely combinatorial.

Counted across the pipeline there are roughly **fifteen** such guards and repairs:

| where | mechanisms |
|---|---|
| holes, `PerformCSGJob:149`–`:164` | `RemoveAntiparallelEdgePairs`, `RemoveSimpleChords`, `CloseSingleOpenChain`, `RewindClosedLoop` |
| base loops, `CleanUp` | `kKeepEdgesRestingOnTheOtherLoopsBoundary`, `kKeepReverseAlignedBaseEdges`, `RemoveAntiparallelEdgePairs` twice (pre- and post-merge), `RemoveSimpleChords`, `CloseSingleOpenChain` |
| triangulation | collinear collapse + its `protectedVertices` exception list, `RemoveTinyComponents` (`MeshAlgorithms:130`), `RepairBoundary`, `CheckForSelfIntersections` / `RemoveSelfIntersectingEdges`, `IsDegenerate`, and the whole fallback retry |

Two of them undo each other (the collinear collapse deletes the T-junction vertices `SplitEdgesAtVertices` inserted, hence the exception list). One repairs deleting too little and the next repairs deleting too much. `RemoveAntiparallelEdgePairs` runs up to three times on one surface. `CloseSingleOpenChain` and `RepairBoundary` both re-close open loops, one in 3D and one in 2D.

None of that is careless work — each repair is defensively written, refuses ambiguous input, and several carry measurements. But fifteen exact repairs downstream of five float decisions is a description of where the exactness went, and `BooleanEdgesUtility`'s own comment says what to do instead: split the edge at the boundary, so the decision is never wrong, rather than classify it wrongly and repair the damage.

## The answer already exists in this repository

The decal subsystem (`Core/2.Processing/Decals/`, 1502 lines) does the same kind of work — clip convex polygons against a volume, triangulate the result — and does it the way the rest of this document keeps arguing for. It was written later, by the same author, and it is the cheapest available reference for what a repair would look like.

| concern | the CSG core | the decal subsystem |
|---|---|---|
| precision | `float3` positions, float predicates, one unused `double` helper per operation | `double3` throughout — positions, barycentrics, areas |
| 2D projection | `Map3DTo2D`: cross products against a tie-broken reference axis, and the axes are **not normalized**, so 2D distances scale per surface | drop the dominant axis (chosen by `math.abs`), keep the other two in cyclic order, mirror if the normal points down it — `Project` (`:678`) is a coordinate copy and a sign flip, **no arithmetic at all**, so the 2D coordinates are bit-exact and the winding is preserved by construction |
| clipping | classify against planes, then repair the misclassifications | parametric `enter`/`exit` interval clipping (`GetEdgePoints:168`) |
| attributes | recomputed downstream | barycentrics carried on the vertex and interpolated by the same `t` as the position (`DecalClipVertex.Lerp:14`) |
| triangulation | hand a bag of edges to a constrained triangulator and repair on failure | `TriangulateRing:495` constructs the triangles directly between the two polygons, **adding no points**, with explicit winding checks at each step |
| failure | silently repaired by ~15 mechanisms | a `DecalSplitFailure` enum — `TooManyPoints`, `RingTurnedOver`, `TooManyTriangles`, `AreaMismatch` — returned to the caller. It **checks its own output area against its input** and reports a mismatch rather than shipping it |
| tolerances | 57 declarations, 36 distinct names, 24 inline literals | **two**: `kPlaneEpsilon = 1e-4` and `kMinArea = 1e-10`, both `double`, in 1502 lines |

The projection row is the sharpest of these. `Map3DTo2D` builds a basis with cross products against a reference axis picked by comparison, and `CSGMath.Orient2D`'s own warning block says the resulting non-normalized axes can make 2D coordinates large enough to break the collinearity test. Dropping a coordinate instead costs nothing, loses nothing, and removes the tie-break entirely — and the code for it is already in this package.

This is not an argument that the decal code is flawless, and it solves an easier problem: its inputs are convex and it never has to merge a base loop with holes. But it is evidence that the approach this document keeps pointing at is practical here, in this codebase, in Burst, on doubles — rather than something to be taken on faith from a paper.

## Defects and hazards found while walking this pipeline

Kept here rather than in chat so a later pass does not rediscover them. Each says **how it was established**, because 'I read it' and 'I ran it' are not the same evidence and this pipeline has already cost several days to claims that were only ever the first one.

### The vertex weld does not return the nearest vertex

- **Where:** `Core/2.Processing/Containers/HashedVertices.cs`
- **How established:** read, NOT yet proven by a running test

`closestVertexIndex` is declared outside the 27-cell search loop (`:47`) and `closestDistance` inside it (`:53`), so the distance threshold resets on every cell while the winning index persists. A candidate at 0.010 in a later cell therefore overwrites a candidate at 0.001 found in an earlier one: the function returns the closest vertex in the LAST cell that held any, not the closest overall. All seven entry points share the structure - `SnapToExistingVertex`, both `ReplaceIfExists`, all three `AddNoResize`, `Add` - and the instance methods delegate straight into them, so every live weld path is affected: `BrushMeshManager` at input, `CreateBlobPolygonsBlobsJob`, `CreateIntersectionLoopsJob`, `FindLoopOverlapIntersectionJob`, `PerformCSGJob`, `MergeTouchingBrushVerticesIndirectJob`, `GenerateSurfaceTrianglesJob`. The output stays within tolerance, so this does not show up as a wrong number; it shows up as INCONSISTENCY - two brushes querying the same seam point visit cells in a different order and can resolve to different vertices, which is a crack. Fix is one line per copy (hoist the declaration). Needs a container test first: two existing vertices straddling a cell boundary, both inside the weld ball, assert the nearer one wins.

### Planes are normalized on the predicate path, twice

- **Where:** `CreateBrushTreeSpacePlanesJob.cs:46`, `InternedPlanes.cs:137`, `PerformCSGJob.cs:538`
- **How established:** read

`treePlane /= math.length(treePlane.xyz)` runs for every plane of every brush as the pipeline's first act on a plane. Predicate SIGNS are invariant under positive scaling, so this buys nothing for any sign decision and costs the property that matters: it is a `sqrt` and a divide, so one geometric plane reached through two transforms becomes two different `float4`s.

### The 2D basis for triangulation is per-brush and tie-broken

- **Where:** `GenerateSurfaceTrianglesJob.cs:685`, `MeshAlgorithms.cs:19`
- **How established:** read; not measured firing

`Map3DTo2D` is constructed from `math.mul(nodeToTreeInvTrans, plane)` - the brush's OWN local plane through its OWN transform, not a shared or interned plane - and its constructor picks projection axes by comparing `|dot(normal, axis)|`. Near a tie, one geometric plane can get two different 2D frames. `MeshAlgorithms` then tests collinearity with `|Orient2D| < 1e-12` on coordinates whose scale depends on those non-normalized axes, which is exactly the failure the warning block in `CSGMath.Orient2D` describes.

### Edge categorisation answers a 2D question from a 3D volume

- **Where:** `PerformCSGJob.cs:292` / `:304`, and the comment at `:382`
- **How established:** read; the code comments it itself

`CategorizeEdge` decides Inside/Outside from the intersecting brush's 3D volume, while the question being asked is whether an edge lies in the 2D region the loop bounds on this plane. The job's own comment says so, and records that 94.6% of cuts producing a non-simple hole keep at least one edge because of it. This is a design mismatch, not a tolerance that is set wrong.

### `MergeTouchingBrushVerticesJob` is dead

- **Where:** `Core/2.Processing/Jobs/MergeTouchingBrushVerticesJob.cs:15`
- **How established:** verified - no references anywhere in the package

Only `MergeTouchingBrushVerticesIndirectJob` (`:77`) is scheduled, from the 'Merge vertices' region. Worth knowing before reasoning about the weld stage from the wrong job, which is a mistake already made once.

### Saved output is keyed on the input only — nothing identifies the algorithm that made it

- **Where:** `CSGManager.SkipUnchangedTrees.cs:24`, `:73`; `ChiselModelManager.CanSkipTreeUpdate:143`
- **How established:** read; the consequence follows from the mechanism, not yet observed

`GetTreeInputHash` hashes everything the CSG builds the output **from**: node operations, contents, transformations, brush meshes and surfaces in hierarchy order, the decals, the model settings, the contents count. It is careful and, as far as input data goes, it looks complete. What it contains nothing of is **which version of the CSG produced the stored mesh**.

The version constant beside it does not close that gap — its rule is *"Bump when GetTreeInputHash hashes something else, or the same things differently"* (`:24`). That tracks the hash function, not the producer. So changing what the CSG *does* leaves every stored mesh matching its key and therefore 'valid'.

Stated precisely, because the alarming version is wrong: the skip is one-shot per load — `CanSkipTreeUpdate` consumes the model from `s_SavedOutputCandidates`, and after the first change a model builds all of itself. A script that dirties every model in a running session is **not** affected. What is affected is a freshly opened scene, and a reload after a code or toggle change.

Two consequences worth having in hand before any of the repairs in this document are attempted:

1. **Fix the weld, reload `bm_c2a5a`, and the saved meshes hide the fix.** Every unchanged model keeps its old geometry and the CSG never runs. That looks exactly like 'the fix did not work'.
2. The A/B toggles are defeated in the same way across a reload. `kUseIncidenceWeld` and `kCanonicalVertexStage` are deliberately not `readonly`, with comments saying *"a script can A/B one rebuild"* — but their values are not in the hash, so an A/B done over a scene reload compares saved output against saved output. Any past measurement taken that way is worth re-checking before it is trusted; that is a question about the method, not a claim that any particular number is wrong.

The importer side of this project already solved the same problem: generated assets carry a `chisel-<generator>-v<N>` stamp so bumping it rebuilds old outputs. CSG output has no equivalent.

### The weld defect, traced to the job that actually welds

- **Where:** `MergeTouchingBrushVerticesIndirectJob:183`–`:208` into `HashedVertices.cs:824` / `:838`
- **How established:** read end to end; the consequence is reasoned, not yet run

The live weld is `MergeTouchingBrushVerticesIndirectJob` (the non-Indirect one is dead). For each brush it snaps that brush's vertices onto its touching neighbours' — but only neighbours with a **lower node order** (`:186`, `:200`). That asymmetry is correct and deliberate: imposing a total order is how a snapping pass is made convergent instead of chasing its own tail, and the fixpoint gate at `:135` is derived from this brush's own reads so it does not assume the touch graph is symmetric. Careful code.

It reaches the vertices through `ReplaceIfExists(..., min, max)`, whose two bounded overloads (`:814`, `:828`) cull by a box padded with `kCellSize` — exact, since the pad exceeds the weld radius — and then delegate straight to `HashedVerticesUtility.ReplaceIfExists`. Which is the second copy of the defect recorded above: `closestDistance` resets on every one of the 27 cells while `closestVertexIndex` persists, so the snap target is the closest candidate in the **last cell that held any**, not the closest overall.

So the chain is complete: the weld is documented and intended to snap each vertex to its nearest anchor, the total order makes which anchor wins deterministic, and the lookup then picks a non-nearest one whose identity depends on where the query sits relative to the cell grid. Two vertices that should resolve to the same anchor can resolve to different ones. That is a seam crack, produced with every tolerance set correctly.

Also worth knowing: the output depends on tree node order by construction. Reordering siblings, or adding a brush that shifts node orders, can move welded vertices by up to the weld radius. Not a defect — it is the tie-break that makes the fixpoint terminate — but it means 'the same scene' is only the same geometry while the hierarchy order is the same.

### A known geometry-destroying failure is described in the code, and its guard is off

- **Where:** `MergeTouchingBrushVerticesIndirectJob:150`, `CSGManager.UpdateTreeMeshes.cs:193`
- **How established:** read

The comment above the weld filter: *"A snap may pull a vertex onto a neighbour's, but not off the faces of its own brush: that is what collapsed a 19 mm slab onto a neighbour vertex lying between its two faces."* A specific, observed, geometry-destroying failure, with the fix built — `WeldIncidenceFilter`, which permits a merge only when each vertex stays on the faces the other lies on. `useIncidenceWeld` comes from `kUseIncidenceWeld`, which is **false**. So the failure the comment describes is currently unguarded, by choice, and the measurement in the toggle's own comment says the guard would refuse 13,106 of 34,354 welds — which is why it is off, and also why turning it on is a question about what those 13,106 were doing rather than a switch to flip.

### The pipeline already knows exactly when its core decision is wrong, and does not act on it

- **Where:** `BooleanEdgesUtility.cs:144`, `:189`, and `PerformCSGJob.cs:579`
- **How established:** read, and every caller counted

`BooleanEdgesUtility` states the theorem twice, in its own comments: *"For a convex plane set the midpoint test in CategorizeEdge is only WRONG when the edge straddles the boundary (one endpoint inside the planes, the other outside) — i.e. it crossed the boundary and should have been split there"* (`:144`), and again at `:189`: *"This is the case `FindBasePolygonPlaneIntersections` should have split at the boundary face; if it didn't, the edge reaches `CleanUp` straddling and the midpoint test misclassifies it."*

That is a complete characterisation of the defect, an upstream cause, and a bound on it — the straddle is the **only** case. Three predicates implement it. None of them decides anything:

| predicate | production callers | tests | effect on output |
|---|---|---|---|
| `EdgeStraddlesSegmentPlanes` (`:149`) | 0 | 4 | none — nothing calls it |
| `EdgeStrictlyCrossesSegmentPlanes` (`:195`) | 2 | 4 | none — both sites are gated by `kLogStrictCrossing`, which is `false` (`PerformCSGJob.cs:579`); it feeds a log |
| `EdgeRestsOnSegmentPlanes` (`:163`) | 2 | **0** | the only one that changes output — it is the `kKeepEdgesRestingOnTheOtherLoopsBoundary` guard |

The coverage is inverted. The two predicates that cannot affect a single triangle have eight tests between them; the one that decides whether `b1114ba`'s rule fires — the rule the pad regression is about — has none.

**This is the cheapest real measurement left in the pipeline.** Setting `kLogStrictCrossing = true` for one rebuild of `bm_c2a5a` reports every edge whose midpoint verdict is a true unsplit crossing, on real data, with code that already exists. It answers directly whether the pad's lost edge is a straddle, and it needs no new instrumentation — only the editor.

It also reframes the repair. If the straddle really is the only way the midpoint test goes wrong for a convex plane set, then the fix is not exact predicates at all: it is either splitting the edge at the boundary upstream, where `FindBasePolygonPlaneIntersections` was supposed to, or refusing to destroy an edge that straddles. Both are local, and both are testable against `FlushContactFaceTests` and `SourceConcretePadTests` together, which is the bar `b1114ba` failed.

### CategorizeEdge tests an edge against its own loop's plane

- **Where:** `BooleanEdgesUtility.CategorizeEdge:139`
- **How established:** read; the TODO is the author's

Directly above the midpoint test: `// TODO: shouldn't be testing against our own plane`. The plane set the midpoint is tested against includes the plane of the surface the loop lies on, against which every point of the loop is on-plane and therefore within the fat band by construction. Small, but it is one more term in a decision that is already known to be wrong in a specific way.

### NOT what its name says: AreLoopsOverlapping tests edge-set equality

- **Where:** `BooleanEdgesUtility.AreLoopsOverlapping:233`
- **How established:** read; one production caller

It returns false unless the two loops have the **same edge count** and every edge of the first appears in the second — that is set equality (up to direction), not overlap. Two loops that genuinely overlap on part of their area answer `false`. The name says otherwise, and a reader checking an overlap decision would believe it. Recorded as a naming hazard rather than a defect: with one caller, whether it is a bug depends on what that caller wants, which is worth reading before anyone 'fixes' the function.

### A truncated update is indistinguishable from a converged one

- **Where:** `CSGManager.UpdateTreeMeshes.cs:134` and `:965`
- **How established:** read both the loop and the guard; not yet observed firing

An incremental update is a fixed point. After each round, brushes whose welding / T-junction inputs were left stale are dirtied and the tree runs again, *"until nothing moves"*. The comment at `:72` claims this *"is what makes an incremental update end in exactly the geometry a full rebuild produces"* — which holds only if it converges inside `kMaxPropagationRounds = 8`.

When it does not, the failure is silent in the strongest sense. The dirtying block at `:965` is guarded by `if (s_PropagationRound + 1 < kMaxPropagationRounds && ...)`, so on the last permitted round the whole block is skipped: the stale brushes are never counted, `staleCount` stays 0, `s_StaleBrushCount` is not incremented, and `s_PropagationRequested` is never set. The `while` at `:134` then exits because `s_PropagationRequested` is false — the same way a genuinely converged run exits.

So a truncated update reports **exactly** what a converged one reports: no stale brushes, no propagation requested, no log line, no warning. The code that would have recorded the outstanding work is the code that was skipped. The only residual signal is `LastUpdateRounds == 8`, and that is also what a run that converged on round 8 reports.

This is the shape recorded in the `false-green-guards` note: when the pass condition is 'nothing changed', an instrument that is not looking passes it perfectly. It also matches the reported symptom of geometry that *"disappeared suddenly"* — output that depends on update history rather than on the scene.

Cheap to make visible without changing behaviour: count `staleLoopBrushes.Length` unconditionally and only skip the `SetDirty` calls, so `LastUpdateStaleBrushCount` reports what was abandoned. Worth contrasting with the sibling bound `kMergeIterations = 30`, which is justified by a measured convergence tail in its own comment (a 5700-brush map settling at pass 17) and whose truncation *is* recovered, because a brush still moving gets its neighbours re-run next round. One bound is measured and self-correcting; the other is asserted and terminal.

### Ten repair mechanisms stand between a merged loop and a triangle

- **Where:** `PerformCSGJob.CleanUp` and `GenerateSurfaceTrianglesJob:707`–`:808`
- **How established:** read end to end, both jobs

Continuing the entry below into the triangulation stage. `CleanUp` contributes five; `GenerateSurfaceTrianglesJob` adds five more before it will accept a loop:

6. `ConvertToPlaneSpace` (`:710`) collapses collinear points — and needs a `protectedVertices` list, because that collapse would otherwise delete the cross-brush T-junction vertices the same job inserted a few hundred lines earlier to close seams. One mechanism actively undoing another, with an exception list to keep the peace.
7. `RepairBoundary` (`:716`, `kCloseOpenChains2D`, **on**) — a **second** open-chain repair, in 2D. `CloseSingleOpenChain` already did this in 3D in `CleanUp`.
8. `CheckForSelfIntersections` / `RemoveSelfIntersectingEdges` (`:718`, `:726`) — removing the self-intersections the `atan2` ordering and the concatenation produce.
9. `IsDegenerate` (`:739`) pre-check.
10. and on failure, a **fallback retry** (`:765`–`:807`): `RemoveAntiparallelEdgePairs` once more, then 6–9 again, then triangulate a second time.

`RemoveAntiparallelEdgePairs` therefore runs up to **three times** on one surface — `PerformCSGJob:786` pre-merge, `:1134` post-merge, and here in the retry. The comment justifying the third (`:771`) says `CleanUp` *"only stripped from the pre-hole-merge base loop"*, which `:1134` contradicts: it strips post-merge and says so. Third stale comment found on this walk.

For the pad this narrows things usefully. `zeroTriangles=1` is only reachable at `:802` `if (!recovered)`, so every one of 6–10 ran and the surface still produced nothing — and it is **not** a `TriangulatorError`, which is a separate reason logged at `:761`. The triangulator accepted the input and found no area to fill, which is what an open boundary looks like to a constrained triangulation.

### The hole merge is destroy-then-repair, with five mechanisms layered on it

- **Where:** `PerformCSGJob.CleanUp`, `:1090`–`:1150`
- **How established:** read end to end

This answers 'why do we even delete edges?' with the machinery that exists to undo the deleting.

The merge does not construct a merged boundary. It **concatenates**: for each hole, `AddEdgesNoResize(ref baseLoopEdges, in holeEdges)` (`:1127`) tips that hole's edges into the base loop's list — one flat bag, explicitly allowed to contain duplicates (`:1125`), carrying an unexplained `// TODO: why is baseLoopEdges sometimes not properly allocated?` capacity workaround at `:1121`. Merging is then defined as *removing* the edges another loop took over, decided by `CategorizeEdge` — an edge's midpoint against the other brush's **planes**, a volume test standing in for a question about a polygon.

Because that is wrong in known ways, five mechanisms sit on top of it:

1. `kKeepEdgesRestingOnTheOtherLoopsBoundary` (`:662`, **on**) — suppress destruction when the Inside verdict is only a fat-band touch. Hole-vs-hole only.
2. `kKeepReverseAlignedBaseEdges` (`:599`, **off**) — the same idea for ReverseAligned base edges.
3. `RemoveAntiparallelEdgePairs` (`:1134`) — strips reverse-edge slits that, by its own comment, *"were not present on either loop alone"*: damage the concatenation itself introduced.
4. `RemoveSimpleChords` (`:1140`, **on**) — drops interior chords *"the destroyedEdges categorization under-deleted"*.
5. `CloseSingleOpenChain` (`:1145`, **on**) — re-closes a loop the destruction **over-deleted**.

Mechanisms 4 and 5 are worth reading together: one repairs deleting too little, the other repairs deleting too much, and both run every time because neither the test nor its failure mode is understood well enough to avoid either.

**And 5 is strictly limited.** `LoopEdgeSplitter.CloseSingleOpenChain` (`:323`–`:360`) builds a degree/union-find pass and refuses unless the edge set is a **single connected component** with **exactly two degree-1 endpoints** and every other vertex degree-2. So it fixes exactly one missing edge, in one clean chain. Two unreplaced deletions, or damage leaving any vertex of degree>2, or two components, and it gives up — the loop stays open, the triangulator returns only closed loops, and the surface is dropped as `ZeroTrianglesReturned`.

That yields a concrete, testable hypothesis for the pad (Solid 4838), whose measured drop reason is exactly `zeroTriangles=1`: its merge loses more than one edge, or leaves a chord or a second component, so the one repair that would have closed it refuses. Checking it does not need new instrumentation — it needs the degree histogram of `baseLoopEdges` at `:1144` for that surface.

### Nothing validates a brush's shape before the CSG sees it

- **Where:** `BrushMesh.Validate.cs:233`, `BrushMesh.Optimize.cs:313`/`:319`, `ChiselBoxDefinitions.cs:61`, `ChiselCylinderDefinition.cs:286`, `ChiselExtrudedShapeDefinition.cs:93`
- **How established:** read every validator; then ran Chisel's own rules offline over the whole sample scene

`ValidateShape` is three checks, and two of them are stubs: `IsSelfIntersecting()` is `return false;` under a TODO, and `HasVolume()` only checks that the arrays are non-empty — its own TODO says it never asks whether the brush is 1D or flat. Only `IsConcave()` does geometry. The generators are more permissive still: `ChiselBox.Validate()` swaps Min and Max and returns `true` (a zero dimension raises an inspector *warning*, "not allowed", and the flat box proceeds); `ChiselCylinder.Validate()` takes `abs()` of the diameters, clamps `sides` to 3 and returns `true`, never looking at height; `ChiselExtrudedShape.Validate()` is `return true;`. So a flat, collapsed or self-intersecting brush reaches the CSG with nothing having objected.

Measured rather than assumed, on the package's sample scene, by mirroring those rules exactly offline (`validate_brushes.py`, `validate_generators.py`; same constants, same Newell-plus-average plane fit) and adding the checks the stubs skip: **all 353 are valid.** The 121 explicit brushes pass Chisel's rules *and* the missing ones — positive volume, planar faces, every vertex inside every plane, no duplicate vertices — and the 232 generators' parameters have no zero dimension, flat cylinder, self-intersecting outline or collapsed path. So whatever that scene gets wrong, it is not a bad definition: it is the pipeline's handling of good ones, which only running the CSG can show.

The one systematic oddity is handedness: 52 of the 353 sit under a mirrored world transform, mostly through mirrored Composites. That is handled deliberately in the two places read so far — `CreateIntersectionLoopsJob` tests `math.determinant(...) < 0` and flips the normal its `atan2` sort uses (`:1162`, `:772`), apparently so intersection loops reverse together with the base loops `CreateBlobPolygonsBlobsJob` builds straight from the mesh's half-edge order; and output facing comes from the plane normal via `Map3DTo2D`, which the inverse-transpose keeps pointing outward under a mirror. Whether it is handled *everywhere* is not something reading settles, and this entry says so rather than calling it a defect.

### GOOD NEWS: the boolean logic itself is exact

- **Where:** `CreateRoutingTableJob.cs`
- **How established:** read the whole job

The job that turns the CSG tree into per-brush routing consults **no geometry at all** — no positions, no planes, no distances, no tolerances. Its only `math.` calls are `min`/`max` on node ids, and it decides everything from `CSGOperationType` and integer table lookups. So the boolean semantics are combinatorial and exact, and every robustness problem in this pipeline lives in the stages that decide *which category a piece of geometry falls into*, not in what the operations then do with those categories. That is a useful boundary: it says where work is worth spending and where it is not.

### Two more guessed capacities, unguarded, in the routing table

- **Where:** `CreateRoutingTableJob.cs:54` and `:77`
- **How established:** read; reachability not demonstrated

Same family as the overflow that crashed a real session (`Length 8211 exceeds Capacity 8192` in `CreateIntersectionLoopsJob`, fixed by counting instead of guessing). Two remain here:

- `queuedEvents` is `new NativeArray<QueuedEvent>(4096)` — a bare literal. Five sites write `queuedEvents[queuedEventCount++]` (`:247`, `:335`, `:346`, `:358`, `:371`) and **none** checks the bound. The loop at `:324` pushes one event per sibling that touches the brush being processed, so the count scales with touching-sibling count and nesting.
- `maxRoutes = maxNodes * kMaxRoutesPerNode`, where `kMaxRoutesPerNode = 32` carries *"TODO: figure out the actual possible theoretical maximum"* — the comment states outright that nobody knows the bound. It sizes five arrays (`tempStackArray`, `combineUsedIndices`, `combineIndexRemap`, `routingSteps`, `routingTable`), written through `outputLength++` at `:277`, `:283`, `:292`, `:301`, `:463` and `routingSteps[routingStepsLength]` at `:498`, again with no check.

**Status, 26 September 2026.** Both are fixed, and a third problem sat behind them. `queuedEvents` is sized exactly
from the tree (`MaxQueuedEvents`, 05f06b8) and the five arrays grow to what is written (e3cac4c). Growing only moved
the failure: the rows themselves grew exponentially with nesting, because `Combine` never merged EQUIVALENT rows - only
rows identical when written, before the rows they point to had been merged - and because its reachability bits
(`combineUsedIndices`) were never cleared between Combines, so copies nothing led to were written as well. From about
eleven nested subtractions the copy addresses (`routingRow + routingOffset`, a ushort) wrapped and both pipelines
dropped faces. `MergeEquivalentRows` now minimises every stack after every Combine, the way an acyclic automaton is
minimised (ac4cab5); the rows per node level off, and a node that would still need more rows than a ushort address can
reach in six copies is refused with an error naming the brush, instead of routing with wrapped addresses.

Worth noting these fail *worse* than the one already fixed. That was a `NativeList.AddNoResize`, which throws a clear exception. These are `NativeArray` indexed writes: with collection checks on they throw `IndexOutOfRange`, and in a Burst build with checks off they are silent out-of-bounds writes. Reachability is NOT established here — the fix pattern from `CountIntersectionLoopsJob` applies (derive the count instead of guessing it), but the first step is a test that drives the count up and shows where it lands. `outputSurfaceVertices` (`65535 * 10`, `// TODO: find actual vertex count`) is a third of the same family, still untouched.

### The centralized predicates are mostly unused, and their tests hide it

- **Where:** `Core/2.Processing/CSGMath.cs`
- **How established:** counted, after correcting the probe

`CSGMath` exists to hold the robustness-critical operations in one place, in double. Production callers: `SignedDistance` 6, `NewellTerm` 1, `Orient2D` 1. **Dead in production: `SqrDistance`, `VerticesEqual`, `EdgePlaneCrossing`, `PlaneIntersection`** — each used only by its own tests. (`DifferenceOfProducts`, `TwoSum` and `TwoProduct` first looked dead too; they are reached from `Orient2D` by unqualified intra-class calls that a grep for `CSGMath.` cannot see. Corrected rather than reported.) The whole compensated-arithmetic apparatus therefore serves exactly one call site, `MeshAlgorithms.Orientation`. And because the dead functions have passing tests, the module reads as healthy and covered — green tests over code nothing calls.

### The double-precision edge/plane crossing is dead; five float copies of it are live

- **Where:** `CSGMath.EdgePlaneCrossing:35` vs `FindLoopOverlapIntersectionJob:652`, `:660`, `:810`, `:818`, `BrushMesh.Utility.cs:624`
- **How established:** verified - zero production callers of the helper, five inline duplicates

`CSGMath.EdgePlaneCrossing` computes an edge/plane crossing with the delta and the lerp in **double**, and keeps the 'always interpolate from the positive side' branch whose stated purpose is cross-edge consistency. Nothing calls it. The four sites in `FindLoopOverlapIntersectionJob` reimplement it inline in **float**, carrying the same positive-side comment at `:647`. Those four are where one brush's loop is split against another brush's planes — precisely the place where two loops must agree on a shared point or the seam cracks. The centralized double version was written for this and is not used by it.

### The weld distance is computed in float, beside an unused double version

- **Where:** `HashedVertices.cs:57` vs `CSGMath.SqrDistance:24` / `VerticesEqual:28`
- **How established:** read

The weld compares `math.lengthsq(verticesPtr[chainIndex] - vertex)` — `float3` subtraction, `float` length-squared — against a `double closestDistance`. So the threshold is double and the quantity it judges is float. `CSGMath.SqrDistance` casts both to `double3` first and `VerticesEqual` wraps it against `kSqrVertexEqualEpsilon`, documented as matching the `HashedVertices` weld test. Neither is called.

### The author has already written down the fix, three times, in TODOs

- **Where:** `FindLoopOverlapIntersectionJob:349`–`:350`, and `:678` / `:834` / `:927`
- **How established:** read

At the site where one loop is split against another brush: *"TODO: merge these so that intersections will be identical on both loops (without using math, use logic)"* and *"TODO: make sure that intersections between loops will be identical on OTHER brushes (without using math, use logic)"*. Each loop currently computes its own crossing independently in float, so two loops that must share a split point are not guaranteed to get the same one — which is the crack mechanism, stated by the author. And three times over: *"TODO: store two end planes for each edge instead (the planes whose intersections with the infinite edge create the vertices)"* — plane-based vertex identity, noted at each of the three places that need it.

### A loop's topology is decided by an atan2 angular sort

- **Where:** `CreateIntersectionLoopsJob.SortIndices:103`
- **How established:** read; the author's own TODO sits on the line above it

The vertices of an intersection loop are ordered by `math.atan2` about the polygon's centroid, and that order IS the polygon's edge sequence — its topology. The TODO reads *"sort by using plane information instead of unreliable floating point math"*. Two vertices at nearly equal angle can order either way, and a wrong order produces a self-intersecting loop, which is exactly the non-simple-loop family that `PerformCSGJob` then cannot subtract and `GenerateSurfaceTrianglesJob` drops as `ZeroTrianglesReturned`. The centroid itself is a float sum over the loop's vertices (`:97`), so the sort key depends on accumulation order as well. This is topology derived from geometry, and it is the most likely origin of a missing surface that no epsilon will fix.

### The inside-brush test consults synthesized bevel planes, not just real faces

- **Where:** `CreateIntersectionLoopsJob.IsOutsidePlanes:67`, fed from `PrepareBrushPairIntersectionsJob:85`–`:86`
- **How established:** read, and confirmed at the length that is passed in

`ConvertToBrushMeshBlobJob` appends one extra plane per half-edge, `normalize(plane1.xyz + plane2.xyz)` through that edge's vertex, to stop vertices being accepted at very sharp angles. `PrepareBrushPairIntersectionsJob` then keeps two lengths: `intersectingPlaneLength = localPlaneCount` (the real faces) and `intersectingPlanesAndEdgesLength = localPlanes.Length` (faces **plus** those bevels). `IsOutsidePlanes` is called with the second. So whether a vertex counts as inside a brush is decided partly by planes that are an average of two neighbours and correspond to no face of the brush. A legitimate vertex on a sharp edge can be rejected by one.

### Why the fat band has to be as wide as it is

- **Where:** `kFatPlaneWidthEpsilon`, consumed by `IsOutsidePlanes:78`
- **How established:** reasoning from the two findings above; not measured

`IsOutsidePlanes` asks `dot(plane, vertex) <= kFatPlaneWidthEpsilon` against a **fitted** plane — one whose `d` is the average of `-dot(normal, vertex)` over the polygon, so it need not pass through any of its own vertices. The band therefore has to be wide enough to absorb the fit residual, on top of everything else it absorbs. That is a concrete reason to expect carrying authored planes across `BrushMeshPointers` to let the band shrink, and a concrete thing to measure before believing it: the distribution of `dot(fitted plane, its own vertices)` over a real map.

### NOT a bug: SortIndices picks its tangent basis with signed comparisons

- **Where:** `CreateIntersectionLoopsJob.SortIndices:110`–`:131`
- **How established:** checked, all four branches

It branches on `normal.x > normal.y` and `normal.y > normal.z` — **signed**, where `Map3DTo2D` and `MeshAlgorithms.LightmapAxes` both use magnitudes, so it reads like the usual dominant-axis bug. It is not. Enumerating the four branches against the reference axis each one picks: A `(x>y && x>z)` takes `(0,1,0)`, B `(x>y && x<=z)` takes `(0,0,1)`, C `(x<=y && y>z)` takes `(1,0,0)`, D `(x<=y && y<=z)` takes `(0,1,0)`. For each, a normal parallel to that reference fails the branch's own condition — e.g. branch A would need `x > y` with `normal ≈ (0,±1,0)`. So it never selects a reference parallel to the normal and the cross product never degenerates. Recorded because it looks wrong and 'fixing' it would change vertex ordering across every loop in the pipeline for no defect.

### NOT a bug: the IsOutsidePlanes SIMD loop bound is off by one group

- **Where:** `CreateIntersectionLoopsJob.IsOutsidePlanes:70`
- **How established:** checked by tracing both loops

The vectorised loop runs `for (; n + 4 < planesLength; n += 4)` while reading `planes[n+0..n+3]`, which needs only `n + 4 <= planesLength`. The bound is conservative by exactly one group, so with 8 planes the SIMD loop handles 0–3 and the scalar loop handles 4–7. Every plane is still tested. It costs a little throughput and tests nothing wrongly.

### Coplanarity depends on brush scale

- **Where:** `PrepareBrushPairIntersectionsJob.FindAlignedPlanes:390`, with the space set at `:539`
- **How established:** read, and confirmed at the source of the transform

Whether two faces count as lying on one plane is decided by `math.abs(localPlane1.w - localPlane2.w) < kPlaneWAlignEpsilon`. The two brushes' planes are compared in **brush 0's local space** — `:539` builds `node1ToNode0 = math.mul(transformations0.treeToNode, transformations1.nodeToTree)` and pushes brush 1's planes through it — so that `w` difference is a distance in brush 0's local units, while the epsilon is a fixed number that does not transform with it. Scale a brush and you scale the effective tolerance. The same geometry therefore aligns or fails to align depending on the transform it is authored under, which is a direct problem for hand-built levels where scaling a brush is ordinary. The toggle comment at `CSGManager.UpdateTreeMeshes.cs:181` says the same thing and is the reason `kUsePlaneIdsForAlignment` exists.

### NOT a bug: HashedVertices truncates where InternedPlanes floors

- **Where:** `HashedVertices.cs:29` and 6 more, vs `InternedPlanes.cs:152`
- **How established:** checked, and it holds

`InternedPlanes.CellOf` uses `math.floor` and comments (`:150`) that truncation makes a double-width cell straddling the origin. `HashedVertices` truncates via `(int)` in seven places, which does exactly that. But the cell size is 0.03125 and the weld radius 0.0125, so any two vertices within the weld ball still land in the same or an adjacent cell and the 27-cell search covers them. It doubles the occupancy of the origin cell and contradicts a comment explaining why it is wrong; it does not lose welds. Recorded so the next pass does not spend time re-deriving that.

### A corpus of 26 minimal failing scenes, harvested from the sample scene

- **Where:** `Core/Tests/Contents/SampleSceneExtraFaceTests.cs` (20), `SampleSceneHoleTests.cs` (6); harvester `ContentsHarvest.HarvestSites`
- **How established:** ran: the real CSG over each sample-scene model's dumped tree, judged by the independent oracle; every case shrunk, re-verified, and watched FAIL in the editor (all 26 on the oracle assertion, none on an exception)

Every model of the package's sample scene was replayed through the contents harness and every disagreement with the oracle shrunk, per failing spot, to the fewest brushes that still fail there. Model3 agrees everywhere; Model1 gives 9 distinct minimal scenes, Model2 7, Model4 10. **Every one is 3 or 4 brushes with no composite left, and every failing point lies on a plane another brush shares** (nearest other plane 0): the whole corpus is coplanar contact.

What it says beyond the individual cases:

- **Subtraction is not needed.** 10 of the 26 are unions of additive brushes only - Model2 F6 is three additive boxes, and one of them loses a face over 1390 sample points. Whatever goes wrong at a coplanar contact already goes wrong in the plain additive case, which every hand-built level is full of.
- **Two directions of failure at the same kind of place.** 20 are EXTRA faces (drawn where both sides are filled or both empty, typically one brush's face drawn across its coplanar neighbour's) and 6 are HOLES (a face the oracle requires, drawn nowhere). A hole can be a whole face: Model1 F1 loses the entire top of a 26 x 26 floor (1079 samples) once one subtractive and one small additive box touch it.
- **Sander's report is Model2 F2, F5 and F7**: `Extruded Shape (Additive)` drawn at y = -1 and y = -2 where the oracle expects nothing, with `Extruded Shape (Remove)` and the boxes it is flush with. F2 did not reproduce from any neighbourhood of its site (0.5, 2 or 8 units around the failing points, or the face + 1) and had to be shrunk from the whole 174-brush model, yet it ends at 4 brushes. So at least one of those four sits away from the failing spot and still decides it - a long-range dependency that a repair looking only locally would miss.
- Model4's ten are one configuration ten times: a row of identical subtractive boxes whose bottoms lie on the top of a pair of additive boxes, and each cutter shows the additive's face where nothing belongs. Kept as ten tests because each sits at different coordinates, which is where float decisions differ.

How to use it: run category `CSGCorpus` (red on purpose) against any change to routing, loops, welding, the hole merge or triangulation, so a fix for one case is judged against all of them. Re-harvesting is cheap - about 380 CSG runs in 83 s for all four models - by calling `SampleSceneHarvestTests.Harvest` from `script_evaluate` in an empty scene (the harness refuses to flush with a model open, and the test runner would save or reload the open scene).

What it cannot see: brushes are rebuilt from their planes (`CreateFromPlanes`), not from the generators' own vertices; only points away from crossing planes (5 cm band) are judged; and it runs fresh CSG, so it says nothing about stale saved output. No CSG run threw, and none of the known crash signatures appeared.

## The pipeline


### Build Lookup Tables

#### `BuildLookupTablesJob` — scheduler line 1073

- **Read:** `brushes`, `brushCount`
- **Read/Write:** `nodeIDValueToNodeOrder`
- **Write:** `nodeIDValueToNodeOrderOffsetRef`, `allTreeBrushIndexOrders`
- *declared handle reads:* `brushesJobHandle`, `nodeIDValueToNodeOrderArrayJobHandle`, `nodeIDValueToNodeOrderOffsetRefJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `brushIDValuesJobHandle`
- *declared handle writes:* `nodeIDValueToNodeOrderArrayJobHandle`, `nodeIDValueToNodeOrderOffsetRefJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `basePolygonCacheJobHandle`, `routingTableCacheJobHandle`, `transformationCacheJobHandle`, `brushRenderBufferCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `loopVerticesCacheJobHandle`, `brushesThatNeedIndirectUpdateHashMapJobHandle`


### CacheRemapping

#### `CacheRemappingJob` — scheduler line 1101

- **Read:** `nodeIDValueToNodeOrder`, `nodeIDValueToNodeOrderOffsetRef`, `brushes`, `brushCount`, `allTreeBrushIndexOrders`, `brushIDValues`, `compactHierarchy`
- **Read/Write:** `basePolygonCache`, `routingTableCache`, `transformationCache`, `brushRenderBufferCache`, `treeSpaceVerticesCache`, `brushTreeSpacePlaneCache`, `brushTreeSpaceBoundCache`, `brushesTouchedByBrushCache`, `loopVerticesCache`
- **Write:** `brushesThatNeedIndirectUpdateHashMap`, `needRemappingRef`
- *declared handle reads:* `nodeIDValueToNodeOrderArrayJobHandle`, `nodeIDValueToNodeOrderOffsetRefJobHandle`, `brushesJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `brushIDValuesJobHandle`
- *declared handle writes:* `basePolygonCacheJobHandle`, `routingTableCacheJobHandle`, `transformationCacheJobHandle`, `brushRenderBufferCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `loopVerticesCacheJobHandle`, `brushesThatNeedIndirectUpdateHashMapJobHandle`, `needRemappingRefJobHandle`, `brushIDValuesJobHandle`


### Update BrushID Values

#### `UpdateBrushIDValuesJob` — scheduler line 1153

- **Read:** `brushes`, `brushCount`
- **Read/Write:** `brushIDValues`
- *declared handle reads:* `brushesJobHandle`, `allTreeBrushIndexOrdersJobHandle`
- *declared handle writes:* `brushIDValuesJobHandle`, `compactHierarchyJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`, `transformTreeBrushIndicesListJobHandle`


### Find Modified Brushes

#### `FindModifiedBrushesJob` — scheduler line 1177

- **Read:** `brushes`, `brushCount`, `allTreeBrushIndexOrders`, `compactHierarchy`
- **Read/Write:** `rebuildTreeBrushIndexOrders`
- **Write:** `transformTreeBrushIndicesList`
- *declared handle reads:* `brushesJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `decalVolumesJobHandle`, `brushTreeSpaceBoundCacheJobHandle`
- *declared handle writes:* `compactHierarchyJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`, `transformTreeBrushIndicesListJobHandle`


### Find Decal Affected Brushes

#### `FindDecalAffectedBrushesJob` — scheduler line 1213

- **Read:** `changedDecalBounds`, `brushTreeSpaceBoundCache`, `allTreeBrushIndexOrders`, `brushCount`
- **Read/Write:** `rebuildTreeBrushIndexOrders`
- *declared handle reads:* `decalVolumesJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `needRemappingRefJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `brushesJobHandle`, `nodeIDValueToNodeOrderArrayJobHandle`, `nodeIDValueToNodeOrderOffsetRefJobHandle`, `compactHierarchyJobHandle`
- *declared handle writes:* `rebuildTreeBrushIndexOrdersJobHandle`, `brushesThatNeedIndirectUpdateHashMapJobHandle`


### Invalidate Brushes

#### `InvalidateBrushesJob` — scheduler line 1238

- **Read:** `needRemappingRef`, `rebuildTreeBrushIndexOrders`, `brushesTouchedByBrushCache`, `brushes`, `brushCount`, `nodeIDValueToNodeOrder`, `nodeIDValueToNodeOrderOffsetRef`, `compactHierarchy`
- **Write:** `brushesThatNeedIndirectUpdateHashMap`
- *declared handle reads:* `needRemappingRefJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `brushesJobHandle`, `nodeIDValueToNodeOrderArrayJobHandle`, `nodeIDValueToNodeOrderOffsetRefJobHandle`, `compactHierarchyJobHandle`, `brushMeshBlobsLookupJobHandle`
- *declared handle writes:* `brushesThatNeedIndirectUpdateHashMapJobHandle`, `allKnownBrushMeshIndicesJobHandle`, `parametersJobHandle`, `parameterCountsJobHandle`, `allBrushMeshIDsJobHandle`


### Update BrushMesh IDs

#### `UpdateBrushMeshIDsJob` — scheduler line 1273

- **Read:** `brushMeshBlobs`, `brushCount`, `brushes`, `compactHierarchy`
- **Read / Write:** `allKnownBrushMeshIndices`, `parameters`, `parameterCounts`
- **Write:** `allBrushMeshIDs`
- *declared handle reads:* `brushMeshBlobsLookupJobHandle`, `brushesJobHandle`, `compactHierarchyJobHandle`, `transformTreeBrushIndicesListJobHandle`
- *declared handle writes:* `allKnownBrushMeshIndicesJobHandle`, `parametersJobHandle`, `parameterCountsJobHandle`, `allBrushMeshIDsJobHandle`, `transformationCacheJobHandle`


### Perform CSG / Prepare / Update Transformations

#### `UpdateTransformationsJob` — scheduler line 1318

- **Read:** `transformTreeBrushIndicesList`, `compactHierarchy`
- **Write:** `transformationCache`
- *declared handle reads:* `transformTreeBrushIndicesListJobHandle`, `compactHierarchyJobHandle`, `brushesJobHandle`, `nodesJobHandle`, `brushMeshBlobsLookupJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `allBrushMeshIDsJobHandle`
- *declared handle writes:* `transformationCacheJobHandle`, `compactTreeRefJobHandle`, `brushMeshLookupJobHandle`


### Perform CSG / Prepare / Build CSG Tree

#### `BuildCompactTreeJob` — scheduler line 1340

- **Read:** `treeCompactNodeID`, `contentsCount`, `brushes`, `nodes`, `compactHierarchy`
- **Write:** `compactTreeRef`
- *declared handle reads:* `brushesJobHandle`, `nodesJobHandle`, `compactHierarchyJobHandle`, `brushMeshBlobsLookupJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `allBrushMeshIDsJobHandle`
- *declared handle writes:* `compactTreeRefJobHandle`, `brushMeshLookupJobHandle`, `surfaceCountRefJobHandle`


### Perform CSG / Prepare / Update BrushMeshBlob Lookup table

#### `FillBrushMeshBlobLookupJob` — scheduler line 1368

- **Read:** `brushMeshBlobs`, `allTreeBrushIndexOrders`, `allBrushMeshIDs`
- **Write:** `brushMeshLookup`, `surfaceCountRef`
- *declared handle reads:* `brushMeshBlobsLookupJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `allBrushMeshIDsJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`
- *declared handle writes:* `brushMeshLookupJobHandle`, `surfaceCountRefJobHandle`, `basePolygonCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `routingTableCacheJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `brushRenderBufferCacheJobHandle`


### Perform CSG / Prepare / Invalidate outdated caches

#### `InvalidateBrushCacheJob` — scheduler line 1395

- **Read:** `rebuildTreeBrushIndexOrders`
- **Read/Write:** `basePolygonCache`, `treeSpaceVerticesCache`, `brushesTouchedByBrushCache`, `routingTableCache`, `brushTreeSpacePlaneCache`, `brushRenderBufferCache`
- **Write:** `basePolygonDisposeList`, `routingTableDisposeList`, `brushRenderBufferDisposeList`, `treeSpaceVerticesDisposeList`, `brushTreeSpacePlaneDisposeList`, `brushesTouchedByBrushDisposeList`
- *declared handle reads:* `rebuildTreeBrushIndexOrdersJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `nodeIDValueToNodeOrderArrayJobHandle`, `nodeIDValueToNodeOrderOffsetRefJobHandle`
- *declared handle writes:* `basePolygonCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `routingTableCacheJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `brushRenderBufferCacheJobHandle`


### Perform CSG / Prepare / Fixup brush cache data order

#### `FixupBrushCacheIndicesJob` — scheduler line 1434

- **Read:** `allTreeBrushIndexOrders`, `nodeIDValueToNodeOrder`, `nodeIDValueToNodeOrderOffsetRef`, `basePolygonCache`, `brushesTouchedByBrushCache`
- *declared handle reads:* `allTreeBrushIndexOrdersJobHandle`, `nodeIDValueToNodeOrderArrayJobHandle`, `nodeIDValueToNodeOrderOffsetRefJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`, `transformationCacheJobHandle`, `brushMeshLookupJobHandle`, `brushMeshBlobsLookupJobHandle`, `compactHierarchyJobHandle`
- *declared handle writes:* `basePolygonCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `compactHierarchyJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`


### Perform CSG / Prepare / Update brush tree space vertices and bounds

#### `CreateTreeSpaceVerticesAndBoundsJob` — scheduler line 1462

- **Read:** `rebuildTreeBrushIndexOrders`, `transformationCache`, `brushMeshLookup`
- **Read / Write:** `compactHierarchyManager`
- **Write:** `brushTreeSpaceBounds`, `treeSpaceVerticesCache`
- *declared handle reads:* `rebuildTreeBrushIndexOrdersJobHandle`, `transformationCacheJobHandle`, `brushMeshLookupJobHandle`, `brushMeshBlobsLookupJobHandle`, `compactHierarchyJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `brushTreeSpaceBoundCacheJobHandle`
- *declared handle writes:* `compactHierarchyJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushBoundsSweepJobHandle`


### Perform CSG / Prepare / Build the bounds broad-phase

#### `BuildBrushBoundsSweepJob` — scheduler line 1497

- **Read:** `allTreeBrushIndexOrders`, `brushTreeSpaceBounds`
- **Write:** `sweepEntries`
- *declared handle reads:* `allTreeBrushIndexOrdersJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `transformationCacheJobHandle`, `brushMeshLookupJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`, `brushBoundsSweepJobHandle`
- *declared handle writes:* `brushBoundsSweepJobHandle`, `brushBrushIntersectionsJobHandle`, `brushesThatNeedIndirectUpdateHashMapJobHandle`


### Perform CSG / Prepare / Update intersection pairs

#### `FindAllBrushIntersectionPairsJob` — scheduler line 1521

- **Read:** `allTreeBrushIndexOrders`, `transformationCache`, `brushMeshLookup`, `brushTreeSpaceBounds`, `rebuildTreeBrushIndexOrders`, `brushBoundsSweep`
- **Read / Write:** `allocator`, `brushBrushIntersections`
- **Write:** `brushesThatNeedIndirectUpdateHashMap`
- *declared handle reads:* `allTreeBrushIndexOrdersJobHandle`, `transformationCacheJobHandle`, `brushMeshLookupJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`, `brushBoundsSweepJobHandle`, `brushesThatNeedIndirectUpdateHashMapJobHandle`, `brushesThatNeedIndirectUpdateJobHandle`
- *declared handle writes:* `brushBrushIntersectionsJobHandle`, `brushesThatNeedIndirectUpdateHashMapJobHandle`, `brushesThatNeedIndirectUpdateJobHandle`


### Perform CSG / Prepare / Update list of brushes that touch brushes

#### `FindUniqueIndirectBrushIntersectionsJob` — scheduler line 1558

- **Read:** `brushesThatNeedIndirectUpdateHashMap`
- **Read / Write:** `brushesThatNeedIndirectUpdate`
- *declared handle reads:* `brushesThatNeedIndirectUpdateHashMapJobHandle`, `brushesThatNeedIndirectUpdateJobHandle`
- *declared handle writes:* `brushesThatNeedIndirectUpdateJobHandle`, `basePolygonCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `routingTableCacheJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `brushRenderBufferCacheJobHandle`


### Perform CSG / Prepare / Invalidate indirectly outdated caches (when brush touches a brush that has changed)

#### `InvalidateIndirectBrushCacheJob` — scheduler line 1580

- **Read:** `brushesThatNeedIndirectUpdate`
- **Read/Write:** `basePolygonCache`, `treeSpaceVerticesCache`, `brushesTouchedByBrushCache`, `routingTableCache`, `brushTreeSpacePlaneCache`, `brushRenderBufferCache`
- *declared handle reads:* `brushesThatNeedIndirectUpdateJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`, `transformationCacheJobHandle`, `brushMeshLookupJobHandle`, `brushMeshBlobsLookupJobHandle`, `compactHierarchyJobHandle`
- *declared handle writes:* `basePolygonCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `routingTableCacheJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `brushRenderBufferCacheJobHandle`, `compactHierarchyJobHandle`, `brushTreeSpaceBoundCacheJobHandle`


### Perform CSG / Prepare / Fixup indirectly brush cache data order (when brush touches a brush that has changed)

#### `CreateTreeSpaceVerticesAndBoundsJob` — scheduler line 1611

- **Read:** `rebuildTreeBrushIndexOrders`, `transformationCache`, `brushMeshLookup`
- **Read / Write:** `compactHierarchyManager`
- **Write:** `brushTreeSpaceBounds`, `treeSpaceVerticesCache`
- *declared handle reads:* `brushesThatNeedIndirectUpdateJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`, `transformationCacheJobHandle`, `brushMeshLookupJobHandle`, `brushMeshBlobsLookupJobHandle`, `compactHierarchyJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `brushBoundsSweepJobHandle`
- *declared handle writes:* `compactHierarchyJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushBrushIntersectionsJobHandle`


### Perform CSG / Prepare / Update intersection pairs (when brush touches a brush that has changed)

#### `FindAllIndirectBrushIntersectionPairsJob` — scheduler line 1645

- **Read:** `allTreeBrushIndexOrders`, `transformationCache`, `brushMeshLookup`, `brushTreeSpaceBounds`, `brushesThatNeedIndirectUpdate`, `brushBoundsSweep`
- **Read / Write:** `allocator`, `brushBrushIntersections`
- *declared handle reads:* `allTreeBrushIndexOrdersJobHandle`, `transformationCacheJobHandle`, `brushMeshLookupJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `brushesThatNeedIndirectUpdateJobHandle`, `brushBoundsSweepJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`, `brushBrushIntersectionsJobHandle`, `brushIntersectionsWithJobHandle`
- *declared handle writes:* `brushBrushIntersectionsJobHandle`, `allUpdateBrushIndexOrdersJobHandle`


### Perform CSG / Prepare / Update list of brushes that touch brushes (when brush touches a brush that has changed)

#### `AddIndirectUpdatedBrushesToListAndSortJob` — scheduler line 1677

- **Read:** `allTreeBrushIndexOrders`, `brushesThatNeedIndirectUpdate`, `rebuildTreeBrushIndexOrders`
- **Write:** `allUpdateBrushIndexOrders`
- *declared handle reads:* `allTreeBrushIndexOrdersJobHandle`, `brushesThatNeedIndirectUpdateJobHandle`, `rebuildTreeBrushIndexOrdersJobHandle`, `brushBrushIntersectionsJobHandle`, `brushIntersectionsWithJobHandle`, `compactTreeRefJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `brushIntersectionsWithRangeJobHandle`
- *declared handle writes:* `allUpdateBrushIndexOrdersJobHandle`, `brushIntersectionsWithJobHandle`, `brushIntersectionsWithRangeJobHandle`, `brushesTouchedByBrushCacheJobHandle`


### Perform CSG / Prepare / Gather all brush intersections

#### `GatherBrushIntersectionPairsJob` — scheduler line 1702

- **Read:** `brushBrushIntersections`
- **Write:** `brushIntersectionsWithRange`
- **Read / Write:** `brushIntersectionsWith`
- *declared handle reads:* `brushBrushIntersectionsJobHandle`, `brushIntersectionsWithJobHandle`, `compactTreeRefJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `brushIntersectionsWithRangeJobHandle`
- *declared handle writes:* `brushIntersectionsWithJobHandle`, `brushIntersectionsWithRangeJobHandle`, `brushesTouchedByBrushCacheJobHandle`

#### `StoreBrushIntersectionsJob` — scheduler line 1721

- **Read:** `treeCompactNodeID`, `compactTreeRef`, `allTreeBrushIndexOrders`, `allUpdateBrushIndexOrders`, `brushIntersectionsWith`, `brushIntersectionsWithRange`
- **Write:** `brushesTouchedByBrushCache`
- *declared handle reads:* `compactTreeRefJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `brushIntersectionsWithJobHandle`, `brushIntersectionsWithRangeJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `uniqueBrushPairsJobHandle`, `brushMeshLookupJobHandle`
- *declared handle writes:* `brushesTouchedByBrushCacheJobHandle`, `uniqueBrushPairsJobHandle`, `intersectionLoopCountRefJobHandle`


### Perform CSG / Determine Intersection Surfaces

#### `FindBrushPairsJob` — scheduler line 1759

- **Read:** `maxOrder`, `allUpdateBrushIndexOrders`, `brushesTouchedByBrushes`, `uniqueBrushPairs`
- *declared handle reads:* `allUpdateBrushIndexOrdersJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `uniqueBrushPairsJobHandle`, `brushMeshLookupJobHandle`, `transformationCacheJobHandle`
- *declared handle writes:* `uniqueBrushPairsJobHandle`, `intersectionLoopCountRefJobHandle`, `intersectingBrushesStreamJobHandle`, `internedPlanesJobHandle`

#### `CountIntersectionLoopsJob` — scheduler line 1777

- **Read:** `uniqueBrushPairs`, `brushMeshLookup`
- **Write:** `intersectionLoopCountRef`
- *declared handle reads:* `uniqueBrushPairsJobHandle`, `brushMeshLookupJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `transformationCacheJobHandle`
- *declared handle writes:* `intersectionLoopCountRefJobHandle`, `intersectingBrushesStreamJobHandle`, `internedPlanesJobHandle`

#### `InternBrushPlanesJob` — scheduler line 1806

- **Read:** `allUpdateBrushIndexOrders`, `brushMeshLookup`, `transformationCache`
- **Write:** `internedPlanes`, `brushPlaneIds`, `brushPlaneIdRange`
- *declared handle reads:* `allUpdateBrushIndexOrdersJobHandle`, `brushMeshLookupJobHandle`, `transformationCacheJobHandle`, `uniqueBrushPairsJobHandle`, `internedPlanesJobHandle`
- *declared handle writes:* `internedPlanesJobHandle`, `intersectingBrushesStreamJobHandle`, `brushTreeSpacePlaneCacheJobHandle`

#### `PrepareBrushPairIntersectionsJob` — scheduler line 1827

- **Read:** `uniqueBrushPairs`, `transformationCache`, `brushMeshLookup`, `usePlaneIds`, `brushPlaneIds`, `brushPlaneIdRange`, `canonicalAlignment`
- **Write:** `intersectingBrushesStream`
- *declared handle reads:* `uniqueBrushPairsJobHandle`, `transformationCacheJobHandle`, `brushMeshLookupJobHandle`, `internedPlanesJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushTreeSpacePlaneCacheJobHandle`
- *declared handle writes:* `intersectingBrushesStreamJobHandle`, `brushTreeSpacePlaneCacheJobHandle`

#### `CreateBrushTreeSpacePlanesJob` — scheduler line 1856

- **Read:** `allUpdateBrushIndexOrders`, `brushMeshLookup`, `transformationCache`
- **Write:** `brushTreeSpacePlanes`
- *declared handle reads:* `allUpdateBrushIndexOrdersJobHandle`, `brushMeshLookupJobHandle`, `transformationCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `intersectionLoopCountRefJobHandle`
- *declared handle writes:* `brushTreeSpacePlaneCacheJobHandle`, `basePolygonCacheJobHandle`, `outputSurfacesJobHandle`

#### `CreateBlobPolygonsBlobsJob` — scheduler line 1881

- **Read:** `allUpdateBrushIndexOrders`, `brushesTouchedByBrushCache`, `brushMeshLookup`, `treeSpaceVerticesCache`, `canonicalVertexStage`, `brushTreeSpacePlaneCache`
- **Write:** `basePolygonCache`
- *declared handle reads:* `allUpdateBrushIndexOrdersJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `brushMeshLookupJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `intersectionLoopCountRefJobHandle`, `uniqueBrushPairsJobHandle`, `intersectingBrushesStreamJobHandle`
- *declared handle writes:* `basePolygonCacheJobHandle`, `outputSurfacesJobHandle`, `outputSurfaceVerticesJobHandle`

#### `CreateIntersectionLoopsJob` — scheduler line 1918

- **unlabelled:** `useIncidenceWeld`, `uniqueBrushPairs`
- **Read:** `brushTreeSpacePlaneCache`, `treeSpaceVerticesCache`, `intersectingBrushesStream`, `canonicalVertexStage`, `brushesTouchedByBrushCache`
- **Write:** `outputSurfaceVertices`, `outputSurfaces`
- *declared handle reads:* `uniqueBrushPairsJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `treeSpaceVerticesCacheJobHandle`, `intersectingBrushesStreamJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `outputSurfacesJobHandle`, `allUpdateBrushIndexOrdersJobHandle`
- *declared handle writes:* `outputSurfaceVerticesJobHandle`, `outputSurfacesJobHandle`, `outputSurfacesRangeJobHandle`, `dataStream1JobHandle`

#### `GatherOutputSurfacesJob` — scheduler line 1952

- **unlabelled:** `outputSurfaces`
- **Write:** `outputSurfacesRange`
- *declared handle reads:* `outputSurfacesJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `loopVerticesCacheJobHandle`
- *declared handle writes:* `outputSurfacesJobHandle`, `outputSurfacesRangeJobHandle`, `dataStream1JobHandle`, `loopVerticesLookupJobHandle`

#### `SeedLoopVerticesFromCacheJob` — scheduler line 1986

- **Read:** `allTreeBrushIndexOrders`, `allUpdateBrushIndexOrders`, `loopVerticesCache`, `allocator`
- **Write:** `loopVerticesLookup`
- *declared handle reads:* `allTreeBrushIndexOrdersJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `loopVerticesCacheJobHandle`, `outputSurfaceVerticesJobHandle`, `outputSurfacesJobHandle`, `outputSurfacesRangeJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `basePolygonCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`
- *declared handle writes:* `loopVerticesLookupJobHandle`, `dataStream1JobHandle`

#### `FindLoopOverlapIntersectionsJob` — scheduler line 2006

- **unlabelled:** `useIncidenceWeld`
- **Read:** `allUpdateBrushIndexOrders`, `outputSurfaceVertices`, `outputSurfaces`, `outputSurfacesRange`, `maxNodeOrder`, `brushTreeSpacePlaneCache`, `basePolygonCache`, `canonicalVertexStage`, `brushesTouchedByBrushCache`, `allocator`, `loopVerticesLookup`
- **Write:** `output`
- *declared handle reads:* `allUpdateBrushIndexOrdersJobHandle`, `outputSurfaceVerticesJobHandle`, `outputSurfacesJobHandle`, `outputSurfacesRangeJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `basePolygonCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`
- *declared handle writes:* `loopVerticesLookupJobHandle`, `dataStream1JobHandle`


### Perform CSG / Merge vertices

#### `MergeTouchingBrushVerticesIndirectJob` — scheduler line 2063

- **unlabelled:** `useIncidenceWeld`, `canonicalVertexStage`, `brushTreeSpacePlaneCache`, `basePolygonCache`
- **Read:** `allUpdateBrushIndexOrders`, `brushesTouchedByBrushCache`, `treeSpaceVerticesArray`, `loopVerticesLookup`, `iterationIndex`, `brushState`
- **Write:** `loopVerticesLookupOut`
- *declared handle reads:* `brushTreeSpacePlaneCacheJobHandle`, `basePolygonCacheJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `treeSpaceVerticesCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `loopVerticesLookupJobHandle`, `loopVerticesLookupOutJobHandle`, `mergeBrushStateJobHandle`
- *declared handle writes:* `loopVerticesLookupOutJobHandle`, `mergeBrushStateJobHandle`, `loopVerticesLookupJobHandle`

#### `CopyBackLoopVerticesJob` — scheduler line 2094

- **unlabelled:** `allUpdateBrushIndexOrders`, `loopVerticesLookupOut`, `loopVerticesLookup`, `brushState`, `iterationIndex`
- *declared handle reads:* `allUpdateBrushIndexOrdersJobHandle`, `loopVerticesLookupOutJobHandle`, `mergeBrushStateJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `loopVerticesLookupJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `brushTreeSpaceBoundCacheJobHandle`
- *declared handle writes:* `loopVerticesLookupJobHandle`, `loopVerticesCacheJobHandle`, `staleLoopBrushesJobHandle`


### Perform CSG / Persist merged loop vertices, find the brushes this update left stale

#### `StoreLoopVerticesJob` — scheduler line 2120

- **Read:** `allUpdateBrushIndexOrders`, `allTreeBrushIndexOrders`, `loopVerticesLookup`, `brushesTouchedByBrushCache`, `brushTreeSpaceBounds`, `mergeBrushState`, `lastMergeIteration`
- **Read / Write:** `loopVerticesCache`
- **Write:** `staleBrushes`, `stats`
- *declared handle reads:* `allUpdateBrushIndexOrdersJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `loopVerticesLookupJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `mergeBrushStateJobHandle`, `compactTreeRefJobHandle`
- *declared handle writes:* `loopVerticesCacheJobHandle`, `staleLoopBrushesJobHandle`, `routingTableCacheJobHandle`, `dataStream2JobHandle`


### Perform CSG / Perform CSG

#### `CreateRoutingTableJob` — scheduler line 2162

- **Read:** `allUpdateBrushIndexOrders`, `brushesTouchedByBrushes`, `compactTreeRef`
- **Write:** `routingTableLookup`
- *declared handle reads:* `allUpdateBrushIndexOrdersJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `compactTreeRefJobHandle`, `routingTableCacheJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `dataStream1JobHandle`, `loopVerticesLookupJobHandle`
- *declared handle writes:* `routingTableCacheJobHandle`, `dataStream2JobHandle`

#### `PerformCSGJob` — scheduler line 2195

- **unlabelled:** `useIncidenceWeld`, `canonicalVertexStage`
- **Read:** `allUpdateBrushIndexOrders`, `routingTableCache`, `brushTreeSpacePlaneCache`, `brushesTouchedByBrushCache`, `loopVerticesLookup`, `compactTreeRef`, `input`
- **Write:** `output`
- *declared handle reads:* `compactTreeRefJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `routingTableCacheJobHandle`, `brushTreeSpacePlaneCacheJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `dataStream1JobHandle`, `loopVerticesLookupJobHandle`, `basePolygonCacheJobHandle`, `transformationCacheJobHandle`, `dataStream2JobHandle`, `meshQueriesJobHandle`, `decalVolumesJobHandle`
- *declared handle writes:* `dataStream2JobHandle`, `brushRenderBufferCacheJobHandle`


### Perform CSG / Triangulate Surfaces

#### `GenerateSurfaceTrianglesJob` — scheduler line 2235

- **unlabelled:** `useIncidenceWeld`, `canonicalVertexStage`, `brushTreeSpacePlaneCache`
- **Read:** `allUpdateBrushIndexOrders`, `basePolygonCache`, `transformationCache`, `loopVerticesLookup`, `brushesTouchedByBrushCache`, `input`, `meshQueries`, `entityIDLookup`, `subtractiveWorkflow`, `normalSmoothingAngle`, `decalVolumes`, `decalTargets`
- **Write:** `brushRenderBufferCache`
- *declared handle reads:* `brushTreeSpacePlaneCacheJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `basePolygonCacheJobHandle`, `transformationCacheJobHandle`, `loopVerticesLookupJobHandle`, `brushesTouchedByBrushCacheJobHandle`, `dataStream2JobHandle`, `meshQueriesJobHandle`, `decalVolumesJobHandle`, `allTreeBrushIndexOrdersJobHandle`
- *declared handle writes:* `brushRenderBufferCacheJobHandle`, `brushRenderDataJobHandle`


### Store Results / Find all generated brush specific geometry

#### `FindBrushRenderBuffersJob` — scheduler line 2312

- **Read:** `meshQueryLength`, `allTreeBrushIndexOrders`, `brushRenderBufferCache`
- **Write:** `brushRenderData`
- **Read/Write:** `surfaceCountRef`
- *declared handle reads:* `meshQueriesJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `brushRenderBufferCacheJobHandle`, `surfaceCountRefJobHandle`, `brushRenderDataJobHandle`
- *declared handle writes:* `brushRenderDataJobHandle`, `surfaceCountRefJobHandle`, `subMeshDescriptionsJobHandle`, `vertexBufferContents_subMeshSectionsJobHandle`, `subMeshSurfacesJobHandle`


### Store Results / Allocate sub meshes

#### `AllocateSubMeshesJob` — scheduler line 2340

- **Read:** `meshQueryLength`, `surfaceCountRef`
- **Read/Write:** `subMeshDescriptions`, `subMeshSections`
- *declared handle reads:* `meshQueriesJobHandle`, `surfaceCountRefJobHandle`, `brushRenderDataJobHandle`, `subMeshSurfacesJobHandle`
- *declared handle writes:* `subMeshDescriptionsJobHandle`, `vertexBufferContents_subMeshSectionsJobHandle`, `subMeshSurfacesJobHandle`


### Store Results / Prepare sub sections

#### `PrepareSubSectionsJob` — scheduler line 2364

- **Read:** `meshQueries`, `brushRenderData`
- **Write:** `allocator`, `subMeshSurfaces`
- *declared handle reads:* `meshQueriesJobHandle`, `brushRenderDataJobHandle`, `subMeshSurfacesJobHandle`, `subMeshDescriptionsJobHandle`
- *declared handle writes:* `subMeshSurfacesJobHandle`, `subMeshDescriptionsJobHandle`, `vertexBufferContents_subMeshSectionsJobHandle`, `vertexBufferContents_meshDescriptionsJobHandle`


### Store Results / Sort surfaces

#### `SortSurfacesParallelJob` — scheduler line 2386

- **Read:** `meshQueries`, `subMeshSurfaces`
- **Write:** `subMeshDescriptions`
- *declared handle reads:* `meshQueriesJobHandle`, `subMeshSurfacesJobHandle`, `subMeshDescriptionsJobHandle`
- *declared handle writes:* `subMeshDescriptionsJobHandle`, `vertexBufferContents_subMeshSectionsJobHandle`, `vertexBufferContents_meshDescriptionsJobHandle`

#### `GatherSurfacesJob` — scheduler line 2401

- **Read / Write:** `subMeshDescriptions`
- **Write:** `subMeshSections`
- *declared handle reads:* `subMeshDescriptionsJobHandle`
- *declared handle writes:* `subMeshDescriptionsJobHandle`, `vertexBufferContents_subMeshSectionsJobHandle`, `vertexBufferContents_meshDescriptionsJobHandle`


### Store Results / Generate mesh descriptions

#### `GenerateMeshDescriptionJob` — scheduler line 2422

- **Read:** `subMeshDescriptions`, `meshDescriptions`
- *declared handle reads:* `subMeshDescriptionsJobHandle`
- *declared handle writes:* `vertexBufferContents_meshDescriptionsJobHandle`


### Store Results / Create Meshes

#### `AssignMeshesJob` — scheduler line 2473

- **Read:** `meshDescriptions`, `subMeshSections`, `meshDatas`
- **Write:** `meshes`, `meshUpdatesColliders`, `meshUpdatesRenderable`, `meshUpdatesDebugVisualization`
- *declared handle reads:* `vertexBufferContents_meshDescriptionsJobHandle`, `vertexBufferContents_subMeshSectionsJobHandle`, `meshDatasJobHandle`, `subMeshDescriptionsJobHandle`, `subMeshSurfacesJobHandle`, `vertexBufferContents_renderDescriptorsJobHandle`, `vertexBufferContents_colliderDescriptorsJobHandle`, `meshUpdatesJobHandle`
- *declared handle writes:* `vertexBufferContents_meshesJobHandle`, `debugHelperMeshesJobHandle`, `renderMeshesJobHandle`, `meshUpdatesJobHandle`, `colliderMeshUpdatesJobHandle`

#### `OutputCopyJob` — scheduler line 2507

- **Read:** `subMeshSource`, `descriptors`, `meshUpdates`
- **Read/Write:** `meshDataArray`
- *declared handle reads:* `vertexBufferContents_subMeshSectionsJobHandle`, `subMeshDescriptionsJobHandle`, `subMeshSurfacesJobHandle`, `vertexBufferContents_renderDescriptorsJobHandle`, `vertexBufferContents_colliderDescriptorsJobHandle`, `meshUpdatesJobHandle`
- *declared handle writes:* `vertexBufferContents_meshesJobHandle`

#### `OutputCopyJob` — scheduler line 2527

- **Read:** `subMeshSource`, `descriptors`, `meshUpdates`
- **Read/Write:** `meshDataArray`
- *declared handle reads:* `vertexBufferContents_subMeshSectionsJobHandle`, `subMeshDescriptionsJobHandle`, `subMeshSurfacesJobHandle`, `vertexBufferContents_renderDescriptorsJobHandle`, `vertexBufferContents_colliderDescriptorsJobHandle`, `meshUpdatesJobHandle`, `compactHierarchyJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `brushMeshBlobsLookupJobHandle`
- *declared handle writes:* `vertexBufferContents_meshesJobHandle`, `brushOutlineManagerJobHandle`

#### `OutputCopyJob` — scheduler line 2547

- **Read:** `subMeshSource`, `descriptors`, `meshUpdates`
- **Read / Write:** `meshDataArray`
- *declared handle reads:* `vertexBufferContents_subMeshSectionsJobHandle`, `subMeshDescriptionsJobHandle`, `subMeshSurfacesJobHandle`, `vertexBufferContents_renderDescriptorsJobHandle`, `vertexBufferContents_colliderDescriptorsJobHandle`, `meshUpdatesJobHandle`, `compactHierarchyJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `brushMeshBlobsLookupJobHandle`
- *declared handle writes:* `vertexBufferContents_meshesJobHandle`, `brushOutlineManagerJobHandle`, `vertexBufferContents_triangleBrushIndicesJobHandle`

#### `UpdateBrushWireframeJob` — scheduler line 2575

- **Read:** `allUpdateBrushIndexOrders`, `compactHierarchy`, `brushMeshBlobs`
- **Write:** `brushWireframeManager`
- *declared handle reads:* `compactHierarchyJobHandle`, `allUpdateBrushIndexOrdersJobHandle`, `brushMeshBlobsLookupJobHandle`, `vertexBufferContents_subMeshSectionsJobHandle`, `subMeshDescriptionsJobHandle`, `subMeshSurfacesJobHandle`, `renderMeshesJobHandle`
- *declared handle writes:* `brushOutlineManagerJobHandle`, `vertexBufferContents_triangleBrushIndicesJobHandle`

#### `AllocateVertexBuffersJob` — scheduler line 2600

- **Read:** `subMeshSections`, `subMeshTriangleLookups`
- *declared handle reads:* `vertexBufferContents_subMeshSectionsJobHandle`, `subMeshDescriptionsJobHandle`, `subMeshSurfacesJobHandle`, `renderMeshesJobHandle`
- *declared handle writes:* `vertexBufferContents_triangleBrushIndicesJobHandle`

#### `FindTriangleBrushIndicesJob` — scheduler line 2614

- **Read:** `subMeshDescriptions`, `subMeshSurfaces`, `meshUpdates`, `entityIDLookup`
- **Read / Write:** `subMeshTriangleLookups`
- *declared handle reads:* `vertexBufferContents_subMeshSectionsJobHandle`, `subMeshDescriptionsJobHandle`, `subMeshSurfacesJobHandle`, `renderMeshesJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `brushRenderBufferCacheJobHandle`
- *declared handle writes:* `vertexBufferContents_triangleBrushIndicesJobHandle`, `storeToCacheJobHandle`

#### `FindTriangleBrushIndicesJob` — scheduler line 2633

- **Read:** `subMeshDescriptions`, `subMeshSurfaces`, `meshUpdates`, `entityIDLookup`
- **Read / Write:** `subMeshTriangleLookups`
- *declared handle reads:* `vertexBufferContents_subMeshSectionsJobHandle`, `subMeshDescriptionsJobHandle`, `subMeshSurfacesJobHandle`, `renderMeshesJobHandle`, `allTreeBrushIndexOrdersJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `brushRenderBufferCacheJobHandle`
- *declared handle writes:* `vertexBufferContents_triangleBrushIndicesJobHandle`, `storeToCacheJobHandle`


### Store Results / Store cached values back into cache (by node Index)

#### `StoreToCacheJob` — scheduler line 2663

- **Read:** `allTreeBrushIndexOrders`, `brushTreeSpaceBoundCache`, `brushRenderBufferCache`
- **Read / Write:** `brushRenderBufferLookup`
- *declared handle reads:* `allTreeBrushIndexOrdersJobHandle`, `brushTreeSpaceBoundCacheJobHandle`, `brushRenderBufferCacheJobHandle`
- *declared handle writes:* `storeToCacheJobHandle`


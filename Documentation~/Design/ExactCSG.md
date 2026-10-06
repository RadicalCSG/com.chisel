# Exact CSG: every decision a sign, no distance ever compared

Status: **built, and on by default since 24 September 2026** (`CompactHierarchyManager.TreeUpdate.kExactCSG`, which
still switches back to the loop pipeline). Written 23-26 September 2026.
[PipelineMap.md](PipelineMap.md) is the survey that led here, [ExactPredicates.md](ExactPredicates.md) the first
proposal, [CanonicalVertices.md](CanonicalVertices.md) the attempt before it; this document says what was built instead,
why each part is the way it is, and how it is checked.

**Every change follows [ExactCSGRules.md](ExactCSGRules.md).**

## The design

From the conclusion of [CanonicalVertices.md](CanonicalVertices.md): *exact, plane-based classification: planes snapped
once at the input, and exact predicates after that, so that no other distance is ever compared.* Approved on 16
September 2026, in answer to "can we do this without epsilons?":

1. The brushes' planes are transformed into tree space. **The transformed planes are the input**, whatever produced
   them.
2. **Each plane is snapped once, on its own** (`ExactPlane.Quantize`), to a fixed grid, which makes it exact integers.
   Nothing merges planes: two planes become one only by snapping to the same integers ("The snap", below).
3. **Only plane intersections.** Every decision is an exact sign test on planes; no distance is compared anywhere,
   including the broad phase and bounds.
4. **A vertex is an exact point.** More than three planes can meet at a point, so its identity is the point, never one
   triple.
5. **Output is rounded to float32 once, at the end.**
6. **Vertices are welded, and slivers cleaned up, only at the very end**, on each model's whole output mesh, which is
   regenerated whole anyway, so the decision is consistent. Nothing there feeds back into a CSG decision.

It has to hold for any model a user builds: any generator, any convex brush, any transform, any operations.

**The weld at the very end (step 6) is built in its exact form, without any distance** (`OutputWeld`, com.chisel 3522151,
1 October 2026). Sander deferred it on 24 September 2026 ("it's essentially a mesh optimization"); it is the only place
distance logic may ever be used, and it was built first without any. What rounding does to the output, measured over the
86 Black Mesa maps (4,821,321 triangles): 39,176 triangles (0.81%) had two corners at one float position - two exact
points closer than a float step - and drew nothing under any transform. The weld:

- drops a triangle with two corners at one float position (compared by value: 0 and -0 are one position, NaN is never
  equal, so a broken vertex is never hidden). Its other two edges are the same segment run both ways, so every edge
  stays matched: nothing opens.
- shares vertices that are the same in every stream (collider, select, render), bit for bit, and removes vertices no
  triangle uses. No attribute of a kept triangle changes.
- does not simply drop a needle - three DISTINCT float positions on one line (3,431 of them, 0.07%): under the GPU's
  transform its corners round apart again, and it is what fills the hairline between its long edge and its two short
  ones. It takes one away only where what lies across its long edge lets it, without opening anything. Whether three
  floats lie on one line is decided exactly (`OutputWeld.OnOneLine`: the nine coordinates on one power-of-two integer
  grid, the cross product in 128 bit integers, or 512 bits up to 228 powers of two; NaN, infinity or a wider span is
  undecided, and the needle stays). Over four whole maps (bm_c0a0a, bm_c3a2c, bm_c3a2d, bm_c4a3c), of 101 needles, what
  lies across the long edge is:
  - **a triangle of the needle's own surface** (3): the two are flipped (com.chisel b088dec, 1 October 2026; Sander:
    "yes, build the needle edge flip"). The needle u-m-w (m strictly between u and w) and the neighbour u-w-d become
    u-m-d and m-w-d, in the same two slots: they tile what the neighbour covered and run the same outer edges, so
    nothing opens, and the triangle count stays.
  - **a triangle of another brush** (22) **or of another face of the needle's own brush** (35): the needle closes the
    seam there - its surface has a corner m on the line that the surface across lacks. The triangle across is split at
    m into u-m-d and m-w-d and the needle is dropped (`OutputModelWeld`, com.chisel 389c86a, 2 October 2026; Sander: "yes,
    build both removals"). The surface across gets the corner the needle's surface has, every edge stays matched, and
    the new corner is m's position bit for bit, with the attributes of the surface across between those of u and w.
  - **the identical needle, facing the other way** (41): the two close nothing but each other and go together.
  - **a needle with another middle corner** (a chain), or nothing: the needle stays.

  What lies across a needle can change as the others go: a needle across which lies a chain needle stays only until
  that one goes and splits the triangle across it - a half of which then lies across the first. So the weld goes over
  the needles until a pass changes nothing (com.chisel 3ffc9c7): every pass that changes something takes a needle away,
  and no split makes one, so it ends. Visited once, 18 needles of bm_c2a5a and bm_c4a3c were left that way.

  Rebuilt live (bm_c0a0a, bm_c2a5a, bm_c3a2c, bm_c3a2d, bm_c4a3c, 2 October 2026): 202 needles in the replayed meshes
  before the weld across the model, 44 after 389c86a and 19 after 3ffc9c7, every one of them a chain; 211, 44 and 20 in
  the saved meshes. The exact judge, mesh included, finds 0 mismatches over 2,943,137 triangles, every triangle the weld
  drops, flips, splits or pairs accounted for; no triangle has two corners at one position, and the colliders (counted
  after 389c86a) have one vertex per position. Pairs of identical triangles facing opposite ways rose from 50 to 80 (0.0444 to 0.0452 m2):
  faces that lie back to back in one float plane - a bevel 6 um tall that rounds flat onto its own brush's face, a
  brush thinner than a float step - now meet triangle for triangle where a split gave one side the other's corner. They
  covered the same area before.
- drops two triangles at the same three positions, bit for bit, wound the other way round (`OutputModelWeld`, com.chisel
  1168c3e, 2 October 2026; Sander: "the triangle positions are completely 100% identical? then yeah, remove them"):
  faces closer than a float step - two brushes all but touching, a bevel a few micrometres tall - back to back once
  rounded. Each of the one's edges is the other's run the other way, so every other edge keeps its partner and nothing
  opens. Where the two are faces of a solid thinner than a float step, that solid is drawn no more; where they meet
  across a gap, they were never seen. Only where both surfaces go into the same meshes (the same destination flags,
  neither a decal), so each mesh - the collider included - loses both or neither; a needle is left to the needle steps.
  The pairs go before the needles are split, as a split of one of them would leave the other alone, and a split can
  make such a pair, so the two take turns until neither changes anything. Rebuilt live on the same five maps: of the
  80 such triangles the replayed meshes had (0.045 m2, the largest 0.022 m2 where two brushes' sloped faces meet), none
  is left; the saved meshes keep 28 (0.0002 m2), where the replay has all faces go into the same meshes and the scene
  does not (other destination flags, or a decal). The judge finds 0
  mismatches over the same 2,943,137 triangles.
- gives a collider one vertex per position across the whole mesh: nothing follows a collider's triangles.

The weld of one surface runs as the surface's buffer is made (`ChiselSurfaceRenderBuffer.Construct`), and records the
needles it leaves (`ChiselSurfaceRenderBuffer.needles`). The weld across a model runs once per tree update, on the
brushes' buffers as they are gathered for the meshes (`FindBrushRenderBuffersJob`), and changes no cached buffer: a brush
it changes gets a copy for this update's meshes, disposed with them. So the copy into the output meshes
(`ChiselOutputRenderable`) and the per-triangle picking lookup (`SubMeshTriangleLookup`) count a surface's triangles from
the same buffer and stay in step, and a later update starts again from the buffers the CSG made. Every update of the
contents harness checks what the weld leaves (`ContentsTreeHarness.WeldProblems`), and the exact judge does not look in
the mesh for the triangles it drops (`ExactJudge.CheckMesh`, f6a2de4); it recognises a flipped pair (b088dec), a split -
followed through a half that is split again - a pair of facing needles (`CancelModelWeld`, 389c86a, ced1792), and a
pair of identical triangles facing opposite ways (`CancelOppositesOnce`, 1168c3e). Faces that meet across a gap thinner
than a float step but are split along different diagonals do not pair triangle by triangle, and stay. Not built: any
distance-based clean-up of the snap's slivers (see "What it costs" below).

## What it replaces

With `kExactCSG` on, an update skips intersection loops (`PrepareBrushPairIntersectionsJob`, `CreateIntersectionLoopsJob`,
`GatherOutputSurfacesJob`), loop overlap (`FindLoopOverlapIntersectionsJob`), the vertex merge
(`MergeTouchingBrushVerticesIndirectJob` and its fixpoint rounds), `StoreLoopVerticesJob` and `PerformCSGJob`. In their
place:

1. `ExactInputJob` and `ExactBoundsJob` - each brush that changed gets its `ExactBrush`: its face planes moved into tree
   space and made exact, the brush those planes make as exact corners, and float bounds that contain them (below);
2. the broad phase on those brushes, decided exactly (`IntersectionUtility.FindIntersectionExact`);
3. `ExactCSGJob` - for each updated brush, each face decided and triangulated exactly (`ExactFaceBuilder`,
   `ExactTriangulator`);
4. `GenerateSurfaceTrianglesJob` with `exactInput` set - the unchanged per-surface finish (UVs, normals, decals, render
   buffers) on the exact triangles.

Everything else (hierarchy, transforms, touching lists, routing tables before; mesh assembly after) is the existing
pipeline. The routing tables are the same data `PerformCSGJob` reads: the boolean semantics do not change, only how a
piece of surface is found to be inside, aligned with or outside a brush.

## The representation

**Planes are exact input, made once.** `ExactPlane` holds integers: `a x + b y + c z + w = 0`, the solid where it is
negative. `ExactPlane.Quantize` normalizes the double plane and snaps it (next section): a brush's plane gets
`|a|, |b|, |c| <= 2^20` and `|w| <= 2^44` (planes up to 2^24 units from the origin). Every bound in `ExactVertex` and
`ExactPredicates` is derived for `|a|, |b|, |c| <= 2^32` and `|w| <= 2^56` (`kMaxNormal`, `kMaxW`), which only the world's
own box planes (`ExactFace.WorldPlane`) reach. Rounding is half away from zero, so `Quantize(-p) == -Quantize(p)`. After
that no plane is ever rounded again, and no plane ever looks at another brush's.

**A vertex is an exact point.** It is computed from three planes through it: `ExactVertex.Intersect` keeps the point as
homogeneous integers, `W` the determinant of the three normals (`< 2^98.6`) and `X, Y, Z` minus the adjugate times the
offsets (`< 2^122.6`), all `Int128`. But any number of planes may pass through it, and it is identified by the point
alone, never by the triple it was computed from. The same point reached through different triples has different
integers and the same ratios, so nothing ever compares the integers themselves. `SamePoint` decides identity. On a line,
events at the same point share one rank, whichever planes produced them. A clipping plane through an existing corner
has sign 0 there, and adds no new point.

Planes that met in one point in the input - a cone's apex, rotated content - generally no longer do once each has been
made exact on its own: the point becomes a tiny cluster of exact vertices and edges. That is exact, and the same in every
brush that sees it; the offline suite builds such apexes on purpose (below).

**Every decision is the sign of a polynomial in those integers** (`ExactPredicates`): which side of a plane a vertex is
on (`Side`, below 2^157), the order of points along a line (`CompareAlongLine`), whether two planes are one
(`SamePlane`), whether two lines are one (`SameLine`, rank of a 3x4 matrix), orientation of three points in a face's
projection (`Orientation`, below 2^346). Each has a double filter whose error bound is derived in its comment; when the
filter cannot prove the sign, `Int128`/`Int192`/`BigInt` (512 bits) decides. No predicate returns a magnitude.

**Rounding happens once, at output.** `RoundToFloat(X, W)` gives the float nearest to `X/W`, exactly (a double estimate,
then an exact comparison with the midpoint to the neighbouring float when the estimate is close). The same point
through any triple rounds to the same bits, which is what makes a seam vertex identical on every face that shares it.

**Planes enter through the brush's transform.** They are moved by `ExactAffine`: the cofactor matrix of the linear part
times the sign of its determinant, `|det A| A^-T`, from `nodeToTree`. They are never moved by `treeToNode`, a float
inverse whose rounding put two tops that coincide in the input 4.8e-7 apart (`SampleSceneExtraFaceTests` Model1 F03,
traced). Nothing is divided, so quarter turns with representable scales move a plane with no rounding at all, and a
mirrored brush's planes still face out of it. What comes out is the input (design, step 1).

## The snap

`ExactPlane.Quantize` snaps each plane after its transform, as a function of that plane alone: the unit normal to
multiples of 2^-20 (`a, b, c = round(n * 2^20)`), the offset to multiples of 2^-14 units (`w = round(d * 2^14) * 2^6`,
so that `w / 2^20` is the snapped offset). Sander decided it on 26 September 2026, when planes meant to coincide kept
arriving apart: "we need to snap planes, like we planned, instead of modifying generators".

**Why planes arrive apart.** Registration derives every plane from the brush's float vertices (`BrushMeshManager`:
`CalculatePlane` in the generator job, `BrushMesh.CalculatePlanes` in `ConvertToBrushMeshBlob`, which also overwrites the
cutting planes `CreateFromPlanes` assigned), and each brush sits at its own float transform. So two brushes built to share
a plane arrive a fraction of a micrometre apart and tilted. In the sample scene the octagonal pit at (0, -2, -30) is
carved by two subtractive octagons meeting at y = -2, whose planes arrived as y = -2 - 5.96e-8 (z + 30) and
y = -2 - 8.14e-8 (z + 30); for z < -30 they left a slab of solid up to 1.1e-7 thick, and its two faces drew a sheet over
the pit. The CSG built exactly what those planes said; snapped, they are one plane, y = -2.

**How the grid was chosen.** Measured on the planes of both harvested scenes (bm_c2a5a and the sample scene, 25,086 face
planes, in tree space): planes of different brushes meant to be one plane arrive up to about 1e-5 apart at their faces,
and distinct parallel planes are about 1e-4 apart and more. These grids snap 98.9% of the first to one plane and merge
none of the second. The grid is one for every tree; it is never tuned to a scene.

**What it costs.** The snap moves a face by at most 2^-15 units plus sqrt(3) * 2^-21 (8.3e-7) of its distance from the
origin: the offset moves by half an offset step, and a point p on the face moves by the change in the normal times p.
That is 0.03 mm at the origin and under a millimetre 1000 units from it. A box described 0.3 mm taller than its neighbour
stays taller, by the snapped amount: its top at 1.0003 becomes 16389 / 2^14 = 1.000305. And where four or more planes
of different brushes met in one point, each moves on its own, so the point becomes a small cluster of exact points (see
"A vertex is an exact point" above): what was far below a float step with the old 2^-32 rounding is now visible. The
shared-corner survey measured such clusters 0.05 to 0.08 mm across where two wedges' slopes pass through one grid point
(its negative control passed with the snap undone). The output stays closed - every update is judged on its planes -
but those are slivers the weld at the very end (design, step 6) is there to clean up. Its exact form, built, does not touch
them: their corners are distinct floats 0.05 to 0.08 mm apart, so only a distance could, and none is used yet.

**What it does not do.** Rounding each plane on its own cannot catch every pair: two planes a hair apart on either side
of a grid boundary snap to neighbouring grid planes, one step apart (about 1% of the measured pairs). Nothing merges them
afterwards (design, step 2): they stay two planes, and the CSG builds exactly what they say.

**Checked by** `ExactPlaneSnapTests` (the pit's two planes snap to y = -2; planes one step apart in offset or normal stay
two; the opposite plane is exactly the negation; 1000 random planes move by at most half a step) and
`SampleSceneOpeningTests.ThePitUnderExtrudedShapeRemove_AsFirstBuilt_IsOpenOnceThePlanesAreSnapped`, which rebuilds the
pit from the octagons' original data and requires nothing drawn in it.

## The input: one `ExactBrush` per brush, rebuilt when the brush changes

`ExactBrushes.Build` makes a brush's `ExactBrush` from its mesh and its transform alone: its planes (`ExactAffine`, then
`Quantize`), the brush they make as its corners - every corner of every face polygon, each exact point once
(`ExactFaceBuilder.PolytopeCorners`) - and float bounds that contain them exactly (`ExactPredicates.DirectedToFloat`
rounds each coordinate down for the minimum and up for the maximum). `cornerCount` says what the brush is: more than zero
it has volume, zero its planes enclose none (a flat brush), -1 it takes no part (no mesh, a plane that cannot be made
exact - `InvalidPlane` - or planes that do not close it within the world - `OpenBrush`).

They are kept per brush, by node order, in `ChiselTreeLookup.Data.exactBrushCache`, and moved with every other per-brush
cache when brushes leave the tree (`CacheRemappingJob`). `ExactInputJob` rebuilds the entry of each brush in the rebuild
list, in parallel; since nothing in an entry depends on another brush, an entry only changes when its own brush does, and
a changed brush is always in that list (`FindModifiedBrushesJob` lists every brush with a status flag). `ExactBoundsJob`
then builds any entry still missing and gathers every brush's bounds for the sweep. `ExactCSGStat.InputBrushes` counts
the entries built: every brush on the first update, one after one brush moves, none after a brush is removed
(`ExactInputTests`). Switching the algorithm (`ChiselTreeLookup.Data.builtExact`) rebuilds every brush of the tree.

## The broad phase, decided exactly

The sweep (`BuildBrushBoundsSweepJob`, `FindAllBrushIntersectionPairsJob`) runs on the exact bounds with no margin; its
box tests are inclusive, so brushes that only touch are still paired. `IntersectionUtility.FindIntersectionExact` then
says two brushes are apart only when their bounds are apart, or when a face plane of one has every exact corner of the
other strictly outside it (`Side > 0`). Anything else may meet, and the face builder decides the rest: it tells
containment from contact itself, so this never answers `AInsideB` or `BInsideA`. Only face planes are tried, so two
brushes that only a plane through an edge of each separates are paired anyway, which adds no events and is harmless. A
brush without corners meets nothing.

## One face: `ExactFaceBuilder.BuildFace`

1. **The face** is its plane clipped by the brush's other planes, starting from the plane inside the representable world
   (`BoundingQuad`: the four `WorldPlane`s at 2^24 on the face's projection axes), so no bounds are chosen and there are
   no margins. A world plane that survives means the planes do not close the brush: `FaceOutsideBounds`. The same plane
   twice in one brush draws once (the first); a plane and its opposite make a flat brush that draws nothing.
2. **Regions.** Every touching brush with a routing lookup contributes the part of the face it covers, clipped the same
   way, and the category that part carries into routing: `Inside`, or `Aligned` / `ReverseAligned` when the brush has a
   face on the face's own plane (same way / other way; a brush with both is flat and covers nothing), after
   `ContentsRules.Rewrite`.
3. **Lines.** Every distinct line an edge of the face or of a region lies on (`SameLine`), with who owns an edge there and
   on which side.
4. **Events** on each line: the points where it enters and leaves the face, and where it enters and leaves **every
   touching brush** (not just the regions' polygons). A candidate end only becomes an event once the interval is known
   not to be empty, so every event is a real crossing.
5. **Segments** between consecutive distinct events are judged on both sides: membership of each region, then the routing
   table (the same walk as `PerformCSGJob`), gives the category left and right. A drawn category (`SelfAligned` or
   `SelfReverseAligned`) on one side only makes the segment an output edge, directed with the drawn side on its left.

**Why seams agree without welding.** The vertices on an output edge are exactly the points where its line enters or
leaves a brush of the face's brush or one touching it. Such a point on a seam between brushes B and C lies in both, so the
brush whose boundary makes it touches both B and C and is in both their touching lists: B's face and C's face compute the
same set of points on the seam, from the same planes, and round them to the same floats. No propagation rounds, no
T-junction pass. The broad phase may include brushes that do not touch (harmless: they add no events), but it must never
exclude two that do, and it cannot: it only excludes brushes that are apart exactly.

## Triangulation: `ExactTriangulator`

The drawn region of a face is the area to the left of the emitted edges. Loops are chained at each vertex; where several
boundaries meet, every incoming edge is paired with the next outgoing edge clockwise (exact angle comparison), which keeps
each piece of region on its own loop. A loop is a hole when the region at its lexicographically smallest vertex reaches
towards -u. Holes are bridged to the boundary around them left to right. A hole's bridge runs from its leftmost vertex to
a vertex at or left of it: a ray from there towards -u stays in the region until it meets a boundary already joined up,
and Eberly's argument (*Triangulation by Ear Clipping*, 2002) gives a visible vertex of that boundary at or left of the
hole. So those vertices are tried from the largest u down, each exactly (sector tests at both ends, and no edge met
anywhere but at the ends); no distance is measured, since any visible vertex is a valid bridge. Then ear clipping cuts
corners that turn left, whose diagonal leaves and arrives inside the region's sector at both ends, and whose triangle
holds no other vertex. No vertices are added. Triangles come out counter-clockwise seen from the face's plane;
`GenerateSurfaceTrianglesJob` flips them where its own convention needs (`SelfAligned` in a subtractive model, or
`SelfReverseAligned` in an additive one).

Ear clipping cuts a long strip with vertices along both of its sides into fans of slivers: the tops of a light-blocker
grate's slats in bm_c2a3c, 4 cm by 17 m with a vertex wherever a crossing slat meets them, came out as 16,118 triangles
metres long, which also took the exact judge's sweep to ~10^10 steps. So the triangles are then made locally Delaunay by
**Lawson's flips** (`MakeDelaunay`): an inner edge (two triangles on it in opposite directions, and no input edge there)
is replaced by the quad's other diagonal when the far vertex lies inside the circle through the other three. Twins are
found with two stable counting sorts of all edges by their (smaller, larger) vertex. The circle is judged on the float
positions the triangles are drawn with, by Shewchuk's filtered incircle (`iccerrboundA`): a flip is made only when the
filter is certain. Both new triangles have to turn left exactly (the exact vertices, as the judge sees them) and certainly
in the floats (the filtered orient2d, `ccwerrboundA`), so the result is as valid as the ear clipping's and no drawn
triangle turns over. On exact geometry an edge that fails the circle test always lies in a convex quad, so the two
validity tests only ever refuse a flip where rounding makes the floats and the exact points disagree. Each flip lowers the
sum over the triangles of their volume under the lifted points (u, v, u^2 + v^2) by the lifted tetrahedron's, which is
what the circle determinant measures, so the flips end. The flips change which diagonals are drawn, never the region or
its vertices, and nothing feeds back into a CSG decision.

Two box tests skip exact work that cannot matter (`BoxesMayMeet`, `PointMayBeInTriangle`). They compare approximate
coordinates `X / W` in doubles, each within 6u of itself (u = 2^-53: `Int128.ToDouble` is off by at most 2u + u^2 for each
of `X` and `W`, and the division by u), so two values in order can look reversed by at most 12u of the largest magnitude,
and the addition that applies the slack rounds once more: a slack of 16u of the largest magnitude involved means a test
only ever rejects what is apart exactly (`FilterSlack`).

## Failures are reported, not repaired

`ExactCSGStat` counts, per update (summed in `TreeUpdate.s_LastExactCSGStats`): brushes, faces, input brushes built, and
then the failures: `InvalidPlane`, `OpenBrush`, `FaceOutsideBounds`, `DegenerateVertex`, `RoutingOutOfRange`,
`UnbalancedVertex`, `UnpairedEdge`, `NoBridge`, `NoEar`. A failed face draws nothing and is counted. Under the contents
test harness any failure throws: it is a bug of the CSG, never something to average away.

## How it is checked

**Offline**, without the editor (`EXACT_OFFLINE`: the same sources compiled against `System` only, lists on `Marshal`), in
a test program that runs under Unity's mono - [exact_offline/](exact_offline/): `run.py ExactArithmeticTests.cs`, and
`run.py --args "600 150" ExactFaceTests.cs` for 600 regular and 150 noisy scenes and 40 strip scenes (`--replace` swaps in
a mutant; a third argument `r<N>`, `n<N>` or `s<N>` runs one round on its own and prints its brushes):

- **Arithmetic**: every integer operation and predicate against `System.Numerics.BigInteger`, 5.4 million checks,
  including planted boundary cases (a point exactly on a plane, half an ulp off).
- **Faces**: 600 generated scenes - boxes on a grid, random polytopes, slivers down to 2^-30, rotated sets, the same 4-8 km
  from the origin, 3-47-sided prisms, crowded floors, cones whose apexes split once each plane is made exact - built brush
  by brush with exact touching lists plus 25% random over-inclusion, and judged by an **independent oracle**: sample
  points on every face (one between every two crossings along sample lines, so no cell is too thin to be sampled), each
  routed directly from the brushes' planes. Every sample must be covered once by a triangle of the right facing, or not at
  all. Then the **whole output** must be watertight: every directed edge has a reverse partner by float bits, and no
  vertex lies inside another triangle's edge where a seam runs along it (a cone's apex touching a flat face in one point
  shares no seam and opens nothing).
- **Noise**: 150 scenes built clean and again with float-sized noise (each brush turned by up to 2e-7 rad and shifted by
  up to 2e-7 (1 + |position|), a third left exact), both judged by the exact oracle on their own planes.
- **Delaunay**: every face's triangles are judged by `ExactDelaunayCheck` (Core/Tests/Contents, the same file the editor's
  `ExactDelaunayTests` use): exact integer incircle and orientation on the floats, a violation counted only beyond 2^-45
  of the permanent, where the triangulator's filters are certain. 40 strip scenes (a strip with boxes against both long
  sides and slats across it, clean and noisy) are the comb that ear clipping cut into fans: 2,168 of 21,826 inner edges
  failed before the flips, none after.
- **The checks themselves**: unit cases for the watertightness check, the Delaunay check, polytope corners and
  `ExactAffine` (20,000 transforms, mirrors included, against an explicit inverse).
- **Mutants**: deliberate errors in the predicates, the face builder, the triangulator and the checks are each caught,
  which is what shows the checks can fail at all - including a bridge search on the wrong side of the hole (`NoBridge`)
  and a triangle box test that rejects everything (`NoEar`, holes). Of the flips: the circle test reversed, a flip that
  does not queue the edges around it, neighbours left pointing at the old triangles, and no validity test at all or only
  the float one (both give triangles that do not turn left exactly, where rounding made a quad look convex) are caught.
  Leaving out only the float validity is not: in these scenes no quad that fails the circle test is convex exactly but
  not certainly so in the floats.

The latest full run: 980 scenes, 3,890,712 checks, 0 failures; 97,679 inner edges judged, all locally Delaunay.

The offline suite found one crash in the core: `ExactList.Add` took its value by reference, and `Clip` adds an element of
the same list, so growing the list freed the value before it was read. Under Burst the freed temporary memory still held
the old data, so nothing showed; offline, a large enough buffer is returned to the system and the read faults. `Add` now
takes the value by copy, and `ListSelfAddTest` faults on the old code.

**In the editor**, the core test suite runs with `kExactCSG` set (by reflection, before the run), and the tests judge
exactly ([rule 12](ExactCSGRules.md)): `ExactJudge` (Tests/Contents) replaces the tolerance oracle whenever the exact CSG
runs.

- **What it judges.** The pipeline keeps what it decided when a test asks (`ExactCSGCapture`, keyed by `NodeID`): every
  brush's exact planes, and every brush's exact output triangles with their category. The operations and contents are
  the scene description's (`ContentsOracle`). The judge works in `System.Numerics.BigInteger` and shares no code with the
  core beyond reading its integers.
- **How.** On every distinct plane a brush has a face on, it overlays every brush's cross-section with that plane (its
  face, where it has one there) and every output triangle on it. Between two consecutive u-coordinates of the overlay's
  vertices no two of its edges cross, so the line through the middle of such a slab (the simplest dyadic `k / 2^e`
  between them) passes through every cell of the overlay the slab holds, and between two consecutive crossings of that
  line nothing changes. Each such interval is judged: the faces the oracle expects there, decided with probes an
  infinitely small step off the plane on either side, against the triangles that cover it, with the tie rules between
  contents types. Every cell is judged however thin it is; nothing is skipped for being close to something.
- **And around it.** Every output vertex lies exactly on its face's plane and was rounded to the nearest float (computed
  independently); every triangle is counter-clockwise from its face's normal and has area; the planes the CSG used are
  exactly the ones each brush's current mesh and transform make (so a stale or misplaced `ExactBrush` fails, which the
  judge would otherwise agree with); and every brush's triangles in the mesh the harness received are its exact
  triangles rounded, bit for bit, wound as their category says.
- **Everywhere.** `ContentsTreeHarness.Update` judges every exact update this way, whatever else the test checks, so a
  wrong output fails where it happens and not only where a test looks with a tolerance. Two controls that build something
  other than their description on purpose opt out (`JudgeEachUpdate`). `SourceConcretePadTests` judge the pad exactly
  instead of comparing its area with figures measured with a 1 mm tolerance.
- **It can fail.** `ExactJudgeTests` plant errors in what the CSG captured or in the mesh, and the judge reports each:
  a triangle missing across a 2^-12 gap (four steps of the snap, so the gap survives it), a face drawn the wrong way, a
  triangle drawn twice, a face stretched over the 2^-12 strip between two boxes, a plane moved by 2^-20, a vertex one
  float off, a triangle taken out of the mesh, a
  triangle missing from a pad top among 65 neighbours; `ExactInputTests` plants a brush moved without an update.

On 24 September 2026, with the judge: **772 tests, 768 pass, 1 fails, 3 skipped** with the exact CSG, including the 26
sample-scene corpus cases and the concrete pad. The failure is not in the exact CSG: `NestedSubtractGroups_Route(16)` only
asserted that meshes arrived until the harness judged it, and from 12 nested subtract groups on both pipelines drop the
block's plain outer faces - the routing lookups reach 46,656 rows at depth 12 and 65,536 at depth 13, past the `ushort`
row indices (`ExactCSGProbeTests.NestedSubtractionDepths`). The same suite on the loop pipeline: 755 pass and 1 fails,
`IntersectionLoopCapacityTests` (ten overlapping brushes), a hole in the loop pipeline's output. On the live sample scene
the exact CSG rebuilds 357 brushes, 2,273 faces in 131 ms with no failures.

Getting there found two faults outside the exact core, both worth knowing:
- **The comparison harness took slivers for coverage.** `ContentsFaceSampler.CoversPoint` compared edge cross products
  - a distance times the edge's length - with 0.001, so a triangle a few micrometres across "covered" points metres away
  and was reported as a face drawn where it does not belong. It now compares distances. (The exact judge has no such
  test at all.)
- **Nested subtractions lose faces** in both pipelines from depth 12, as above; found only because the harness now
  judges every exact update.

## Brushes given as planes: `ChiselBrushDefinition.SetPlanes`

Generators give planes ([rule 1](ExactCSGRules.md)). A brush can be handed over as its planes alone, in its own space
(`ChiselBrushDefinition.SetPlanes`), and the CSG side derives its outline from them, exactly (`ExactBrushOutline`, which
runs `ExactPolytope`). Each face keeps the plane it was given bit for bit, and has that plane's index as its surface, so
a brush of planes has one surface per plane. Registration keeps a mesh's own planes, so the CSG's input is the given
planes. The outline is what the editor, the bounds and the rest of the pipeline see; assigning an outline from outside
(an edit) makes the brush its outline from then on.

Until then every generator's planes went through a float cut of a big box first (`BrushMeshFactory.CreateFromPlanes`,
8192 units for the importer, plane by plane, with a distance epsilon), and whatever that cut made of them was the brush.
2,183 brushes of the 78 imported Black Mesa maps never reached the CSG: 1,460 had float outlines that failed validation
(a thin light blocker came out as two quads back to back, its sides lost), and 723 had non-finite numbers in their
registered meshes. Not all of those came from the cut: the old importer built bm_c1a3a's Light Blocker 2244158, a slat
9.5 mm across, finite and valid, and registration's 1.25 cm weld, removed in b85d3e8, collapsed it into NaN planes; 459
of the 722 that chisel-dev-44b found in the VMFs are thinner than 9.5 mm. And where many planes nearly meet it broke the
brush: bm_c1a4b's func_brush 3926590 (solid 3926591), a cone of 49 planes whose tip is cut off by a small cap (19 of the
planes nearly meet in a point just outside the brush), came out with 51 polygons, more than 49 planes can bound.
Exactly, from the same float planes, it has 49 faces and 89 corners; from the VMF's own rational planes 74, with at most
four planes through any of them, since rounding each plane to float splits such corners. `PlaneDefinedBrushTests` holds
the cone's planes as float bits.

`ExactPolytope`:

1. **Each plane is made exact as it is given** (`ExactPlane.AsGiven`): not normalized, only multiplied by the power of
   two that brings its largest normal component into [2^31, 2^32). A float plane then comes out exactly, unless a
   coefficient has bits below 2^-32 of the largest. This is not the CSG's snap: the CSG snaps the brush's planes after
   they are transformed, as always.
2. **Each face is its plane clipped by all the others** (`ExactFaceBuilder.BoundingQuad` and `Clip`), so a plane that
   only touches the brush along an edge or at a corner makes no face, and a corner is an exact point however many
   planes meet there. The same plane twice is one face (the later of two); a plane with its opposite leaves no brush.
   The brush is built in the exact world's box; a face of that box left over means the planes do not close the brush.
3. **The faces are joined by their exact corners**: a corner is the point (`SamePoint`), and a half-edge's twin is the
   exact edge the other way. So the brush is closed by construction.
4. **Only then is each corner rounded to float, once**, and corners that round to the same float are one vertex. What a
   float cannot show drops out without opening anything: an edge whose corners round to one vertex goes from both of its
   faces; a face that runs out to a corner and straight back (an antenna, A B A) loses it, and the faces on either side
   of that edge become each other's twins; a face left with fewer than three vertices goes.

Rounding one corner at a time is where the obvious version fails: welding by float position and then pairing edges by
their float vertices breaks where many planes nearly meet, where a cluster of exact corners about one float step across
straddles a cell boundary. The exact topology is kept for that reason, and float coincidences only ever remove something.

Checked offline ([exact_offline/](exact_offline/), `run.py --args 10000 ExactPolytopeTests.cs`): the cone, touching and
duplicate planes, and generated brushes (cones of 3-50 sides whose planes nearly meet at the apex, rounded to float, up to
2 km from the origin; planes tangent to spheres). Every brush must be closed, have one face per plane at most and no face
with fewer than three edges, no zero-length edge and no antenna, wind counter-clockwise seen from outside, and have every
vertex inside every plane and on its faces' planes within what rounding a point to float allows. Planted errors (no
antenna removal, no twin zip, no zero-length removal, reversed winding, coarse rounding, a skipped clip, the CSG's snap
instead of `AsGiven`, the first duplicate winning) are each caught.

And every solid of Black Mesa's 565 Singleplayer VMFs, maps and instances (490,334), converted as the Source importer
converts its sides, made local to the average of its plane points and rounded to float once, then built as
`ExactBrushOutline` builds it: all closed, 490,330 with one face per side, 3 with a side that only touches, and one that
encloses nothing (122 sides that contradict each other, in an instance); 0.53 ms a solid on average, 28 ms at most.

## Open

1. **The weld at the very end** (design, step 6): built in its exact form on 1 October 2026, with the needle edge flip,
   and across a whole model on 2 October, cancelling identical triangles that face opposite ways too (see the top of
   this document); only chains of needles on one line stay. Left: cleaning up the snap's slivers, which would take
   distance logic - only ever here, and only when Sander asks for it.
   On 26 September: "the end step where we weld vertices is an optimization step. It makes the mesh more efficient, it
   doesn't actually fix anything".
2. **Planes meant to coincide that the snap does not catch.** The snap ("The snap", above) makes most of them one plane;
   a pair that straddles a grid boundary stays two planes one grid step apart (about 1% of the measured pairs), and the
   CSG builds exactly what they say.
3. **Nested subtractions** - fixed 26 September 2026 (ac4cab5). The routing tables were never minimised, so their rows
   grew exponentially with nesting until the copy addresses wrapped; see PipelineMap.md, "Two more guessed
   capacities". Rows per node now level off (31 for the nested chain) and `NestedSubtractGroups_Route(16)` passes.
4. **Cost.** Measured on bm_c2a5a (2883 brushes in one model) the live CSG runs; the judge takes ~4 minutes there.

## Real scenes, checked (26 September 2026)

Both open scenes were dumped (`Library/ChiselHarvest/<scene>.json`, each brush's planes, transform, destination flags
and own mesh) and replayed in the harness from each brush's own mesh, so registration derived the very planes the
scene's CSG was given - checked bit for bit, 0 differences. On those replays the exact judge finds no mismatch (the
worldspawn alone: 1,915,907 intervals), and every model's live meshes equal the replay's triangles bit for bit, apart
from decals and faces that never draw.

The one exception taught two things outside the CSG. The sample scene's Model4 had kept its saved meshes, and the dump
built its nodes on demand (`BuildTheNodesOfNow`) - which left every brush at an identity transform, so the dump, the
replay and a forced rebuild all saw its 24 boxes piled up at the model's origin (108 triangles) while its real geometry
is 502 (fixed in 8a830b0; a save had also stored the piled-up result once). Looking for it found a second, real bug: a
save stamped an edit's hash onto older meshes while the edit was still queued for the hierarchy manager (fixed in
05ba3a5). A dump of a model that kept its saved meshes is only right with 8a830b0.

**Sheets and cracks in bm_c2a5a, before and after the snap.** A measurement of the float output, not a check: pairs of
opposite-facing triangles of one model, less than 2 mm apart, whose projections overlap, with the brush faces behind them
found in the dump by distance and angle. A *sheet* is solid that thin drawn from both sides (the pit's); a *crack* is air
that thin between two solids, drawn facing into it - seen only as a hairline at its mouth, but it costs triangles and
lightmap area.

- Before the snap: 1,425 m² of sheets, a few 1e-7 to 1e-5 thick, 93.5 m² of them on rendered faces; 24,178 m² of cracks,
  16,899 m² of them between planes that snap to one plane.
- After: 0.009 m² of sheets, the largest 64 cm², all on faces that do not render, in irregular Details brushes whose
  input planes really differ. 7,448 m² of cracks: 5,581 m² a 1.95 mm gap the map has (two brushes 32 offset steps apart,
  faces that do not render), 966 m² between planes one offset step apart (the pairs the snap cannot catch, "The snap"),
  900 m² between planes whose input normals differ by more than the normal grid (rotated Details brushes, up to 1.6e-5
  radians apart).

# Canonical vertices: one identity per corner, decided from its planes

Design plan, written 16 September 2026, approved the same day. **Implemented through stage 4 behind
`CompactHierarchyManager.TreeUpdate.kCanonicalVertexStage`, which ships at 0 (off). It was stopped there by the stop
rule and parked after a diagnostic round: see [Measured](#measured-16-september-2026) and
[Diagnosis](#diagnosis-16-september-2026). [As built](#as-built) lists where the implementation departs from
the plan.** The plan as approved, with a figure, is at
https://claude.ai/artifact/UF3VxkGxgeCQDbxY1QawHF. Line numbers refer to com.chisel `main` at `5d30b9d`.

Every brush pair computes the corners it shares on its own, in its own local space, and a 12.5 mm distance weld then
decides which of the resulting points are "the same". That decision depends on what else happens to be nearby, so two
brushes can decide the same corner differently. This plan replaces the weld with an identity that both brushes derive
identically: a vertex *is* the set of planes it lies on, and its position is computed once from that set.

- **Example:** bm_c0a0a, trim Solid 126232 under wall Solid 46225: a 19.06 mm face cut 7.96 mm above its bottom edge
- **Evidence:** the plane-incidence gate (`40a1778`, ships off), its traces, and the shared-corner survey
  (`5d30b9d`, `Core/Tests/Contents`, report in `Logs/SharedCornerSurvey.txt`)
- **Units:** metres in tree space; τ = `CSGConstants.kFatPlaneWidthEpsilon` = 0.6 mm
- **Touches:** com.chisel Core only: six jobs, `HashedVertices`, `InternedPlanes`, the job setup in
  `CSGManager.UpdateTreeMeshes`

## What breaks today

The weld merges any two vertices within `kVertexEqualEpsilon` (12.5 mm). It runs at 14 sites in six jobs, per
brush or per brush pair, and `MergeTouchingBrushVerticesIndirectJob` repeats it up to 30 times (`kMergeIterations`) until
neighbouring brushes stop moving each other's vertices. Two measured consequences:

1. **Features thinner than 12.5 mm vanish, often on one side only.** On bm_c0a0a the trim's 7.96 mm strip is snapped
   onto its corner, and the face survives whole inside the wall: a 43.3 m fin. The shared-corner survey finds the same
   defect with the gate off, where one brush has no vertex at a crossing the other brush draws: 307 of the sliver
   crossings, 71 box crossings, 15 ledge and 5 cut-brush crossings. On the slivers, traced by hand, the weld collapses
   the few-millimetre slot on the wedge while the box keeps its seam vertex.
2. **Any rule that only refuses merges makes the two brushes disagree.** The plane-incidence gate removes the fin and
   drops T-junction stretches from 107 to 27, but opens 35 real holes. The traces show the loops are clean when built,
   and the plane splits are where the pinches start. The survey confirms it with the gate on: both brushes draw the
   corner, but in different places, and never less than 1.2 mm apart. That floor is exactly where the gate stops
   allowing merges (`kOffPlane` = 2τ).

Shared corners, generic crossings only (an edge of one brush through the inside of a face of the other), 50 seeds per
population:

| Population | Two-sided splits, gate off → on | One-sided misses, gate off / on |
|---|---|---|
| sloped slivers (the trim case) | 1 → 10 | 307 / 305 |
| slivers, rotated | 1 → 10 | — |
| ledges | 2 → 3 | 15 / 10 |
| near-miss boxes | 0 → 12 (+7 other) | 71 / 62 |
| boxes, rotated | 0 → 18 (+4 other) | — |
| grid boxes (control) | 0 → 0 | — |
| cut brushes | 0 → 2 | 5 / 3 |

**Why it happens.** `CreateIntersectionLoopsJob.cs:359` computes a corner from three known planes, then drops them.
It works in the local space of the pair's first brush (both directions, lines 1025 and 1044, pass that brush's
matrix). Line 418 transforms the corner to tree space, and lines 419–420 snap and weld it in a hash private to that
pair. Within a pair that is consistent, but a corner on a seam where three brushes meet belongs to several pairs, and
each pair computes it separately, in its own frame, with different bits. The later welds reconcile them. They can
only decide by distance, and what lies within the distance differs from brush to brush. The author's own TODO at
line 416 asks for this plan: *"should be having a Loop for each plane that intersects this vertex, and add that
vertex to ensure they are identical"*.

## The idea

A vertex is identified by the planes it lies on (Hoffmann, Hopcroft and Karasick 1988/89). Plane-based Boolean
algorithms work this way: Bernstein and Fussell 2009, Campen and Kobbelt 2010, EMBER 2022. Their exact forms need
quantised integer planes. Chisel takes float planes from any brush, generator or transform, so only the tolerance form
applies: planes are grouped into classes within τ, and everything else follows from class ids.

Most of the groundwork exists and ships off:

- `InternedPlanes` and `InternBrushPlanesJob` intern every brush face into plane classes. On bm_c0a0a, 34,040 faces
  become 5,183 classes, with no face further than τ from its class. Toggle: `kInternBrushPlanes`,
  `CSGManager.UpdateTreeMeshes.cs:169`.
- `Temporaries.brushPlaneIds` / `brushPlaneIdRange` (lines 225–227) map every brush face to its class, flipped or not.
- `kUsePlaneIdsForAlignment` (line 176) already decides coplanar faces by class id. Measured on 12 September, it
  took duplicate triangle groups from 84 to 7, at roughly 450 ms extra per full rebuild.

The loop machinery doesn't change: `Edge { ushort index1, index2 }` still indexes a per-brush vertex list. Only the
way an index is assigned changes. That is what makes the plan adoptable in stages.

## Rules

1. **Plane classes.** `InternedPlanes` with τ, admitted by a point test over each face's vertices. Features thinner
   than τ merge, and they merge consistently, because a class is global.
2. **Vertex key.** The sorted set of class ids of every face plane passing within τ of the point, taken from the
   brushes whose closed volume contains the point: the *local closure*.
   - The closure is symmetric. Every brush containing the point touches every other brush meeting there, so each of
     them finds the same set. It is also local, so an edit far away can't change a key.
   - Size on bm_c0a0a: 81% of vertices lie on more than three planes, five on average. An inline capacity of 8 covers
     96%; the rest go to an overflow table.
   - The containment test and the touch graph must use the same tolerance, or the symmetry breaks.
3. **Canonical position.** The intersection of the best-conditioned triple in the key (largest |det| of the three
   normals), in double precision, in tree space, from the class planes. Ties are broken on the plane
   *coefficients*, never on class ids: ids follow insertion order, an incremental edit can renumber them, and brushes
   that weren't rebuilt would keep the old bits.
4. **Identity.** Two vertices are the same exactly when their keys are equal. Equality is transitive, so the
   "A~B, B~C, but not A~C" chains that distance and pairwise-incidence rules produce cannot occur.
5. **Keys at creation.**
   - A brush corner starts from the classes of its incident faces.
   - A pair intersection starts from its three planes.
   - An edge split by a plane starts from the planes both edge endpoints share (the edge's line) plus the cutting
     plane.
   - Each then gets the closure.
6. **T-junctions.** v lies on edge (a, b) when key(v) contains key(a) ∩ key(b) and v lies strictly between a and b.
   No distance band decides it.
7. **Distance only finds, never decides.** A hash lookup may still be spatial, but a candidate matches only on an
   equal key.

## Where it plugs in

| Site | Today | With canonical vertices |
|---|---|---|
| `CreateBlobPolygonsBlobsJob.cs:94` | brush corners hashed by position | key from incident faces, canonical position |
| `CreateIntersectionLoopsJob.cs:359, 418–420` | local-space intersection, then snap and weld | key from the triple, position in tree space |
| `CreateIntersectionLoopsJob.cs:256, 618, 1005, 1009` | snaps and seed replacement by distance | lookup by key |
| `FindLoopOverlapIntersectionJob.cs:85` (`CopyFrom`) | loop positions re-hashed | keys travel with loop vertices |
| `FindLoopOverlapIntersectionJob.cs:565, 723` | split point, "same as endpoint" by distance | split key; same only if keys are equal |
| `FindLoopOverlapIntersectionJob.cs:409, 874` | vertex on edge by distance | rule 6 |
| `MergeTouchingBrushVerticesJob.cs:234`, 30 rounds | brushes weld each other's vertices until stable | nothing to settle: keys and positions agree already; becomes a check |
| `PerformCSGJob.cs:1167` | re-weld through `indexRemap` | dedupe by key |
| `GenerateSurfaceTrianglesJob.cs:301, 537` | re-add, T-junction insert by distance | by key, rule 6 |

Data flow: `Temporaries.outputSurfaceVertices` carries only `float3`, so it needs a parallel key list. So do
`HashedVertices` and `loopVerticesLookup`.

## As built

The implementation is `Core/2.Processing/Containers/CanonicalVertices.cs`. It departs from the plan in these ways,
each for a reason found while building it:

- **Identity travels with the position, not in a key list.** The position is a pure function of the key, computed
  from the same three planes in the same order wherever it runs, so two computations of one vertex produce the same
  bits. A vertex is a fixed point of "take its key, then its canonical position", and "same key" becomes "same
  position". No parallel key lists are needed: `HashedVertices`, `loopVerticesLookup` and `outputSurfaceVertices` stay
  `float3`. The weld becomes `WeldIncidenceFilter.SameVertexOnly`, which merges only positions within
  `CanonicalVertices.kSameVertex` (0.01 mm), enough to absorb the rounding of a stored and transformed position.
- **One stage toggle instead of `kCarryVertexKeys`.** `CompactHierarchyManager.TreeUpdate.kCanonicalVertexStage` is an
  int, read once per update: 0 Off, 1 Measure (D1), 2 Positions (D2), 3 LoopIdentity (D3), 4 Everywhere (D4).
- **No plane classes: a key holds the brushes' own face planes.** The first version keyed vertices on `InternedPlanes`
  classes and intersected the classes' stored planes. A class is global, though: its stored plane is one member's,
  so a brush far away with a face less than τ from a seam's plane could become that member and move the seam's
  corners, and interning had to cover the whole tree on every update. An edit rebuilds the edited brush and the
  brushes it touches, not their other neighbours, so a rebuilt brush would put the corners it shares with an
  untouched neighbour somewhere else, and the seam would crack
  (`CanonicalVerticesTests.ABrushAddedOnTheSamePlane_DoesNotMoveCornersItDoesNotContain`). Now the key is the set
  of face planes, each turned to face along its largest normal component, sorted by coefficients, without exact
  duplicates. Only brushes that contain the vertex contribute, and every one of them touches the others, so an edit
  that can change a key rebuilds every brush that uses it. Faces less than τ apart are both in the key, and the
  triple rule picks the same one of them everywhere, so thin features still merge consistently. Plane interning is
  not used, and `kInternBrushPlanes` keeps its old meaning.
- **Incidence is decided with each brush's own plane**, and so is containment: every computation that looks at a
  brush decides the same way.
- **Keys come from the local closure only** (the brush itself and the brushes it touches whose volume contains the
  point), with one refinement pass: the key is gathered again at the canonical position, and if it differs, that
  key's position is used. The key holds 32 planes; a fuller key keeps its 32 smallest, so it still doesn't depend on
  the order its planes were gathered in, and the overflow is counted.
- **The tie-break is the sort order.** Triples are visited in the key's order and the first of equally conditioned
  triples wins, which is the triple with the smallest planes. `BrushTreeSpacePlanes` now records how many of its
  planes are faces (`faceCount`), because the planes after the faces are edge planes and must not decide
  containment.
- **A displacement guard.** Every plane of a key passes within τ of the computed point, so a well-conditioned triple
  lands within a few τ of it. A canonical position more than 4τ away is rejected and the computed position kept.
  Triples with |det| below 1e-6 are not used at all.
- **Split candidates are tested before they are canonicalized.** `FindLoopOverlapIntersectionsJob` computes a
  crossing for every edge against every plane of the other brush and discards most of them because the crossing
  lies outside that brush. The first D1 run canonicalized all 3.8 million candidates. The containment test now runs
  first, on the computed point, so a canonical position can't carry a crossing across the other brush's planes.
- **`CreateBlobPolygonsBlobsJob` now runs after the tree-space planes are built**, because brush corners need them for
  their closure.
- **Counters per creation site** (`CanonicalVertexStats`): vertices computed, keys too small or too degenerate to
  place, placements rejected by the guard, displacement bins, key sizes, overflows, and faces between τ and 2τ.
- **Two more distance rules follow τ.**
  - From stage 3, two faces are one surface only when one lies within τ of the other's plane.
    `PrepareBrushPairIntersectionsJob.FaceLiesOnPlane` used 10 mm, which was only safe while the 12.5 mm weld hid
    what it merged. `kCanonicalAlignment` switches this rule off, to measure it.
  - At stage 4, `PerformCSGJob.LoopsOutlineSameRegion` compares corners within τ instead of 25 mm. A corner is never
    split in two any more, and 25 mm would let a real 3 mm feature count as the same region.

Tests:
- `Core/Tests/CanonicalVerticesTests.cs`: the rules on small fixtures.
- `Core/Tests/Contents/CanonicalVertexEditTests.cs`: a seam with a brush an edit doesn't rebuild.
- `SharedCornerSurveyTests.Survey_CanonicalVertexStages`: the survey at stages 0, 2, 3 and 4.
- `SharedCornerSurveyTests.Survey_ControlAgainstItself`: the survey's own run-to-run determinism.

## Measured, 16 September 2026

Measured on d379d3e plus the two τ rules above, with one full rebuild of bm_c0a0a per stage. Stage 1 renders
bit-identically to stage 0 (same hash), so its scans are the same.

| bm_c0a0a | 0 (shipped) | 2 Positions | 3 Loop identity | 4 Everywhere | Bar |
|---|---|---|---|---|---|
| Rebuild | 1.24 s | 1.21 s | 1.92 s | 1.16 s | at most +15% |
| Fin | 43.3 m | 58.4 m in 7 | 36.5 m in 20 | **0** | 0 |
| T-junction stretches | 129.3 m | 129.3 m | 222.3 m | **0** | |
| Uncovered boundary | 380.7 m | 461.8 m | 1031.0 m | **235.8 m** | below 380.7 m |
| Real holes (sphere probes) | 0 | 12 (62.1 m²) | 1 (0.30 m²) | **8 (2.76 m²)** | 0 |
| Non-manifold edges | 389 | 392 | 243 | **266** | at most 389 |
| Duplicate collider groups | 2 | 4 | 6 | **12** | at most 2 |

Canonicalization at stage 1 on the map:
- 716,670 vertices placed. 342 had no usable key (0.05%) and 172 were rejected by the 4τ guard (0.02%).
- 381,000 of the 418,000 moved vertices moved less than 0.1 mm, and 2,648 moved more than τ (at most 2.39 mm).
- No key overflowed.
- Counting costs no measurable time; the first version cost 300–500 ms.

Survey at stage 4, with the control in brackets:
- **Two-sided splits** (apart, one-vs-two, other): 86 [4], mostly 0.6–3 mm apart.
- **One-sided misses:** 159 [307] on both sliver populations, 44 and 42 [71] on the box populations, 8 [15] on
  ledges and 3 [5] on cut brushes.
- **Isolated corners judged by the oracle:** 6 fixed, 8 broken.
- **Negative control:** no isolated corner where the two sides put the corner in different places.

Stage 3 on its own is a mixed state. Loops keep features that the merge job's 12.5 mm weld still collapses, which
puts 3–12.5 mm splits into the survey and costs the map an extra propagation round (1.9 s). The τ alignment rule
changed nothing in the survey; on the map it was only measured switched on.

**Verdict: stages 3 and 4 miss the bar, so work stopped here, as the stop rule requires.**
- **What canonical identity does:** it removes the fin and every T-junction stretch and cuts the uncovered
  boundary by 38%, at no time cost.
- **What it breaks:** it opens 8 holes and adds 10 duplicate collider groups, and the two sides still disagree at 86
  survey corners.
- **Two open questions:**
  1. Most of those disagreements are 0.6–3 mm apart, just past τ. The likely cause, still unconfirmed, is a plane
     between τ and 2τ from a vertex that is in one computation's key and not in the other's. On the map at stage 4,
     12,399 vertices have such a face.
  2. About half of the remaining sliver misses are features thinner than τ, which merge on both sides by design. The
     survey still counts them as misses, and a class for them is being added.

## Diagnosis, 16 September 2026

After the stop, Sander asked for a bounded diagnostic round. It used a drop log on bm_c0a0a, and a throwaway probe
that reads the pipeline's own caches at stage 4 and dumps every survey corner the two sides disagree on.

- **Holes.** At stage 4, triangulation drops 35 whole surfaces (no triangles returned, too few points after
  deduplication, one triangulator error); at stage 0 it drops none. Six of the eight holes are exactly such drops. The
  other two were lost earlier in the pipeline and were not traced. The dropped loops are thin features that the
  12.5 mm weld used to erase.
- **Duplicate collider groups.** Eleven of the twelve at stage 4 are triangles that match three or more brush faces,
  which means thin features drawn more than once. The two groups of stage 0, which are real 0.9 mm gaps in the map, are
  gone at stage 4.
- **Survey disagreements.** The vertices themselves agree: both sides compute the same keys and the same bits. The 86
  disagreements are thin features. In sliver seed 6, for example, the box's corner edge lies 0.29 mm from the
  wedge's slope, inside τ. The two crossings that bound the sliver lie 0.92 mm and 2.75 mm from that edge, outside
  τ.
  - The core's fat-plane tests (`kFatPlaneWidthEpsilon`, used throughout the CSG) put the corner on the slope in the
    wedge's loops.
  - Canonical identity keeps the corner apart from the crossings.
  - The box's own faces don't contain the corner.
  
  37 of the 86 have a containing face between τ and 2τ, but that band is not the mechanism.

**Conclusion.** Point identity can't make a feature consistent when it is thinner than τ in one direction but wider
than τ in another, as long as the core classifies against a 0.6 mm fat plane. Reaching the bar needs one rule for thin
features throughout the core: either collapse them everywhere, or classify exactly. Canonical vertices are parked at
stage 0. The alternative under consideration is exact, plane-based classification: planes snapped once at the input,
and exact predicates after that, so that no other distance is ever compared.

## Stages

Each stage sits behind a toggle and is compiled offline first. It is measured with the Core tests, the survey and the
bm_c0a0a A/B, and committed with the toggle off until it earns its place.

**D0 · Baselines and the acceptance test (½ day).**
- Make the survey runnable as a named acceptance check. Record today's numbers as the bar.
- Explain the seven "isolated" survey corners whose verdict flips with the gate: most likely pipeline vertices lying
  off the analytic planes. The acceptance check can't rely on "isolated" until this is understood.

**D1 · Keys as data, no behaviour change (1–1.5 days).**
- A per-vertex key list next to `HashedVertices`, filled at every creation site, carried through
  `outputSurfaceVertices` and `loopVerticesLookup`.
- Toggle `kCarryVertexKeys`, which requires `kInternBrushPlanes`.
- Measure: key sizes, the overflow rate, how many planes sit between τ and 2τ of their vertex (the ambiguity that
  decides rule 2's final form), and the cost.
- Output must stay identical, because nothing reads the keys yet.

**D2 · Canonical positions (½ day).**
- Positions come from keys.
- Measure: displacement against today (expected at most τ; 93% of output vertices already sit within 1e-7 of their
  planes), and whether both brushes now produce bit-identical corners.
- bm_c0a0a should be neutral apart from sub-τ area changes.

**D3 · Identity by key while building loops (1½–2 days). The decisive stage.**
- `CreateIntersectionLoopsJob` and `FindLoopOverlapIntersectionsJob` stop welding by distance and match by key.
- This is where the fin should disappear *without* opening holes.
- **Stop rule:** if the survey misses the bar here, stop and report instead of carrying on into D4.

**D4 · Merge, re-weld and triangulation (1½–2 days).**
- The merge job becomes a check.
- `PerformCSGJob` dedupes by key, and `GenerateSurfaceTrianglesJob` inserts T-junctions by rule 6.
- 9e's seed 1, a brush with a redundant seventh plane whose weld doesn't settle, should settle in one round.

**D5 · Retire the distance weld and the incidence gate (½ day).**
- Remove `WeldIncidenceFilter` and the distance-based paths once the toggle has shipped on.
- Update this document with what was measured.

About 6–7 days of work plus measurement. The answer that matters comes at D3, around day 4.

## Testing

- **Rules, as unit tests in `Core/Tests`:**
  - two brushes derive equal keys and bit-identical positions for their shared corners, whichever order the pair is
    computed in;
  - renumbered class ids leave positions unchanged;
  - the trim/wall fixture from `WeldIncidenceTests` keeps its cut;
  - a sloped sliver keeps its slot on both brushes;
  - a brush with a redundant plane gets one key per corner;
  - rule 6 agrees with the distance test on the 24,006 T-junctions both found on bm_c0a0a, and each of the 659
    disagreements (597 found only by planes, 62 only by distance, all at the edge of one band or the other) is
    explained.
- **The survey** as the acceptance test, gate setting replaced by the canonical-vertex toggle.
- **bm_c0a0a A/B:**
  - `q_cover6` (FIN, TJUNCTION, OTHER);
  - `q_scan4` (sphere-probed real holes);
  - `q_manifold`, `q_dupcol`;
  - `LogSurfaceDrop` count;
  - rebuild time.
- **Existing suites:** Core EditMode, including the contents pipeline cases.

## Done when

- **Survey:**
  - two-sided splits at gate-off levels or better (4 across all populations today);
  - one-sided misses down by at least 90% (slivers 307 → 30 or fewer);
  - the isolated-corner control clean.
- **bm_c0a0a:**
  - fin 0 m;
  - real holes 0;
  - dropped surfaces 0;
  - uncovered boundary below today's 380.7 m;
  - non-manifold edges at most 389;
  - duplicate collider groups at most 2;
  - rebuild time no more than 15% above today's ~1.2 s.
- **Tests:** the Core suite passes, apart from red tests already known for other reasons.

## Risks and open questions

1. **Ambiguous planes.** A plane between τ and 2τ from a vertex may enter one computation's closure and not
   another's. D1 counts them before rule 2 is fixed; hysteresis (in below τ, out above 2τ, flag in between) is the
   likely answer.
2. **Id renumbering** under incremental updates. Hence rule 3's tie-break on coefficients. Needs a test that edits one
   brush and checks that its untouched neighbours still match bit for bit.
3. **Cost.** A closure is roughly (touching brushes × their faces) plane tests per vertex: about 5 million on
   bm_c0a0a, which Burst should handle in tens of milliseconds. Interning alone measured 77 ms for 34,040 planes, but
   the whole id path measured about 450 ms extra per rebuild on 12 September. It ships off today; with this plan it
   runs on every rebuild. At 450 ms it would break the 15% budget on its own, so D1 re-measures it first. Some time
   comes back in D4, when the 30-round merge becomes a check.
4. **Thin features now survive.** Anything between τ and 12.5 mm used to be welded away and will now be drawn: slivers,
   thin triangles, collinearity near the triangulator's limits. Watch the drop counts.
5. **Memory.** 78,000 vertices × 8 ids ≈ 2.5 MB of Temp per rebuild, plus the overflow table.
6. **Large coordinates.** τ is about 40 float32 steps at 150 m. Far larger worlds need τ scaled with coordinate size.
7. **Coordination.** Contents hooks live in `PerformCSGJob` and `CreateRoutingTableJob`; edits there need a word with
   whoever owns contents.

## What it does not fix

- The last two duplicate collider groups on bm_c0a0a: real 0.9 mm gaps in the map, above τ, so they stay two planes.
  That is a tolerance decision, not an identity one.
- Non-simple loops with other causes (see the missing-triangles work). They may shrink; nothing guarantees it.
- Intersect semantics, fixed separately in `b4fe5b7`.

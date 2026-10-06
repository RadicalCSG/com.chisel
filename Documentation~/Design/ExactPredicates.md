# Exact predicates: decide signs exactly, and stop asking how far

Status: **proposal, nothing built — and its central premise has since been disproved. Read
[PipelineMap.md](PipelineMap.md) first.** Written 22 September 2026 after `b1114ba` was found to trade one missing
surface for another. [CanonicalVertices.md](CanonicalVertices.md) is the previous attempt on the same problem, and
the reasons it stopped are the most useful evidence here.

> **Superseded, 22 September 2026.** This document's plan begins at "Stage 1 — plane identity": intern the planes
> so that one geometric plane has one representation, rather than snapping coordinates. Walking the input stage
> showed that there is no point in the pipeline where an exact plane exists to be interned.
>
> The planes the CSG consumes are not input. They are per-brush least-squares fits, recomputed from the brush's own
> **welded** vertices by `ConvertToBrushMeshBlobJob.CalculatePlane`: Newell in double, `math.normalize`, and `d`
> set to the *average* of `-dot(normal, vertex)` over the polygon — so a plane need not pass through any of its own
> vertices. Exact planes do exist earlier: the VMF importer computes them from each side's three integer points and
> `BrushFactory.AssignCuttingPlanes` deliberately writes them back. They are lost at exactly one boundary,
> `BrushMeshPointers` (`BrushMeshManager.Internal.cs:210`), which has fields for vertices, polygons and half-edges
> and none for planes.
>
> So the first question is not how to canonicalise planes. It is whether to carry the authored planes across that
> boundary at all — which is a small, local change — and what to do for the generators, which have no authored
> planes to carry. Everything below this line was written before that was known, and the stage plan in particular
> should not be followed as written. The tolerance inventory it points at is still valid; the *organisation* of
> this document into eleven pipeline stages was invented, and [PipelineMap.md](PipelineMap.md) replaces it with the
> 33 regions the scheduler actually has.

## How this document was built

**The first version of this section claimed to be exhaustive and was not.** It listed 18 tolerances; there are 57.
It read the tolerance sites in 13 of 21 files and inferred the rest from constant and file names. Its table of
"tolerances outside `CSGConstants`, which an audit that only greps the constants will miss" was itself built by
grepping the constants, so it missed the whole input/generator stage, both third-party files, and every inline
literal. It is recorded here because a plan that overstates its own coverage is worse than one that admits a gap.

The inventory now lives in **[ToleranceInventory.md](ToleranceInventory.md)**, which is **generated** by
[`tolerance_inventory.py`](tolerance_inventory.py) and should be re-run and diffed rather than hand-edited. It walks all 196
non-test sources under `Core/`, finds every declared tolerance, every use of each, and every inline numeric literal
in a comparison that has no named constant.

It checks itself in three ways, because the first version of the script under-reported too — it keyed declarations
by *name*, and `kEpsilon` is declared ten times, so they silently overwrote each other:

- declarations are keyed by (file, name), and names declared more than once are listed as their own finding;
- the count is cross-checked against a second, independently written detector, and the script **refuses to write
  the file** if they disagree;
- files that fall outside its pipeline map are reported as `UNCLASSIFIED` rather than dropped.

Its one known imprecision is stated in its own output: where a single file declares the same name twice, the use
counts cannot attribute a bare mention to one declaration, so both are credited. The use *lists* are complete.

**The pipeline order below was also invented.** This line used to claim it "comes from the 53 jobs
`CSGManager.UpdateTreeMeshes` schedules, read in schedule order". It did not: the eleven stages were mine, and
everything in the tolerance inventory was then classified by them, which made the classification fiction even
where the counts were right. There are 53 job *instantiations* of 49 distinct types across **33 regions**, and the
regions are the author's own. See [PipelineMap.md](PipelineMap.md), which is generated from the scheduler.

## The problem, stated as the field states it

Robustness failures in solid modelling almost never come from coordinates being slightly wrong. They come from
**predicates** — sign decisions — being answered inconsistently, so the code builds a topology no geometry could
have produced. An open loop is not a slightly-wrong polygon; it is an impossible one. No epsilon created it, an
inconsistent *decision* did.

Ours are inconsistent because of **constructions**. A brush is a set of planes, which is exact input. A vertex is
*constructed* as the intersection of three of those planes and rounded to `float3`. Predicates are then asked about
the rounded point. `kFatPlaneWidthEpsilon` = 0.6 mm exists to absorb that rounding — and once a tolerance is inside
a predicate, two predicates about the same geometry can disagree.

Exact predicates are cheap. Exact *constructions* are what cost. Every construction fed back into a predicate is
where robustness dies.

### What it costs today, measured

| symptom | measured |
|---|---|
| `b1114ba`: a parapet wall on bm_c2a5a | 0.93 m² drew 0.0116 m² — an edge destroyed with nothing to replace it |
| its fix, on concrete pad solid 4838 | 45.79 m² draws 0.00 m² — an edge *kept* that should have gone |
| the pad, by drop reason | 26 surfaces reach triangulation, 1 dropped, reason `ZeroTrianglesReturned` |
| canonical vertices at stage 4 | 35 surfaces dropped, 8 new holes, dup collider groups 2 → 12 |

`ZeroTrianglesReturned` means the triangulator returned OK and produced nothing: the surface **had** loops, was
accepted, and enclosed no area. That is the open-loop signature, and it confirms the loss happens at the loop
merge. Control for that measurement: two overlapping boxes, 12 surfaces seen, 0 dropped.

The first two rows are the same rule in opposite directions. Today `kKeepEdgesRestingOnTheOtherLoopsBoundary` is a
straight choice between them. Both are pinned by tests (`FlushContactFaceTests`, `SourceConcretePadTests`); the
pad's two are committed **red** on purpose.

### Where the mixed comparison is

`BooleanEdgesUtility.CategorizeEdge` has two paths:

1. exact match against the other loop's **edges** → `Aligned` / `ReverseAligned`. A point-vs-point test on vertex
   identity. Correct.
2. otherwise the edge's **midpoint** against the other loop's brush **planes** → `Inside` / `Outside`.

Path 2 compares a point to a plane set — two representations, met with a tolerance. `b1114ba`'s rule,
`RemoveAntiparallelEdgePairs` and the fat band all exist to patch its answers. **Path 2 is only reached when path 1
fails**, and path 1 fails because vertex identity is approximate.

`LogStrictCrossing` in `PerformCSGJob` says the rest: it reports *"TRUE crossings — one endpoint strictly inside the
other loop's volume, the other strictly outside — i.e. crossings the upstream split missed"*, and notes that for a
convex plane set this straddle is **the only case where the midpoint test can be wrong**. The midpoint test is
sound only if edges were already split at every crossing, and the file carries a diagnostic for when they were not.

## The principle

**Exact predicates, no constructions, identity by interning.**

- **Floats are already exact.** An IEEE double is an exact dyadic rational, so a float plane is an exact plane —
  just not a tidy one. Exactness is not about the input being nice; it is about every sign decision on whatever the
  input *is* being evaluated exactly. This matters because the importer is a test harness: real content is
  hand-built and generator-made, and any scheme resting on Hammer's integer coordinates would pass every imported
  map and fail the content people actually author.
- **Never construct a vertex in order to reason about it.** A vertex is the set of planes through it; predicates
  about it are determinants of those planes' coefficients.
- **A plane must have exactly one representation.** This, not precision, is what breaks set identity with float
  input. If one conceptual plane is computed along two paths and the results differ by an ulp, "the same plane"
  compares as two planes and identity is dead before any predicate runs.

**Bit growth stays bounded because planes never multiply.** Every plane comes from a brush; CSG selects and
combines, it never fits a new plane to computed points. That is the condition that makes this work for a brush
modeller and not for a general mesh boolean.

### Prior art

Shewchuk (1997), adaptive-precision predicates. Bernstein & Fussell, *Fast, Exact, Linear Booleans* (SGP 2009) —
solids as plane sets, vertices implicit, predicates as determinant signs; the closest fit. Attene, *Indirect
Predicates for Geometric Constructions* (2020) and Cherchi et al., *Interactive and Robust Mesh Booleans* (2022) —
constructed points kept implicit. Edelsbrunner & Mücke, *Simulation of Simplicity* (1990) — degeneracies by
symbolic perturbation instead of special cases. Zhou, Grinspun, Zorin & Jacobson, *Mesh Arrangements for Solid
Geometry* (2016) — exact arrangement then classify. Sugihara & Iri — topology-oriented: never let a numeric test
create an impossible topology. Hobby (1999) and Halperin & Packer — snap rounding, for the output stage only.

## What already exists to build on

Found while enumerating, and it changes the cost estimate:

- **`CSGMath.TwoSum` / `TwoProduct` / `DifferenceOfProducts`** — Knuth and Dekker error-free transforms, already
  here, already used by `CSGMath.Orient2D`. This is the arithmetic stage 2 needs.
- **A proven Burst constraint**, written on `Orient2D`: the EFTs require strict IEEE with no reassociation and no
  FMA contraction; `FloatMode.Default` is Strict and keeps them valid, and **`FloatMode.Fast` breaks them**. Any
  job reaching an exact predicate, directly or by inlining, must not be `FloatMode.Fast`. That constraint already
  applies and must be carried to every new predicate.
- **A recorded reason not to filter `Orient2D`**: `MeshAlgorithms.Orientation` consumes the *magnitude* (treating
  `|value| < 1e-12` as collinear) and `Map3DTo2D` projects with non-normalised axes, so a sign-only adaptive filter
  could disagree with the compensated result. **New predicates must return signs only, and any consumer wanting a
  magnitude is a site that has to be fixed, not accommodated.**
- **`InternedPlanes`** — plane interning exists. The canonical-vertex work deliberately keyed on each brush's own
  face planes *instead* of interned classes; that decision is the one to revisit.
- **`ContentsOracle` + `ContentsComparison`** — an independent oracle to judge output against, and
  `ContentsSceneShrinker` to minimise any scene that disagrees.

## Inventory

The full list is generated: **[ToleranceInventory.md](ToleranceInventory.md)**. As of 22 September 2026 it reports
**57 declared tolerances across 36 distinct names, 307 uses, and 24 inline literals** with no named constant, over
196 source files.

What the enumeration turned up that a hand list did not:

- **`kEpsilon` is declared ten times**, in ten unrelated places, with ten values: 0.001 (linear stairs), 0.0001
  (pathed stairs), `kFatPlaneWidthEpsilon` (revolved shape), 0.001 (spiral stairs ×2), `DecalVolume.kPlaneEpsilon`
  (decal clipping), 0.0001 and 1e-9 (`MathExtensions`), 0.0006 and 1e-5 (`PlaneExtensions`). `kDistanceEpsilon` is
  declared five times with five values. A reader who sees `kEpsilon` in a diff learns nothing about which tolerance
  changed.
- **The whole input/generator stage has twelve tolerances**, and the first version of this plan did not mention it.
  `BrushMesh.Optimize` alone declares three, one of them `const float kDistanceEpsilon = 0.0001f; // TODO: why??`.
  This matters more than the count: generators are where real content's planes come from, so this is the stage that
  decides whether hand-built levels can ever have exact plane identity.
- **Two third-party files carry their own**: `BayazitDecomposerBursted` (`kConvexTestEpsilon` 1e-5,
  `kDistanceEpsilon` 0.0006) and `ConvexHullCalculator` (0.001). Vendored code cannot simply be converted, and
  whether the exact layer has to stop at their boundary is an open question.
- **Three tolerances are declared and never used** — dead, and worth deleting so the count means something.
- **24 inline literals have no name at all**, including `MeshAlgorithms.cs:238` (`dlen2 < 1e-12`), which is the
  collinearity test `CSGMath.Orient2D`'s own comment warns about. I cited that warning in the first version without
  having found the site it refers to.

### By pipeline stage

Ordered as `UpdateTreeMeshes` schedules them. Stage names match the generated inventory's sections, so the two can
be read side by side. Jobs with no tolerance are omitted; the other 43 need no work.

**1. Input / generators** — 12 tolerances (`BrushMesh.Optimize` ×3, `BrushFactory.Utility`, capsule, linear /
pathed / spiral stairs, revolved shape, stadium, `UVMatrix`), plus inline literals in `BrushFactory.Utility`.
→ Where a generator's planes are made. Stage 1 requires them to be made **once**, through interning, rather than
recomputed equal. `BrushMesh.Optimize`'s welding is a second, independent vertex-identity mechanism to reconcile
with stage 3.

**2. Broad phase — `FindAllBrushIntersectionPairsJob`** — `kBoundsDistanceEpsilon`, 8 uses.
→ **Keeps its tolerance, deliberately.** A conservative filter: may over-include, must never exclude.
→ Test: a pair whose bounds touch within the epsilon is always considered.

**3. Planes — `InternBrushPlanesJob`, `InternedPlanes`, `CreateBrushTreeSpacePlanesJob`, `PlaneExtensions`** —
`kDivideMinimumEpsilon`, `kPlaneDAlignEpsilon` (a stored plane is accepted for a point set if it passes within
0.6 mm), and `PlaneExtensions`' two `kEpsilon`s (0.0006, 1e-5).
→ **This is the identity layer and it is currently approximate.** Interning by "close enough" is what makes one
plane into two, or two into one, depending on order. `CreateBrushTreeSpacePlanesJob` *derives* a second
representation of every plane, which is the same risk in its most concrete form.
→ Stage 1.

**4. Pair preparation — `PrepareBrushPairIntersectionsJob`, `CreateBlobPolygonsBlobsJob`** —
`kFatPlaneWidthEpsilon` ×5, `kNormalDotAlignEpsilon` ×4, `kPlaneDAlignEpsilon`, `kPlaneVertexAlignEpsilon` (0.01),
`kSqrEdgeDistanceEpsilon`.
→ Classifies each pair as coincident / aligned / crossing. Every one is a sign question. Stage 2 and 4.

**5. Intersection loops — `CreateIntersectionLoopsJob`** — `kFatPlaneWidthEpsilon` ×8, `kNormalDotAlignEpsilon` ×3.
→ **Where vertices are constructed**, via `PlaneIntersection`. The mutually-parallel guard is a real degeneracy
test and becomes exact. The construction stays; nothing downstream may re-derive identity from its result.

**6. Loop overlap / splitting — `FindLoopOverlapIntersectionsJob`, `LoopEdgeSplitter`, `LoopVerticesCacheJobs`** —
the largest concentration: `kFatPlaneWidthEpsilon` ×13, `kSqrVertexEqualEpsilon` ×6, `kSqrEdgeDistanceEpsilon` ×2.
→ **The split step**, which `LogStrictCrossing` reports as incomplete. If splitting is exact and complete,
`CategorizeEdge`'s fallback stops being reachable. **Highest value in this document.**
→ `LoopVerticesCacheJobs`' tolerances are an *influence radius* for cache invalidation — a conservative bound, not
a decision. **Keeps its tolerance**, and must stay ≥ the real reach.

**7. Welding — `MergeTouchingBrushVerticesJob`, `HashedVertices`, `WeldIncidence`, `CanonicalVertices`** —
`kSqrVertexEqualEpsilon` ×7 in `HashedVertices` alone, `kCellSize`, τ and 2τ, `kSameVertex`.
→ Becomes "same plane set". Stage 3, and **the riskiest change**: this is where canonical vertices broke, because
exact identity splits vertices the weld merges and thin features are where that shows.

**8. CSG — `PerformCSGJob`, `BooleanEdgesUtility`** — `kSameRegionEpsilon`, `kCanonicalSameRegionEpsilon`,
`kSqrVertexEqualEpsilon`, `kFatPlaneWidthEpsilon` ×4 in the predicate library, and the three repairs
(`kKeepEdgesRestingOnTheOtherLoopsBoundary`, `kKeepReverseAlignedBaseEdges`, `RemoveAntiparallelEdgePairs`).
→ `LoopsOutlineSameRegion` becomes set equality. **Stage 4 deletes the fallback and all three repairs.** The payoff.

**9. Triangulation / output — `GenerateSurfaceTrianglesJob`, `MeshAlgorithms`, `MeshManifoldValidation`, Bayazit,
ConvexHull** — `kEdgeIntersectionEpsilon` ×4, `kSqrEdgeDistanceEpsilon` ×2, `kNormalizeEpsilon`,
`kDefaultWeldEpsilon`, the two vendored files' own, and inline 1e-12 / 1e-9 / 0.005 / 0.0001.
→ T-junction insertion is a distance question by nature. **May keep a tolerance** — see below.
→ `MeshManifoldValidation` should be the **last** thing changed, because it is how the rest is judged.
→ The vendored files are an open question: converting them is out of scope, so the exact layer may have to stop at
their boundary and that boundary needs naming.

**10. Decals — `DecalVolume`, `DecalClipping`** — `kPlaneEpsilon` 1e-4, `kEpsilon`, `kMinArea`, inline 1e-12 ×3,
1e-6.
→ Not addressed by this plan; listed so it is not mistaken for covered.

**11. Shared math — `CSGMath`, `MathExtensions`, `CSGConstants`** — the nine `CSGConstants` values,
`MathExtensions`' four, and `CSGMath`'s EFT machinery.
→ Where the exact predicates go. Stage 2.

## What keeps a tolerance, on purpose

Not everything should become exact, and saying which is part of the design:

- **Broad-phase bounds** (`FindAllBrushIntersectionPairsJob`): a conservative filter. Must never exclude, may
  include.
- **Cache influence radii** (`LoopVerticesCacheJobs`): a bound on reach, not a decision.
- **T-junction insertion**: asks whether two independently-built surfaces meet. Exactness here needs the *shared*
  structure, which is stage 4's arrangement; until then it is a modelling tolerance and should be named as one.
- **Output snapping** (stage 5): the only place rounding belongs.

Everything else in the inventory is a sign question wearing a distance.

## Why the previous attempt got worse, and why that is encouraging

Canonical vertices computed a **position** from a plane set, with τ deciding which planes were in the set, then
compared **positions** with `kSameVertex` = 1e-5. It kept the construction and it kept the epsilon. Its own open
lead names the consequence: *"planes between τ and 2τ entering only one computation's key (12,399 map vertices have
such a face)"* — an ambiguously incident plane lands in one key and not the other, so one vertex becomes two.

That is the half-measure being the worst option. **If set membership is exact, that failure mode cannot occur.** It
is also the warning that shapes the staging below: a partial move costs the full disruption and returns none of the
benefit, so nothing changes behaviour until the exact layer has been shadow-compared against the approximate one.

## Stages

### Stage 0 — measure the exposure. No behaviour change.

Carry the exact unnormalised plane beside the float one and count, on an imported map **and** a hand-built scene
(different populations — the importer is a test harness):

1. how often `CategorizeEdge` reaches the midpoint fallback;
2. of those, how often the exact answer differs from the fat-band answer;
3. how many float planes are bit-different but conceptually one plane (the interning exposure);
4. how many vertices have a plane between τ and 2τ (the canonical-vertex lead, re-measured);
5. how often `EdgeStrictlyCrossesSegmentPlanes` fires — crossings the split missed;
6. how many surfaces are dropped, by reason, as a baseline.

**Stop rule: if (1) is rare and (2) rarer, this is a narrow path to fix, not a foundation to rebuild, and the work
ends here with a much cheaper recommendation.**

### Stage 1 — plane identity
One plane, computed once, referred to by id. Revisit the canonical-vertex decision to key on brush face planes
rather than interned classes. Remove normalisation from the paths feeding predicates (a `sqrt` is where one plane
becomes two; determinant signs are scale-invariant).
**Measured by:** count (3) going to zero. **Stop rule:** if planes cannot be single-representation without
restructuring the transform pipeline, stop and say so — everything below depends on it.

### Stage 2 — exact predicates, shadowing only
Sign-only exact evaluation over plane coefficients, built on the existing `TwoSum`/`TwoProduct`, filter-first. Run
alongside the current predicates and count disagreements. Every such job must not be `FloatMode.Fast`.
**Stop rule:** if the filter does not make the common case free, stop and reconsider.

### Stage 3 — exact vertex identity
A vertex is its plane-id set; identity is set equality. Shadow-compare against `HashedVertices` and `WeldIncidence`.
**Stop rule:** if exact identity splits vertices the weld merges in ways that matter (thin features), that is the
canonical-vertex failure returning and it must be understood *before* stage 4.

### Stage 4 — switch over
`CategorizeEdge`'s midpoint fallback is **deleted**, not narrowed. `kKeepEdgesRestingOnTheOtherLoopsBoundary`,
`kKeepReverseAlignedBaseEdges` and `RemoveAntiparallelEdgePairs` go with it. Degeneracies move to
Simulation-of-Simplicity-style symbolic perturbation instead of tie rules.
**Done when:** `FlushContactFaceTests` and `SourceConcretePadTests` are green **together**, which today's switch
cannot do, and the oracle harness and `ContentsPipelineTests` are unmoved.

### Stage 5 — round once, at output
Floats for rendering, produced at the end. Snap rounding if topological guarantees are needed on the rounded
result.

## Tests

### Already exist and are the acceptance bar
- `FlushContactFaceTests` (6) — the parapet. Must stay green.
- `SourceConcretePadTests` (4, **2 red today**) — the pad. Must go green **without** the parapet regressing.
- `ContentsPipelineTests` (21) — oracle comparison over generated scenes. Must stay green.
- `IntersectionLoopCapacityTests` (3), `SurfaceDropReasonTests` (2, reporting) — recent, unaffected but must stay.
- `BrushFromPlanesTouchingPlaneTests`, `AlignedFaceDetectionTests`, `AlignedFaceLoopDeliveryTests`,
  `LoopValidationTests`, `LoopEdgeSplitterTests`, `MeshManifoldValidationTests`, `InternedPlanesTests`,
  `CanonicalVerticesTests`, `WeldIncidenceTests`, `CSGMathTests`, `PlaneExtensionsTests`.

### New, per stage

**Stage 0** — a survey fixture reporting counts (1)–(6) on a generated scene and, driven from a slot, on bm_c2a5a
and a hand-built scene. Reports, asserts nothing: the point is to learn the numbers, and a test asserting numbers I
guessed would measure my expectations.

**Stage 1 — plane identity**
- the same plane reached by two paths (brush-local transformed vs cached tree-space) interns to one id;
- a plane and its positive scalar multiple intern to one id; a negative multiple does **not** (opposite facing);
- interning is order-independent: the same scene built in two orders produces the same id set;
- a generator that makes the same plane twice gets one id;
- **a falsification test**: two planes that are genuinely distinct but within `kPlaneDAlignEpsilon` must stay
  distinct — this is what the current approximate interning gets wrong.

**Stage 2 — exact predicates**
- sign agreement with the compensated `Orient2D` over random inputs;
- exactness on constructed near-degenerate cases: a point at distance 0, ±½ulp, ±ε from a plane;
- the filter falls back on exactly the cases where it must — count filter hits vs expansion hits;
- **no predicate returns a magnitude** (a compile-level or reflection test over the predicate API);
- a `FloatMode.Fast` guard: a test that fails if any job reaching a predicate is marked Fast.

**Stage 3 — exact vertex identity**
- two vertices with the same plane set are one vertex regardless of computed position;
- two vertices with different plane sets stay distinct even when their positions are within `kVertexEqualEpsilon`
  — the case the current weld merges wrongly;
- a vertex on a brush's face but not a corner is not given that face's plane;
- thin-feature suite: the sub-τ features that broke canonical vertices, asserted explicitly rather than discovered.

**Stage 4 — behaviour**
- the pad and the parapet, together (the whole point);
- an edge exactly coplanar with another loop's boundary is categorised without a fallback — assert the fallback is
  never reached, which is only meaningful once it is deleted;
- the merged loop is **closed**: a structural assertion over `CleanUp`'s output, every vertex of degree 2 per loop.
  This is the assertion whose absence let both bugs through, and it should be added **before** stage 4 so it fails
  on today's code for the pad;
- Simulation-of-Simplicity consistency: a scene and the same scene under a tiny global translation produce the same
  combinatorics.

**Stage 5**
- snapped output stays within a stated bound of the exact result;
- no new self-intersections are introduced by snapping.

### Cross-cutting
- **The oracle harness is the judge**, not hand-written expectations; `ContentsSceneShrinker` minimises any
  disagreement (its `maxPasses` defaults to 8 and removes one node per pass — pass enough).
- **Determinism**: the same scene built twice produces bit-identical output, at every stage.
- **Burst parity**: Burst-on and Burst-off produce identical combinatorics.

## Risks and open questions

- **Transforms** are the main unknown in stage 1: a rotated brush's planes are float-transformed, which is fine for
  exactness but must happen once.
- **Displacements** do not follow the plane-set model and are not addressed here.
- **Performance** is unmeasured; stage 2 measures it, and the filter is the whole argument.
- **`MeshAlgorithms.Orientation` consumes a magnitude**, which is incompatible with sign-only predicates. That call
  site has to be fixed, not accommodated — and it is currently the documented reason `Orient2D` is compensated
  rather than filtered.
- **Scope**: a foundation change. It should not start while the pad and parapet are open, and stage 0 exists to be
  able to say "don't".

## What this does not fix

Anything upstream of the predicates. The pad's drop is confirmed as an open-loop triangulation drop, so the target
is right — but nothing here addresses geometry lost before the merge, and `LogStrictCrossing`'s existence says the
split step is independently suspect.

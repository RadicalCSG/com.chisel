# Chunking a model's output

Design plan, 20 September 2026. Nothing is implemented. The measurements are from bm_c2a5a, taken with the scratchpad
script `q_chunk_survey.cs` against com.chisel `main` at `7ed29a5`; line numbers refer to that commit.

- **Example:** bm_c2a5a (Black Mesa, *Questionable Ethics*)
- **Units:** metres; a Hammer unit is 1.22/64 m
- **Touches:** com.chisel Core (output grouping) and Components (generated objects)

## What a chunk would be for

Two problems point at the same mechanism, and the measurements below say it only solves one of them.

**Lightmap size.** One renderer gets one lightmap rectangle, sized `√area × lightmapResolution × scaleInLightmap` and
clamped at `lightmapMaxSize` (see [ChiselLightmapUVs](#see-also)). A renderer that spans the map is clamped, so the
whole model loses resolution. Splitting a model over several renderers gives each one its own rectangle.

**Culling.** Unity culls per renderer. A model already is a cullable unit and there are many of them — bm_c2a5a has 130
`ChiselModelComponent`s, bm_c0a0a 40, and the Source importer marks each of them
`BatchingStatic|OccludeeStatic|OccluderStatic|ReflectionProbeStatic` (`VmfBrushModels.cs:94`), so models are culled like
any other renderer, by terrain or by each other. What is *not* cullable is the inside of one model: the world model's
renderers span the map, and a model is one `CSGTree` (`ChiselModelComponent.cs:316`), so the world cannot simply be
split into more models — brushes in different models never cut each other, and every seam would keep the faces the CSG
exists to remove.

So chunking means splitting the *output* of one tree, not making more trees.

## What the map is

bm_c2a5a's geometry is two models with nothing in common but their bounds.

| | `Model` (worldspawn) | `Details` |
|---|---|---|
| render meshes / sub-meshes | 3 / 42 | 3 / 94 |
| vertices | 2,988 | 42,298 |
| area | 890,554 m² | 71,197 m² |
| surfaces | 649 | 9,107 |
| surface span, median / 99th / max | 19.5 / 263.6 / 356.2 m | 1.9 / 41.5 / 78.1 m |
| meshes over 4096 texels today | 2 of 3, worst 7.3× | 2 of 3, worst 1.9× |

A *surface* is one brush face's output — the unit in `ChiselQuerySurface`. It is the smallest thing the grouping can
move, because splitting one would mean re-cutting geometry. That is what makes the span row the important one.

## What the measurements say

Binning each surface into a grid cell by its area-weighted centre, chunk bounds being the union of its surfaces':

```
Details   8 m:  534 chunks, 1270 draws, bounds 1.22x cell, lightmap max 1546,  0 over 4096
         16 m:  213 chunks,  717 draws, bounds 1.12x cell, lightmap max 1820,  0 over 4096
         32 m:   76 chunks,  398 draws, bounds 0.83x cell, lightmap max 3065,  0 over 4096
         64 m:   26 chunks,  256 draws, bounds 0.91x cell, lightmap max 4407,  1 over 4096

Model     8 m:  452 chunks,  534 draws, bounds 4.57x cell, lightmap max 9443, 20 over 4096
         16 m:  345 chunks,  448 draws, bounds 2.94x cell, lightmap max 9443, 20 over 4096
         32 m:  226 chunks,  313 draws, bounds 2.14x cell, lightmap max 9443, 21 over 4096
         64 m:   99 chunks,  182 draws, bounds 1.53x cell, lightmap max 9443, 30 over 4096
```

**A grid works for `Details`.** At 16 m its chunks' bounds are 1.12× the cell — a real spatial partition — only 5.7% of
its surfaces are wider than their cell, and every chunk's lightmap fits with room to spare (1820 against 4096). One
mechanism delivers both payoffs.

**A grid does nothing for `Model`.** The lightmap maximum is 9443 at 8 m and still 9443 at 64 m, because one surface is
already over budget: 74% of its surfaces are wider than an 8 m cell and the largest is 356 m across. At 8 m the chunks'
bounds are 4.57× the cell on median and 44.5× at worst, so they overlap far too much to cull against, while the median
chunk holds six vertices. Finer cells make this worse, not better.

**`Model`'s problem is that almost none of its area draws.** Measured per material (`q_lightmap_materials.cs`):

```
sky_st_day_01 (faces)   555,347 m2  62.4%   asks 29,809 texels   [Chisel/Source/Sky Face]
ForceShadowOnly         324,484 m2  36.4%   asks 22,785 texels   [Custom/ForceShadowOnly]
everything else          10,723 m2   1.2%   asks  4,143 texels
```

**Read those two rows differently, because they land in different renderers.** `ForceShadowOnly` is a draw-time
override applied only where `query == ShadowCasting` (`ChiselGeneratedObjects.cs:512`), so its 324,484 m² is the
shadow-casting-only renderer — which `isRenderable == false` already excludes from `ContributeGI`, so Unity allocates
it no lightmap at all. **That area costs nothing today.** It is not part of the lightmap demand; only the `Renderable`
renderers are.

So the model's lightmapped area is 890,554 − 324,484 = **566,070 m², of which sky faces are 555,347 — 98.1%**. The sky
is the whole problem, by itself:

```
Model    Renderable area 566,070 m2 -> asks 30,096 texels -> clamped to 4096 = 7.3x too coarse
         without sky      10,723 m2 -> asks  4,143 texels -> 39.5 of the 40 texels/m asked for
Details  Renderable area  38,539 m2 -> asks  7,854 texels -> clamped to 4096 = 1.9x too coarse
         (no sky to remove, so chunking or density is the only route)
```

Both clamp factors match the 7.3× and 1.9× measured independently by the first survey, which is the cross-check that
this reading is the right one.

**Why trimming charts helps, given that Unity sizes by mesh area.** Unity allocates
`√(GetCachedMeshSurfaceArea) × lightmapResolution × scaleInLightmap`, clamped at `lightmapMaxSize`; it knows nothing
about our UV1, so excluding sky faces from the charts does *not* shrink the allocation. What it changes is how that
allocation is spent: the effective density a surface ends up with is `(Unity's side / our layout's side) × texelsPerUnit`.
Today `Model` is 4096/30,096 × 40 = 5.4 texels/m. With sky out of the charts our side drops to 4,143 while Unity's
stays clamped at 4096, giving 39.5 texels/m — the full requested density, spent entirely on the 1.2% that draws.

**A consequence worth acting on separately:** that only works because the allocation is clamped. If it were not, Unity
would allocate 30,096 for a layout needing 4,143 and waste most of it. The robust fix is to set each renderer's
`scaleInLightmap` from the layout's own `side`, so Unity allocates exactly what was packed, whatever the clamp does.

**Draw calls are one cost.** Today the two models draw 136 sub-meshes from 6 meshes. At 16 m cells that becomes 1,165,
at 32 m 711. The GPU Resident Drawer is on (`m_GPUResidentDrawerMode: 1`), so these batch, but the number has to be
watched rather than assumed away.

**Scene size is the other, and it has a crossover.** The picking data a scene saves keeps one object reference per
distinct **(renderable, generator) pair** plus one int per selection id — roughly 30 bytes against 8. Break-even is at
~1.4 ids per pair; bm_c2a5a is at 2.4 today (47,756 ids over 20,006 pairs). Past that the dedup costs more than it
saves.

The cost scales with *pairs*, not with renderables, and the two ways of making more renderables differ in kind.
Splitting by **material** duplicates any generator with surfaces on both sides — going from 8 renderer slots to 16
grew the saved picking data from 2,051,942 to 2,224,310 bytes, +8.4% (measured across chisel-dev-44's renderer change,
which also altered how surfaces group, so it is that change's effect rather than a per-slot rate). Splitting
**spatially** is the gentler case: a generator's brushes are local, so most land in one chunk and contribute one pair
however many chunks exist. 200 chunks multiplies pairs by the average number of chunks a generator straddles — near 1
for small generators — not by 200.

**So measure ids, distinct pairs and their ratio, not scene size.** If a chunking experiment drives the ratio toward
1.4, the fix is one index per id into a per-renderable (owner, brush) table, which shrinks with the chunk instead of
staying proportional to the ids. (chisel-dev-9e owns that data and has the table change in their follow-ups;
`scene_fields.py` prints the per-field byte counts.)

## The design

### Split by budget, not by grid

A fixed cell size is the wrong control: it is too fine for `Model` and about right for `Details`, and the survey shows
no single value serves both. Instead, split a model's output while a chunk exceeds a budget, and stop when it cannot be
split further:

- **lightmap budget** — `√area × texelsPerUnit` over `lightmapMaxSize`;
- **vertex budget** — a chunk large enough to be worth culling separately;
- **stop condition** — a chunk holding a single surface is final, whatever its size. This is the `Model` case, and the
  design must degrade to today's behaviour there instead of producing hundreds of overlapping one-surface chunks.

Splitting on the longest axis of the chunk's bounds, recursively, gives cells that follow the geometry. A chunk whose
surfaces all straddle the split plane cannot improve and terminates.

### Where it goes

The grouping already exists and is a counting sort on one key per surface.

- `PrepareSubSectionsJob` (`Core/2.Processing/Jobs/SubMeshesJob.cs:124`) groups surfaces by `surfaceParameter`.
  The chunk index joins that key; the sub-mesh key stays `surfaceParameter`, so a chunk keeps one sub-mesh per material.
- The chunk key must be computable at grouping time, or changing the chunk settings would invalidate every brush's
  cached render buffer. It can be: `ChiselSurfaceRenderBuffer` already carries the surface's `aabb`
  (`Core/2.Processing/Jobs/JobData/ChiselBrushRenderBuffer.cs:31`). `ChiselQuerySurface` needs the centre (or the aabb)
  copied into it, which is built per brush and independent of any chunk setting.
- `GatherSurfacesJob` then emits one `SubMeshSection` per (query, chunk) instead of per query.
- `AssignMeshesJob` (`Core/3.Output/OutputMeshes/Jobs/FillVertexBuffersJob.cs:32`) currently sets a renderable's
  `objectIndex` to `RendererIndex(destinationFlags)`, a small fixed index. It gains a chunk index alongside it, exactly
  as colliders already carry `colliderIndex` next to `objectIndex`.
- Components: `renderables` is a fixed array indexed by that renderer index
  (`Components/Components/Generated/ChiselGeneratedObjects.cs:115`). It becomes a dynamic array keyed by
  (flags, chunk) and reused by key between updates — which is what `colliders` already does, keyed by `surfaceParameter`
  (`ChiselGeneratedObjects.cs:541`). The picking arrays live per `ChiselRenderObjects`, so they follow whatever shape
  the array takes.

### What must not break

- **Determinism.** The lightmap layout's tie-break is a content hash so that a bake stays valid across reloads. Chunk
  assignment and chunk ordering have to be deterministic for the same reason, and must not depend on iteration order of
  a hash map.
- **The input hash.** Chunk settings belong in `GetTreeInputHash` (`CSGManager.SkipUnchangedTrees.cs`), or a model with
  changed chunking would keep its saved meshes.
- **One collider per model.** Colliders are grouped by physics material and are not chunked here. Whether they should be
  is a separate question; nothing in this design changes them.

### LOD

Out of scope, and worth saying why. Chisel generates no simplified geometry, so an `LODGroup` would need either mesh
decimation or a coarser CSG pass — neither exists. Chunks are a prerequisite for LOD rather than a form of it: once a
model's output is several renderers, each can later become an `LODGroup`'s LOD0. Nothing in this design should assume
that will happen.

## What this does not fix

The `Model` case. Its largest chunk asks 9,443 texels at every cell size tried, because a single surface already asks
for that much; no grouping of whole surfaces can reduce it. The fixes are elsewhere and **all of them should come
first**, because they are cheaper and they shrink the problem this document describes from two models to one:

1. **`NoLightmap` on the sky-face material.** This is 98.1% of `Model`'s lightmapped area on its own and takes it from
   5.4 to 39.5 of the 40 texels/m asked for. Sky faces are `Renderable`, so only the material's
   `ChiselSurfaceMetadata` can carry it — which means creating that metadata, since only 2 of the project's 1,401
   materials have any today. If chisel-dev-44's `ExcludedFromGlobalIllumination` bit ends up set on sky faces, a rule
   keyed on *that* would catch them with no importer change at all; re-measure after their commit before writing one.
2. ~~A surface that is not `Renderable` gets no lightmap area.~~ **Not needed — already true twice over.**
   `ChiselRenderableObjects.cs:352` sets `scaleInLightmap = 0` when `!isRenderable`, and `UpdateContainerFlags` strips
   `ContributeGI` from the same containers, so Unity allocates those renderers nothing. Measured: the shadow-only
   renderer reports `scale 0.00` and asks 0 texels for its 324,484 m². Writing the rule a third time in the chart
   layout would add no behaviour. *For the record, if it is ever written: zero texels, not one.* One texel was
   tempting — it would keep the surface bouncing rather than absorbing — but the meta pass runs on the material the
   **renderer** holds, and that is the `ForceShadowOnly` override, whose shader has a single `ShadowCaster` pass and no
   `Meta` pass. There is no albedo to extract, so one texel buys the same bounce as zero. Shadow-only geometry blocks
   light (from its geometry, which needs no texels) and does not bounce — which is what VRAD does too, where nodraw
   faces get no lightmap and do not radiate. To make them bounce one day: give that renderer the surfaces' real
   materials for the meta pass, keeping `ForceShadowOnly` for the shadow pass.
3. **Set `scaleInLightmap` from the layout's `side`**, so Unity allocates what was actually packed instead of what the
   mesh's area implies. Without it the sky fix works only because the allocation happens to be clamped.
4. **Importer density.** Set the model's lightmap density from Source's `lightmapscale` rather than leaving it at 40
   texels/m, and honour per-side `lightmapscale` (`VmfSolidSide.LightmapScale` is parsed and unused).
5. Only if surfaces are still over budget after those: split a surface's triangles, which is a different and much
   larger change than grouping.

Rule 1 alone removes `Model` from the chunking problem. `Details` has no sky to remove and is 1.9× over, so it is the
one model this document is really about.

**What the two chart modes mean** (Sander, 2026-09-20): *"0 pixel size is appropriate for non visible surfaces, or
surfaces that always have to be pure black. 1 pixel is appropriate for surfaces that emit light but do not receive
light."* Shadow-only surfaces are the first case. Sky faces are unlit and receive nothing, so they are also 0 — they
emit through the skybox, not through their own texels.

**On rebuilding:** the UV1 layout is output, not input, so it is not in the tree input hash. A code change to the
layout moves the assembly MVIDs, which *are* hashed, so every map rebuilds once on the next open and skips again after
being saved — nothing extra to bump. To test a new packing *without* recompiling, models have to be dirtied by hand.
A *reimport* is heavier: it rewrites materials and brush data, so every model's input hash changes and the map rebuilds
completely on the next open (~1.5 s against ~0.7 s) until it is saved once. Plan the save into the same slot as the
reimport rather than leaving the map in the rebuilding state (chisel-dev-9e).

**Where bm_c2a5a stands today**, measured per renderer after chisel-dev-44's commit and 9e's save:

```
Model    slot  2  ShadowCasting                       324,484 m2  scale 0.00  asks      0
         slot  7  RenderShadowReceiveAndCasting       565,718 m2  scale 1.00  asks 30,086 -> 4,096
         slot 13  RenderShadowsReceiving, ExcludedGI      351 m2              asks    749
Details  slot  7                                       38,398 m2             asks  7,838 -> 4,096
         slot 13                                          141 m2             asks    476
```

Two things to keep straight. **Nothing has `ContributeGI`**, so Unity allocates no lightmap for anything on this map
today — the 7.3× and 1.9× are what happens once it is set up for a bake, not a present cost. And **the sky faces are
still in slot 7**: 44's change writes the surface parameters when `SkyFaceMaterial` *creates* the material, and this
map's `sky_st_day_01 (faces).mat` predates it and carries no `ChiselSurfaceMetadata` at all. Until the map is
reimported, measurements before and after are not comparable and today's numbers do not include the new grouping.

## Tests

- Layout: a model whose surfaces exceed the budget produces more than one renderer; one whose surfaces do not produces
  exactly one, unchanged from today.
- Degradation: a model of one enormous surface produces one chunk, not many.
- Determinism: the same input produces the same chunk assignment and the same order twice, and across a reload.
- Lightmap: every chunk's `√area × texelsPerUnit` is at or under `lightmapMaxSize`, except chunks holding a single
  surface that is already over it.
- Input hash: changing a chunk setting changes the tree input hash.
- No geometry is lost: the triangles and vertices of the chunked output match the unchunked output.

## Open questions

- What the vertex budget should be, and whether it or the lightmap budget dominates in practice.
- Whether colliders should be chunked on the same key.
- Whether touching coplanar charts should be merged across a chunk boundary, or whether the seam is acceptable.

## See also

- `Documentation~/Design/BrushContents.md` for the surface/brush vocabulary
- The lightmap UV work this grew out of: `Core/3.Output/OutputMeshes/LightmapUVLayout.cs`

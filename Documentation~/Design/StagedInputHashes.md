# Staged input hashes

Design plan, 20 September 2026. Measurements are from bm_c2a5a against com.chisel `main` at `b1f88ee`, taken with the
scratchpad profile script; line numbers refer to that commit.

**Where this has got to**

| | |
|---|---|
| `85641a9` | The hashes exist and are computed from the components: `IChiselNodeGenerator.GetInputHash`, `ChiselGeneratorComponent.GetDefinitionInputHash`, `ChiselStagedInputHashes` (h₁, h₂ per generator, h₃). Nothing decides on them. `StagedInputHashTests` requires them to agree with the built tree's hash about seventeen changes. |
| `01c9711` | A bug that work found: a generator compared `definition.GetHashCode()` to decide whether to remake its brush meshes, and no definition overrides `GetHashCode`, so it was the object's identity. Resizing an already-built box left its mesh at the old size. The check uses the content hash now. |
| next | A scene stores h₃ next to the tree's hash so the two can be held against each other on a real map; then h₃ decides; then stages 1 and 2 are skipped. |

The order is deliberate: a hash that misses an input keeps stale geometry with **no error**, so each step has to be shown
to answer the same as the hash it is replacing before it is allowed to answer instead.

- **Rule (Sander, 20 September 2026):** *"if the input doesn't change, the output shouldn't change either. if the hash
  is the same, why are we doing work?"* and *"we can have hashes for multiple stages to avoid redundant work, but each
  stage hashes input."*
- **Example:** bm_c2a5a, 126 models, 3,811 nodes
- **Touches:** com.chisel Core (the tree input hash, the flush) and Components (the hierarchy manager, generated objects)

## What is wrong with one hash

A saved model keeps its meshes when its input hash matches the one stored at save
(`CSGManager.SkipUnchangedTrees.cs`, `ChiselModelManager.GetInputHash` at `ChiselModelManager.cs:665`). That works, and
it took the post-open frame from 1,525 ms to 715 ms. But the hash is computed *from the rebuilt input*: it walks the
native tree, its brush blobs and its transforms. So the tree has to exist before we can ask whether it needed to exist.

Opening the map today:

| | ms |
|---|---|
| 1. every component registers, the node graph is rebuilt — ~3,685 generators each get their CSG node made from scratch | 379 |
| 2. the generators run, brush meshes become blobs | 77 |
| 3. `Flush` hashes each of the 126 trees and finds them all unchanged | 24 |
| 4. the CSG that the hash saves | (800) |
| 5. renderers, colliders, picking, debug visualisation restored | ~140 |

Steps 1 and 2 are 456 ms of work whose only purpose is to make step 3's question answerable. They run whether or not
anything changed.

## The rule

**Every stage hashes its own input, and a stage's input hash is composed from the input hashes of the stages it
consumes — never from their outputs.**

    h₁ = H(component state of the model's subtree)             // hierarchy order, types, operations, transforms
    h₂ᵢ = H(generator i's definition + its surfaces)            // one per generator
    h₃ = H(h₁, {h₂ᵢ}, model settings, contents)                 // what the CSG is built from
    h₄ = H(h₃, materials, renderer / GI / lightmap settings)    // what the Unity objects are built from

`h₃` is today's `GetTreeInputHash`, expressed so it can be computed without building anything: the stages it consumes
contribute their *input* hashes, which identify their outputs exactly as well as the outputs do, because the same code
produces the same output from the same input. That assumption is already load-bearing today — it is what the code
version in the hash encodes.

`h₁` and `h₂` are computable straight from serialized component data the moment Unity finishes loading the scene.

## The stages

| | input | output | persisted | entry point | cost |
|---|---|---|---|---|---|
| 1 node graph | component state | native tree + hierarchy items | no | `ChiselNodeHierarchyManager.UpdateTrampoline`, children queue at `:1553`, `AddChildrenOfHierarchyItem` at `:1709` | 379 ms |
| 2 brush meshes | a generator's definition + surfaces | brush mesh blob | no | `BrushMeshManager.ConvertBrushMeshesToBrushMeshInstances` (`ChiselNodeHierarchyManager.cs:416`) | 77 ms |
| 3 CSG | h₁, h₂ᵢ, settings | render buffers / meshes | **yes**, the saved meshes | `CompactHierarchyManager.Flush` → `CSGManager.ScheduleTreeMeshJobs` | 800 ms |
| 4 Unity objects | h₃, materials, renderer settings | meshes, renderers, colliders, picking data | **yes**, in the scene | `ChiselGeneratedObjects.UpdateContainers` / `RestoreSkippedUpdate` (`:386`) | ~140 ms |

Granularity differs per stage on purpose: stage 2 is per generator, stages 3 and 4 per model. That is what makes a
single edit cheap for the same reason a load is — one moved brush invalidates one blob and one model, not the map.

## What a match skips

- **h₄ matches** → keep the serialized meshes, renderers, colliders and picking data. Nothing downstream runs.
- **h₃ matches, h₄ differs** → keep the CSG output, re-run only stage 4. This is the case that today's design cannot
  express: a renderer-layout or lightmap-settings change re-runs the CSG of every model in the project for no reason.
- **h₂ᵢ matches** → reuse generator i's blob, if it is still in memory.
- **h₁ matches** → the node graph would be identical; do not build it until something needs it.

## One writer per stage

A skip introduces a second way for a stage's output to come about: built, or restored. If those are two pieces of code,
they drift, and the drift is silent — the restored model differs from the built one in whatever the newer code added.
So **the skip path must call the stage's own writer, not a copy of it.** Stage 4 already works this way:
`RestoreSkippedUpdate` calls `UpdateContainers`, the same method a build goes through, which is why a renderer setting
added for a build reaches a restored model for free.

The same rule points outward. A script or tool that writes generated-object state directly — renderer flags, mesh
assignments, lightmap settings — is a copy of the writer by another name, and goes stale the moment the generator
changes its mind about that state. On 20 September an APV setup script that forced `ContributeGI` onto every renderer
container would have put the sky faces back into the bake and undone `b1f88ee` entirely. Set the input (the model's
flags), run the update, then *read back* what the generator decided.

## Prefix skipping, and why the node graph is not persisted

Stages 1 and 2 have no persisted output. So a mismatch at stage 3 drags them along anyway: the CSG needs a real tree
and real blobs. That is fine — it is a *prefix* skip, and the common case (nothing changed) skips the whole prefix.
It also means **persisting the node graph is a separate question that should not be answered first.** Skip it lazily,
measure, and only then decide whether anything is left worth serializing.

### Saving a model that has no tree

Found by reading, 22 September, before any of stage 1 was skipped. `StoreInputHashes` (`ChiselModelManager.cs:730`)
gates *both* hashes on the tree:

    var settled = model.Node.Valid && !model.Node.Dirty;
    model.generated.inputHash          = settled ? ... : default;
    model.generated.componentInputHash = settled ? ... : default;

A model that skips stage 1 has no tree, so `settled` is false, so saving the scene **wipes the hash of the one model
whose hash is known to be good**. The skip would work on the first reopen and never again — and it would look like the
hash was wrong rather than like it was erased, which is a day of chasing the wrong thing.

`settled` is there to stop a hash being written for a tree that is mid-edit, since then the meshes on disk would not be
what the hash describes. That reasoning does not reach a model that never built: its meshes on disk are exactly the
ones its stored hash describes, because nothing happened to them. So the third case has to be written down rather than
falling through to `default`:

    if (settled)                    store both, as now
    else if (CanSkipTreeUpdate(m))  keep what is already there
    else                            clear both

which puts the question back through the one function that already decides it, instead of restating the decision in
the save path — the same rule as *one writer per stage*, applied to the decision rather than to the output.

**Guard it before it can break.** "Save, reopen, save, reopen, and the model still keeps its meshes" passes today and
fails the moment stage 1 is skipped without the case above. Writing it now costs nothing and turns a silent regression
into a red test.

### The first cut is smaller than the choke point (read this before building the one below)

Reading the load path on 22 September changed the plan. `AddChildrenOfHierarchyItem` (`:1725-1742`) builds a child's
CSG node exactly when `childComponent.TopTreeNode` is invalid — so the hook for skipping stage 1 is one condition in
one loop, and it can be applied **to the generators without being applied to the model**:

- bm_c2a5a has **126 models and 3,685 generators**. The 379 ms is the generators; the models are noise.
- A model that keeps its own `CSGTree` keeps `model.Node` valid, so `CanSkipTreeUpdate`, `IsTreeUpdateSkipped`, the
  candidate dictionary and every existing test go on working **unchanged**.
- And the 65 call sites stop being the feasibility question. Only `ChiselGeneratorComponent.TopTreeNode` comes back
  invalid, and generators already return `CSGTreeNode.Invalid` when they have no node — that path exists and is taken
  today for a generator that has not been built yet.

So the two-accessor design below is for later, if ever. The first cut is: **do not build the generators of a model
whose stored hash still matches, and build them when something needs them.**

**The danger this creates, which must have a failing test of its own before any of it lands.** A model whose
generators were never built has a tree with NO CHILDREN. That is harmless while the hash matches, because nothing
runs the CSG. But if the model is then edited, the CSG would run on an empty tree and produce an *empty model* —
geometry silently deleted, which is far worse than the stale geometry a wrong hash gives. So the build-on-first-touch
path has to be airtight, and the test that matters is not "a skipped model keeps its meshes" but **"a skipped model
that is then edited comes back with all of its geometry"**.

The core already has this shape for stage 3: `UpdateSkippedTrees` builds a tree whose update was skipped once
something needs its CSG, and `SkippedTreeUpdateTests` covers it. Stage 1 needs the same thing one level up.

**Confirmed by measurement, 22 September** (`AModelThatKeepsItsMeshes_BuildsNoNodesAtAll`, 19 of 20 passing with that
one red): *"the generator's CSG node was built for a model that kept its meshes."* The work is real and the test that
states it fails for the stated reason. Its companion guard, `SavingAgain_KeepsTheHashOfAModelThatKeptItsMeshes`,
passes — so today's save path is what the description above says it is.

### How it hangs together

Four pieces, and the third is the one that carries the risk.

1. **One predicate, no tree.** `KeepsItsSavedMeshes(model)` — the stored component hash is valid, the generated
   objects are valid, and `GetComponentInputHash(model)` still equals what was stored. Pure, no mutation. Today that
   test is spelled out inside `CanSkipTreeUpdate`, which also *mutates* (it removes the candidate), so it cannot be
   reused. Extracting it is what lets the save path and the load path ask the same question — *one decider per stage*.
2. **Decide once per model, not once per child.** `OnTreeCreated` already runs exactly once per model and already
   registers the saved-output candidate, so that is where a model is recorded as not-building-its-subtree. It has to
   be there rather than in the child loop: `GetComponentInputHash` walks the model's whole subtree, so asking it per
   child would be O(N²) — the shape of the regression in [[csg-perf-regression-fixes]].
3. **Invalidate on the edit, never by re-hashing.** The tempting version re-checks the hash each update and builds the
   subtree when it stops matching. That costs a full subtree walk per model per update forever, to notice something
   the hierarchy manager is *already told about*: every edit goes through `RebuildTreeNodes`, `NotifyContentsModified`
   or `UpdateTreeNodeTransformation`. So a model leaves the unbuilt set when one of those names it, and the ordinary
   path builds its children. Event-driven, and it costs nothing when nothing changes — which is the whole point of
   the feature.
4. **Do not build nodes inside `CanSkipTreeUpdate`.** It is called by the core from inside `Flush`, and creating CSG
   nodes there moves CompactNodeIDs under a running update — the trap recorded in [[chisel-scene-load-stall]]. The
   subtree must already be built by the time `Flush` asks, which is what (3) guarantees: an edited model has left the
   set during the update phase, and its children were built there.

The failing-first test for (3) and (4) together is **"a model that skipped its nodes and is then changed comes back
whole"** — it passes today, because nothing skips yet, and it is the one that goes red if the invalidation is not
airtight. That is the test to write before the implementation, not after.

### The choke point

Skipping stage 1 means a model that keeps its meshes has **no tree at all** until something asks for one. Everything
that wants a tree must therefore go through one choke point that can build it first. Today there are two properties,
`ChiselModelComponent.Node` and `ChiselNodeComponent.TopTreeNode`, used in 65 places, all of them inside the package
(22 + 43, the editor ones spread over seven files under `Editor/`). Making both build on demand is the whole
feasibility question, and the count says it is tractable. `ChiselSceneQuery` already funnels queries through
`BuildModelsWithSavedOutputs`, and outlines are already made lazily in `GetOutline`, so the pattern exists.

The count is not the difficulty, though. **Two accessors are needed, and the risk is entirely in which sites get the
building one** — a site that only wants to know whether a tree exists will, given a building accessor, build one in
order to be told yes, and the skip evaporates with every model still behaving correctly. Sorting `.Node` by what the
caller actually wants:

| must **not** build | why |
|---|---|
| `ChiselModelManager.cs:730` | `StoreInputHashes` — the case above; building here would also make every save rebuild the map |
| `ChiselNodeHierarchyManager.cs:1490` | `destroyNodesList.Add(itemModel.Node)` — building a tree in order to destroy it |
| `ChiselModelManager.cs:78`, `:139` | lookups *keyed by* the tree (`model.Node == tree`, `s_SavedOutputCandidates[model.Node]`) |
| `ChiselModelManager.cs:691` | `GetInputHash`'s own `!model.Node.Valid` early-out |
| `ChiselDecalManager.cs:81` | `model.Node.Valid` as a precondition |
| `ChiselInternalHierarchyView.cs:444`, `ChiselManagedHierarchyView.cs:324` | debug views, which walk everything |

| must build | why |
|---|---|
| `ChiselGeneratorComponent.cs:475`, `:533` | a generator attaching itself under the model's tree |
| `ChiselCompositeComponent.cs:68` | a composite making its branch under the model's tree |
| `ChiselDecalManager.cs:110`–`:112` | decal sync compares and then writes through the tree |

The asymmetry is the useful part: the *building* list is short and is all "I am about to change this tree", while the
non-building list is longer and is all "is there one / which one is it". So the safe default for the property is **not
to build**, with the building accessor named for what it does and used deliberately — the opposite of the usual
convention, and the reason to write it down before anyone implements it from the call-site count alone.

## What is stored

`Hash128` per model for h₁, h₃ and h₄, and one per generator for h₂. On bm_c2a5a that is 126 × 3 + 3,811 hashes ≈
66 KB of a 92 MB scene — next to nothing, and far less than the 2.2 MB of picking data already there.

## Code identity has to be per stage too

The assembly `ModuleVersionId` is in every hash today (`ChiselModelManager.cs:674`). It is a correct safety net and far
too coarse to express staging: *any* edit to *any* file in core or components invalidates *everything*. That happened
twice on 20 September alone — a picking-serialization change (stage 4) and a renderer-layout change (stage 4) each
forced a full CSG rebuild of every map in the project.

So each stage carries its own version constant, bumped by whoever changes that stage's behaviour — the same discipline
as the generated-asset version stamps the importer already uses. The MVID stays available as a development-time net
(off by default, on when working on the hashes themselves), because with it on, nothing can be cached wrongly.

This is the one part of the design that trades safety for precision, and it is the reason staging is worth doing: with
per-stage stamps, 44's renderer change would have kept every map's CSG output and re-run only stage 4.

## Tests, failing first

1. **A model whose h₃ matches creates no tree nodes when its scene opens.** Fails today (126 trees and 3,811 nodes are
   created). This is the precise statement of the problem.
2. A model whose h₄ matches restores its renderers without re-running the CSG — exists today as
   `ChiselSavedOutputTests`.
3. **A change that only affects stage 4 keeps stage 3's output.** Change a material or a renderer setting, reopen,
   assert the CSG did not run and the renderers did change. Fails today.
4. A change to one generator rebuilds that generator's blob and its model, and leaves the other models' blobs alone.
5. Touching a model that skipped stage 1 builds its tree at that moment, and picking, selection and outlines work
   afterwards exactly as for a built model.

## What the first increment taught

- **The differential test earns its keep.** It found `01c9711` on its first run — a defect neither hash was written to
  look for, and one that had been in the product long enough for every existing box test to pass around it. Testing two
  answers against each other finds what testing either against an expectation does not.
- **Identity hashes are invisible.** `definition.GetHashCode()` looked like a content hash at every call site. Nothing
  in the type system distinguishes it from one, and the failure is silent and wrong rather than loud. The blast radius
  was narrow only by luck: the other half of the same check, `SurfaceDefinition.GetHashCode()`, *is* content-based, so
  surface edits were noticed and only geometry edits to a built generator were dropped.
- **A test that has to be told about a change is testing something else.** Unity's callbacks do not run in EditMode, so
  the fixture hands its changes to the tree itself (`UpdateTreeNodeTransformation`, `ClearHashes`,
  `UpdateGeneratorNodes`). The first run without that looked like five gaps in the component hash and was none.
- **The component hash cannot see the output, by construction.** It reads transforms, definitions, surfaces and model
  settings; it never touches `model.generated`. So a change to the renderer layout — 8 slots to 16, sky faces moved to
  a renderer of their own — is invisible to it. That is what makes it stage-3 input rather than stage-4 output, and it
  is the property that lets the question be asked before anything is built.

### What the two attempts settled (22 September)

**The first attempt replaced the unbuilt set with a pure predicate and failed six tests.** The reasoning was that a
set is bookkeeping, bookkeeping drifts, and drift here costs geometry rather than freshness. That is all true, and it
is still the wrong trade: a predicate can say *this model still matches its stored hash*; it cannot say **build it
anyway**, and "build it anyway" is what a rebuild, a scene query, a click and an edit all mean. The six failures were
five different consumers saying so.

**The second attempt is the set, with the drift closed by construction rather than by care.** `s_ModelsWithoutNodes`
IS the fact: while a model is in it, its tree has no children, and `CanSkipTreeUpdate` keeps the CSG off it for that
reason and no other. A model leaves the set in `AddChildrenOfHierarchyItem`, which is the same step that gives it
children, so "has no children" and "its CSG is skipped" cannot come apart. `s_ModelsToBuild` is how *build it anyway*
is spelled, and it deliberately does **not** take the model out of the first set — a model that has been asked for but
not yet built is still without children, so its CSG is skipped one more round instead of running on an empty tree.
`AModelAskedToBuild_KeepsItsMeshesUntilItsNodesExist` is that round, and it is the case the first attempt had no way to
express.

Four of the six went green, including both dangerous ones. `ForgetSavedOutputs` also had to move *before* the trees are
remade in `Rebuild()` and latch until the update finishes: it used to be called afterwards, which worked only while the
skip lived at Flush time, later still than anything it needed to affect.

**The two that remain are one fact, and it is the choke point this section said might never be needed.** Picking and
outlines fail with `NodeID is invalid`, and not because of where the tests reach: saved picking data is keyed by live
CSG node identity. `ChiselRenderObjects` says so in its own comment — *"EntityIds and CompactNodeIDs don't outlast the
session"* — which is why `RestoreSelection` exists, and what it does is walk each generator's `TopTreeNode` to find its
brushes again and rebuild the ids. A generator with no node has nothing to walk, so `BrushesOf` returns an empty list
and a model that kept its meshes has no picking data at all. Outlines are the same shape: `CSGTreeBrush.Outline` needs
the brush.

So the skip and the saved picking data are in direct conflict, and no amount of care in the hash layer resolves it.
What resolves it is that **both consumers are per-selection, not per-load**. 456 ms over 126 models is ~3.6 ms each, so
building one model's nodes when something picks in it or draws its outline is free at the scale it happens. Scene
queries are already through the door — `ChiselSceneQuery` calls `BuildModelsWithSavedOutputs` in four places, and that
is why `AQuery_BuildsTheModelsThatKeptTheirMeshes` passes. Selection restore and outline fetching have to go through
the same one.

That is the two-accessor choke point above, needed after all but far narrower than the 65 call sites made it look:
the building accessor is wanted at the places that resolve a *selection*, and nowhere else.

## Risks

- **A wrong h₁ or h₂ is silent.** A component field that is input but is not hashed means stale geometry with no error.
  The existing hash has the same exposure; staging widens it because more decisions hang off it. Mitigation: the
  development-time MVID net, and a test that hashes every serialized field of a generator definition by reflection and
  fails when a new field is not covered.
- **Lazy trees change when errors surface.** A malformed generator that throws while building its node will now throw
  on first interaction rather than on load. Better for load time, worse for "the map opened fine, then I clicked".
- **Editor code paths that read `TopTreeNode` in a loop** (the hierarchy views, the debug views) would build everything
  they touch. They need the non-building accessor, not the building one.

## See also

- [Chunking](Chunking.md) — chunk settings become part of h₃ when they exist, or a model whose chunking changed keeps
  its old meshes.
- The saved-output skip as it exists today: com.chisel `aff5072` (core), `66cbe99` (components), `93c0808` (picking
  data).

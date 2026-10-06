# Generate the CSG pipeline's actual shape from the scheduler, in schedule order.
#
# WHY THIS IS A SCRIPT, AGAIN. The exact-predicates plan was organised by an 11-stage map I invented and then
# used to classify everything in it. The real pipeline is ~36 nested #regions in CSGManager.UpdateTreeMeshes.cs,
# named by the author, each scheduling named jobs that DECLARE what they read and write. So the map does not have
# to be guessed: it can be read off the scheduler. Anything I write by hand here is a taxonomy; anything this
# prints is the pipeline.
#
# AND WHY IT CHECKS ITSELF. Same failure as before: a first pass that looks complete and is not. So it
#   - cross-checks the job-instantiation count against a second, independently written detector;
#   - reports every *Job type defined under Core/ that this scheduler never instantiates (scheduled elsewhere,
#     or dead) instead of quietly covering only what it found;
#   - reports jobs instantiated more than once (the indirect-update passes re-run several), because a map with
#     one row per type would silently merge them;
#   - refuses to write if the two detectors disagree.
import os, re, sys
from collections import OrderedDict, defaultdict

CHISEL = r"D:\Unity\Chisel.Dev\Packages\com.chisel"
CORE = os.path.join(CHISEL, "Core")
SCHED = os.path.join(CORE, "2.Processing", "Managers", "CSGManager.UpdateTreeMeshes.cs")
OUT = os.path.join(CHISEL, "Documentation~", "Design", "PipelineMap.md")

REGION = re.compile(r"^\s*#region\s+(.*?)\s*$")
ENDREGION = re.compile(r"^\s*#endregion")
# The generic arm matters: three of these are OutputCopyJob<T>, and the first version of this pattern
# required '{' or end-of-line straight after the name, so it silently skipped all three. The cross-check
# below is what caught that.
NEWJOB = re.compile(r"\bnew\s+(\w*Job\w*)\s*(?:<[^>{]*>)?\s*(?:$|\{)")
# Deliberately different and looser, purely to cross-check the count.
AUDIT = re.compile(r"=\s*new\s+(\w+Job)\b")
SECTION = re.compile(r"^\s*//\s*(Read\s*/\s*Write|Read-Write|ReadWrite|Read|Write|Write\s*/\s*Read)\s*$", re.I)
FIELD = re.compile(r"^\s*(\w+)\s*=\s*(.+?),?\s*$")
HANDLES = re.compile(r"JobHandles\.(Read|Write)\s*\(")


def read(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        return f.read().splitlines()


def block(lines, start, open_ch="{", close_ch="}"):
    """Return (end_index, inner_lines) for the brace block that opens at or after `start`."""
    depth, i, began = 0, start, False
    inner = []
    while i < len(lines):
        line = lines[i]
        stripped = line.split("//")[0]
        for ch in stripped:
            if ch == open_ch:
                depth += 1
                began = True
            elif ch == close_ch:
                depth -= 1
        if began:
            inner.append(line)
            if depth <= 0:
                return i, inner
        i += 1
    return len(lines) - 1, inner


def parse_initializer(inner):
    """Fields grouped by the // Read, // Write, // Read/Write comment headers inside a job initializer."""
    groups = OrderedDict()
    current = "unlabelled"
    for line in inner:
        m = SECTION.match(line)
        if m:
            label = re.sub(r"\s+", " ", m.group(1)).replace("ReadWrite", "Read / Write")
            label = label.replace("Read-Write", "Read / Write").replace("Write / Read", "Read / Write")
            current = label.title().replace("Read / Write", "Read / Write")
            groups.setdefault(current, [])
            continue
        if line.strip().startswith("//") or not line.strip():
            continue
        code = line.split("//")[0]
        m = FIELD.match(code)
        if m and "=" in code and "==" not in code:
            groups.setdefault(current, []).append((m.group(1), m.group(2).strip().rstrip(",")))
    return groups


def parse_handles(lines, start, stop):
    """The JobHandleType.* names inside JobHandles.Read(...) / JobHandles.Write(...) after a job initializer."""
    out = {"Read": [], "Write": []}
    i = start
    while i < stop:
        m = HANDLES.search(lines[i])
        if m:
            kind = m.group(1)
            depth = 0
            j = i
            began = False
            text = []
            while j < stop:
                seg = lines[j][m.end() - 1:] if j == i else lines[j]
                for ch in seg:
                    if ch == "(":
                        depth += 1
                        began = True
                    elif ch == ")":
                        depth -= 1
                text.append(seg)
                if began and depth <= 0:
                    break
                j += 1
            for name in re.findall(r"JobHandleType\.(\w+)", "\n".join(text)):
                if name not in out[kind]:
                    out[kind].append(name)
            i = j
        i += 1
    return out


lines = read(SCHED)

# Walk the file once, tracking the region stack, collecting job instantiations in order.
stack = []
entries = []   # (region_path, line_no, job_type, groups, handles)
found_lines = set()

i = 0
while i < len(lines):
    line = lines[i]
    m = REGION.match(line)
    if m:
        stack.append(m.group(1))
        i += 1
        continue
    if ENDREGION.match(line):
        if stack:
            stack.pop()
        i += 1
        continue
    m = NEWJOB.search(line.split("//")[0])
    if m:
        job = m.group(1) or m.group(2)
        # find the initializer block
        end, inner = block(lines, i)
        groups = parse_initializer(inner)
        # the Schedule call follows, up to the next 'new XJob' or the end of the enclosing using-block
        stop = min(end + 60, len(lines))
        handles = parse_handles(lines, end, stop)
        entries.append((" / ".join(stack), i + 1, job, groups, handles))
        found_lines.add(i + 1)
        i = end + 1
        continue
    i += 1

# cross-check against an independent detector
audit_lines = set()
for n, line in enumerate(lines, 1):
    if AUDIT.search(line.split("//")[0]):
        audit_lines.add(n)
missed = audit_lines - found_lines
if missed:
    print(f"THE TWO DETECTORS DISAGREE: {len(missed)} job instantiation(s) the walker missed:")
    for n in sorted(missed):
        print(f"   {os.path.basename(SCHED)}:{n}  {lines[n-1].strip()[:100]}")
    sys.exit("refusing to write a pipeline map that is already known to be incomplete")

# every *Job type defined under Core, so the map can report what it does NOT cover
defined = {}
for root, _, files in os.walk(CORE):
    if os.sep + "Tests" in root:
        continue
    for name in sorted(files):
        if not name.endswith(".cs"):
            continue
        path = os.path.join(root, name)
        for n, line in enumerate(read(path), 1):
            m = re.search(r"\b(?:public|internal|private)?\s*(?:unsafe\s+)?(?:readonly\s+)?struct\s+(\w*Job\w*)\b", line)
            if m and ":" in line or (m and "IJob" in line):
                defined.setdefault(m.group(1), (os.path.relpath(path, CORE).replace("\\", "/"), n))
            elif m:
                defined.setdefault(m.group(1), (os.path.relpath(path, CORE).replace("\\", "/"), n))

scheduled = defaultdict(list)
for region, line, job, _, _ in entries:
    scheduled[job].append((region, line))

never = sorted(set(defined) - set(scheduled))
unknown = sorted(set(scheduled) - set(defined))
repeated = {j: v for j, v in scheduled.items() if len(v) > 1}

report = []
report.append("# The CSG pipeline, in schedule order")
report.append("")
report.append("**Generated** by `scratchpad/pipeline_map.py` from `Core/2.Processing/Managers/"
              "CSGManager.UpdateTreeMeshes.cs`. Do not hand-edit — re-run it, and diff.")
report.append("")
report.append("The regions and their nesting are the author's, read off the scheduler. The Read/Write columns are "
              "the job's own declarations. Nothing here is a taxonomy imposed on the code: an earlier attempt at "
              "this map invented eleven stages and then classified everything by them, which made the "
              "classification fiction even where the counts were right.")
report.append("")
report.append(f"**{len(entries)} job instantiations** of **{len(scheduled)} distinct job types**, across "
              f"**{len(set(r for r, _, _, _, _ in entries))} regions**.")
report.append("")

if repeated:
    report.append("## Scheduled more than once")
    report.append("")
    report.append("A map with one row per job type would merge these. The indirect-update passes deliberately "
                  "re-run earlier jobs over a different brush set, so the same code decides twice on different data.")
    report.append("")
    for job, places in sorted(repeated.items()):
        where = "; ".join(f"line {l} ({r})" for r, l in places)
        report.append(f"- `{job}` — {len(places)}×: {where}")
    report.append("")

if never:
    report.append("## Job types defined under Core/ that this scheduler never instantiates")
    report.append("")
    report.append("Each is scheduled somewhere else, or is dead. Reported rather than dropped, because the point "
                  "of this file is to say what it does not cover.")
    report.append("")
    for job in never:
        path, n = defined[job]
        report.append(f"- `{job}` — {path}:{n}")
    report.append("")

if unknown:
    report.append("## Instantiated here but not found as a struct under Core/")
    report.append("")
    for job in unknown:
        report.append(f"- `{job}`")
    report.append("")

# ---------------------------------------------------------------------------------------------------------
# The input stage. It is NOT scheduled from CSGManager.UpdateTreeMeshes.cs, so the walker above cannot see it:
# GeneratorJobPool dispatches through the IGeneratorJobPool interface, and the explicit-BrushMesh path is
# entered from the Components layer. The ordering below is read from GeneratorJobPool.ScheduleJobs and
# BrushMeshManager.ConvertBrushMeshesToBrushMeshInstances. It is hand-written, so it is CHECKED: every job
# instantiation in those two files must appear here by line number, or the script refuses to write.
INPUT_FILES = ["1.Input/GeneratorBase/GeneratorJobPool.cs", "1.Input/BrushMesh/BrushMeshManager.Internal.cs"]
POOL = "1.Input/GeneratorBase/GeneratorJobPool.cs"
BMM = "1.Input/BrushMesh/BrushMeshManager.Internal.cs"

INPUT_PATHS = [
    ("Path A - generators (box, cylinder, stairs, ...): GeneratorJobPool.ScheduleJobs", [
        ("per pool: ScheduleGenerateJob - brush pools", [(POOL, 586, "CreateBrushesJob<Generator>")]),
        ("per pool: ScheduleGenerateJob - branch pools", [(POOL, 857, "BranchPrepareAndCountBrushesJob<Generator>"),
                                                         (POOL, 865, "BranchAllocateBrushesJob<Generator>"),
                                                         (POOL, 873, "BranchCreateBrushesJob<Generator>")]),
        ("per pool: ScheduleUpdateHierarchyJob", [(POOL, 605, "UpdateHierarchyJob (brush pool)"),
                                                  (POOL, 961, "UpdateHierarchyJob (branch pool)")]),
        ("allocate the shared output lists", [(POOL, 145, "ResizeTempListsJob")]),
        ("SYNC POINT: allocateJobHandle.Complete() - GeneratorJobPool.cs:158, marked 'TODO: get rid of this'", []),
        ("per pool: ScheduleInitializeArraysJob", [(POOL, 621, "InitializeArraysJob (brush pool)"),
                                                   (POOL, 1067, "InitializeArraysJob (branch pool)")]),
        ("BrushMeshManager.ScheduleBrushRegistration", [(BMM, 854, "RegisterBrushMeshesJob")]),
        ("ScheduleAssignMeshesJob", [(POOL, 326, "HierarchySortJob"), (POOL, 329, "AssignMeshesJob")]),
    ]),
    ("Path B - explicit BrushMesh (ChiselBrushComponent, the VMF importer, any hand-built brush): "
     "ChiselNodeHierarchyManager.cs:436 -> BrushMeshManager.ConvertBrushMeshesToBrushMeshInstances", [
        ("convert managed BrushMesh to blob", [(BMM, 366, "ConvertToBrushMeshBlobJob (Schedule + immediate Complete)")]),
        ("register (synchronous, not a job)", []),
    ]),
]

listed = {(f, l) for _, phases in INPUT_PATHS for _, jobs in phases for f, l, _ in jobs}
actual = set()
for rel in INPUT_FILES:
    path = os.path.join(CORE, *rel.split("/"))
    for n, line in enumerate(read(path), 1):
        if NEWJOB.search(line.split("//")[0]):
            actual.add((rel, n))
missing = actual - listed
stale = listed - actual
if missing or stale:
    for rel, n in sorted(missing):
        print(f"INPUT STAGE NOT ACCOUNTED FOR: {rel}:{n}")
    for rel, n in sorted(stale):
        print(f"INPUT STAGE LISTED BUT NO LONGER THERE: {rel}:{n}")
    sys.exit("refusing to write a pipeline map whose input stage does not match the source")

report.append("## The input stage — where brushes and their planes are born")
report.append("")
report.append("Not scheduled from `CSGManager.UpdateTreeMeshes.cs`, so it is absent from the generated walk below. "
              "`GeneratorJobPool` dispatches through `IGeneratorJobPool`, and the explicit-BrushMesh path is entered "
              "from the Components layer. This section is hand-written from the two orchestrating functions and "
              "checked against them: every job instantiation in those files must appear here or the script refuses "
              "to write.")
report.append("")
report.append("**There are two input paths, and they are not equivalent.** Path B is the one a hand-built brush and "
              "the VMF importer take.")
report.append("")
for title, phases in INPUT_PATHS:
    report.append(f"### {title}")
    report.append("")
    for phase, jobs in phases:
        if jobs:
            report.append(f"- **{phase}**")
            for f, l, job in jobs:
                report.append(f"  - `{job}` — {f}:{l}")
        else:
            report.append(f"- **{phase}**")
    report.append("")

report.append("### Where a plane comes from")
report.append("")
report.append("Traced for Path B, because that is the path exact input planes would arrive on — and the path the "
              "VMF importer uses: `VmfWorldConverter.cs:312` and `:502` call `BrushMeshFactory.CreateFromPlanes` "
              "with planes it computed from each side's three integer points.")
report.append("")
report.append("**There is exactly one place the exact planes are lost.** That is worth stating precisely, because "
              "the loose version of this claim - 'planes get recomputed all over the place' - is wrong in a way "
              "that matters: `CalculatePlanes()` has 21 live call sites, but the ones on this path are guarded. "
              "`BrushMesh.Validate` recomputes only `if (planes == null)` (`BrushMesh.Validate.cs:14`), and "
              "`ValidateShape` only when the count disagrees (`:174`). So the exact planes survive validation "
              "intact. They die at a single boundary, and that makes the repair small and local rather than "
              "systemic.")
report.append("")
report.append("1. `BrushFactory.CreateFromPlanes` cuts the brush with the caller's planes, then "
              "`AssignCuttingPlanes` (`BrushFactory.Utility.cs:1131`) writes those **exact** planes back onto "
              "`brushMesh.planes`, with a comment explaining that re-deriving them is wrong.")
report.append("2. `ConvertBrushMeshesToBrushMeshInstances` packs each brush into `BrushMeshPointers` "
              "(`BrushMeshManager.Internal.cs:210`), which has fields for `vertices`, `polygons` and `halfEdges` "
              "and **no field for planes**. The exact planes cannot cross into the job; they are dropped here.")
report.append("3. `ConvertToBrushMeshBlobJob` welds the vertices (`:453`), then re-fits every plane from the "
              "**welded** vertices with its own `CalculatePlane` (`:412`): Newell in double, `math.normalize`, "
              "and `d` set to the **average** of `-dot(normal, vertex)` over the polygon's vertices. That is a "
              "fit, not a plane through the geometry, so the plane does not pass exactly through any of its own "
              "vertices.")
report.append("4. The non-job route `ConvertToBrushMeshBlob` (`:120`) reaches the same end differently: it calls "
              "`brushMesh.CalculatePlanes()` (`:132`) unconditionally, overwriting whatever was there, and then "
              "copies the result.")
report.append("5. `CreateBrushTreeSpacePlanesJob` (`2.Processing/Jobs/CreateBrushTreeSpacePlanesJob.cs:45`) "
              "multiplies by the inverse-transpose of the brush transform and **normalizes again** "
              "(`treePlane /= math.length(treePlane.xyz)`).")
report.append("")
report.append("So between a caller's exact plane and the predicate that consumes it there are two re-derivations "
              "and two normalizations, and the re-derivation is per-brush, from that brush's own vertex set.")
report.append("")
report.append("Stated precisely, because the convenient version is wrong: two brushes meeting on one geometric "
              "plane get the same `float4` only when their Newell sums and their `d` averages agree bitwise. For "
              "an axis-aligned face that generally holds — every vertex shares the coordinate being averaged, and "
              "the normal is exact — which is why axis-aligned test scenes behave. It stops holding when the "
              "vertices are not exactly coplanar (any rotated brush, after rounding), when the two sides carry "
              "different vertex counts, or when the shared face is split differently on each side. So plane "
              "identity across brushes is **incidental** — it is a property of the input happening to be nice, not "
              "something the representation guarantees.")
report.append("")
report.append("That is what a plan starting 'intern the planes so one plane has one representation' has to answer "
              "first. At the point interning would happen the exact planes no longer exist, and the planes that do "
              "exist are per-brush fits whose agreement is a coincidence of the geometry.")
report.append("")
report.append("Both paths also weld at input, through `HashedVertices.AddNoResize` with no weld filter "
              "(`:453` and `:164`).")
report.append("")
report.append("### Machinery for plane/vertex consistency that nothing calls")
report.append("")
report.append("`BrushMesh.Optimize.cs` contains most of what a plane-exact representation would need, and these "
              "have no non-test callers:")
report.append("")
report.append("- `SnapPolygonVerticesToItsPlanes` (`:28`) — would make the fit exact by moving vertices onto their "
              "own planes. Dead.")
report.append("- `GetVertexFromIntersectingPlanes` (`:40`) — derives a vertex from the planes meeting at it, which "
              "is the plane-based vertex identity idea. Dead.")
report.append("- `CenterAndSnapPlanes` (`:120`) — dead, and worth knowing because the comment justifying "
              "`AssignCuttingPlanes` (`BrushFactory.Utility.cs:1125`) cites it as the reason exact planes must be "
              "preserved. The justification outlived the function.")
report.append("- `InvertWhenInsideOut` (`:386`) — dead.")
report.append("")
report.append("`SplitNonPlanarPolygons` (`:430`) is the one that runs, but only through "
              "`ChiselBrushDefinition.EnsurePlanarPolygons`, whose only callers are "
              "`Editor/ComponentEditors/Generators/ChiselBrushEditor.cs:53` and `:94`. So polygons are made planar "
              "when a human edits a brush in the inspector, and never on the import path. Imported geometry "
              "reaches the plane fit with whatever non-planarity it has.")
report.append("")

report.append("## Machinery that already exists, and is switched off")
report.append("")
report.append("See also **[DeadMachinery.md](DeadMachinery.md)**, generated by `dead_machinery.py`: the same "
              "pattern — the careful implementation exists and nothing calls it — turned up four separate times "
              "on this walk, so the rest are enumerated there rather than found one at a time. It reports 11 "
              "methods with no production caller that tests still exercise (the dangerous class: the suite reads "
              "green over code the pipeline cannot run) and 47 with no caller at all. It is a regex, not a "
              "compiler, so those are candidates; it was validated by checking that it independently finds all "
              "six cases found by hand here, and its first version was corrected after it wrongly called "
              "`CSGManager.SkipUnchangedTrees`' API dead by searching only `Core/` for callers.")
report.append("")
report.append("Found while walking `Determine Intersection Surfaces`. This matters more than any single defect "
              "below, because the exact-predicates plan proposed **building** most of it. Four toggles in "
              "`CSGManager.UpdateTreeMeshes.cs`, all off, several carrying measurements:")
report.append("")
report.append("- **`kInternBrushPlanes`** (`:178`) — `InternBrushPlanesJob` gives every face a shared integer id "
              "for the plane it lies on, so two brushes meeting on one wall hold the same id instead of two "
              "`float4`s for an epsilon to reconcile. Built, serial by design, off.")
report.append("- **`kUsePlaneIdsForAlignment`** (`:185`) — `FindAlignedPlanesByID` decides face alignment by "
              "comparing those ids. Its own comment: *\"No epsilon is consulted here at all — it was spent once, "
              "when the planes were interned.\"* Measured against the current path: **they disagree on ~0.13% of "
              "face pairs, and where they do the id answer is the better one.**")
report.append("- **`kUseIncidenceWeld`** (`:193`) — gates every positional weld on plane incidence, so a weld may "
              "only merge two vertices when each stays on the faces the other lies on. Measured on `bm_c0a0a`: of "
              "34,354 real welds it refuses **13,106**, and those sit on **106 of 107** T-junction and 339 of 429 "
              "other uncovered-boundary stretches. Its own note records that `CreateBlobPolygonsBlobsJob` is not "
              "gated, because it runs before the tree-space planes exist (371 welds).")
report.append("- **`kCanonicalVertexStage`** (`:199`) — canonical vertices in five stages, 0 = shipped behaviour "
              "through 4 = vertex identity used in the merge, the CSG re-weld and triangulation.")
report.append("")
report.append("So the question this plan should have opened with is not *how would we build plane identity*. It is "
              "**why is the plane identity that exists switched off, and what does turning it on cost** — a "
              "question with measurements already attached. "
              "[CanonicalVertices.md](CanonicalVertices.md) and the plane-representation notes record that the "
              "payoffs were falsified on the real map, which is evidence about those specific payoffs, not a "
              "verdict on the representation.")
report.append("")
report.append("One caution carried over from the section above: what `InternBrushPlanesJob` interns is the "
              "**fitted** plane — `localPlanes[p]` from `ConvertToBrushMeshBlobJob`, transformed and normalized "
              "(`:97`–`:104`). That is why `InternedPlanes.Matches` needs `kMaxNormalDeviation` and "
              "`kPlaneDAlignEpsilon` at all. Interning is tolerance-based *because* the planes it interns are "
              "per-brush refits; carrying the authored planes across `BrushMeshPointers` is what would let the "
              "match be exact.")
report.append("")
report.append("**Do not trust the comments here.** `InternBrushPlanesJob:15` says \"Nothing consumes these ids "
              "yet\" and `CSGManager.UpdateTreeMeshes.cs:252` repeats it. Both are stale: "
              "`PrepareBrushPairIntersectionsJob` reads `brushPlaneIds` and `brushPlaneIdRange` at `:39`–`:40` and "
              "`:263`–`:268`.")
report.append("")

report.append("## The shape of the whole thing")
report.append("")
report.append("One sentence, after walking every geometric stage: **the exact machinery is not missing — it is "
              "deployed downstream as damage control instead of upstream as the decision.**")
report.append("")
report.append("Every decision that determines topology is made with float geometry:")
report.append("")
report.append("- which vertices are the same — a float distance, resolved to a non-nearest candidate "
              "(`HashedVerticesUtility`);")
report.append("- which plane a face lies on — a per-brush least-squares refit, compared with an epsilon that "
              "does not transform;")
report.append("- what order a loop's vertices go in — `math.atan2` about a float centroid;")
report.append("- whether an edge survives a merge — a midpoint against a volume, known to be wrong exactly when "
              "the edge straddles;")
report.append("- where two loops cross — five inline float copies of a double helper nothing calls.")
report.append("")
report.append("And then a substantial, careful, **exact** layer cleans up afterwards. `LoopEdgeSplitter` holds "
              "six repairs and five of them contain no floating point at all — `RemoveAntiparallelEdgePairs`, "
              "`RemoveSimpleChords`, `RewindClosedLoop`, `RemoveTinyComponents` and `CloseSingleOpenChain` are "
              "union-find, degree counting and index walks. Only `SplitEdgesAtVertices`, which inserts "
              "T-junction vertices and genuinely needs geometry, uses floats. `CreateRoutingTableJob` — the "
              "boolean logic itself — is likewise entirely combinatorial.")
report.append("")
report.append("Counted across the pipeline there are roughly **fifteen** such guards and repairs:")
report.append("")
report.append("| where | mechanisms |")
report.append("|---|---|")
report.append("| holes, `PerformCSGJob:149`–`:164` | `RemoveAntiparallelEdgePairs`, `RemoveSimpleChords`, "
              "`CloseSingleOpenChain`, `RewindClosedLoop` |")
report.append("| base loops, `CleanUp` | `kKeepEdgesRestingOnTheOtherLoopsBoundary`, "
              "`kKeepReverseAlignedBaseEdges`, `RemoveAntiparallelEdgePairs` twice (pre- and post-merge), "
              "`RemoveSimpleChords`, `CloseSingleOpenChain` |")
report.append("| triangulation | collinear collapse + its `protectedVertices` exception list, "
              "`RemoveTinyComponents` (`MeshAlgorithms:130`), `RepairBoundary`, `CheckForSelfIntersections` / "
              "`RemoveSelfIntersectingEdges`, `IsDegenerate`, and the whole fallback retry |")
report.append("")
report.append("Two of them undo each other (the collinear collapse deletes the T-junction vertices "
              "`SplitEdgesAtVertices` inserted, hence the exception list). One repairs deleting too little and "
              "the next repairs deleting too much. `RemoveAntiparallelEdgePairs` runs up to three times on one "
              "surface. `CloseSingleOpenChain` and `RepairBoundary` both re-close open loops, one in 3D and one "
              "in 2D.")
report.append("")
report.append("None of that is careless work — each repair is defensively written, refuses ambiguous input, and "
              "several carry measurements. But fifteen exact repairs downstream of five float decisions is a "
              "description of where the exactness went, and `BooleanEdgesUtility`'s own comment says what to do "
              "instead: split the edge at the boundary, so the decision is never wrong, rather than classify it "
              "wrongly and repair the damage.")
report.append("")

report.append("## The answer already exists in this repository")
report.append("")
report.append("The decal subsystem (`Core/2.Processing/Decals/`, 1502 lines) does the same kind of work — clip "
              "convex polygons against a volume, triangulate the result — and does it the way the rest of this "
              "document keeps arguing for. It was written later, by the same author, and it is the cheapest "
              "available reference for what a repair would look like.")
report.append("")
report.append("| concern | the CSG core | the decal subsystem |")
report.append("|---|---|---|")
report.append("| precision | `float3` positions, float predicates, one unused `double` helper per operation | "
              "`double3` throughout — positions, barycentrics, areas |")
report.append("| 2D projection | `Map3DTo2D`: cross products against a tie-broken reference axis, and the axes "
              "are **not normalized**, so 2D distances scale per surface | drop the dominant axis (chosen by "
              "`math.abs`), keep the other two in cyclic order, mirror if the normal points down it — "
              "`Project` (`:678`) is a coordinate copy and a sign flip, **no arithmetic at all**, so the 2D "
              "coordinates are bit-exact and the winding is preserved by construction |")
report.append("| clipping | classify against planes, then repair the misclassifications | parametric "
              "`enter`/`exit` interval clipping (`GetEdgePoints:168`) |")
report.append("| attributes | recomputed downstream | barycentrics carried on the vertex and interpolated by the "
              "same `t` as the position (`DecalClipVertex.Lerp:14`) |")
report.append("| triangulation | hand a bag of edges to a constrained triangulator and repair on failure | "
              "`TriangulateRing:495` constructs the triangles directly between the two polygons, **adding no "
              "points**, with explicit winding checks at each step |")
report.append("| failure | silently repaired by ~15 mechanisms | a `DecalSplitFailure` enum — `TooManyPoints`, "
              "`RingTurnedOver`, `TooManyTriangles`, `AreaMismatch` — returned to the caller. It **checks its own "
              "output area against its input** and reports a mismatch rather than shipping it |")
report.append("| tolerances | 57 declarations, 36 distinct names, 24 inline literals | **two**: "
              "`kPlaneEpsilon = 1e-4` and `kMinArea = 1e-10`, both `double`, in 1502 lines |")
report.append("")
report.append("The projection row is the sharpest of these. `Map3DTo2D` builds a basis with cross products "
              "against a reference axis picked by comparison, and `CSGMath.Orient2D`'s own warning block says the "
              "resulting non-normalized axes can make 2D coordinates large enough to break the collinearity "
              "test. Dropping a coordinate instead costs nothing, loses nothing, and removes the tie-break "
              "entirely — and the code for it is already in this package.")
report.append("")
report.append("This is not an argument that the decal code is flawless, and it solves an easier problem: its "
              "inputs are convex and it never has to merge a base loop with holes. But it is evidence that the "
              "approach this document keeps pointing at is practical here, in this codebase, in Burst, on doubles "
              "— rather than something to be taken on faith from a paper.")
report.append("")

report.append("## Defects and hazards found while walking this pipeline")
report.append("")
report.append("Kept here rather than in chat so a later pass does not rediscover them. Each says **how it was "
              "established**, because 'I read it' and 'I ran it' are not the same evidence and this pipeline has "
              "already cost several days to claims that were only ever the first one.")
report.append("")

FINDINGS = [
    ("The vertex weld does not return the nearest vertex",
     "`Core/2.Processing/Containers/HashedVertices.cs`",
     "read, NOT yet proven by a running test",
     "`closestVertexIndex` is declared outside the 27-cell search loop (`:47`) and `closestDistance` inside it "
     "(`:53`), so the distance threshold resets on every cell while the winning index persists. A candidate at "
     "0.010 in a later cell therefore overwrites a candidate at 0.001 found in an earlier one: the function "
     "returns the closest vertex in the LAST cell that held any, not the closest overall. All seven entry points "
     "share the structure - `SnapToExistingVertex`, both `ReplaceIfExists`, all three `AddNoResize`, `Add` - and "
     "the instance methods delegate straight into them, so every live weld path is affected: `BrushMeshManager` "
     "at input, `CreateBlobPolygonsBlobsJob`, `CreateIntersectionLoopsJob`, `FindLoopOverlapIntersectionJob`, "
     "`PerformCSGJob`, `MergeTouchingBrushVerticesIndirectJob`, `GenerateSurfaceTrianglesJob`. "
     "The output stays within tolerance, so this does not show up as a wrong number; it shows up as "
     "INCONSISTENCY - two brushes querying the same seam point visit cells in a different order and can resolve "
     "to different vertices, which is a crack. Fix is one line per copy (hoist the declaration). Needs a "
     "container test first: two existing vertices straddling a cell boundary, both inside the weld ball, assert "
     "the nearer one wins."),

    ("Planes are normalized on the predicate path, twice",
     "`CreateBrushTreeSpacePlanesJob.cs:46`, `InternedPlanes.cs:137`, `PerformCSGJob.cs:538`",
     "read",
     "`treePlane /= math.length(treePlane.xyz)` runs for every plane of every brush as the pipeline's first act "
     "on a plane. Predicate SIGNS are invariant under positive scaling, so this buys nothing for any sign "
     "decision and costs the property that matters: it is a `sqrt` and a divide, so one geometric plane reached "
     "through two transforms becomes two different `float4`s."),

    ("The 2D basis for triangulation is per-brush and tie-broken",
     "`GenerateSurfaceTrianglesJob.cs:685`, `MeshAlgorithms.cs:19`",
     "read; not measured firing",
     "`Map3DTo2D` is constructed from `math.mul(nodeToTreeInvTrans, plane)` - the brush's OWN local plane through "
     "its OWN transform, not a shared or interned plane - and its constructor picks projection axes by comparing "
     "`|dot(normal, axis)|`. Near a tie, one geometric plane can get two different 2D frames. `MeshAlgorithms` "
     "then tests collinearity with `|Orient2D| < 1e-12` on coordinates whose scale depends on those "
     "non-normalized axes, which is exactly the failure the warning block in `CSGMath.Orient2D` describes."),

    ("Edge categorisation answers a 2D question from a 3D volume",
     "`PerformCSGJob.cs:292` / `:304`, and the comment at `:382`",
     "read; the code comments it itself",
     "`CategorizeEdge` decides Inside/Outside from the intersecting brush's 3D volume, while the question being "
     "asked is whether an edge lies in the 2D region the loop bounds on this plane. The job's own comment says "
     "so, and records that 94.6% of cuts producing a non-simple hole keep at least one edge because of it. This "
     "is a design mismatch, not a tolerance that is set wrong."),

    ("`MergeTouchingBrushVerticesJob` is dead",
     "`Core/2.Processing/Jobs/MergeTouchingBrushVerticesJob.cs:15`",
     "verified - no references anywhere in the package",
     "Only `MergeTouchingBrushVerticesIndirectJob` (`:77`) is scheduled, from the 'Merge vertices' region. Worth "
     "knowing before reasoning about the weld stage from the wrong job, which is a mistake already made once."),

    ("Saved output is keyed on the input only — nothing identifies the algorithm that made it",
     "`CSGManager.SkipUnchangedTrees.cs:24`, `:73`; `ChiselModelManager.CanSkipTreeUpdate:143`",
     "read; the consequence follows from the mechanism, not yet observed",
     "`GetTreeInputHash` hashes everything the CSG builds the output **from**: node operations, contents, "
     "transformations, brush meshes and surfaces in hierarchy order, the decals, the model settings, the "
     "contents count. It is careful and, as far as input data goes, it looks complete. What it contains nothing "
     "of is **which version of the CSG produced the stored mesh**.\n\n"
     "The version constant beside it does not close that gap — its rule is *\"Bump when GetTreeInputHash hashes "
     "something else, or the same things differently\"* (`:24`). That tracks the hash function, not the "
     "producer. So changing what the CSG *does* leaves every stored mesh matching its key and therefore "
     "'valid'.\n\n"
     "Stated precisely, because the alarming version is wrong: the skip is one-shot per load — "
     "`CanSkipTreeUpdate` consumes the model from `s_SavedOutputCandidates`, and after the first change a model "
     "builds all of itself. A script that dirties every model in a running session is **not** affected. What is "
     "affected is a freshly opened scene, and a reload after a code or toggle change.\n\n"
     "Two consequences worth having in hand before any of the repairs in this document are attempted:\n\n"
     "1. **Fix the weld, reload `bm_c2a5a`, and the saved meshes hide the fix.** Every unchanged model keeps its "
     "old geometry and the CSG never runs. That looks exactly like 'the fix did not work'.\n"
     "2. The A/B toggles are defeated in the same way across a reload. `kUseIncidenceWeld` and "
     "`kCanonicalVertexStage` are deliberately not `readonly`, with comments saying *\"a script can A/B one "
     "rebuild\"* — but their values are not in the hash, so an A/B done over a scene reload compares saved "
     "output against saved output. Any past measurement taken that way is worth re-checking before it is "
     "trusted; that is a question about the method, not a claim that any particular number is wrong.\n\n"
     "The importer side of this project already solved the same problem: generated assets carry a "
     "`chisel-<generator>-v<N>` stamp so bumping it rebuilds old outputs. CSG output has no equivalent."),

    ("The weld defect, traced to the job that actually welds",
     "`MergeTouchingBrushVerticesIndirectJob:183`–`:208` into `HashedVertices.cs:824` / `:838`",
     "read end to end; the consequence is reasoned, not yet run",
     "The live weld is `MergeTouchingBrushVerticesIndirectJob` (the non-Indirect one is dead). For each brush it "
     "snaps that brush's vertices onto its touching neighbours' — but only neighbours with a **lower node "
     "order** (`:186`, `:200`). That asymmetry is correct and deliberate: imposing a total order is how a "
     "snapping pass is made convergent instead of chasing its own tail, and the fixpoint gate at `:135` is "
     "derived from this brush's own reads so it does not assume the touch graph is symmetric. Careful code.\n\n"
     "It reaches the vertices through `ReplaceIfExists(..., min, max)`, whose two bounded overloads (`:814`, "
     "`:828`) cull by a box padded with `kCellSize` — exact, since the pad exceeds the weld radius — and then "
     "delegate straight to `HashedVerticesUtility.ReplaceIfExists`. Which is the second copy of the defect "
     "recorded above: `closestDistance` resets on every one of the 27 cells while `closestVertexIndex` persists, "
     "so the snap target is the closest candidate in the **last cell that held any**, not the closest overall.\n\n"
     "So the chain is complete: the weld is documented and intended to snap each vertex to its nearest anchor, "
     "the total order makes which anchor wins deterministic, and the lookup then picks a non-nearest one whose "
     "identity depends on where the query sits relative to the cell grid. Two vertices that should resolve to "
     "the same anchor can resolve to different ones. That is a seam crack, produced with every tolerance set "
     "correctly.\n\n"
     "Also worth knowing: the output depends on tree node order by construction. Reordering siblings, or adding "
     "a brush that shifts node orders, can move welded vertices by up to the weld radius. Not a defect — it is "
     "the tie-break that makes the fixpoint terminate — but it means 'the same scene' is only the same geometry "
     "while the hierarchy order is the same."),

    ("A known geometry-destroying failure is described in the code, and its guard is off",
     "`MergeTouchingBrushVerticesIndirectJob:150`, `CSGManager.UpdateTreeMeshes.cs:193`",
     "read",
     "The comment above the weld filter: *\"A snap may pull a vertex onto a neighbour's, but not off the faces "
     "of its own brush: that is what collapsed a 19 mm slab onto a neighbour vertex lying between its two "
     "faces.\"* A specific, observed, geometry-destroying failure, with the fix built — `WeldIncidenceFilter`, "
     "which permits a merge only when each vertex stays on the faces the other lies on. "
     "`useIncidenceWeld` comes from `kUseIncidenceWeld`, which is **false**. So the failure the comment "
     "describes is currently unguarded, by choice, and the measurement in the toggle's own comment says the "
     "guard would refuse 13,106 of 34,354 welds — which is why it is off, and also why turning it on is a "
     "question about what those 13,106 were doing rather than a switch to flip."),

    ("The pipeline already knows exactly when its core decision is wrong, and does not act on it",
     "`BooleanEdgesUtility.cs:144`, `:189`, and `PerformCSGJob.cs:579`",
     "read, and every caller counted",
     "`BooleanEdgesUtility` states the theorem twice, in its own comments: *\"For a convex plane set the midpoint "
     "test in CategorizeEdge is only WRONG when the edge straddles the boundary (one endpoint inside the planes, "
     "the other outside) — i.e. it crossed the boundary and should have been split there\"* (`:144`), and again "
     "at `:189`: *\"This is the case `FindBasePolygonPlaneIntersections` should have split at the boundary face; "
     "if it didn't, the edge reaches `CleanUp` straddling and the midpoint test misclassifies it.\"*\n\n"
     "That is a complete characterisation of the defect, an upstream cause, and a bound on it — the straddle is "
     "the **only** case. Three predicates implement it. None of them decides anything:\n\n"
     "| predicate | production callers | tests | effect on output |\n"
     "|---|---|---|---|\n"
     "| `EdgeStraddlesSegmentPlanes` (`:149`) | 0 | 4 | none — nothing calls it |\n"
     "| `EdgeStrictlyCrossesSegmentPlanes` (`:195`) | 2 | 4 | none — both sites are gated by "
     "`kLogStrictCrossing`, which is `false` (`PerformCSGJob.cs:579`); it feeds a log |\n"
     "| `EdgeRestsOnSegmentPlanes` (`:163`) | 2 | **0** | the only one that changes output — it is the "
     "`kKeepEdgesRestingOnTheOtherLoopsBoundary` guard |\n\n"
     "The coverage is inverted. The two predicates that cannot affect a single triangle have eight tests between "
     "them; the one that decides whether `b1114ba`'s rule fires — the rule the pad regression is about — has "
     "none.\n\n"
     "**This is the cheapest real measurement left in the pipeline.** Setting `kLogStrictCrossing = true` for one "
     "rebuild of `bm_c2a5a` reports every edge whose midpoint verdict is a true unsplit crossing, on real data, "
     "with code that already exists. It answers directly whether the pad's lost edge is a straddle, and it needs "
     "no new instrumentation — only the editor.\n\n"
     "It also reframes the repair. If the straddle really is the only way the midpoint test goes wrong for a "
     "convex plane set, then the fix is not exact predicates at all: it is either splitting the edge at the "
     "boundary upstream, where `FindBasePolygonPlaneIntersections` was supposed to, or refusing to destroy an "
     "edge that straddles. Both are local, and both are testable against `FlushContactFaceTests` and "
     "`SourceConcretePadTests` together, which is the bar `b1114ba` failed."),

    ("CategorizeEdge tests an edge against its own loop's plane",
     "`BooleanEdgesUtility.CategorizeEdge:139`",
     "read; the TODO is the author's",
     "Directly above the midpoint test: `// TODO: shouldn't be testing against our own plane`. The plane set the "
     "midpoint is tested against includes the plane of the surface the loop lies on, against which every point "
     "of the loop is on-plane and therefore within the fat band by construction. Small, but it is one more term "
     "in a decision that is already known to be wrong in a specific way."),

    ("NOT what its name says: AreLoopsOverlapping tests edge-set equality",
     "`BooleanEdgesUtility.AreLoopsOverlapping:233`",
     "read; one production caller",
     "It returns false unless the two loops have the **same edge count** and every edge of the first appears in "
     "the second — that is set equality (up to direction), not overlap. Two loops that genuinely overlap on part "
     "of their area answer `false`. The name says otherwise, and a reader checking an overlap decision would "
     "believe it. Recorded as a naming hazard rather than a defect: with one caller, whether it is a bug depends "
     "on what that caller wants, which is worth reading before anyone 'fixes' the function."),

    ("A truncated update is indistinguishable from a converged one",
     "`CSGManager.UpdateTreeMeshes.cs:134` and `:965`",
     "read both the loop and the guard; not yet observed firing",
     "An incremental update is a fixed point. After each round, brushes whose welding / T-junction inputs were "
     "left stale are dirtied and the tree runs again, *\"until nothing moves\"*. The comment at `:72` claims this "
     "*\"is what makes an incremental update end in exactly the geometry a full rebuild produces\"* — which holds "
     "only if it converges inside `kMaxPropagationRounds = 8`.\n\n"
     "When it does not, the failure is silent in the strongest sense. The dirtying block at `:965` is guarded by "
     "`if (s_PropagationRound + 1 < kMaxPropagationRounds && ...)`, so on the last permitted round the whole "
     "block is skipped: the stale brushes are never counted, `staleCount` stays 0, `s_StaleBrushCount` is not "
     "incremented, and `s_PropagationRequested` is never set. The `while` at `:134` then exits because "
     "`s_PropagationRequested` is false — the same way a genuinely converged run exits.\n\n"
     "So a truncated update reports **exactly** what a converged one reports: no stale brushes, no propagation "
     "requested, no log line, no warning. The code that would have recorded the outstanding work is the code "
     "that was skipped. The only residual signal is `LastUpdateRounds == 8`, and that is also what a run that "
     "converged on round 8 reports.\n\n"
     "This is the shape recorded in the `false-green-guards` note: when the pass condition is 'nothing changed', "
     "an instrument that is not looking passes it perfectly. It also matches the reported symptom of geometry "
     "that *\"disappeared suddenly\"* — output that depends on update history rather than on the scene.\n\n"
     "Cheap to make visible without changing behaviour: count `staleLoopBrushes.Length` unconditionally and only "
     "skip the `SetDirty` calls, so `LastUpdateStaleBrushCount` reports what was abandoned. Worth contrasting "
     "with the sibling bound `kMergeIterations = 30`, which is justified by a measured convergence tail in its "
     "own comment (a 5700-brush map settling at pass 17) and whose truncation *is* recovered, because a brush "
     "still moving gets its neighbours re-run next round. One bound is measured and self-correcting; the other "
     "is asserted and terminal."),

    ("Ten repair mechanisms stand between a merged loop and a triangle",
     "`PerformCSGJob.CleanUp` and `GenerateSurfaceTrianglesJob:707`–`:808`",
     "read end to end, both jobs",
     "Continuing the entry below into the triangulation stage. `CleanUp` contributes five; "
     "`GenerateSurfaceTrianglesJob` adds five more before it will accept a loop:\n\n"
     "6. `ConvertToPlaneSpace` (`:710`) collapses collinear points — and needs a `protectedVertices` list, "
     "because that collapse would otherwise delete the cross-brush T-junction vertices the same job inserted a "
     "few hundred lines earlier to close seams. One mechanism actively undoing another, with an exception list "
     "to keep the peace.\n"
     "7. `RepairBoundary` (`:716`, `kCloseOpenChains2D`, **on**) — a **second** open-chain repair, in 2D. "
     "`CloseSingleOpenChain` already did this in 3D in `CleanUp`.\n"
     "8. `CheckForSelfIntersections` / `RemoveSelfIntersectingEdges` (`:718`, `:726`) — removing the "
     "self-intersections the `atan2` ordering and the concatenation produce.\n"
     "9. `IsDegenerate` (`:739`) pre-check.\n"
     "10. and on failure, a **fallback retry** (`:765`–`:807`): `RemoveAntiparallelEdgePairs` once more, then 6–9 "
     "again, then triangulate a second time.\n\n"
     "`RemoveAntiparallelEdgePairs` therefore runs up to **three times** on one surface — `PerformCSGJob:786` "
     "pre-merge, `:1134` post-merge, and here in the retry. The comment justifying the third (`:771`) says "
     "`CleanUp` *\"only stripped from the pre-hole-merge base loop\"*, which `:1134` contradicts: it strips "
     "post-merge and says so. Third stale comment found on this walk.\n\n"
     "For the pad this narrows things usefully. `zeroTriangles=1` is only reachable at `:802` `if (!recovered)`, "
     "so every one of 6–10 ran and the surface still produced nothing — and it is **not** a `TriangulatorError`, "
     "which is a separate reason logged at `:761`. The triangulator accepted the input and found no area to "
     "fill, which is what an open boundary looks like to a constrained triangulation."),

    ("The hole merge is destroy-then-repair, with five mechanisms layered on it",
     "`PerformCSGJob.CleanUp`, `:1090`–`:1150`",
     "read end to end",
     "This answers 'why do we even delete edges?' with the machinery that exists to undo the deleting.\n\n"
     "The merge does not construct a merged boundary. It **concatenates**: for each hole, "
     "`AddEdgesNoResize(ref baseLoopEdges, in holeEdges)` (`:1127`) tips that hole's edges into the base loop's "
     "list — one flat bag, explicitly allowed to contain duplicates (`:1125`), carrying an unexplained "
     "`// TODO: why is baseLoopEdges sometimes not properly allocated?` capacity workaround at `:1121`. Merging "
     "is then defined as *removing* the edges another loop took over, decided by `CategorizeEdge` — an edge's "
     "midpoint against the other brush's **planes**, a volume test standing in for a question about a polygon.\n\n"
     "Because that is wrong in known ways, five mechanisms sit on top of it:\n\n"
     "1. `kKeepEdgesRestingOnTheOtherLoopsBoundary` (`:662`, **on**) — suppress destruction when the Inside "
     "verdict is only a fat-band touch. Hole-vs-hole only.\n"
     "2. `kKeepReverseAlignedBaseEdges` (`:599`, **off**) — the same idea for ReverseAligned base edges.\n"
     "3. `RemoveAntiparallelEdgePairs` (`:1134`) — strips reverse-edge slits that, by its own comment, "
     "*\"were not present on either loop alone\"*: damage the concatenation itself introduced.\n"
     "4. `RemoveSimpleChords` (`:1140`, **on**) — drops interior chords *\"the destroyedEdges categorization "
     "under-deleted\"*.\n"
     "5. `CloseSingleOpenChain` (`:1145`, **on**) — re-closes a loop the destruction **over-deleted**.\n\n"
     "Mechanisms 4 and 5 are worth reading together: one repairs deleting too little, the other repairs deleting "
     "too much, and both run every time because neither the test nor its failure mode is understood well enough "
     "to avoid either.\n\n"
     "**And 5 is strictly limited.** `LoopEdgeSplitter.CloseSingleOpenChain` (`:323`–`:360`) builds a "
     "degree/union-find pass and refuses unless the edge set is a **single connected component** with **exactly "
     "two degree-1 endpoints** and every other vertex degree-2. So it fixes exactly one missing edge, in one "
     "clean chain. Two unreplaced deletions, or damage leaving any vertex of degree>2, or two components, and it "
     "gives up — the loop stays open, the triangulator returns only closed loops, and the surface is dropped as "
     "`ZeroTrianglesReturned`.\n\n"
     "That yields a concrete, testable hypothesis for the pad (Solid 4838), whose measured drop reason is "
     "exactly `zeroTriangles=1`: its merge loses more than one edge, or leaves a chord or a second component, so "
     "the one repair that would have closed it refuses. Checking it does not need new instrumentation — it needs "
     "the degree histogram of `baseLoopEdges` at `:1144` for that surface."),

    ("Nothing validates a brush's shape before the CSG sees it",
     "`BrushMesh.Validate.cs:233`, `BrushMesh.Optimize.cs:313`/`:319`, `ChiselBoxDefinitions.cs:61`, "
     "`ChiselCylinderDefinition.cs:286`, `ChiselExtrudedShapeDefinition.cs:93`",
     "read every validator; then ran Chisel's own rules offline over the whole sample scene",
     "`ValidateShape` is three checks, and two of them are stubs: `IsSelfIntersecting()` is `return false;` under "
     "a TODO, and `HasVolume()` only checks that the arrays are non-empty — its own TODO says it never asks whether "
     "the brush is 1D or flat. Only `IsConcave()` does geometry. The generators are more permissive still: "
     "`ChiselBox.Validate()` swaps Min and Max and returns `true` (a zero dimension raises an inspector *warning*, "
     "\"not allowed\", and the flat box proceeds); `ChiselCylinder.Validate()` takes `abs()` of the diameters, clamps "
     "`sides` to 3 and returns `true`, never looking at height; `ChiselExtrudedShape.Validate()` is `return true;`. "
     "So a flat, collapsed or self-intersecting brush reaches the CSG with nothing having objected.\n\n"
     "Measured rather than assumed, on the package's sample scene, by mirroring those rules exactly offline "
     "(`validate_brushes.py`, `validate_generators.py`; same constants, same Newell-plus-average plane fit) and adding "
     "the checks the stubs skip: **all 353 are valid.** The 121 explicit brushes pass Chisel's rules *and* the "
     "missing ones — positive volume, planar faces, every vertex inside every plane, no duplicate vertices — and the "
     "232 generators' parameters have no zero dimension, flat cylinder, self-intersecting outline or collapsed "
     "path. So whatever that scene gets wrong, it is not a bad definition: it is the pipeline's handling of good "
     "ones, which only running the CSG can show.\n\n"
     "The one systematic oddity is handedness: 52 of the 353 sit under a mirrored world transform, mostly through "
     "mirrored Composites. That is handled deliberately in the two places read so far — `CreateIntersectionLoopsJob` "
     "tests `math.determinant(...) < 0` and flips the normal its `atan2` sort uses (`:1162`, `:772`), apparently so "
     "intersection loops reverse together with the base loops `CreateBlobPolygonsBlobsJob` builds straight from the "
     "mesh's half-edge order; and output facing comes from the plane normal via `Map3DTo2D`, which the "
     "inverse-transpose keeps pointing outward under a mirror. Whether it is handled *everywhere* is not something "
     "reading settles, and this entry says so rather than calling it a defect."),

    ("GOOD NEWS: the boolean logic itself is exact",
     "`CreateRoutingTableJob.cs`",
     "read the whole job",
     "The job that turns the CSG tree into per-brush routing consults **no geometry at all** — no positions, no "
     "planes, no distances, no tolerances. Its only `math.` calls are `min`/`max` on node ids, and it decides "
     "everything from `CSGOperationType` and integer table lookups. So the boolean semantics are combinatorial "
     "and exact, and every robustness problem in this pipeline lives in the stages that decide *which category a "
     "piece of geometry falls into*, not in what the operations then do with those categories. That is a useful "
     "boundary: it says where work is worth spending and where it is not."),

    ("Two more guessed capacities, unguarded, in the routing table",
     "`CreateRoutingTableJob.cs:54` and `:77`",
     "read; reachability not demonstrated",
     "Same family as the overflow that crashed a real session (`Length 8211 exceeds Capacity 8192` in "
     "`CreateIntersectionLoopsJob`, fixed by counting instead of guessing). Two remain here:\n\n"
     "- `queuedEvents` is `new NativeArray<QueuedEvent>(4096)` — a bare literal. Five sites write "
     "`queuedEvents[queuedEventCount++]` (`:247`, `:335`, `:346`, `:358`, `:371`) and **none** checks the bound. "
     "The loop at `:324` pushes one event per sibling that touches the brush being processed, so the count "
     "scales with touching-sibling count and nesting.\n"
     "- `maxRoutes = maxNodes * kMaxRoutesPerNode`, where `kMaxRoutesPerNode = 32` carries "
     "*\"TODO: figure out the actual possible theoretical maximum\"* — the comment states outright that nobody "
     "knows the bound. It sizes five arrays (`tempStackArray`, `combineUsedIndices`, `combineIndexRemap`, "
     "`routingSteps`, `routingTable`), written through `outputLength++` at `:277`, `:283`, `:292`, `:301`, "
     "`:463` and `routingSteps[routingStepsLength]` at `:498`, again with no check.\n\n"
     "Worth noting these fail *worse* than the one already fixed. That was a `NativeList.AddNoResize`, which "
     "throws a clear exception. These are `NativeArray` indexed writes: with collection checks on they throw "
     "`IndexOutOfRange`, and in a Burst build with checks off they are silent out-of-bounds writes. "
     "Reachability is NOT established here — the fix pattern from `CountIntersectionLoopsJob` applies (derive "
     "the count instead of guessing it), but the first step is a test that drives the count up and shows where "
     "it lands. `outputSurfaceVertices` (`65535 * 10`, `// TODO: find actual vertex count`) is a third of the "
     "same family, still untouched."),

    ("The centralized predicates are mostly unused, and their tests hide it",
     "`Core/2.Processing/CSGMath.cs`",
     "counted, after correcting the probe",
     "`CSGMath` exists to hold the robustness-critical operations in one place, in double. Production callers: "
     "`SignedDistance` 6, `NewellTerm` 1, `Orient2D` 1. **Dead in production: `SqrDistance`, `VerticesEqual`, "
     "`EdgePlaneCrossing`, `PlaneIntersection`** — each used only by its own tests. "
     "(`DifferenceOfProducts`, `TwoSum` and `TwoProduct` first looked dead too; they are reached from `Orient2D` "
     "by unqualified intra-class calls that a grep for `CSGMath.` cannot see. Corrected rather than reported.) "
     "The whole compensated-arithmetic apparatus therefore serves exactly one call site, "
     "`MeshAlgorithms.Orientation`. And because the dead functions have passing tests, the module reads as "
     "healthy and covered — green tests over code nothing calls."),

    ("The double-precision edge/plane crossing is dead; five float copies of it are live",
     "`CSGMath.EdgePlaneCrossing:35` vs `FindLoopOverlapIntersectionJob:652`, `:660`, `:810`, `:818`, "
     "`BrushMesh.Utility.cs:624`",
     "verified - zero production callers of the helper, five inline duplicates",
     "`CSGMath.EdgePlaneCrossing` computes an edge/plane crossing with the delta and the lerp in **double**, and "
     "keeps the 'always interpolate from the positive side' branch whose stated purpose is cross-edge "
     "consistency. Nothing calls it. The four sites in `FindLoopOverlapIntersectionJob` reimplement it inline in "
     "**float**, carrying the same positive-side comment at `:647`. Those four are where one brush's loop is "
     "split against another brush's planes — precisely the place where two loops must agree on a shared point or "
     "the seam cracks. The centralized double version was written for this and is not used by it."),

    ("The weld distance is computed in float, beside an unused double version",
     "`HashedVertices.cs:57` vs `CSGMath.SqrDistance:24` / `VerticesEqual:28`",
     "read",
     "The weld compares `math.lengthsq(verticesPtr[chainIndex] - vertex)` — `float3` subtraction, `float` "
     "length-squared — against a `double closestDistance`. So the threshold is double and the quantity it judges "
     "is float. `CSGMath.SqrDistance` casts both to `double3` first and `VerticesEqual` wraps it against "
     "`kSqrVertexEqualEpsilon`, documented as matching the `HashedVertices` weld test. Neither is called."),

    ("The author has already written down the fix, three times, in TODOs",
     "`FindLoopOverlapIntersectionJob:349`–`:350`, and `:678` / `:834` / `:927`",
     "read",
     "At the site where one loop is split against another brush: *\"TODO: merge these so that intersections will "
     "be identical on both loops (without using math, use logic)\"* and *\"TODO: make sure that intersections "
     "between loops will be identical on OTHER brushes (without using math, use logic)\"*. Each loop currently "
     "computes its own crossing independently in float, so two loops that must share a split point are not "
     "guaranteed to get the same one — which is the crack mechanism, stated by the author. "
     "And three times over: *\"TODO: store two end planes for each edge instead (the planes whose intersections "
     "with the infinite edge create the vertices)\"* — plane-based vertex identity, noted at each of the three "
     "places that need it."),

    ("A loop's topology is decided by an atan2 angular sort",
     "`CreateIntersectionLoopsJob.SortIndices:103`",
     "read; the author's own TODO sits on the line above it",
     "The vertices of an intersection loop are ordered by `math.atan2` about the polygon's centroid, and that "
     "order IS the polygon's edge sequence — its topology. The TODO reads *\"sort by using plane information "
     "instead of unreliable floating point math\"*. Two vertices at nearly equal angle can order either way, and a "
     "wrong order produces a self-intersecting loop, which is exactly the non-simple-loop family that "
     "`PerformCSGJob` then cannot subtract and `GenerateSurfaceTrianglesJob` drops as "
     "`ZeroTrianglesReturned`. The centroid itself is a float sum over the loop's vertices (`:97`), so the sort "
     "key depends on accumulation order as well. This is topology derived from geometry, and it is the most "
     "likely origin of a missing surface that no epsilon will fix."),

    ("The inside-brush test consults synthesized bevel planes, not just real faces",
     "`CreateIntersectionLoopsJob.IsOutsidePlanes:67`, fed from "
     "`PrepareBrushPairIntersectionsJob:85`–`:86`",
     "read, and confirmed at the length that is passed in",
     "`ConvertToBrushMeshBlobJob` appends one extra plane per half-edge, `normalize(plane1.xyz + plane2.xyz)` "
     "through that edge's vertex, to stop vertices being accepted at very sharp angles. "
     "`PrepareBrushPairIntersectionsJob` then keeps two lengths: `intersectingPlaneLength = localPlaneCount` (the "
     "real faces) and `intersectingPlanesAndEdgesLength = localPlanes.Length` (faces **plus** those bevels). "
     "`IsOutsidePlanes` is called with the second. So whether a vertex counts as inside a brush is decided partly "
     "by planes that are an average of two neighbours and correspond to no face of the brush. A legitimate vertex "
     "on a sharp edge can be rejected by one."),

    ("Why the fat band has to be as wide as it is",
     "`kFatPlaneWidthEpsilon`, consumed by `IsOutsidePlanes:78`",
     "reasoning from the two findings above; not measured",
     "`IsOutsidePlanes` asks `dot(plane, vertex) <= kFatPlaneWidthEpsilon` against a **fitted** plane — one whose "
     "`d` is the average of `-dot(normal, vertex)` over the polygon, so it need not pass through any of its own "
     "vertices. The band therefore has to be wide enough to absorb the fit residual, on top of everything else it "
     "absorbs. That is a concrete reason to expect carrying authored planes across `BrushMeshPointers` to let the "
     "band shrink, and a concrete thing to measure before believing it: the distribution of "
     "`dot(fitted plane, its own vertices)` over a real map."),

    ("NOT a bug: SortIndices picks its tangent basis with signed comparisons",
     "`CreateIntersectionLoopsJob.SortIndices:110`–`:131`",
     "checked, all four branches",
     "It branches on `normal.x > normal.y` and `normal.y > normal.z` — **signed**, where `Map3DTo2D` and "
     "`MeshAlgorithms.LightmapAxes` both use magnitudes, so it reads like the usual dominant-axis bug. It is not. "
     "Enumerating the four branches against the reference axis each one picks: A `(x>y && x>z)` takes `(0,1,0)`, "
     "B `(x>y && x<=z)` takes `(0,0,1)`, C `(x<=y && y>z)` takes `(1,0,0)`, D `(x<=y && y<=z)` takes `(0,1,0)`. "
     "For each, a normal parallel to that reference fails the branch's own condition — e.g. branch A would need "
     "`x > y` with `normal ≈ (0,±1,0)`. So it never selects a reference parallel to the normal and the cross "
     "product never degenerates. Recorded because it looks wrong and 'fixing' it would change vertex ordering "
     "across every loop in the pipeline for no defect."),

    ("NOT a bug: the IsOutsidePlanes SIMD loop bound is off by one group",
     "`CreateIntersectionLoopsJob.IsOutsidePlanes:70`",
     "checked by tracing both loops",
     "The vectorised loop runs `for (; n + 4 < planesLength; n += 4)` while reading `planes[n+0..n+3]`, which "
     "needs only `n + 4 <= planesLength`. The bound is conservative by exactly one group, so with 8 planes the "
     "SIMD loop handles 0–3 and the scalar loop handles 4–7. Every plane is still tested. It costs a little "
     "throughput and tests nothing wrongly."),

    ("Coplanarity depends on brush scale",
     "`PrepareBrushPairIntersectionsJob.FindAlignedPlanes:390`, with the space set at `:539`",
     "read, and confirmed at the source of the transform",
     "Whether two faces count as lying on one plane is decided by `math.abs(localPlane1.w - localPlane2.w) < "
     "kPlaneWAlignEpsilon`. The two brushes' planes are compared in **brush 0's local space** — `:539` builds "
     "`node1ToNode0 = math.mul(transformations0.treeToNode, transformations1.nodeToTree)` and pushes brush 1's "
     "planes through it — so that `w` difference is a distance in brush 0's local units, while the epsilon is a "
     "fixed number that does not transform with it. Scale a brush and you scale the effective tolerance. The same "
     "geometry therefore aligns or fails to align depending on the transform it is authored under, which is a "
     "direct problem for hand-built levels where scaling a brush is ordinary. The toggle comment at "
     "`CSGManager.UpdateTreeMeshes.cs:181` says the same thing and is the reason `kUsePlaneIdsForAlignment` "
     "exists."),

    ("NOT a bug: HashedVertices truncates where InternedPlanes floors",
     "`HashedVertices.cs:29` and 6 more, vs `InternedPlanes.cs:152`",
     "checked, and it holds",
     "`InternedPlanes.CellOf` uses `math.floor` and comments (`:150`) that truncation makes a double-width cell "
     "straddling the origin. `HashedVertices` truncates via `(int)` in seven places, which does exactly that. But "
     "the cell size is 0.03125 and the weld radius 0.0125, so any two vertices within the weld ball still land "
     "in the same or an adjacent cell and the 27-cell search covers them. It doubles the occupancy of the origin "
     "cell and contradicts a comment explaining why it is wrong; it does not lose welds. Recorded so the next "
     "pass does not spend time re-deriving that."),

    ("A corpus of 26 minimal failing scenes, harvested from the sample scene",
     "`Core/Tests/Contents/SampleSceneExtraFaceTests.cs` (20), `SampleSceneHoleTests.cs` (6); harvester "
     "`ContentsHarvest.HarvestSites`",
     "ran: the real CSG over each sample-scene model's dumped tree, judged by the independent oracle; every case "
     "shrunk, re-verified, and watched FAIL in the editor (all 26 on the oracle assertion, none on an exception)",
     "Every model of the package's sample scene was replayed through the contents harness and every disagreement "
     "with the oracle shrunk, per failing spot, to the fewest brushes that still fail there. Model3 agrees "
     "everywhere; Model1 gives 9 distinct minimal scenes, Model2 7, Model4 10. **Every one is 3 or 4 brushes with "
     "no composite left, and every failing point lies on a plane another brush shares** (nearest other plane 0): "
     "the whole corpus is coplanar contact.\n\n"
     "What it says beyond the individual cases:\n\n"
     "- **Subtraction is not needed.** 10 of the 26 are unions of additive brushes only - Model2 F6 is three "
     "additive boxes, and one of them loses a face over 1390 sample points. Whatever goes wrong at a coplanar "
     "contact already goes wrong in the plain additive case, which every hand-built level is full of.\n"
     "- **Two directions of failure at the same kind of place.** 20 are EXTRA faces (drawn where both sides are "
     "filled or both empty, typically one brush's face drawn across its coplanar neighbour's) and 6 are HOLES (a "
     "face the oracle requires, drawn nowhere). A hole can be a whole face: Model1 F1 loses the entire top of a "
     "26 x 26 floor (1079 samples) once one subtractive and one small additive box touch it.\n"
     "- **Sander's report is Model2 F2, F5 and F7**: `Extruded Shape (Additive)` drawn at y = -1 and y = -2 "
     "where the oracle expects nothing, with `Extruded Shape (Remove)` and the boxes it is flush with. F2 did not "
     "reproduce from any neighbourhood of its site (0.5, 2 or 8 units around the failing points, or the face + 1) "
     "and had to be shrunk from the whole 174-brush model, yet it ends at 4 brushes. So at least one of those "
     "four sits away from the failing spot and still decides it - a long-range dependency that a repair looking "
     "only locally would miss.\n"
     "- Model4's ten are one configuration ten times: a row of identical subtractive boxes whose bottoms lie on "
     "the top of a pair of additive boxes, and each cutter shows the additive's face where nothing belongs. Kept "
     "as ten tests because each sits at different coordinates, which is where float decisions differ.\n\n"
     "How to use it: run category `CSGCorpus` (red on purpose) against any change to routing, loops, welding, the "
     "hole merge or triangulation, so a fix for one case is judged against all of them. Re-harvesting is cheap - "
     "about 380 CSG runs in 83 s for all four models - by calling `SampleSceneHarvestTests.Harvest` from "
     "`script_evaluate` in an empty scene (the harness refuses to flush with a model open, and the test runner "
     "would save or reload the open scene).\n\n"
     "What it cannot see: brushes are rebuilt from their planes (`CreateFromPlanes`), not from the generators' "
     "own vertices; only points away from crossing planes (5 cm band) are judged; and it runs fresh CSG, so it "
     "says nothing about stale saved output. No CSG run threw, and none of the known crash signatures appeared."),
]

for title, where, how, detail in FINDINGS:
    report.append(f"### {title}")
    report.append("")
    report.append(f"- **Where:** {where}")
    report.append(f"- **How established:** {how}")
    report.append("")
    report.append(detail)
    report.append("")

report.append("## The pipeline")
report.append("")
last_region = None
for region, line, job, groups, handles in entries:
    if region != last_region:
        report.append("")
        report.append(f"### {region}")
        report.append("")
        last_region = region
    report.append(f"#### `{job}` — scheduler line {line}")
    report.append("")
    for label, fields in groups.items():
        if not fields:
            continue
        names = ", ".join(f"`{k}`" for k, _ in fields)
        report.append(f"- **{label}:** {names}")
    if handles["Read"] or handles["Write"]:
        if handles["Read"]:
            report.append("- *declared handle reads:* " + ", ".join(f"`{h}`" for h in handles["Read"]))
        if handles["Write"]:
            report.append("- *declared handle writes:* " + ", ".join(f"`{h}`" for h in handles["Write"]))
    report.append("")

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(report) + "\n")

print(f"{len(entries)} job instantiations, {len(scheduled)} distinct types, "
      f"{len(set(r for r, _, _, _, _ in entries))} regions")
if repeated:
    print(f"  {len(repeated)} job type(s) scheduled more than once")
if never:
    print(f"  {len(never)} *Job type(s) under Core/ NOT scheduled by this file:")
    for job in never:
        print(f"     {job}  ({defined[job][0]}:{defined[job][1]})")
if unknown:
    print(f"  {len(unknown)} instantiated but not found as a struct: {', '.join(unknown)}")
print("wrote " + OUT)

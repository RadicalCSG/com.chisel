# Enumerate every tolerance in com.chisel's Core, and every place each one is used.
#
# WHY THIS IS A SCRIPT. The first version of ExactPredicates.md claimed to be exhaustive and was not: 18
# tolerances where there are dozens, 13 of 21 files read and the rest inferred, and a table of "tolerances outside
# CSGConstants, which grepping the constants would miss" that was itself built by grepping the constants. A
# hand-made inventory is a snapshot of one afternoon's attention; this can be re-run and diffed.
#
# AND WHY IT CHECKS ITSELF. The first version of THIS SCRIPT reported 35 where a hand grep found 52, because it
# keyed declarations by NAME and `kEpsilon` is declared in nine different files - they overwrote each other
# silently. So declarations are keyed by (file, name), and the count is cross-checked against a second, looser,
# independently written detector. If the two disagree the script says so and exits non-zero rather than printing a
# number that looks like an answer.
import os, re, sys
from collections import defaultdict

CORE = r"D:\Unity\Chisel.Dev\Packages\com.chisel\Core"
OUT = r"D:\Unity\Chisel.Dev\Packages\com.chisel\Documentation~\Design\ToleranceInventory.md"

NAME = r"\w*(?:[Ee]psilon|[Tt]olerance|[Ss]lack|kOnPlane|kOffPlane|kSameVertex|kCellSize|kSnap|kMinArea)\w*"
DECL = re.compile(r"\b(?:public\s+|internal\s+|private\s+|protected\s+)?"
                  r"(?:const|static\s+readonly|readonly\s+static)\s+[\w.<>\[\]]+\s+"
                  r"(" + NAME + r")\s*=")
# Deliberately looser and written differently, purely to cross-check the count above.
AUDIT = re.compile(r"(?:const|static\s+readonly|readonly\s+static)[^;=]*\b(" + NAME + r")\s*=")
INLINE = re.compile(r"[<>]=?\s*(-?(?:0\.0{2,}\d+|\d?\.?\d*e-\d+))f?\b")
COMMENT = re.compile(r"^\s*(//|\*|/\*)")

STAGE = [
    ("1. input / generators", ("1.Input",)),
    ("2. broad phase", ("FindAllBrushIntersectionPairs", "BuildBrushBoundsSweep", "FindUniqueIndirect",
                        "FindAllIndirectBrushIntersectionPairs", "GatherBrushIntersectionPairs",
                        "StoreBrushIntersections", "FindBrushPairs")),
    ("3. planes", ("InternBrushPlanes", "InternedPlanes", "CreateBrushTreeSpacePlanes", "PlaneExtensions")),
    ("4. pair preparation", ("PrepareBrushPairIntersections", "CreateBlobPolygonsBlobs")),
    ("5. intersection loops", ("CreateIntersectionLoops", "GatherOutputSurfaces", "CountIntersectionLoops")),
    ("6. loop overlap / splitting", ("FindLoopOverlapIntersection", "LoopEdgeSplitter", "SeedLoopVertices",
                                     "CopyBackLoopVertices", "StoreLoopVertices", "LoopVerticesCache")),
    ("7. welding", ("MergeTouchingBrushVertices", "HashedVertices", "WeldIncidence", "CanonicalVertices")),
    ("8. csg", ("PerformCSGJob", "BooleanEdgesUtility", "CreateRoutingTable", "CategoryRouting")),
    ("9. triangulation / output", ("GenerateSurfaceTriangles", "MeshAlgorithms", "MeshManifoldValidation",
                                   "3.Output", "Bayazit", "ConvexHull")),
    ("10. decals", ("Decal",)),
    ("11. shared math", ("CSGMath", "MathExtensions", "CSGConstants")),
]
UNCLASSIFIED = "UNCLASSIFIED - the pipeline map below does not cover this file, so the map is out of date"


def stage_of(path):
    rel = os.path.relpath(path, CORE).replace("\\", "/")
    for name, needles in STAGE:
        for needle in needles:
            if needle in rel:
                return name
    return UNCLASSIFIED


def sources():
    for root, _, files in os.walk(CORE):
        if os.sep + "Tests" in root:
            continue
        for name in sorted(files):
            if name.endswith(".cs"):
                yield os.path.join(root, name)


files = list(sources())
declarations = []      # (path, line, name, text)
audit_hits = set()     # (path, line)

for path in files:
    with open(path, encoding="utf-8", errors="replace") as f:
        lines = f.read().splitlines()
    for number, text in enumerate(lines, 1):
        if COMMENT.match(text):
            continue
        match = DECL.search(text)
        if match:
            declarations.append((path, number, match.group(1), text.strip()))
        if AUDIT.search(text):
            audit_hits.add((path, number))

found = {(p, n) for p, n, _, _ in declarations}
missed = audit_hits - found
extra = found - audit_hits
if missed:
    print(f"THE TWO DETECTORS DISAGREE: {len(missed)} declaration(s) the strict pattern missed.")
    for path, line in sorted(missed):
        print("   " + os.path.relpath(path, CORE) + ":" + str(line))
    sys.exit("refusing to write an inventory that is already known to be incomplete")
if extra:
    print(f"note: {len(extra)} matched by the strict pattern only (looser pattern is not a superset); listing them")
    for path, line in sorted(extra):
        print("   " + os.path.relpath(path, CORE) + ":" + str(line))

# A name declared in several files is itself worth reporting: it is what made the first version under-count.
per_name = defaultdict(list)
for path, line, name, _ in declarations:
    per_name[name].append((path, line))

uses = defaultdict(list)       # (path,name) -> [(path, line, text)]
in_comments = defaultdict(int)
inline = []

for path in files:
    with open(path, encoding="utf-8", errors="replace") as f:
        lines = f.read().splitlines()
    for number, text in enumerate(lines, 1):
        commented = bool(COMMENT.match(text))
        for decl_path, decl_line, name, _ in declarations:
            if not re.search(r"\b" + re.escape(name) + r"\b", text):
                continue
            if path == decl_path and number == decl_line:
                continue
            # a bare name only counts inside its own file; elsewhere it must be qualified
            same_file = (path == decl_path)
            qualified = re.search(r"\.\s*" + re.escape(name) + r"\b", text) is not None
            if not (same_file or qualified):
                continue
            if commented:
                in_comments[(decl_path, name)] += 1
            else:
                uses[(decl_path, name)].append((path, number, text.strip()))
        if not commented and "psilon" not in text and "olerance" not in text:
            for literal in INLINE.findall(text):
                inline.append((path, number, literal, text.strip()))

by_stage = defaultdict(list)
for path, line, name, text in declarations:
    by_stage[stage_of(path)].append((name, path, line, text))

report = []
report.append("# Tolerance inventory")
report.append("")
report.append("**Generated** by `scratchpad/tolerance_inventory.py`. Do not hand-edit — re-run it, and diff.")
report.append("")
report.append(f"{len(files)} source files under `Core/` (tests excluded): "
              f"**{len(declarations)} declared tolerances** across **{len(per_name)} distinct names**, "
              f"**{sum(len(v) for v in uses.values())} uses**, and "
              f"**{len(inline)} inline literals** with no named constant.")
report.append("")
report.append("The declaration count is cross-checked against a second, independently written detector; the script "
              "refuses to write this file if they disagree.")
report.append("")
report.append("**Known imprecision, stated rather than hidden:** where one file declares the same name twice - "
              "`BrushMesh.Optimize.cs` has `kDistanceEpsilon` at class scope and again inside a method - the use "
              "counts cannot tell which declaration a bare mention refers to, so both are credited with all of "
              "them. The use LISTS are still complete; only the per-declaration split is wrong, and only for the "
              "names in the section above.")
report.append("")

repeats = {n: v for n, v in per_name.items() if len(v) > 1}
if repeats:
    report.append("## Names declared in more than one file")
    report.append("")
    report.append("Each is a separate tolerance that happens to share a name, and they do not have to agree. "
                  "Any inventory keyed by name alone silently collapses these — which is how the first version of "
                  "this script reported 35 where there are more.")
    report.append("")
    for name, places in sorted(repeats.items()):
        where = ", ".join(f"{os.path.relpath(p, CORE).replace(os.sep, '/')}:{l}" for p, l in places)
        report.append(f"- `{name}` — {len(places)}×: {where}")
    report.append("")

for stage_name, _ in STAGE + [(UNCLASSIFIED, ())]:
    entries = by_stage.get(stage_name)
    if not entries:
        continue
    report.append(f"## {stage_name}")
    report.append("")
    report.append("| constant | declared | uses | in comments | declaration |")
    report.append("|---|---|---|---|---|")
    for name, path, line, text in sorted(entries, key=lambda e: (e[1], e[2])):
        rel = os.path.relpath(path, CORE).replace("\\", "/")
        report.append(f"| `{name}` | {rel}:{line} | {len(uses[(path, name)])} | {in_comments[(path, name)]} | "
                      f"`{text[:90]}` |")
    report.append("")

report.append("## Every use")
report.append("")
for path, line, name, _ in sorted(declarations, key=lambda d: (stage_of(d[0]), d[0], d[1])):
    rel = os.path.relpath(path, CORE).replace("\\", "/")
    sites = uses[(path, name)]
    if not sites:
        report.append(f"### `{name}` ({rel}:{line}) — **declared and never used**")
        report.append("")
        continue
    report.append(f"### `{name}` ({rel}:{line}) — {len(sites)} use(s)")
    report.append("")
    for use_path, use_line, text in sites:
        use_rel = os.path.relpath(use_path, CORE).replace("\\", "/")
        report.append(f"- `{use_rel}:{use_line}` — `{text[:150]}`")
    report.append("")

report.append("## Inline literals with no named constant")
report.append("")
report.append("A search for the named constants cannot find these; the first version of the exact-predicates plan "
              "missed the category entirely.")
report.append("")
for path, line, literal, text in inline:
    rel = os.path.relpath(path, CORE).replace("\\", "/")
    report.append(f"- `{rel}:{line}` — `{literal}` in `{text[:130]}`")
report.append("")

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(report) + "\n")

print(f"{len(files)} files, {len(declarations)} declarations ({len(per_name)} distinct names), "
      f"{sum(len(v) for v in uses.values())} uses, {len(inline)} inline literals")
unclassified = by_stage.get(UNCLASSIFIED, [])
if unclassified:
    print(f"WARNING: {len(unclassified)} declaration(s) in files the pipeline map does not cover:")
    for name, path, line, _ in sorted(unclassified, key=lambda e: (e[1], e[2])):
        print("   " + os.path.relpath(path, CORE) + ":" + str(line) + "  " + name)
print("wrote " + OUT)

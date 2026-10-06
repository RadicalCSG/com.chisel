# Find methods in com.chisel's Core that nothing in production calls.
#
# WHY. Walking the pipeline turned up the same thing four separate times: the correct, careful implementation of
# something exists and is not called, while a worse inline version is. BrushMesh.Optimize's
# SnapPolygonVerticesToItsPlanes / GetVertexFromIntersectingPlanes / CenterAndSnapPlanes; CSGMath's SqrDistance /
# VerticesEqual / EdgePlaneCrossing / PlaneIntersection (EdgePlaneCrossing is double, and five inline float copies
# of it are live); MergeTouchingBrushVerticesJob entirely. Four by hand is enough - the rest should be enumerated.
#
# WHAT IT CANNOT DO. This is a regex, not a C# compiler. It cannot resolve overloads, generics, interface dispatch
# or reflection, so it reports CANDIDATES, and the classes below are excluded because a textual search cannot see
# their callers at all. Every number it prints is an upper bound on deadness, and the output says so. The point is
# to produce a short list a human can check, not a verdict.
import os, re, sys
from collections import defaultdict

CHISEL = r"D:\Unity\Chisel.Dev\Packages\com.chisel"
CORE = os.path.join(CHISEL, "Core")
OUT = os.path.join(CHISEL, "Documentation~", "Design", "DeadMachinery.md")

# Called by Unity, by a job scheduler, by an interface, or by the language itself - a caller search cannot see it.
INVISIBLE_CALLERS = {
    "Execute", "Dispose", "Compare", "CompareTo", "Equals", "GetHashCode", "ToString", "OnEnable", "OnDisable",
    "OnDestroy", "Awake", "Start", "Update", "Reset", "OnValidate", "InitializeLookups", "MoveNext", "GetEnumerator",
    "Current", "Schedule", "Run", "ScheduleParallel", "ScheduleByRef", "RunByRef", "Clear", "Add", "Remove",
    "Contains", "IndexOf", "CopyTo", "GetSubArray", "AsArray", "AsReadOnly", "IsCreated", "Length", "Capacity",
}

DECL = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"                              # attributes
    r"(?:public|internal|private|protected)\s+"            # accessibility is required, to skip locals
    r"(?:static\s+|readonly\s+|unsafe\s+|virtual\s+|override\s+|sealed\s+|extern\s+|partial\s+|new\s+)*"
    r"(?!class|struct|interface|enum|delegate|event|operator|implicit|explicit|return|if|for|while)"
    r"(?:[\w.<>\[\],\s]+?)\s+"                             # return type
    r"(\w+)\s*(?:<[^>(]*>)?\s*\("                          # NAME (
)
PROPERTY = re.compile(r"\b(get|set)\s*(?:=>|\{)")


def sources(root, want_tests):
    for base, _, files in os.walk(root):
        is_test = (os.sep + "Tests") in base
        if is_test != want_tests:
            continue
        for name in sorted(files):
            if name.endswith(".cs"):
                yield os.path.join(base, name)


def read(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        return f.read().splitlines()


# Declarations come from Core. CALLERS must be looked for across the whole package: Core's public API is consumed
# by the Components and Editor assemblies, and a first version of this script searched only Core and therefore
# reported CSGManager.SkipUnchangedTrees' GetTreeInputHash / IsTreeUpdateSkipped / UpdateSkippedTrees as dead.
# They are not; they are called from outside Core. Validating against a known-live case is what caught it.
CALLER_ROOTS = [CORE,
                os.path.join(CHISEL, "Components"),
                os.path.join(CHISEL, "Editor")]

prod_files = list(sources(CORE, False))
test_files = list(sources(CORE, True))

caller_files, caller_test_files = [], []
for root in CALLER_ROOTS:
    if not os.path.isdir(root):
        continue
    caller_files.extend(sources(root, False))
    caller_test_files.extend(sources(root, True))

# 1. every declaration
declarations = []          # (path, line, name, text)
for path in prod_files:
    for n, line in enumerate(read(path), 1):
        if line.lstrip().startswith("//") or PROPERTY.search(line):
            continue
        m = DECL.match(line)
        if m:
            name = m.group(1)
            if name in INVISIBLE_CALLERS:
                continue
            declarations.append((path, n, name, line.strip()))

# 2. count textual call sites of each name, production and test, excluding the declaration lines themselves
decl_lines = {(p, n) for p, n, _, _ in declarations}
names = {name for _, _, name, _ in declarations}
prod_calls = defaultdict(int)
test_calls = defaultdict(int)
call_sites = defaultdict(list)

patterns = {name: re.compile(r"(?<![\w.])" + re.escape(name) + r"\s*(?:<[^>()]*>)?\s*\(") for name in names}
qualified = {name: re.compile(r"\.\s*" + re.escape(name) + r"\s*(?:<[^>()]*>)?\s*\(") for name in names}

for path in caller_files:
    lines = read(path)
    for n, line in enumerate(lines, 1):
        if (path, n) in decl_lines or line.lstrip().startswith("//"):
            continue
        code = line.split("//")[0]
        for name in names:
            if name not in code:
                continue
            if patterns[name].search(code) or qualified[name].search(code):
                prod_calls[name] += 1
                if len(call_sites[name]) < 3:
                    call_sites[name].append((os.path.relpath(path, CORE).replace("\\", "/"), n))

for path in caller_test_files:
    for n, line in enumerate(read(path), 1):
        if line.lstrip().startswith("//"):
            continue
        code = line.split("//")[0]
        for name in names:
            if name not in code:
                continue
            if patterns[name].search(code) or qualified[name].search(code):
                test_calls[name] += 1

# 3. a name is a candidate when NOTHING in production mentions it as a call
by_name = defaultdict(list)
for path, n, name, text in declarations:
    by_name[name].append((path, n, text))

dead_tested = []     # nothing in production calls it, tests do  <- the interesting class
dead_untested = []   # nothing calls it at all
for name in sorted(by_name):
    if prod_calls[name] > 0:
        continue
    entry = (name, by_name[name], test_calls[name])
    (dead_tested if test_calls[name] > 0 else dead_untested).append(entry)

report = []
report.append("# Machinery nothing calls")
report.append("")
report.append("**Generated** by `scratchpad/dead_machinery.py`. Re-run it and diff; do not hand-edit.")
report.append("")
report.append("Walking the pipeline turned up the same thing four separate times: the careful implementation of "
              "something exists and is not called, while a worse inline version is. "
              "`BrushMesh.Optimize`'s `SnapPolygonVerticesToItsPlanes`, `GetVertexFromIntersectingPlanes` and "
              "`CenterAndSnapPlanes`; `CSGMath`'s `SqrDistance`, `VerticesEqual`, `EdgePlaneCrossing` and "
              "`PlaneIntersection` — `EdgePlaneCrossing` computes in double and **five inline float copies of it "
              "are live**; `MergeTouchingBrushVerticesJob` in its entirety. Four found by hand is enough reason to "
              "enumerate the rest.")
report.append("")
report.append("## What this is and is not")
report.append("")
report.append("This is a regular expression, not a C# compiler. It cannot resolve overloads, generics, interface "
              "dispatch or reflection. Every entry is a **candidate** to check, and the counts are an upper bound "
              "on deadness. Names that Unity, the job system or the language calls for you are excluded outright "
              "(`Execute`, `Dispose`, `Compare`, `GetHashCode`, …), because a caller search cannot see those "
              "callers — which also means anything reached only through an interface is wrongly listed here.")
report.append("")
report.append(f"{len(declarations)} method declarations in Core's {len(prod_files)} production files, searched for callers across {len(caller_files)} production files and {len(caller_test_files)} test files in Core, Components and Editor.")
report.append("")
report.append(f"- **{len(dead_tested)}** have no production caller but **are covered by tests** — the dangerous "
              "class, because the suite reads as green over code that cannot run in anger.")
report.append(f"- **{len(dead_untested)}** have no caller anywhere.")
report.append("")

report.append("## No production caller, but tests exercise it")
report.append("")
report.append("Read these first. A passing test here proves the function works, not that the pipeline uses it.")
report.append("")
report.append("| method | declared | test call sites |")
report.append("|---|---|---|")
for name, places, tests in sorted(dead_tested, key=lambda e: -e[2]):
    where = "; ".join(f"{os.path.relpath(p, CORE).replace(os.sep, '/')}:{n}" for p, n, _ in places)
    report.append(f"| `{name}` | {where} | {tests} |")
report.append("")

report.append("## No caller anywhere")
report.append("")
report.append("| method | declared |")
report.append("|---|---|")
for name, places, _ in dead_untested:
    where = "; ".join(f"{os.path.relpath(p, CORE).replace(os.sep, '/')}:{n}" for p, n, _ in places)
    report.append(f"| `{name}` | {where} |")
report.append("")

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    f.write("\n".join(report) + "\n")

print(f"{len(declarations)} declarations from {len(prod_files)} Core files; callers searched across {len(caller_files)} files")
print(f"  {len(dead_tested)} with no production caller but WITH tests")
print(f"  {len(dead_untested)} with no caller anywhere")
print("wrote " + OUT)

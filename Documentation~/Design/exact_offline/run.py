# Offline checks of the exact CSG (Core/2.Processing/Exact, Documentation~/Design/ExactCSG.md), without the editor.
#
# Compiles the pure exact sources with EXACT_OFFLINE defined (lists on Marshal instead of Unity's allocator, no Unity
# assemblies) together with a test program, and runs it under Unity's mono:
#
#   run.py [--replace Name.cs=path/to/mutant.cs ...] [--out name] [--quiet] [--args "..."] <test source> [more sources...]
#
#   run.py ExactArithmeticTests.cs                     every integer operation and predicate against BigInteger
#   run.py --args 10000 ExactPolytopeTests.cs          brushes given as planes (ExactPolytope, ChiselBrushDefinition.
#                                                      SetPlanes): bm_c1a4b's cone, touching and duplicate planes,
#                                                      10000 generated
#   run.py --args "600 150" ExactFaceTests.cs          600 generated scenes against the oracle, 150 more run both
#                                                      clean and with float-sized noise, 40 strip scenes (a fourth
#                                                      argument) both ways too, and the unit cases
#
# The test code the editor's tests share (SHARED, Core/Tests/Contents) is compiled in as well.
#
# --replace swaps one exact source for another file, which is how a planted error (a mutant) is checked to be caught.
# The Unity install is taken from UNITY_EDITOR_DATA (the Editor/Data folder) or the default below.
import os, subprocess, sys

HERE   = os.path.dirname(os.path.abspath(__file__))
CORE   = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Core", "2.Processing"))
EXACT  = os.path.join(CORE, "Exact")
UNITY  = os.environ.get("UNITY_EDITOR_DATA", r"C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Data")
DOTNET = os.path.join(UNITY, "DotNetSdk", "dotnet.exe")
MONO   = os.path.join(UNITY, "MonoBleedingEdge", "bin", "mono.exe")
LIB    = os.path.join(UNITY, "MonoBleedingEdge", "lib", "mono", "4.5")
OUTDIR = os.path.join(os.environ.get("TEMP", HERE), "chisel_exact_offline")

def find_csc():
    sdk = os.path.join(UNITY, "DotNetSdk", "sdk")
    for version in sorted(os.listdir(sdk), reverse=True):
        csc = os.path.join(sdk, version, "Roslyn", "bincore", "csc.dll")
        if os.path.isfile(csc):
            return csc
    raise SystemExit("no Roslyn csc.dll under " + sdk)

# the exact sources that do not touch Unity
PURE = ["ExactInt.cs", "ExactPlane.cs", "ExactList.cs", "ExactFace.cs", "ExactTriangulator.cs", "ExactPolytope.cs"]
# test code shared with the editor's tests, pure as well
SHARED = [os.path.normpath(os.path.join(HERE, "..", "..", "..", "Core", "Tests", "Contents", "ExactDelaunayCheck.cs"))]

args = sys.argv[1:]
replace, tests, name, quiet, runargs = {}, [], "exact_tests", False, []
i = 0
while i < len(args):
    if args[i] == "--replace":
        k, v = args[i + 1].split("=", 1); replace[k] = os.path.abspath(v); i += 2
    elif args[i] == "--out":
        name = args[i + 1]; i += 2
    elif args[i] == "--quiet":
        quiet = True; i += 1
    elif args[i] == "--args":
        runargs = args[i + 1].split(); i += 2
    else:
        tests.append(os.path.abspath(args[i])); i += 1

sources = [os.path.join(CORE, "Categorization", "CategoryIndex.cs")]
for f in PURE:
    sources.append(replace.get(f, os.path.join(EXACT, f)))
for f in SHARED:
    sources.append(replace.get(os.path.basename(f), f))
sources += tests
os.makedirs(OUTDIR, exist_ok=True)
out = os.path.join(OUTDIR, name + ".exe")
if os.path.exists(out):
    os.remove(out)
cmd = [DOTNET, find_csc(), "-nologo", "-nostdlib+", "-noconfig", "-unsafe+", "-langversion:9.0", "-optimize+",
       "-define:EXACT_OFFLINE", "-out:" + out,
       "-r:" + os.path.join(LIB, "mscorlib.dll"), "-r:" + os.path.join(LIB, "System.dll"),
       "-r:" + os.path.join(LIB, "System.Core.dll"), "-r:" + os.path.join(LIB, "System.Numerics.dll")] + sources
r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
msgs = [l for l in (r.stdout + r.stderr).splitlines() if ": error " in l or (": warning " in l and not quiet)]
for l in msgs[:60]:
    print(l)
if not os.path.isfile(out):
    print("COMPILE FAILED")
    sys.exit(2)
r = subprocess.run([MONO, out] + runargs, capture_output=True, text=True, encoding="utf-8", errors="replace")
print(r.stdout[-20000:])
print(r.stderr[-5000:])
print("exit", r.returncode)
sys.exit(r.returncode)

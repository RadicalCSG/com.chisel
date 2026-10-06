# Validate the sample scene's generator PARAMETERS offline: Boxes, Cylinders, Extruded Shapes.
#
# Their geometry only exists at runtime, but the parameters that produce it are serialised, and all three
# generators accept anything:
#   - ChiselBox.Validate() swaps Min/Max and returns true. A zero dimension only raises an inspector WARNING
#     ("One or more dimensions of the box is zero, which is not allowed") - the flat box still goes through.
#   - ChiselCylinder.Validate() takes abs() of the diameters, clamps sides to >= 3, returns true. It never looks
#     at height.
#   - ChiselExtrudedShape.Validate() is `return true;`, and the definition's own only fills in a null shape/path.
# So a degenerate parameter set is not caught anywhere before the CSG, which is what makes it worth finding here.
import math, os, re, json

PROJECT = r"D:\Unity\Chisel.Dev"
SCENE = os.path.join(PROJECT, "Assets", "Samples", "Chisel 3D Level Editor", "0.3.0-preview",
                     "Sample scene", "sample scene.unity")
PACKAGES = os.path.join(PROJECT, "Packages")
NUM = r"(-?[\d.]+(?:[eE][+-]?\d+)?|-?Infinity|NaN)"
EPS = 1e-5


def guid_map():
    out = {}
    for root, _, files in os.walk(PACKAGES):
        for n in files:
            if n.endswith(".cs.meta"):
                try:
                    t = open(os.path.join(root, n), encoding="utf-8", errors="replace").read()
                except OSError:
                    continue
                m = re.search(r"^guid:\s*([0-9a-f]{32})", t, re.M)
                if m:
                    out[m.group(1)] = n[:-8]
    return out


def documents(path):
    lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
    head = re.compile(r"^--- !u!(\d+) &(\d+)")
    cur, body = None, []
    for l in lines:
        m = head.match(l)
        if m:
            if cur:
                yield cur[0], cur[1], body
            cur, body = (int(m.group(1)), int(m.group(2))), []
        elif cur:
            body.append(l)
    if cur:
        yield cur[0], cur[1], body


def field(body, name):
    for l in body:
        m = re.match(r"^\s*" + re.escape(name) + r":\s*(.*)$", l)
        if m:
            return m.group(1).strip()
    return None


def rid(v):
    m = re.search(r"fileID:\s*(-?\d+)", v) if v else None
    return int(m.group(1)) if m else None


def fl(v):
    if v is None:
        return None
    if "Infinity" in v:
        return -math.inf if v.startswith("-") else math.inf
    if v == "NaN":
        return math.nan
    return float(v)


def block(body, key, start=0):
    for i in range(start, len(body)):
        m = re.match(r"^(\s*)" + re.escape(key) + r":\s*$", body[i])
        if m:
            indent = len(m.group(1))
            out = []
            for l2 in body[i + 1:]:
                if not l2.strip():
                    continue
                lead = len(l2) - len(l2.lstrip())
                if lead < indent or (lead == indent and not l2.lstrip().startswith("-")):
                    break
                out.append(l2)
            return out
    return None


def inline_vec(line):
    got = dict(re.findall(r"([xyzw]):\s*" + NUM, line or ""))
    return {k: fl(v) for k, v in got.items()}


def items(lines):
    """Split a YAML list block into its items (each a list of lines)."""
    out, cur = [], None
    base = None
    for l in lines:
        s = l.lstrip()
        lead = len(l) - len(s)
        if s.startswith("- ") and (base is None or lead == base):
            base = lead
            if cur is not None:
                out.append(cur)
            cur = [" " * (lead + 2) + s[2:]]
        elif cur is not None:
            cur.append(l)
    if cur is not None:
        out.append(cur)
    return out


def scalar_block_vec(lines, key):
    """A value like `Min:` followed by indented x:/y:/z: lines."""
    b = block(lines, key)
    if not b:
        return None
    out = {}
    for l in b:
        m = re.match(r"^\s*([xyz]):\s*" + NUM + r"\s*$", l)
        if m:
            out[m.group(1)] = fl(m.group(2))
    return out


def seg_intersect(p1, p2, p3, p4):
    def orient(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
    d1 = orient(p3, p4, p1); d2 = orient(p3, p4, p2)
    d3 = orient(p1, p2, p3); d4 = orient(p1, p2, p4)
    return ((d1 > EPS and d2 < -EPS) or (d1 < -EPS and d2 > EPS)) and \
           ((d3 > EPS and d4 < -EPS) or (d3 < -EPS and d4 > EPS))


scripts = guid_map()
names, tr, gens = {}, {}, []
for cid, fid, body in documents(SCENE):
    if cid == 1:
        names[fid] = (field(body, "m_Name") or "").strip().strip('"')
    elif cid == 4:
        s = inline_vec(field(body, "m_LocalScale"))
        tr[fid] = {"go": rid(field(body, "m_GameObject")), "parent": rid(field(body, "m_Father")),
                   "scale": [s.get(k, 1.0) for k in "xyz"]}
    elif cid == 114:
        s = field(body, "m_Script")
        g = re.search(r"guid:\s*([0-9a-f]{32})", s) if s else None
        cls = scripts.get(g.group(1)) if g else None
        if cls in ("ChiselBoxComponent", "ChiselCylinderComponent", "ChiselExtrudedShapeComponent"):
            op = field(body, "operation")
            gens.append({"cls": cls, "go": rid(field(body, "m_GameObject")),
                         "operation": {"0": "Additive", "1": "Subtractive", "2": "Intersecting"}.get(op, op),
                         "body": body})

go2tr = {t["go"]: fid for fid, t in tr.items() if t["go"]}


def path_of(go):
    out = []
    while go:
        out.append(names.get(go, "?"))
        tid = go2tr.get(go)
        if not tid:
            break
        p = tr[tid]["parent"]
        go = tr[p]["go"] if p in tr else None
    return "/".join(reversed(out))


def world_det(go):
    det = 1.0
    while go:
        tid = go2tr.get(go)
        if not tid:
            break
        s = tr[tid]["scale"]
        det *= s[0] * s[1] * s[2]
        p = tr[tid]["parent"]
        go = tr[p]["go"] if p in tr else None
    return det


findings = []   # (where, problem)
counts = {"ChiselBoxComponent": 0, "ChiselCylinderComponent": 0, "ChiselExtrudedShapeComponent": 0}
parse_failures = []

for g in gens:
    counts[g["cls"]] += 1
    where = f"{path_of(g['go'])} [{g['operation']}]"
    body = g["body"]
    kind = g["cls"].replace("Chisel", "").replace("Component", "")

    if g["cls"] == "ChiselBoxComponent":
        bounds = block(body, "bounds")
        mn = scalar_block_vec(bounds or [], "Min")
        mx = scalar_block_vec(bounds or [], "Max")
        if not mn or not mx or len(mn) < 3 or len(mx) < 3:
            parse_failures.append((where, "could not read bounds"))
            continue
        size = [abs(mx[a] - mn[a]) for a in "xyz"]
        if any(not math.isfinite(v) for v in size):
            findings.append((where, kind, f"non-finite bounds {mn} .. {mx}"))
        elif min(size) == 0:
            findings.append((where, kind, f"a ZERO dimension, size {size} - flat box. Inspector warns, Validate() "
                                          "returns true, and it reaches the CSG"))
        elif min(size) < 1e-3:
            findings.append((where, kind, f"a sliver, smallest dimension {min(size):.3g} (< 1 mm), size {size}"))
        if any(mx[a] < mn[a] for a in "xyz"):
            findings.append((where, kind, f"Min > Max on some axis ({mn} .. {mx}) - Validate() swaps it silently"))

    elif g["cls"] == "ChiselCylinderComponent":
        s = block(body, "settings") or []
        get = lambda k: fl(field(s, k))
        sides, height = get("sides"), get("height")
        diam = {k: get(k) for k in ("topDiameterX", "topDiameterZ", "bottomDiameterX", "bottomDiameterZ")}
        if sides is None or height is None or any(v is None for v in diam.values()):
            parse_failures.append((where, "could not read cylinder settings"))
            continue
        if sides < 3:
            findings.append((where, kind, f"{int(sides)} sides - Validate() clamps it to 3 silently"))
        if height == 0:
            findings.append((where, kind, "height 0 - a FLAT cylinder; Validate() never checks height"))
        zero = [k for k, v in diam.items() if v == 0]
        if len(zero) == 4:
            findings.append((where, kind, "every diameter is 0 - a line, not a volume"))
        elif ("topDiameterX" in zero or "topDiameterZ" in zero) and ("bottomDiameterX" in zero or "bottomDiameterZ" in zero):
            findings.append((where, kind, f"zero diameters at both ends {zero} - no volume"))
        elif zero:
            findings.append((where, kind, f"zero diameter(s) {zero} - a cone apex or a flat side; legal but degenerate "
                                          "faces meet at a point"))
        neg = [k for k, v in diam.items() if v < 0]
        if neg:
            findings.append((where, kind, f"negative diameter(s) {neg} - Validate() takes abs() silently"))

    else:   # extruded shape
        shape = block(body, "shape") or []
        closed = field(shape, "closed")
        cps = items(block(shape, "controlPoints") or [])
        points, curved = [], 0
        for it in cps:
            pos = inline_vec(field(it, "position"))
            t1 = inline_vec(field(it, "tangent1"))
            t2 = inline_vec(field(it, "tangent2"))
            if "x" not in pos:
                continue
            points.append((pos["x"], pos.get("y", 0.0)))
            if any(abs(v) > 0 for v in list(t1.values()) + list(t2.values())):
                curved += 1
        segs = items(block(block(body, "path") or [], "segments") or [])
        path = []
        for it in segs:
            pos = inline_vec(field(it, "position"))
            sc = inline_vec(field(it, "scale"))
            path.append(((pos.get("x", 0.0), pos.get("y", 0.0), pos.get("z", 0.0)), (sc.get("x", 1.0), sc.get("y", 1.0))))
        if not points or not path:
            parse_failures.append((where, f"could not read shape ({len(points)} points) or path ({len(path)} segments)"))
            continue

        n = len(points)
        if n < 3:
            findings.append((where, kind, f"{n} control point(s) - cannot enclose an area"))
        if closed != "1":
            findings.append((where, kind, f"shape is not closed (closed: {closed}) - an extruded OPEN curve has no volume"))
        for i in range(n):
            a, b = points[i], points[(i + 1) % n]
            if math.hypot(b[0] - a[0], b[1] - a[1]) < EPS:
                findings.append((where, kind, f"control points {i} and {(i + 1) % n} coincide at {a} - zero-length edge"))
        area = 0.0
        for i in range(n):
            a, b = points[i], points[(i + 1) % n]
            area += a[0] * b[1] - b[0] * a[1]
        area *= 0.5
        if abs(area) < EPS:
            findings.append((where, kind, f"shape encloses no area (signed area {area:.3g})"))
        for i in range(n):
            a, b, c = points[i - 1], points[i], points[(i + 1) % n]
            cross = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0])
            if abs(cross) < EPS and math.hypot(b[0] - a[0], b[1] - a[1]) > EPS:
                findings.append((where, kind, f"control point {i} at {b} is collinear with its neighbours - a "
                                              "degenerate corner, zero-width face"))
        crossings = []
        for i in range(n):
            for j in range(i + 1, n):
                if abs(i - j) <= 1 or (i == 0 and j == n - 1):
                    continue
                if seg_intersect(points[i], points[(i + 1) % n], points[j], points[(j + 1) % n]):
                    crossings.append((i, j))
        if crossings:
            findings.append((where, kind, f"the shape's outline CROSSES ITSELF at edge pairs {crossings[:4]}"
                                          + (" (control polygon; curved segments may differ)" if curved else "")
                                          + " - Validate() is `return true;`"))
        # convexity: ConvexPartition splits a concave shape into several brushes - legal, but worth knowing
        signs = set()
        for i in range(n):
            a, b, c = points[i - 1], points[i], points[(i + 1) % n]
            cross = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0])
            if abs(cross) > EPS:
                signs.add(cross > 0)
        concave = len(signs) > 1
        if len(path) < 2:
            findings.append((where, kind, f"{len(path)} path segment(s) - nothing to extrude along"))
        for i in range(len(path) - 1):
            a, b = path[i][0], path[i + 1][0]
            if math.dist(a, b) < EPS:
                findings.append((where, kind, f"path segments {i} and {i + 1} coincide at {a} - zero-length "
                                              "extrusion, a FLAT brush"))
        for i, (_, sc) in enumerate(path):
            if abs(sc[0]) < EPS or abs(sc[1]) < EPS:
                findings.append((where, kind, f"path segment {i} has scale {sc} - collapses the shape"))
        g["concave"] = concave
        g["curved"] = curved

    if world_det(g["go"]) < 0:
        findings.append((where, kind, f"mirrored transform (world determinant {world_det(g['go']):+.2f}) - "
                                      "inside-out at CSG time unless something flips it"))

print(f"generators validated offline from their serialised parameters: {counts}")
if parse_failures:
    print(f"\nCOULD NOT READ {len(parse_failures)} - these are NOT examined:")
    for where, why in parse_failures:
        print(f"  {where}: {why}")
by_kind = {}
for where, kind, problem in findings:
    by_kind.setdefault(problem.split(" - ")[0].split("(")[0].strip()[:40], []).append((where, kind, problem))
print(f"\n{len(findings)} finding(s):")
for where, kind, problem in findings:
    print(f"  {kind:14s} {where}")
    print(f"      {problem}")
concave = [g for g in gens if g.get("concave")]
curved = [g for g in gens if g.get("curved")]
print(f"\ncontext: {len(concave)} extruded shape(s) are concave (ConvexPartition splits them into several brushes), "
      f"{len(curved)} have curved segments")
json.dump([{"where": w, "kind": k, "problem": p} for w, k, p in findings],
          open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "generator_validation.json"), "w"), indent=1)

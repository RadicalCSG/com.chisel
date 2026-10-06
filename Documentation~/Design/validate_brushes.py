# Validate every ChiselBrushComponent in the sample scene OFFLINE, from the topology serialised in the scene file.
#
# Two kinds of check, reported separately because they mean different things:
#
#   CHISEL REJECTS - Chisel's own rules, mirrored exactly from BrushMesh.ValidateData, IsConcave and IsInsideOut,
#     with the same constants (kDistanceEpsilon = 1e-5 at class scope, kMinimum* from BrushMesh.Validate.cs) and the
#     same plane construction (CalculatePlane: Newell in double, normalised, d = -average of dot(normal, vertex)).
#     A brush failing one of these is invalid by Chisel's own definition.
#
#   CHISEL LETS THROUGH - checks Chisel does not make. Two of its four shape checks are stubs: IsSelfIntersecting()
#     always returns false, and HasVolume() only checks that the arrays are not empty (its TODO says it never checks
#     for a flat or 1D brush). So a flat, degenerate or self-intersecting brush passes validation and reaches the
#     CSG. These are the more dangerous class: nothing upstream catches them.
#
# Mirrored GameObject transforms are reported too: a valid, outward brush under a negative-determinant transform
# is inside-out by the time the CSG sees it, and IsInsideOut only ever looks at the definition.
import math, os, re, struct, sys, json
from collections import defaultdict

PROJECT = r"D:\Unity\Chisel.Dev"
SCENE = os.path.join(PROJECT, "Assets", "Samples", "Chisel 3D Level Editor", "0.3.0-preview",
                     "Sample scene", "sample scene.unity")
PACKAGES = os.path.join(PROJECT, "Packages")

K_DISTANCE_EPSILON = 1e-5      # BrushMesh.Optimize.cs:12, used by IsConcave / IsInsideOut
K_MIN_VERTICES, K_MIN_POLYGONS, K_MIN_HALFEDGES = 4, 3, 9


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


NUM = r"(-?[\d.]+(?:[eE][+-]?\d+)?|-?Infinity|NaN)"


def block(body, key):
    """The lines of a YAML block `key:` - everything indented deeper than the key, following it."""
    for i, l in enumerate(body):
        m = re.match(r"^(\s*)" + re.escape(key) + r":\s*$", l)
        if m:
            indent = len(m.group(1))
            out = []
            for l2 in body[i + 1:]:
                if not l2.strip():
                    continue
                lead = len(l2) - len(l2.lstrip())
                # list items of a block can sit at the SAME indent as the key ("- x:"), so accept that too
                if lead < indent or (lead == indent and not l2.lstrip().startswith("-")):
                    break
                out.append(l2)
            return out
    return None


def parse_list(lines, keys):
    """A YAML list of maps, one '- ' per item, with the given scalar keys."""
    items, cur = [], None
    for l in lines:
        s = l.strip()
        if s.startswith("- "):
            if cur is not None:
                items.append(cur)
            cur = {}
            s = s[2:]
        if cur is None:
            continue
        m = re.match(r"^(\w+):\s*(.*)$", s)
        if m and m.group(1) in keys:
            cur[m.group(1)] = m.group(2).strip()
    if cur is not None:
        items.append(cur)
    return items


def to_float(v):
    if v in ("Infinity", "+Infinity"):
        return math.inf
    if v == "-Infinity":
        return -math.inf
    if v == "NaN":
        return math.nan
    return float(v)


def parse_outline(body):
    outline = block(body, "brushOutline")
    if outline is None:
        return None
    verts_block = block(outline, "vertices") or []
    edges_block = block(outline, "halfEdges") or []
    polys_block = block(outline, "polygons") or []
    vertices = [(to_float(d.get("x", "0")), to_float(d.get("y", "0")), to_float(d.get("z", "0")))
                for d in parse_list(verts_block, ("x", "y", "z"))]
    halfedges = [(int(d.get("vertexIndex", "-1")), int(d.get("twinIndex", "-1")))
                 for d in parse_list(edges_block, ("vertexIndex", "twinIndex"))]
    polygons = [(int(d.get("firstEdge", "-1")), int(d.get("edgeCount", "0")))
                for d in parse_list(polys_block, ("firstEdge", "edgeCount"))]
    hepi = None
    raw = field(outline, "halfEdgePolygonIndices")
    if raw and re.fullmatch(r"[0-9a-fA-F]+", raw) and len(raw) % 8 == 0:
        data = bytes.fromhex(raw)
        hepi = [struct.unpack_from("<i", data, i)[0] for i in range(0, len(data), 4)]
    return {"vertices": vertices, "halfEdges": halfedges, "polygons": polygons, "hepi": hepi}


# --- Chisel's plane construction, BrushMesh.Utility.cs CalculatePlane --------------------------------------------------
def calculate_plane(polygon, halfedges, vertices):
    first, count = polygon
    last = first + count
    nx = ny = nz = 0.0
    prev = vertices[halfedges[last - 1][0]]
    for n in range(first, last):
        cur = vertices[halfedges[n][0]]
        nx += (prev[1] - cur[1]) * (prev[2] + cur[2])
        ny += (prev[2] - cur[2]) * (prev[0] + cur[0])
        nz += (prev[0] - cur[0]) * (prev[1] + cur[1])
        prev = cur
    length = math.sqrt(nx * nx + ny * ny + nz * nz)
    if length == 0.0 or not math.isfinite(length):
        return None, length
    nx, ny, nz = nx / length, ny / length, nz / length
    d = 0.0
    for n in range(first, last):
        v = vertices[halfedges[n][0]]
        d -= nx * v[0] + ny * v[1] + nz * v[2]
    d /= count
    return (nx, ny, nz, d), length


def distance(plane, v):
    return plane[0] * v[0] + plane[1] * v[1] + plane[2] * v[2] + plane[3]


# --- BrushMesh.ValidateData, mirrored -----------------------------------------------------------------------------------
def validate_data(o):
    errors = []
    V, H, P = o["vertices"], o["halfEdges"], o["polygons"]
    if not V: errors.append("no vertices")
    if not H: errors.append("no halfEdges")
    if not P: errors.append("no polygons")
    if errors:
        return errors
    if len(V) < K_MIN_VERTICES: errors.append(f"{len(V)} vertices, needs {K_MIN_VERTICES}")
    if len(P) < K_MIN_POLYGONS: errors.append(f"{len(P)} polygons, needs {K_MIN_POLYGONS}")
    if len(H) < K_MIN_HALFEDGES: errors.append(f"{len(H)} halfEdges, needs {K_MIN_HALFEDGES}")
    if errors:
        return errors
    for h, (vi, ti) in enumerate(H):
        if vi < 0 or vi >= len(V):
            errors.append(f"halfEdges[{h}].vertexIndex {vi} out of range")
        if ti < 0 or ti >= len(H):
            errors.append(f"halfEdges[{h}].twinIndex {ti} out of range")
            continue
        if H[ti][1] != h:
            errors.append(f"halfEdges[{h}].twin is {ti}, whose twin is {H[ti][1]}")
    for p, (first, count) in enumerate(P):
        bad = False
        if first < 0 or first >= len(H):
            errors.append(f"polygons[{p}].firstEdge {first} out of range"); bad = True
        if count <= 2:
            errors.append(f"polygons[{p}].edgeCount {count}"); bad = True
        elif first + count - 1 >= len(H):
            errors.append(f"polygons[{p}] runs past the halfEdges"); bad = True
        elif p < len(P) - 1 and P[p + 1][0] != first + count:
            errors.append(f"polygons[{p + 1}] is not contiguous with polygons[{p}]"); bad = True
        if bad:
            continue
        for i1 in range(count):
            i0 = (i1 - 1) % count
            h0 = H[first + i0]
            h1 = H[first + i1]
            if h1[1] < 0 or h1[1] >= len(H):
                errors.append(f"polygons[{p}] edge twin out of range"); continue
            t1 = H[h1[1]]
            if h0[0] != t1[0]:
                errors.append(f"polygons[{p}]: halfEdges[{first + i0}].vertex {h0[0]} != twin-of-next vertex {t1[0]}")
    return errors


def hepi_of(o):
    if o["hepi"] and len(o["hepi"]) == len(o["halfEdges"]):
        return o["hepi"]
    out = [0] * len(o["halfEdges"])
    for p, (first, count) in enumerate(o["polygons"]):
        for e in range(first, first + count):
            if 0 <= e < len(out):
                out[e] = p
    return out


# --- BrushMesh.IsConcave / IsInsideOut, mirrored ------------------------------------------------------------------------
def neighbour_verdicts(o, planes):
    """For each checked edge: -1 the twin polygon bends inside this plane (convex), +1 outside (concave), 0 undecided."""
    H, P, V = o["halfEdges"], o["polygons"], o["vertices"]
    hepi = hepi_of(o)
    verdicts = []
    for p, (first, count) in enumerate(P):
        plane = planes[p]
        if plane is None:
            continue
        for e in range(first, first + count):
            twin = H[e][1]
            if twin < e:
                continue
            tp = hepi[twin]
            tfirst, tcount = P[tp]
            it = twin
            verdict = 0
            while True:
                it = ((it - tfirst) + 1) % tcount + tfirst
                dist = distance(plane, V[H[it][0]])
                if dist < -K_DISTANCE_EPSILON:
                    verdict = -1; break
                if dist > K_DISTANCE_EPSILON:
                    verdict = +1; break
                if it == twin:
                    break
            verdicts.append(verdict)
    return verdicts


def is_concave(verdicts):
    return (+1 in verdicts) and (-1 in verdicts)


def is_inside_out(verdicts):
    # IsInsideOut returns false the moment any checked edge finds a vertex INSIDE; true otherwise
    return -1 not in verdicts


# --- what Chisel does not check ------------------------------------------------------------------------------------------
def signed_volume(o):
    V, H, P = o["vertices"], o["halfEdges"], o["polygons"]
    vol = 0.0
    for first, count in P:
        a = V[H[first][0]]
        for k in range(1, count - 1):
            b = V[H[first + k][0]]
            c = V[H[first + k + 1][0]]
            vol += (a[0] * (b[1] * c[2] - b[2] * c[1]) -
                    a[1] * (b[0] * c[2] - b[2] * c[0]) +
                    a[2] * (b[0] * c[1] - b[1] * c[0])) / 6.0
    return vol


def extent(o):
    xs = [v[0] for v in o["vertices"]]; ys = [v[1] for v in o["vertices"]]; zs = [v[2] for v in o["vertices"]]
    return max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))


# --- the scene -----------------------------------------------------------------------------------------------------------
scripts = guid_map()
names, tr, brushes = {}, {}, []
for cid, fid, body in documents(SCENE):
    if cid == 1:
        names[fid] = (field(body, "m_Name") or "").strip().strip('"')
    elif cid == 4:
        sc = field(body, "m_LocalScale") or ""
        got = dict(re.findall(r"([xyz]):\s*" + NUM, sc))
        tr[fid] = {"go": rid(field(body, "m_GameObject")), "parent": rid(field(body, "m_Father")),
                   "scale": [to_float(got.get(k, "1")) for k in "xyz"]}
    elif cid == 114:
        s = field(body, "m_Script")
        g = re.search(r"guid:\s*([0-9a-f]{32})", s) if s else None
        if g and scripts.get(g.group(1)) == "ChiselBrushComponent":
            op = field(body, "operation")
            brushes.append({"go": rid(field(body, "m_GameObject")),
                            "operation": {"0": "Additive", "1": "Subtractive", "2": "Intersecting"}.get(op, op),
                            "outline": parse_outline(body)})

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


rejected, let_through, mirrored, clean = [], [], [], 0
for b in brushes:
    o = b["outline"]
    where = f"{path_of(b['go'])} [{b['operation']}]"
    if o is None:
        rejected.append((where, ["no brushOutline in the definition"]))
        continue

    nonfinite = any(not math.isfinite(c) for v in o["vertices"] for c in v)
    data_errors = validate_data(o)
    if nonfinite:
        data_errors = ["non-finite vertex coordinates"] + data_errors
    if data_errors:
        rejected.append((where, data_errors))
        continue

    planes, degenerate = [], []
    for p, poly in enumerate(o["polygons"]):
        plane, newell = calculate_plane(poly, o["halfEdges"], o["vertices"])
        planes.append(plane)
        if plane is None:
            degenerate.append(f"polygons[{p}] has zero area (Newell length {newell:.3g}) - CalculatePlane divides by it")

    verdicts = neighbour_verdicts(o, planes)
    chisel = []
    if is_concave(verdicts):
        chisel.append("IsConcave: some edges bend in, some out - ValidateShape rejects it")
    if is_inside_out(verdicts):
        chisel.append("IsInsideOut: Validate inverts it silently (ChiselBrushDefinition.Validate)")

    through = list(degenerate)
    vol = signed_volume(o)
    size = extent(o)
    if abs(vol) <= 1e-9 * max(size, 1e-6) ** 3 or size < 1e-6:
        through.append(f"no volume (signed volume {vol:.3g}, extent {size:.3g}) - HasVolume() does not check this")
    worst, worst_poly = 0.0, -1
    for p, (first, count) in enumerate(o["polygons"]):
        if planes[p] is None:
            continue
        for e in range(first, first + count):
            d = abs(distance(planes[p], o["vertices"][o["halfEdges"][e][0]]))
            if d > worst:
                worst, worst_poly = d, p
    if worst > K_DISTANCE_EPSILON:
        through.append(f"non-planar polygon {worst_poly}: a vertex sits {worst:.3g} off its own fitted plane "
                       f"(> kDistanceEpsilon {K_DISTANCE_EPSILON}; nothing on the import path splits it)")
    # global convexity: every vertex inside every plane. For a valid convex brush this holds; a vertex clearly
    # outside some plane means concave or self-intersecting, which IsConcave's edge-local test can miss.
    outside = 0
    for p, plane in enumerate(planes):
        if plane is None:
            continue
        for v in o["vertices"]:
            if distance(plane, v) > 1e-3:
                outside += 1
    if outside and not is_concave(verdicts):
        through.append(f"{outside} vertex/plane pairs lie > 1 mm OUTSIDE the brush's own planes, yet IsConcave "
                       "passes it - concave or self-intersecting in a way the edge-local test does not see "
                       "(IsSelfIntersecting() is a stub returning false)")
    seen = {}
    for i, v in enumerate(o["vertices"]):
        key = tuple(round(c, 5) for c in v)
        if key in seen:
            through.append(f"duplicate vertices {seen[key]} and {i} at {v}")
            break
        seen[key] = i

    det = world_det(b["go"])
    if det < 0:
        mirrored.append((where, det, is_inside_out(verdicts)))

    if chisel:
        rejected.append((where, chisel + through))
    elif through:
        let_through.append((where, through))
    else:
        clean += 1

print(f"{len(brushes)} ChiselBrushComponents validated offline, from the topology serialised in the scene")
print(f"  {len(rejected)} fail Chisel's OWN rules")
print(f"  {len(let_through)} pass Chisel's rules but are invalid in ways it does not check")
print(f"  {len(mirrored)} sit under a mirrored (negative-determinant) transform")
print(f"  {clean} clean")
for title, rows in (("FAIL CHISEL'S OWN RULES", rejected), ("CHISEL LETS THESE THROUGH", let_through)):
    if not rows:
        continue
    print(f"\n=== {title} ===")
    for where, problems in rows:
        print(f"  {where}")
        for problem in problems[:6]:
            print(f"      - {problem}")
if mirrored:
    print("\n=== MIRRORED (inside-out at CSG time unless something flips them back) ===")
    for where, det, already in mirrored:
        print(f"  det {det:+.2f}  {where}" + ("   (and the definition is already inside-out)" if already else ""))

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "brush_validation.json")
json.dump({"rejected": rejected, "let_through": let_through,
           "mirrored": [(w, d, a) for w, d, a in mirrored], "clean": clean}, open(out, "w"), indent=1)

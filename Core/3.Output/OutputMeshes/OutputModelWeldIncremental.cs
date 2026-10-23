using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    // The triangles the weld welds this update (OutputModelWeld.PrepareIncremental); a triangle a split added always is
    internal struct WeldScope
    {
        public NativeReference<bool> all;
        // per brush: where its surfaces start in surfaceStart, -1 when none of its triangles is in it
        public NativeList<int>  surfaceBase;
        // per surface of such a brush and one past its last: where its triangles start in inside
        public NativeList<int>  surfaceStart;
        public NativeList<bool> inside;

        public static WeldScope Create(Allocator allocator)
        {
            var scope = new WeldScope
            {
                all          = new NativeReference<bool>(allocator),
                surfaceBase  = new NativeList<int>(16, allocator),
                surfaceStart = new NativeList<int>(16, allocator),
                inside       = new NativeList<bool>(16, allocator)
            };
            scope.all.Value = true;
            return scope;
        }

        public readonly bool Touches(int brush)
        {
            return !all.IsCreated || all.Value || surfaceBase[brush] >= 0;
        }

        public readonly bool Contains(int brush, int surface, int triangle)
        {
            if (!all.IsCreated || all.Value)
                return true;
            var first = surfaceBase[brush];
            if (first < 0)
                return false;
            var start = surfaceStart[first + surface];
            if (triangle >= surfaceStart[first + surface + 1] - start)
                return true;
            return inside[start + triangle];
        }

        public void Dispose()
        {
            if (all.IsCreated)          all.Dispose();
            if (surfaceBase.IsCreated)  surfaceBase.Dispose();
            if (surfaceStart.IsCreated) surfaceStart.Dispose();
            if (inside.IsCreated)       inside.Dispose();
        }

        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle dependency)
        {
            var a = all.IsCreated          ? all.Dispose(dependency)          : dependency;
            var b = surfaceBase.IsCreated  ? surfaceBase.Dispose(dependency)  : dependency;
            var c = surfaceStart.IsCreated ? surfaceStart.Dispose(dependency) : dependency;
            var d = inside.IsCreated       ? inside.Dispose(dependency)       : dependency;
            return Unity.Jobs.JobHandle.CombineDependencies(Unity.Jobs.JobHandle.CombineDependencies(a, b, c), d);
        }
    }

    // An input triangle of the weld, by its brush's slot in WeldMemory
    internal struct WeldTriangle : System.IEquatable<WeldTriangle>
    {
        public int slot, surface, triangle;
        public bool Equals(WeldTriangle other) => slot == other.slot && surface == other.surface && triangle == other.triangle;
        public override int GetHashCode() => (int)math.hash(new int3(slot, surface, triangle));
    }

    // An input edge under twice the collapse length, from one of its corners, of the triangle that has it
    internal struct WeldShortLink : System.IEquatable<WeldShortLink>
    {
        public int3 to;
        public WeldTriangle owner;
        public bool Equals(WeldShortLink other) => math.all(to == other.to) && owner.Equals(other.owner);
        public override int GetHashCode() => (int)math.hash(new int4(to, owner.GetHashCode()));
    }

    // Where a surface of a brush the weld changed differs from the brush's buffer: a triangle's corners (-1: dropped) or
    // a vertex; root: the input triangle of the surface it belongs to, -1 for what the surface's splits added; at: where a
    // triangle's corners are (WeldMemory.roundsAt)
    internal struct WeldEntry
    {
        public int  surface;
        public bool vertex;
        public int  index;
        public int  root;
        public int3 corners;
        public int3x3 at;
        public float3       collider;
        public SelectVertex select;
        public RenderVertex render;
    }

    // A brush as the last update welded it
    internal struct WeldBrush
    {
        public CompactNodeID id;
        public BlobAssetReference<ChiselBrushRenderBuffer> input, output;
        public int  order;
        public bool used, live, changed, dirty;
        // per surface and one past the last: where its triangles start in corners and needle
        public UnsafeList<int>    surfaceStart;
        public UnsafeList<int3x3> corners;
        public UnsafeList<bool>   needle;
        // what the rounds did to it (CancelOpposites, WeldNeedles), by the input triangle it belongs to
        public UnsafeList<WeldEntry> entries;
        // what the whole weld made of it (the rounds, the moves and the drops of the collapse), as its buffer was stored
        public UnsafeList<WeldEntry> final;

        public void FreeInput()
        {
            if (surfaceStart.IsCreated) surfaceStart.Dispose();
            if (corners.IsCreated)      corners.Dispose();
            if (needle.IsCreated)       needle.Dispose();
            if (entries.IsCreated)      entries.Dispose();
            if (final.IsCreated)        final.Dispose();
            surfaceStart = default; corners = default; needle = default; entries = default; final = default;
        }
    }

    // What the weld did at the last update, by piece (IncrementalWeld.md), kept with the tree
    internal struct WeldMemory
    {
        public NativeList<WeldBrush> brushes;   // by slot
        public NativeList<int> freeSlots;
        public NativeParallelHashMap<CompactNodeID, int> slotOf;
        // the relation's index over every input triangle
        public NativeParallelMultiHashMap<int3, WeldTriangle> atPosition;
        public NativeParallelMultiHashMap<OutputModelWeld.TriangleKey, WeldTriangle> byTriple;
        public NativeParallelMultiHashMap<int3, WeldShortLink> shortLinks;
        // (slot, surface) -> the input triangles its splits started from
        public NativeParallelMultiHashMap<long, int> splitRoots;
        // the surfaces whose triangles the rounds rewrote or added have a corner here: (slot, surface), once per corner
        public NativeParallelMultiHashMap<int3, long> roundsAt;
        // the collapse's moves, by the position moved
        public NativeParallelHashMap<int3, float3x2> moves;
        // this update: the corners of the triangles the rounds were done again for, before and after (their point groups
        // are clustered again)
        public NativeList<int3> touched;
        public NativeReference<bool> valid;
        // what the last update welded: everything (-1) or this many triangles
        public NativeReference<int> lastScope;
        // this update's: the scope, its slot of each brush
        public NativeParallelHashSet<WeldTriangle> scope;
        public NativeList<int> slotOfBrush;
        // brush by brush: its buffer is the one it was handed at the last update (FinishIncremental)
        public NativeList<bool> sameBuffer;

        public static WeldMemory Create(Allocator allocator)
        {
            return new WeldMemory
            {
                brushes     = new NativeList<WeldBrush>(64, allocator),
                freeSlots   = new NativeList<int>(16, allocator),
                slotOf      = new NativeParallelHashMap<CompactNodeID, int>(64, allocator),
                atPosition  = new NativeParallelMultiHashMap<int3, WeldTriangle>(256, allocator),
                byTriple    = new NativeParallelMultiHashMap<OutputModelWeld.TriangleKey, WeldTriangle>(256, allocator),
                shortLinks  = new NativeParallelMultiHashMap<int3, WeldShortLink>(64, allocator),
                splitRoots  = new NativeParallelMultiHashMap<long, int>(16, allocator),
                roundsAt    = new NativeParallelMultiHashMap<int3, long>(64, allocator),
                moves       = new NativeParallelHashMap<int3, float3x2>(64, allocator),
                touched     = new NativeList<int3>(64, allocator),
                valid       = new NativeReference<bool>(allocator),
                lastScope   = new NativeReference<int>(allocator),
                scope       = new NativeParallelHashSet<WeldTriangle>(256, allocator),
                slotOfBrush = new NativeList<int>(64, allocator),
                sameBuffer  = new NativeList<bool>(64, allocator)
            };
        }

        public void Clear()
        {
            for (int i = 0; i < brushes.Length; i++)
            {
                ref var brush = ref brushes.ElementAt(i);
                brush.FreeInput();
                if (brush.output.IsCreated)
                    brush.output.Dispose();
                brush.output = default;
            }
            brushes.Clear(); freeSlots.Clear(); slotOf.Clear();
            atPosition.Clear(); byTriple.Clear(); shortLinks.Clear(); splitRoots.Clear(); moves.Clear(); roundsAt.Clear(); touched.Clear();
            scope.Clear(); slotOfBrush.Clear();
            valid.Value = false;
        }

        public void Dispose()
        {
            if (brushes.IsCreated)
            {
                Clear();
                brushes.Dispose();
            }
            if (freeSlots.IsCreated)   freeSlots.Dispose();
            if (slotOf.IsCreated)      slotOf.Dispose();
            if (atPosition.IsCreated)  atPosition.Dispose();
            if (byTriple.IsCreated)    byTriple.Dispose();
            if (shortLinks.IsCreated)  shortLinks.Dispose();
            if (splitRoots.IsCreated)  splitRoots.Dispose();
            if (moves.IsCreated)       moves.Dispose();
            if (roundsAt.IsCreated)    roundsAt.Dispose();
            if (touched.IsCreated)     touched.Dispose();
            if (valid.IsCreated)       valid.Dispose();
            if (lastScope.IsCreated)   lastScope.Dispose();
            if (scope.IsCreated)       scope.Dispose();
            if (slotOfBrush.IsCreated) slotOfBrush.Dispose();
            if (sameBuffer.IsCreated)  sameBuffer.Dispose();
        }
    }

    internal static partial class OutputModelWeld
    {
        // Runs the full weld beside the incremental one and compares (FinishIncremental)
        public static bool VerifyIncremental;
        // and logs what each update welded when it is the same
        public static bool ReportIncremental;

        const float kRelationLength = 2 * kCollapseLength;

        static long Key(WeldTriangle t) => Key(t.slot, t.surface);

        static OutputModelWeld.TriangleKey Unordered(int3 a, int3 b, int3 c)
        {
            if (Less(b, a)) (a, b) = (b, a);
            if (Less(c, b)) (b, c) = (c, b);
            if (Less(b, a)) (a, b) = (b, a);
            return new TriangleKey { a = a, b = b, c = c };
        }

        static void RemoveOne<TKey, TValue>(NativeParallelMultiHashMap<TKey, TValue> map, TKey key, TValue value)
            where TKey : unmanaged, System.IEquatable<TKey> where TValue : unmanaged, System.IEquatable<TValue>
        {
            if (!map.TryGetFirstValue(key, out var item, out var iterator))
                return;
            do
            {
                if (item.Equals(value))
                {
                    map.Remove(iterator);
                    return;
                }
            } while (map.TryGetNextValue(out item, ref iterator));
        }

        // A brush's input triangles read into its slot and the index
        static void Load(ref WeldMemory memory, int slot, BlobAssetReference<ChiselBrushRenderBuffer> buffer)
        {
            ref var brush = ref memory.brushes.ElementAt(slot);
            brush.FreeInput();
            brush.input = buffer;
            ref var surfaces = ref buffer.Value.surfaces;
            brush.surfaceStart = new UnsafeList<int>(surfaces.Length + 1, Allocator.Persistent);
            var count = 0;
            for (int s = 0; s < surfaces.Length; s++)
            {
                brush.surfaceStart.Add(count);
                count += surfaces[s].indices.Length / 3;
            }
            brush.surfaceStart.Add(count);
            brush.corners = new UnsafeList<int3x3>(count, Allocator.Persistent);
            brush.needle  = new UnsafeList<bool>(count, Allocator.Persistent);
            brush.entries = new UnsafeList<WeldEntry>(0, Allocator.Persistent);
            brush.needle.Resize(count, NativeArrayOptions.ClearMemory);
            var limitSq = kRelationLength * kRelationLength;
            for (int s = 0; s < surfaces.Length; s++)
            {
                ref var indices   = ref surfaces[s].indices;
                ref var positions = ref surfaces[s].colliderVertices;
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    var p0 = positions[indices[t]]; var p1 = positions[indices[t + 1]]; var p2 = positions[indices[t + 2]];
                    var c = new int3x3(Bits(p0), Bits(p1), Bits(p2));
                    brush.corners.Add(c);
                    var me = new WeldTriangle { slot = slot, surface = s, triangle = t / 3 };
                    memory.atPosition.Add(c.c0, me);
                    memory.atPosition.Add(c.c1, me);
                    memory.atPosition.Add(c.c2, me);
                    memory.byTriple.Add(Unordered(c.c0, c.c1, c.c2), me);
                    Link(ref memory, p0, p1, c.c0, c.c1, me, limitSq);
                    Link(ref memory, p1, p2, c.c1, c.c2, me, limitSq);
                    Link(ref memory, p2, p0, c.c2, c.c0, me, limitSq);
                }
                ref var needles = ref surfaces[s].needles;
                for (int n = 0; n < needles.Length; n++)
                    brush.needle[brush.surfaceStart[s] + needles[n]] = true;
            }
        }

        static void Link(ref WeldMemory memory, float3 p, float3 q, int3 a, int3 b, WeldTriangle owner, float limitSq)
        {
            var d = math.distancesq(p, q);
            if (!(d > 0) || !(d < limitSq))
                return;
            memory.shortLinks.Add(a, new WeldShortLink { to = b, owner = owner });
            memory.shortLinks.Add(b, new WeldShortLink { to = a, owner = owner });
        }

        // A brush's input triangles taken out of the index, what the weld did to it forgotten
        static void Unload(ref WeldMemory memory, int slot)
        {
            ref var brush = ref memory.brushes.ElementAt(slot);
            if (!brush.corners.IsCreated)
                return;
            for (int s = 0; s + 1 < brush.surfaceStart.Length; s++)
            {
                memory.splitRoots.Remove(Key(slot, s));
                for (int k = brush.surfaceStart[s]; k < brush.surfaceStart[s + 1]; k++)
                {
                    var c = brush.corners[k];
                    var me = new WeldTriangle { slot = slot, surface = s, triangle = k - brush.surfaceStart[s] };
                    RemoveOne(memory.atPosition, c.c0, me);
                    RemoveOne(memory.atPosition, c.c1, me);
                    RemoveOne(memory.atPosition, c.c2, me);
                    RemoveOne(memory.byTriple, Unordered(c.c0, c.c1, c.c2), me);
                    Unlink(ref memory, c.c0, c.c1, me);
                    Unlink(ref memory, c.c1, c.c2, me);
                    Unlink(ref memory, c.c2, c.c0, me);
                }
            }
            for (int e = 0; brush.entries.IsCreated && e < brush.entries.Length; e++)
                ForgetAt(ref memory, slot, brush.entries[e]);
            brush.FreeInput();
        }

        // whether Link added it or not (a distance computed again may round otherwise)
        static void Unlink(ref WeldMemory memory, int3 a, int3 b, WeldTriangle owner)
        {
            RemoveOne(memory.shortLinks, a, new WeldShortLink { to = b, owner = owner });
            RemoveOne(memory.shortLinks, b, new WeldShortLink { to = a, owner = owner });
        }

        static void Visit(ref WeldMemory memory, NativeList<WeldTriangle> queue, WeldTriangle t)
        {
            if (memory.scope.Add(t))
                queue.Add(t);
        }

        // The scope grown, from the triangles in queue, to whole pieces, and to every split root of a surface one of whose
        // split roots it holds
        static void Grow(ref WeldMemory memory, NativeList<WeldTriangle> queue)
        {
            for (int q = 0; q < queue.Length; q++)
            {
                var t = queue[q];
                ref var brush = ref memory.brushes.ElementAt(t.slot);
                var k = brush.surfaceStart[t.surface] + t.triangle;
                var c = brush.corners[k];
                var needle = brush.needle[k];
                if (memory.byTriple.TryGetFirstValue(Unordered(c.c0, c.c1, c.c2), out var other, out var tripleIt))
                {
                    do { Visit(ref memory, queue, other); } while (memory.byTriple.TryGetNextValue(out other, ref tripleIt));
                }
                for (int i = 0; i < 3; i++)
                {
                    var p = c[i];
                    if (memory.atPosition.TryGetFirstValue(p, out other, out var it))
                    {
                        do
                        {
                            ref var at = ref memory.brushes.ElementAt(other.slot);
                            if (needle || at.needle[at.surfaceStart[other.surface] + other.triangle])
                                Visit(ref memory, queue, other);
                        } while (memory.atPosition.TryGetNextValue(out other, ref it));
                    }
                }
                // the surface's splits go together
                if (memory.splitRoots.TryGetFirstValue(Key(t), out var root, out var rootIt))
                {
                    var isRoot = false;
                    do { isRoot |= root == t.triangle; } while (memory.splitRoots.TryGetNextValue(out root, ref rootIt));
                    if (isRoot)
                    {
                        memory.splitRoots.TryGetFirstValue(Key(t), out root, out rootIt);
                        do { Visit(ref memory, queue, new WeldTriangle { slot = t.slot, surface = t.surface, triangle = root }); }
                        while (memory.splitRoots.TryGetNextValue(out root, ref rootIt));
                    }
                }
            }
        }

        // What the rounds did to the scope's triangles forgotten: their entries, what their surfaces' splits added; their
        // corners are touched (RecordIncremental clusters their point groups again)
        static void ForgetScope(ref WeldMemory memory)
        {
            var slots = new NativeParallelHashSet<int>(64, Allocator.Temp);
            foreach (var t in memory.scope)
            {
                ref var brush = ref memory.brushes.ElementAt(t.slot);
                if (!brush.corners.IsCreated)
                    continue;
                slots.Add(t.slot);
                var c = brush.corners[brush.surfaceStart[t.surface] + t.triangle];
                memory.touched.Add(c.c0); memory.touched.Add(c.c1); memory.touched.Add(c.c2);
            }
            foreach (var slot in slots)
            {
                ref var brush = ref memory.brushes.ElementAt(slot);
                brush.dirty = true;
                var entries = brush.entries;
                var kept = 0;
                for (int e = 0; e < entries.Length; e++)
                {
                    var entry = entries[e];
                    var gone = entry.root >= 0 ? memory.scope.Contains(new WeldTriangle { slot = slot, surface = entry.surface, triangle = entry.root })
                                               : SplitsInScope(ref memory, slot, entry.surface);
                    if (!gone)
                        entries[kept++] = entry;
                    else
                        ForgetAt(ref memory, slot, entry);
                }
                entries.Resize(kept);
                brush.entries = entries;
                for (int s = 0; s + 1 < brush.surfaceStart.Length; s++)
                {
                    if (SplitsInScope(ref memory, slot, s))
                        memory.splitRoots.Remove(Key(slot, s));
                }
            }
            slots.Dispose();
        }

        // A rounds entry's corners out of roundsAt, and touched
        static void ForgetAt(ref WeldMemory memory, int slot, WeldEntry entry)
        {
            if (entry.vertex || entry.corners.x < 0)
                return;
            var key = Key(slot, entry.surface);
            for (int i = 0; i < 3; i++)
            {
                RemoveOne(memory.roundsAt, entry.at[i], key);
                memory.touched.Add(entry.at[i]);
            }
        }

        static bool SplitsInScope(ref WeldMemory memory, int slot, int surface)
        {
            if (!memory.splitRoots.TryGetFirstValue(Key(slot, surface), out var root, out var it))
                return false;
            do
            {
                if (memory.scope.Contains(new WeldTriangle { slot = slot, surface = surface, triangle = root }))
                    return true;
            } while (memory.splitRoots.TryGetNextValue(out root, ref it));
            return false;
        }

        // The scope as the weld's steps read it, brush by brush
        static void BuildScope(NativeList<BrushData> brushes, ref WeldMemory memory, ref WeldState state)
        {
            var scope = state.scope;
            scope.all.Value = false;
            scope.surfaceBase.ResizeUninitialized(brushes.Length);
            scope.surfaceStart.Clear();
            scope.inside.Clear();
            var touched = new NativeParallelHashSet<int>(64, Allocator.Temp);
            foreach (var t in memory.scope)
                touched.Add(t.slot);
            for (int b = 0; b < brushes.Length; b++)
            {
                var slot = memory.slotOfBrush[b];
                if (!touched.Contains(slot))
                {
                    scope.surfaceBase[b] = -1;
                    continue;
                }
                ref var brush = ref memory.brushes.ElementAt(slot);
                scope.surfaceBase[b] = scope.surfaceStart.Length;
                var start = scope.inside.Length;
                for (int s = 0; s < brush.surfaceStart.Length; s++)
                    scope.surfaceStart.Add(start + brush.surfaceStart[s]);
                for (int s = 0; s + 1 < brush.surfaceStart.Length; s++)
                {
                    for (int t = 0; t < brush.surfaceStart[s + 1] - brush.surfaceStart[s]; t++)
                        scope.inside.Add(memory.scope.Contains(new WeldTriangle { slot = slot, surface = s, triangle = t }));
                }
            }
            touched.Dispose();
            state.scope = scope;
        }

        // Which triangles this update welds (state.scope): the pieces of the brushes that changed since the last update
        // (updated: the brushes whose buffers were made again), before and after; the memory's index brought up to date.
        // Everything when there is no memory yet or the brushes' order changed.
        public static void PrepareIncremental(NativeList<BrushData> brushes, NativeArray<IndexOrder> updated, ref WeldMemory memory, ref WeldState state)
        {
            var updatedIDs = new NativeParallelHashSet<CompactNodeID>(math.max(16, updated.Length), Allocator.Temp);
            for (int i = 0; i < updated.Length; i++)
                updatedIDs.Add(updated[i].compactNodeID);
            var full = !memory.valid.Value;
            for (int i = 0; i < memory.brushes.Length; i++)
            {
                ref var brush = ref memory.brushes.ElementAt(i);
                brush.live = brush.changed = brush.dirty = false;
            }
            memory.touched.Clear();
            memory.slotOfBrush.ResizeUninitialized(brushes.Length);
            var lastOrder = -1;
            for (int b = 0; b < brushes.Length; b++)
            {
                var id = brushes[b].brushIndexOrder.compactNodeID;
                if (!memory.slotOf.TryGetValue(id, out var slot))
                {
                    if (memory.freeSlots.Length > 0)
                    {
                        slot = memory.freeSlots[memory.freeSlots.Length - 1];
                        memory.freeSlots.RemoveAt(memory.freeSlots.Length - 1);
                        memory.brushes[slot] = default;
                    } else
                    {
                        slot = memory.brushes.Length;
                        memory.brushes.Add(default);
                    }
                    memory.slotOf.Add(id, slot);
                    ref var added = ref memory.brushes.ElementAt(slot);
                    added.id = id;
                    added.used = true;
                    added.order = -1;
                    added.changed = true;
                } else
                {
                    ref var known = ref memory.brushes.ElementAt(slot);
                    if (known.order <= lastOrder)
                        full = true;
                    lastOrder = known.order;
                    known.changed = updatedIDs.Contains(id) || !known.input.Equals(brushes[b].brushRenderBuffer);
                }
                ref var item = ref memory.brushes.ElementAt(slot);
                item.live = true;
                memory.slotOfBrush[b] = slot;
            }
            updatedIDs.Dispose();
            var changedCount = 0;
            for (int b = 0; b < brushes.Length; b++)
            {
                ref var brush = ref memory.brushes.ElementAt(memory.slotOfBrush[b]);
                brush.order = b;
                if (brush.changed)
                    changedCount++;
            }
            // most of the model: welding all of it is cheaper than finding its pieces
            if (changedCount * 2 > brushes.Length)
                full = true;

            memory.scope.Clear();
            if (full)
            {
                // the brushes, as they are now, in new slots
                memory.Clear();
                memory.valid.Value = true;
                for (int b = 0; b < brushes.Length; b++)
                {
                    var id = brushes[b].brushIndexOrder.compactNodeID;
                    memory.slotOf.Add(id, b);
                    memory.brushes.Add(new WeldBrush { id = id, used = true, order = b, live = true, changed = true, dirty = true });
                    memory.slotOfBrush.Add(b);
                    Load(ref memory, b, brushes[b].brushRenderBuffer);
                }
                state.scope.all.Value = true;
                memory.lastScope.Value = -1;
                return;
            }

            var queue  = new NativeList<WeldTriangle>(256, Allocator.Temp);
            // the pieces the changed brushes were in
            for (int slot = 0; slot < memory.brushes.Length; slot++)
            {
                ref var brush = ref memory.brushes.ElementAt(slot);
                if (!brush.corners.IsCreated || (brush.live && !brush.changed))
                    continue;
                for (int s = 0; s + 1 < brush.surfaceStart.Length; s++)
                {
                    for (int t = 0; t < brush.surfaceStart[s + 1] - brush.surfaceStart[s]; t++)
                        Visit(ref memory, queue, new WeldTriangle { slot = slot, surface = s, triangle = t });
                }
            }
            Grow(ref memory, queue);
            ForgetScope(ref memory);

            // the changed brushes taken out, the gone ones freed, the others read again
            for (int slot = 0; slot < memory.brushes.Length; slot++)
            {
                ref var brush = ref memory.brushes.ElementAt(slot);
                if (brush.live && !brush.changed)
                    continue;
                if (!brush.live)
                {
                    if (brush.used)
                    {
                        Unload(ref memory, slot);
                        if (brush.output.IsCreated)
                            brush.output.Dispose();
                        memory.slotOf.Remove(brush.id);
                        memory.brushes[slot] = default;
                        memory.freeSlots.Add(slot);
                    }
                    continue;
                }
                Unload(ref memory, slot);
                brush.dirty = true;
            }
            var survivors = new NativeList<WeldTriangle>(memory.scope.Count(), Allocator.Temp);
            foreach (var t in memory.scope)
            {
                ref var brush = ref memory.brushes.ElementAt(t.slot);
                if (brush.live && !brush.changed)
                    survivors.Add(t);
            }
            memory.scope.Clear();
            queue.Clear();
            for (int b = 0; b < brushes.Length; b++)
            {
                var slot = memory.slotOfBrush[b];
                ref var brush = ref memory.brushes.ElementAt(slot);
                if (!brush.changed)
                    continue;
                Load(ref memory, slot, brushes[b].brushRenderBuffer);
            }
            // the pieces they are in now, and those the old pieces' other triangles are in
            for (int b = 0; b < brushes.Length; b++)
            {
                var slot = memory.slotOfBrush[b];
                ref var brush = ref memory.brushes.ElementAt(slot);
                if (!brush.changed)
                    continue;
                for (int s = 0; s + 1 < brush.surfaceStart.Length; s++)
                {
                    for (int t = 0; t < brush.surfaceStart[s + 1] - brush.surfaceStart[s]; t++)
                        Visit(ref memory, queue, new WeldTriangle { slot = slot, surface = s, triangle = t });
                }
            }
            for (int i = 0; i < survivors.Length; i++)
                Visit(ref memory, queue, survivors[i]);
            Grow(ref memory, queue);
            ForgetScope(ref memory);
            BuildScope(brushes, ref memory, ref state);
            memory.lastScope.Value = memory.scope.Count();
            survivors.Dispose(); queue.Dispose();
        }

        // After PlanWeldBeforeCollapse: a surface the scope split that another piece's splits added to as well takes that
        // piece in (true: plan again)
        public static bool TakeInSplitSurfaces(NativeList<BrushData> brushes, ref WeldMemory memory, ref WeldState state)
        {
            if (state.scope.all.Value)
                return false;
            var queue = new NativeList<WeldTriangle>(16, Allocator.Temp);
            for (int w = 0; w < state.work.Length; w++)
            {
                var work = state.work[w];
                if (!work.splitRoots.IsCreated || work.splitRoots.Length == 0)
                    continue;
                var slot = memory.slotOfBrush[work.brush];
                if (!memory.splitRoots.TryGetFirstValue(Key(slot, work.surface), out var root, out var it))
                    continue;
                do { Visit(ref memory, queue, new WeldTriangle { slot = slot, surface = work.surface, triangle = root }); }
                while (memory.splitRoots.TryGetNextValue(out root, ref it));
            }
            var grew = queue.Length > 0;
            if (grew)
            {
                Grow(ref memory, queue);
                ForgetScope(ref memory);
                BuildScope(brushes, ref memory, ref state);
                memory.lastScope.Value = memory.scope.Count();
            }
            queue.Dispose();
            return grew;
        }

        // After the rounds (PlanWeldBeforeCollapse): what they did to the scope's surfaces kept as entries of their brushes,
        // with where each triangle's corners are (roundsAt, touched)
        public static void RecordRounds(NativeList<BrushData> brushes, ref WeldMemory memory, ref WeldState state)
        {
            var work = state.work;
            for (int w = 0; w < work.Length; w++)
            {
                var item = work[w];
                if (item.CollapseOnly)
                    continue;
                var slot = memory.slotOfBrush[item.brush];
                ref var brush = ref memory.brushes.ElementAt(slot);
                brush.dirty = true;
                ref var source = ref brushes[item.brush].brushRenderBuffer.Value.surfaces[item.surface];
                var sourceTriangles = source.indices.Length / 3;
                var sourceVertices  = source.colliderVertices.Length;
                var key = Key(slot, item.surface);
                for (int t = 0; t < item.indices.Length / 3; t++)
                {
                    var corners = new int3(item.indices[t * 3], item.indices[t * 3 + 1], item.indices[t * 3 + 2]);
                    if (t < sourceTriangles && math.all(corners == new int3(source.indices[t * 3], source.indices[t * 3 + 1], source.indices[t * 3 + 2])))
                        continue;
                    var entry = new WeldEntry { surface = item.surface, index = t, root = t < sourceTriangles ? t : -1, corners = corners };
                    if (corners.x >= 0)
                    {
                        entry.at = new int3x3(Bits(item.collider[corners.x]), Bits(item.collider[corners.y]), Bits(item.collider[corners.z]));
                        for (int i = 0; i < 3; i++)
                        {
                            memory.roundsAt.Add(entry.at[i], key);
                            memory.touched.Add(entry.at[i]);
                        }
                    }
                    brush.entries.Add(entry);
                }
                // the first triangle left that uses each vertex, as an input triangle (-1: only what a split added does)
                var firstUser = new NativeArray<int>(item.collider.Length, Allocator.Temp);
                for (int v = 0; v < firstUser.Length; v++)
                    firstUser[v] = -2;
                for (int i = 0; i < item.indices.Length; i++)
                {
                    var v = item.indices[i];
                    if (v >= 0 && firstUser[v] == -2)
                        firstUser[v] = i / 3 < sourceTriangles ? i / 3 : -1;
                }
                for (int v = 0; v < item.collider.Length; v++)
                {
                    if (v < sourceVertices && SameVertex(ref source, v, item))
                        continue;
                    brush.entries.Add(new WeldEntry { surface = item.surface, vertex = true, index = v, root = v < sourceVertices ? math.max(-1, firstUser[v]) : -1,
                                                      collider = item.collider[v], select = item.select[v], render = item.render[v] });
                }
                firstUser.Dispose();
                if (item.splitRoots.IsCreated)
                {
                    for (int r = 0; r < item.splitRoots.Length; r++)
                        memory.splitRoots.Add(key, item.splitRoots[r]);
                }
            }
        }

        // After ApplyCollapse: the collapse of the point groups the rounds touched (IncrementalWeld.md, step 3), then every
        // brush near them or the rounds changed made again from its buffer, its rounds and the moves, stored again
        // (state.copies) unless that is what it had; moves: every move the weld makes. Welding everything, the collapse
        // jobs did it: what they made is kept.
        public static void RecordIncremental(NativeList<BrushData> brushes, ref WeldMemory memory, ref WeldState state, NativeList<float3x2> moves)
        {
            if (state.scope.all.Value)
            {
                RecordFull(brushes, ref memory, ref state, moves);
                return;
            }
            var limitSq = kCollapseLength * kCollapseLength;
            var brushOfSlot = new NativeArray<int>(memory.brushes.Length, Allocator.Temp);
            for (int i = 0; i < brushOfSlot.Length; i++)
                brushOfSlot[i] = -1;
            for (int b = 0; b < brushes.Length; b++)
                brushOfSlot[memory.slotOfBrush[b]] = b;

            // the point groups the rounds touched: every point linked to a touched one
            var points = new NativeParallelHashSet<int3>(math.max(64, memory.touched.Length * 2), Allocator.Temp);
            var stack  = new NativeList<int3>(64, Allocator.Temp);
            for (int i = 0; i < memory.touched.Length; i++)
            {
                var p = memory.touched[i];
                if (!memory.shortLinks.ContainsKey(p) || !points.Add(p))
                    continue;
                stack.Add(p);
                while (stack.Length > 0)
                {
                    var q = stack[stack.Length - 1];
                    stack.RemoveAt(stack.Length - 1);
                    if (memory.shortLinks.TryGetFirstValue(q, out var link, out var it))
                    {
                        do
                        {
                            if (points.Add(link.to))
                                stack.Add(link.to);
                        } while (memory.shortLinks.TryGetNextValue(out link, ref it));
                    }
                }
            }
            stack.Dispose();

            // the surfaces with a triangle, as the rounds left it, with a corner in them: (brush, surface)
            var surfaces = new NativeParallelHashSet<long>(64, Allocator.Temp);
            foreach (var p in points)
            {
                if (memory.atPosition.TryGetFirstValue(p, out var triangle, out var it))
                {
                    do
                    {
                        var b = brushOfSlot[triangle.slot];
                        if (b >= 0) surfaces.Add(Key(b, triangle.surface));
                    } while (memory.atPosition.TryGetNextValue(out triangle, ref it));
                }
                if (memory.roundsAt.TryGetFirstValue(p, out var key, out var kit))
                {
                    do
                    {
                        var b = brushOfSlot[(int)(key >> 32)];
                        if (b >= 0) surfaces.Add(Key(b, (int)(uint)key));
                    } while (memory.roundsAt.TryGetNextValue(out key, ref kit));
                }
            }

            // their short edges and kept corners (ShortEdgesOf), clustered (ClusterShortEdges)
            var pairs = new NativeList<float3x2>(64, Allocator.Temp);
            var kept  = new UnsafeHashSet<float3>(64, Allocator.Temp);
            foreach (var key in surfaces)
            {
                var b = (int)(key >> 32);
                var s = (int)(uint)key;
                ref var source = ref brushes[b].brushRenderBuffer.Value.surfaces[s];
                // a decal's pieces follow the corners that move, but their short edges do not move anything
                if (source.decalEntityID != 0)
                    continue;
                var after = AfterRounds(brushes, ref memory, ref state, b, s, Allocator.Temp, out var temporary);
                var sourceTriangles = source.indices.Length / 3;
                for (int t = 0; t < after.indices.Length / 3; t++)
                {
                    var c0 = after.indices[t * 3]; var c1 = after.indices[t * 3 + 1]; var c2 = after.indices[t * 3 + 2];
                    if (c0 < 0)
                        continue;
                    var q0 = after.collider[c0] + 0.0f;
                    var q1 = after.collider[c1] + 0.0f;
                    var q2 = after.collider[c2] + 0.0f;
                    if (!points.Contains(Bits(q0)) && !points.Contains(Bits(q1)) && !points.Contains(Bits(q2)))
                        continue;
                    var changed = t >= sourceTriangles ||
                                  c0 != source.indices[t * 3] || c1 != source.indices[t * 3 + 1] || c2 != source.indices[t * 3 + 2];
                    if (IsNeedle(q0, q1, q2) || changed)
                    {
                        kept.Add(q0); kept.Add(q1); kept.Add(q2);
                        continue;
                    }
                    // a short edge's corners are in one point group: those of the groups not clustered again stay out
                    AddShort(pairs, points, q0, q1, limitSq); AddShort(pairs, points, q1, q2, limitSq); AddShort(pairs, points, q2, q0, limitSq);
                }
                if (temporary)
                    FreeWorking(after);
            }
            var movedNow = new NativeList<float3x2>(64, Allocator.Temp);
            ClusterPairs(pairs, kept, movedNow);
            pairs.Dispose(); kept.Dispose();

            // the moves of those groups, and of the corners that are not in one any more, replaced
            for (int i = 0; i < memory.touched.Length; i++)
                memory.moves.Remove(memory.touched[i]);
            foreach (var p in points)
                memory.moves.Remove(p);
            for (int i = 0; i < movedNow.Length; i++)
                memory.moves[Bits(movedNow[i].c0)] = movedNow[i];
            movedNow.Dispose();
            if (moves.IsCreated)
            {
                moves.Clear();
                foreach (var pair in memory.moves)
                    moves.Add(pair.Value);
            }

            // every brush near them made again; one the same as it was keeps its buffer
            foreach (var key in surfaces)
                memory.brushes.ElementAt(memory.slotOfBrush[(int)(key >> 32)]).dirty = true;
            WeldState.FreeWork(state.work);
            state.work.Clear();
            state.workIndex.Clear();
            state.copies.Clear();
            state.decisions.Clear();
            var final = new NativeList<WeldEntry>(64, Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
            {
                ref var brush = ref memory.brushes.ElementAt(memory.slotOfBrush[b]);
                if (!brush.dirty)
                    continue;
                var first = state.work.Length;
                final.Clear();
                ref var surfacesOf = ref brushes[b].brushRenderBuffer.Value.surfaces;
                for (int s = 0; s < surfacesOf.Length; s++)
                {
                    var copy = AfterRounds(brushes, ref memory, ref state, b, s, Allocator.Persistent, out _);
                    if (!Collapse(ref memory, ref copy) && !HasRounds(ref brush, s))
                    {
                        FreeWorking(copy);
                        continue;
                    }
                    copy.brush = b;
                    copy.surface = s;
                    state.workIndex.TryAdd(Key(b, s), state.work.Length);
                    state.work.Add(copy);
                    Diff(ref surfacesOf[s], copy, final);
                }
                if (!brush.changed && SameEntries(brush.final, final))
                {
                    brush.dirty = false;
                    for (int w = first; w < state.work.Length; w++)
                    {
                        state.workIndex.Remove(Key(b, state.work[w].surface));
                        FreeWorking(state.work[w]);
                    }
                    state.work.ResizeUninitialized(first);
                    continue;
                }
                SetFinal(ref brush, final);
                if (state.work.Length > first)
                    state.copies.Add(new WeldCopy { brush = b });
            }
            final.Dispose();
            surfaces.Dispose(); points.Dispose(); brushOfSlot.Dispose();
        }

        // Welding everything: the moves the collapse made, and what the weld made of every brush
        static void RecordFull(NativeList<BrushData> brushes, ref WeldMemory memory, ref WeldState state, NativeList<float3x2> moves)
        {
            memory.moves.Clear();
            if (moves.IsCreated)
            {
                for (int m = 0; m < moves.Length; m++)
                    memory.moves[Bits(moves[m].c0)] = moves[m];
            }
            var final = new NativeList<WeldEntry>(64, Allocator.Temp);
            for (int b = 0; b < brushes.Length; b++)
            {
                ref var brush = ref memory.brushes.ElementAt(memory.slotOfBrush[b]);
                final.Clear();
                if (brushes[b].brushRenderBuffer.IsCreated)
                {
                    ref var surfacesOf = ref brushes[b].brushRenderBuffer.Value.surfaces;
                    for (int s = 0; s < surfacesOf.Length; s++)
                    {
                        var w = Find(state.workIndex, b, s);
                        if (w < 0)
                            continue;
                        var item = state.work[w];
                        if (item.CollapseOnly)
                        {
                            var made = Materialize(ref surfacesOf[s], item, state.decisions);
                            Diff(ref surfacesOf[s], made, final);
                            made.indices.Dispose(); made.collider.Dispose(); made.select.Dispose(); made.render.Dispose();
                        } else
                            Diff(ref surfacesOf[s], item, final);
                    }
                }
                SetFinal(ref brush, final);
            }
            final.Dispose();
        }

        static void SetFinal(ref WeldBrush brush, NativeList<WeldEntry> final)
        {
            if (!brush.final.IsCreated)
                brush.final = new UnsafeList<WeldEntry>(final.Length, Allocator.Persistent);
            brush.final.Clear();
            for (int i = 0; i < final.Length; i++)
                brush.final.Add(final[i]);
        }

        static bool HasRounds(ref WeldBrush brush, int surface)
        {
            for (int e = 0; brush.entries.IsCreated && e < brush.entries.Length; e++)
            {
                if (brush.entries[e].surface == surface)
                    return true;
            }
            return false;
        }

        static void AddShort(NativeList<float3x2> pairs, NativeParallelHashSet<int3> points, float3 x, float3 y, float limitSq)
        {
            var d = math.distancesq(x, y);
            if (!(d > 0) || !(d < limitSq) || !points.Contains(Bits(x)))
                return;
            pairs.Add(new float3x2(x, y));
        }

        // ClusterShortEdges for these short edges and kept corners: (from, to) of every corner that moves
        static void ClusterPairs(NativeList<float3x2> pairs, UnsafeHashSet<float3> keptPoints, NativeList<float3x2> moved)
        {
            var ids    = new UnsafeHashMap<float3, int>(64, Allocator.Temp);
            var points = new NativeList<float3>(64, Allocator.Temp);
            var edges  = new NativeList<ShortEdge>(64, Allocator.Temp);
            int IdOf(float3 p)
            {
                if (!ids.TryGetValue(p, out var id)) { id = points.Length; ids.Add(p, id); points.Add(p); }
                return id;
            }
            for (int e = 0; e < pairs.Length; e++)
            {
                var x = pairs[e].c0; var y = pairs[e].c1;
                if (keptPoints.Contains(x) || keptPoints.Contains(y))
                    continue;
                int a = IdOf(x), c = IdOf(y);
                edges.Add(new ShortEdge { a = math.min(a, c), b = math.max(a, c), length = math.distance(x, y) });
            }
            if (edges.Length > 0)
            {
                var to = new NativeArray<float3>(points.Length, Allocator.Temp);
                Cluster(points.AsArray(), edges, to);
                for (int i = 0; i < points.Length; i++)
                {
                    if (!to[i].Equals(points[i]))
                        moved.Add(new float3x2(points[i], to[i]));
                }
                to.Dispose();
            }
            edges.Dispose(); points.Dispose(); ids.Dispose();
        }

        // A surface as the rounds left it: the run's working copy, or the buffer with the rounds kept for it
        static unsafe Working AfterRounds(NativeList<BrushData> brushes, ref WeldMemory memory, ref WeldState state, int b, int s, Allocator allocator,
                                          out bool temporary)
        {
            var w = Find(state.workIndex, b, s);
            ref var source = ref brushes[b].brushRenderBuffer.Value.surfaces[s];
            var copy = new Working { brush = b, surface = s };
            if (w >= 0 && allocator == Allocator.Temp && !state.work[w].CollapseOnly)
            {
                temporary = false;
                return state.work[w];
            }
            temporary = true;
            copy.indices  = new UnsafeList<int>(source.indices.Length, allocator);
            copy.collider = new UnsafeList<float3>(source.colliderVertices.Length, allocator);
            copy.select   = new UnsafeList<SelectVertex>(source.selectVertices.Length, allocator);
            copy.render   = new UnsafeList<RenderVertex>(source.renderVertices.Length, allocator);
            copy.indices .AddRange(source.indices.GetUnsafePtr(),          source.indices.Length);
            copy.collider.AddRange(source.colliderVertices.GetUnsafePtr(), source.colliderVertices.Length);
            copy.select  .AddRange(source.selectVertices.GetUnsafePtr(),   source.selectVertices.Length);
            copy.render  .AddRange(source.renderVertices.GetUnsafePtr(),   source.renderVertices.Length);
            ref var brush = ref memory.brushes.ElementAt(memory.slotOfBrush[b]);
            for (int e = 0; brush.entries.IsCreated && e < brush.entries.Length; e++)
            {
                var entry = brush.entries[e];
                if (entry.surface != s)
                    continue;
                if (entry.vertex)
                {
                    while (copy.collider.Length <= entry.index)
                    {
                        copy.collider.Add(default); copy.select.Add(default); copy.render.Add(default);
                    }
                    copy.collider[entry.index] = entry.collider;
                    copy.select[entry.index]   = entry.select;
                    copy.render[entry.index]   = entry.render;
                } else
                {
                    while (copy.indices.Length < entry.index * 3 + 3)
                        copy.indices.Add(-1);
                    copy.indices[entry.index * 3]     = entry.corners.x;
                    copy.indices[entry.index * 3 + 1] = entry.corners.y;
                    copy.indices[entry.index * 3 + 2] = entry.corners.z;
                }
            }
            return copy;
        }

        static void FreeWorking(Working item)
        {
            if (item.indices.IsCreated)  item.indices.Dispose();
            if (item.collider.IsCreated) item.collider.Dispose();
            if (item.select.IsCreated)   item.select.Dispose();
            if (item.render.IsCreated)   item.render.Dispose();
            if (item.splitRoots.IsCreated) item.splitRoots.Dispose();
        }

        // The collapse of one surface as the rounds left it (DecideCollapse, ApplyCollapse): a triangle with a corner that
        // moves and left without area dropped, the corners of the triangles left that move moved; false when nothing moves
        static bool Collapse(ref WeldMemory memory, ref Working copy)
        {
            var count = copy.collider.Length;
            if (count == 0 || memory.moves.Count() == 0)
                return false;
            var targets = new NativeArray<float3>(count, Allocator.Temp);
            var movedAt = new NativeArray<bool>(count, Allocator.Temp);
            var any = false;
            for (int v = 0; v < count; v++)
            {
                if (!memory.moves.TryGetValue(Bits(copy.collider[v] + 0.0f), out var move))
                    continue;
                targets[v] = move.c1;
                movedAt[v] = true;
                any = true;
            }
            if (any)
            {
                for (int t = 0; t + 2 < copy.indices.Length; t += 3)
                {
                    var c0 = copy.indices[t]; var c1 = copy.indices[t + 1]; var c2 = copy.indices[t + 2];
                    if (c0 < 0 || (!movedAt[c0] && !movedAt[c1] && !movedAt[c2]))
                        continue;
                    var p0 = movedAt[c0] ? targets[c0] : copy.collider[c0];
                    var p1 = movedAt[c1] ? targets[c1] : copy.collider[c1];
                    var p2 = movedAt[c2] ? targets[c2] : copy.collider[c2];
                    if (OutputWeld.WithoutArea(p0, p1, p2))
                        copy.indices[t] = copy.indices[t + 1] = copy.indices[t + 2] = -1;
                }
                var used = new NativeArray<bool>(count, Allocator.Temp);
                for (int t = 0; t + 2 < copy.indices.Length; t += 3)
                {
                    if (copy.indices[t] < 0)
                        continue;
                    used[copy.indices[t]] = used[copy.indices[t + 1]] = used[copy.indices[t + 2]] = true;
                }
                for (int v = 0; v < count; v++)
                {
                    if (used[v] && movedAt[v])
                        MoveCorner(ref copy, v, targets[v]);
                }
                used.Dispose();
            }
            targets.Dispose(); movedAt.Dispose();
            return any;
        }

        // Where a surface of the weld's differs from the buffer's (as final entries)
        static void Diff(ref ChiselSurfaceRenderBuffer source, Working item, NativeList<WeldEntry> final)
        {
            var sourceTriangles = source.indices.Length / 3;
            var sourceVertices  = source.colliderVertices.Length;
            for (int t = 0; t < item.indices.Length / 3; t++)
            {
                var corners = new int3(item.indices[t * 3], item.indices[t * 3 + 1], item.indices[t * 3 + 2]);
                if (t < sourceTriangles && math.all(corners == new int3(source.indices[t * 3], source.indices[t * 3 + 1], source.indices[t * 3 + 2])))
                    continue;
                final.Add(new WeldEntry { surface = item.surface, index = t, corners = corners });
            }
            for (int v = 0; v < item.collider.Length; v++)
            {
                if (v < sourceVertices && SameVertex(ref source, v, item))
                    continue;
                final.Add(new WeldEntry { surface = item.surface, vertex = true, index = v, collider = item.collider[v], select = item.select[v], render = item.render[v] });
            }
        }

        struct BySurfaceAndIndex : System.Collections.Generic.IComparer<WeldEntry>
        {
            public int Compare(WeldEntry x, WeldEntry y)
            {
                if (x.surface != y.surface) return x.surface.CompareTo(y.surface);
                if (x.vertex != y.vertex)   return x.vertex.CompareTo(y.vertex);
                return x.index.CompareTo(y.index);
            }
        }

        // The same final entries: what Copy makes of them is the same
        static unsafe bool SameEntries(UnsafeList<WeldEntry> before, NativeList<WeldEntry> now)
        {
            var count = before.IsCreated ? before.Length : 0;
            if (count != now.Length)
                return false;
            if (count == 0)
                return true;
            var a = new NativeArray<WeldEntry>(count, Allocator.Temp);
            var f = new NativeArray<WeldEntry>(count, Allocator.Temp);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    a[i] = now[i];
                    f[i] = before[i];
                }
                a.Sort(new BySurfaceAndIndex());
                f.Sort(new BySurfaceAndIndex());
                for (int i = 0; i < count; i++)
                {
                    var x = a[i]; var y = f[i];
                    if (x.surface != y.surface || x.vertex != y.vertex || x.index != y.index)
                        return false;
                    if (x.vertex)
                    {
                        if (!math.all(math.asint(x.collider) == math.asint(y.collider)) ||
                            UnsafeUtility.MemCmp(&x.select, &y.select, UnsafeUtility.SizeOf<SelectVertex>()) != 0 ||
                            UnsafeUtility.MemCmp(&x.render, &y.render, UnsafeUtility.SizeOf<RenderVertex>()) != 0)
                            return false;
                    } else
                    if (!math.all(x.corners == y.corners))
                        return false;
                }
                return true;
            }
            finally
            {
                a.Dispose(); f.Dispose();
            }
        }

        static unsafe bool SameVertex(ref ChiselSurfaceRenderBuffer source, int v, Working item)
        {
            return math.all(math.asint(source.colliderVertices[v]) == math.asint(item.collider[v])) &&
                   UnsafeUtility.MemCmp((SelectVertex*)source.selectVertices.GetUnsafePtr() + v, item.select.Ptr + v, UnsafeUtility.SizeOf<SelectVertex>()) == 0 &&
                   UnsafeUtility.MemCmp((RenderVertex*)source.renderVertices.GetUnsafePtr() + v, item.render.Ptr + v, UnsafeUtility.SizeOf<RenderVertex>()) == 0;
        }

        // A surface only the collapse changed, as Copy makes it
        static Working Materialize(ref ChiselSurfaceRenderBuffer from, Working item, NativeList<CollapseDecision> decisions)
        {
            var copy = item;
            copy.indices  = new UnsafeList<int>(from.indices.Length, Allocator.Temp);
            copy.collider = new UnsafeList<float3>(from.colliderVertices.Length, Allocator.Temp);
            copy.select   = new UnsafeList<SelectVertex>(from.selectVertices.Length, Allocator.Temp);
            copy.render   = new UnsafeList<RenderVertex>(from.renderVertices.Length, Allocator.Temp);
            unsafe
            {
                copy.indices .AddRange(from.indices.GetUnsafePtr(),          from.indices.Length);
                copy.collider.AddRange(from.colliderVertices.GetUnsafePtr(), from.colliderVertices.Length);
                copy.select  .AddRange(from.selectVertices.GetUnsafePtr(),   from.selectVertices.Length);
                copy.render  .AddRange(from.renderVertices.GetUnsafePtr(),   from.renderVertices.Length);
            }
            for (int m = copy.decisionStart; m < copy.decisionStart + copy.decisionCount; m++)
            {
                var decision = decisions[m];
                if (decision.move)
                    MoveCorner(ref copy, decision.index, decision.to);
                else
                    copy.indices[decision.index * 3] = copy.indices[decision.index * 3 + 1] = copy.indices[decision.index * 3 + 2] = -1;
            }
            return copy;
        }

        // The copies handed to their brushes and kept; every other brush gets what it was given last time, or its own
        // buffer. verify: the full weld run beside it, any difference logged
        public static void FinishIncremental(NativeList<BrushData> brushes, NativeList<WeldCopy> copies, ref WeldMemory memory, bool verify, bool report, NativeList<float3x2> moves)
        {
            NativeList<BrushData> reference = default;
            NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> referencePatched = default;
            NativeList<float3x2> referenceMoves = default;
            if (verify)
            {
                reference        = new NativeList<BrushData>(brushes.Length, Allocator.Temp);
                referencePatched = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(16, Allocator.Temp);
                referenceMoves   = new NativeList<float3x2>(16, Allocator.Temp);
                reference.AddRange(brushes.AsArray());
                WeldModel(reference, referencePatched, out _, out _, out _, out _, referenceMoves);
            }

            // neither its input nor what the weld made of it changed
            memory.sameBuffer.ResizeUninitialized(brushes.Length);
            for (int b = 0; b < brushes.Length; b++)
            {
                ref var brush = ref memory.brushes.ElementAt(memory.slotOfBrush[b]);
                memory.sameBuffer[b] = !brush.changed && !brush.dirty;
            }
            for (int i = 0; i < copies.Length; i++)
            {
                ref var brush = ref memory.brushes.ElementAt(memory.slotOfBrush[copies[i].brush]);
                if (brush.output.IsCreated)
                    brush.output.Dispose();
                brush.output = copies[i].result;
                brush.dirty = false;
            }
            for (int b = 0; b < brushes.Length; b++)
            {
                ref var brush = ref memory.brushes.ElementAt(memory.slotOfBrush[b]);
                if (brush.dirty && brush.output.IsCreated)
                {
                    // stored again with nothing the weld changed: its own buffer
                    brush.output.Dispose();
                    brush.output = default;
                }
                brush.dirty = false;
                var data = brushes[b];
                if (brush.output.IsCreated)
                {
                    KeepHotspotGroups(data.brushRenderBuffer, brush.output);
                    data.brushRenderBuffer = brush.output;
                }
                brushes[b] = data;
            }

            if (!verify)
                return;
            var different = 0;
            var first = -1;
            for (int b = 0; b < brushes.Length; b++)
            {
                if (SameBuffer(brushes[b].brushRenderBuffer, reference[b].brushRenderBuffer))
                    continue;
                if (first < 0) first = b;
                different++;
            }
            var movesDiffer = referenceMoves.Length != moves.Length;
            for (int m = 0; m < referenceMoves.Length && !movesDiffer; m++)
            {
                movesDiffer = !memory.moves.TryGetValue(Bits(referenceMoves[m].c0), out var move) ||
                              !math.all(math.asint(move.c1) == math.asint(referenceMoves[m].c1));
            }
            var movesDifferent = movesDiffer ? 1 : 0;
            if (different > 0 || movesDiffer)
                Debug.LogError($"The incremental weld differs from the full weld: {different} of {brushes.Length} brushes (first {first}), moves differ: {movesDifferent} ({moves.Length} and {referenceMoves.Length}); scope {memory.lastScope.Value}");
            else if (report)
                Debug.Log($"The incremental weld is the full weld: scope {memory.lastScope.Value}, {copies.Length} brushes stored again of {brushes.Length}");
            for (int i = 0; i < referencePatched.Length; i++)
                referencePatched[i].Dispose();
            reference.Dispose(); referencePatched.Dispose(); referenceMoves.Dispose();
        }

        // The weld as an update's jobs run it, one step after another; updated: the brushes whose buffers are new
        public static void WeldIncremental(NativeList<BrushData> brushes, NativeArray<IndexOrder> updated, ref WeldMemory memory, NativeList<float3x2> moves)
        {
            var state = WeldState.Create(Allocator.Temp);
            try
            {
                PrepareIncremental(brushes, updated, ref memory, ref state);
                do
                    PlanWeldBeforeCollapse(brushes, ref state, out _, out _, out _, moves);
                while (TakeInSplitSurfaces(brushes, ref memory, ref state));
                RecordRounds(brushes, ref memory, ref state);
                Collapse(brushes, ref state, moves);
                RecordIncremental(brushes, ref memory, ref state, moves);
                var copies = state.copies.AsArray();
                for (int i = 0; i < copies.Length; i++)
                    copies[i] = CopyPlanned(copies[i], brushes.AsArray(), state.work, state.workIndex, state.decisions, default);
                FinishIncremental(brushes, state.copies, ref memory, VerifyIncremental, ReportIncremental, moves);
            }
            finally
            {
                state.Dispose();
            }
        }

        // What the hotspot cut writes into a brush's own buffer in place (OutputHotspotSections.StartCutKeys), as Copy
        // would have copied it
        static void KeepHotspotGroups(BlobAssetReference<ChiselBrushRenderBuffer> input, BlobAssetReference<ChiselBrushRenderBuffer> output)
        {
            ref var from = ref input.Value.surfaces;
            ref var to   = ref output.Value.surfaces;
            for (int s = 0; s < from.Length && s < to.Length; s++)
            {
                to[s].hotspot.group           = from[s].hotspot.group;
                to[s].hotspot.groupVariations = from[s].hotspot.groupVariations;
            }
        }

        internal static unsafe bool SameBuffer(BlobAssetReference<ChiselBrushRenderBuffer> a, BlobAssetReference<ChiselBrushRenderBuffer> b)
        {
            if (a.Equals(b))
                return true;
            if (!a.IsCreated || !b.IsCreated)
                return false;
            var pa = (byte*)a.GetUnsafePtr(); var pb = (byte*)b.GetUnsafePtr();
            var la = *(int*)(pa - 32 + 8);    var lb = *(int*)(pb - 32 + 8);
            return la == lb && UnsafeUtility.MemCmp(pa, pb, la) == 0;
        }
    }
}

using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Chisel.Core.Tests
{
    [TestFixture]
    public class RoutingTableCapacityTests
    {
        // What CreateRoutingTableJob allocates for the event stack (CreateRoutingTableJob.cs:77).
        const int kQueuedEventCapacity = 4096;

        static CompactNodeID NodeID(int index)
        {
            return new CompactNodeID(new CompactHierarchyID(new SlotIndex { index = 1, generation = 1 }),
                                     new SlotIndex { index = index, generation = 1 });
        }

        /// <summary>
        /// One additive branch with <paramref name="childCount"/> additive brush children, indices 1..childCount.
        /// </summary>
        static BlobAssetReference<CompactTree> Tree(int childCount)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<CompactTree>();

            var nodes = builder.Allocate(ref root.compactHierarchy, childCount + 1);
            nodes[0] = new CompactHierarchyNode
            {
                Type          = CSGNodeType.Branch,
                Operation     = CSGOperationType.Additive,
                CompactNodeID = NodeID(0),
                childCount    = childCount,
                childOffset   = 1
            };
            for (int i = 1; i <= childCount; i++)
            {
                nodes[i] = new CompactHierarchyNode
                {
                    Type          = CSGNodeType.Brush,
                    Operation     = CSGOperationType.Additive,
                    CompactNodeID = NodeID(i),
                    childCount    = 0,
                    childOffset   = 0
                };
            }

            // The routing job only reads these through GetBrushContents / brushIDValueToContents; every brush is an
            // ordinary solid, which is the default.
            root.minBrushIDValue = 1;
            builder.Allocate(ref root.brushIDValueToContents, childCount);
            builder.Allocate(ref root.brushIDValueToAncestorLegend, childCount);
            builder.Allocate(ref root.brushAncestorLegend, 0);
            builder.Allocate(ref root.brushAncestors, 0);
            root.minNodeIDValue = 0;
            root.maxNodeIDValue = childCount;

            return builder.CreateBlobAssetReference<CompactTree>(Allocator.Persistent);
        }

        /// <summary>
        /// A touching table that says every one of the <paramref name="childCount"/> brushes intersects the brush
        /// being routed - which is what puts one event per child on the stack at CreateRoutingTableJob.cs:324.
        /// </summary>
        static BlobAssetReference<BrushesTouchedByBrush> Touching(int childCount)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BrushesTouchedByBrush>();

            var intersections = builder.Allocate(ref root.brushIntersections, childCount);
            for (int i = 0; i < childCount; i++)
            {
                intersections[i] = new BrushIntersection
                {
                    nodeIndexOrder = new IndexOrder { compactNodeID = NodeID(i + 1), nodeOrder = i },
                    type           = IntersectionType.Intersection,
                    bottomUpStart  = 0,
                    bottomUpEnd    = 0
                };
            }

            root.BitOffset = 0;
            root.BitCount  = childCount + 1;
            var wordCount  = ((root.BitCount * 2) + 31) / 32;
            var bits = builder.Allocate(ref root.intersectionBits, wordCount);
            for (int i = 0; i < wordCount; i++)
                bits[i] = 0;
            for (int id = 0; id <= childCount; id++)
            {
                var bitIndex = id << 1;
                bits[bitIndex >> 5] |= (uint)IntersectionType.Intersection << (bitIndex & 31);
            }

            return builder.CreateBlobAssetReference<BrushesTouchedByBrush>(Allocator.Persistent);
        }

        /// <summary>Routes one brush out of a branch of <paramref name="childCount"/> mutually touching brushes.</summary>
        static void RouteOneBrushOf(int childCount)
        {
            var tree     = Tree(childCount);
            var touching = Touching(childCount);
            var orders   = new NativeList<IndexOrder>(1, Allocator.Persistent);
            var touched  = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(childCount, Allocator.Persistent);
            var output   = new NativeList<BlobAssetReference<RoutingTable>>(childCount, Allocator.Persistent);
            var treeRef  = new NativeReference<BlobAssetReference<CompactTree>>(tree, Allocator.Persistent);
            try
            {
                // Route the FIRST brush: every other child of the branch is a touching sibling of it.
                orders.Add(new IndexOrder { compactNodeID = NodeID(1), nodeOrder = 0 });
                for (int i = 0; i < childCount; i++)
                    touched.Add(touching);
                output.Resize(childCount, NativeArrayOptions.ClearMemory);

                var job = new CreateRoutingTableJob
                {
                    allUpdateBrushIndexOrders = orders,
                    brushesTouchedByBrushes   = touched,
                    compactTreeRef            = treeRef,
                    routingTableLookup        = output
                };
                job.Execute(0);
            }
            finally
            {
                for (int i = 0; i < output.Length; i++)
                    if (output[i].IsCreated) output[i].Dispose();
                output.Dispose();
                touched.Dispose();
                orders.Dispose();
                treeRef.Dispose();
                touching.Dispose();
                tree.Dispose();
            }
        }

        [Test]
        public void ABranchWithFewerTouchingChildrenThanTheEventCapacity_Routes()
        {
            Assert.DoesNotThrow(() => RouteOneBrushOf(kQueuedEventCapacity - 96));
        }

        [Test]
        public void ABranchWithMoreTouchingChildrenThanTheEventCapacity_StillRoutes()
        {
            Assert.DoesNotThrow(() => RouteOneBrushOf(kQueuedEventCapacity + 4));
        }



        /// <summary>
        /// A right-deep chain: each level is a branch holding one additive brush and, except at the bottom, another
        /// subtractive branch. <paramref name="depth"/> levels means <paramref name="depth"/> brushes, at the odd node
        /// indices 1, 3, 5, ...
        /// </summary>
        static BlobAssetReference<CompactTree> NestedTree(int depth)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<CompactTree>();

            // level i occupies: branch at 2i, brush at 2i+1, next branch at 2i+2
            var nodeCount = depth * 2;
            var nodes = builder.Allocate(ref root.compactHierarchy, nodeCount);
            for (int i = 0; i < depth; i++)
            {
                var branchIndex = i * 2;
                var isLast = (i == depth - 1);
                nodes[branchIndex] = new CompactHierarchyNode
                {
                    Type          = CSGNodeType.Branch,
                    Operation     = (i == 0) ? CSGOperationType.Additive : CSGOperationType.Subtractive,
                    CompactNodeID = NodeID(branchIndex),
                    childCount    = isLast ? 1 : 2,
                    childOffset   = branchIndex + 1
                };
                nodes[branchIndex + 1] = new CompactHierarchyNode
                {
                    Type          = CSGNodeType.Brush,
                    Operation     = CSGOperationType.Additive,
                    CompactNodeID = NodeID(branchIndex + 1),
                    childCount    = 0,
                    childOffset   = 0
                };
            }

            root.minBrushIDValue = 0;
            builder.Allocate(ref root.brushIDValueToContents, nodeCount);
            builder.Allocate(ref root.brushIDValueToAncestorLegend, nodeCount);
            builder.Allocate(ref root.brushAncestorLegend, 0);
            builder.Allocate(ref root.brushAncestors, 0);
            root.minNodeIDValue = 0;
            root.maxNodeIDValue = nodeCount;

            return builder.CreateBlobAssetReference<CompactTree>(Allocator.Persistent);
        }

        /// <summary>Every node of a <see cref="NestedTree"/> touching, with only the brushes counted as intersections.</summary>
        static BlobAssetReference<BrushesTouchedByBrush> TouchingNested(int depth)
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BrushesTouchedByBrush>();

            var intersections = builder.Allocate(ref root.brushIntersections, depth);
            for (int i = 0; i < depth; i++)
            {
                intersections[i] = new BrushIntersection
                {
                    nodeIndexOrder = new IndexOrder { compactNodeID = NodeID((i * 2) + 1), nodeOrder = i },
                    type           = IntersectionType.Intersection,
                    bottomUpStart  = 0,
                    bottomUpEnd    = 0
                };
            }

            var nodeCount = depth * 2;
            root.BitOffset = 0;
            root.BitCount  = nodeCount;
            var wordCount  = ((root.BitCount * 2) + 31) / 32;
            var bits = builder.Allocate(ref root.intersectionBits, wordCount);
            for (int i = 0; i < wordCount; i++)
                bits[i] = 0;
            for (int id = 0; id < nodeCount; id++)
            {
                var bitIndex = id << 1;
                bits[bitIndex >> 5] |= (uint)IntersectionType.Intersection << (bitIndex & 31);
            }

            return builder.CreateBlobAssetReference<BrushesTouchedByBrush>(Allocator.Persistent);
        }

        /// <summary>A routing table copied out of its blob: per node in routing order, its node index and its rows.</summary>
        internal sealed class RoutedTable
        {
            public readonly System.Collections.Generic.List<int>        nodes = new();
            public readonly System.Collections.Generic.List<ushort[][]> rows  = new();
        }

        /// <summary>
        /// Runs CreateRoutingTableJob for the brush at <paramref name="brushIndex"/> of a <see cref="NestedTree"/> and
        /// copies out the table it wrote, or null when it wrote none.
        /// </summary>
        internal static RoutedTable RouteNestedBrush(int depth, int brushIndex, int rowsPerNodeCopyLimit = 0)
        {
            var tree     = NestedTree(depth);
            var touching = TouchingNested(depth);
            var orders   = new NativeList<IndexOrder>(1, Allocator.Persistent);
            var touched  = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(depth, Allocator.Persistent);
            var output   = new NativeList<BlobAssetReference<RoutingTable>>(depth, Allocator.Persistent);
            var treeRef  = new NativeReference<BlobAssetReference<CompactTree>>(tree, Allocator.Persistent);
            try
            {
                var nodeOrder = (brushIndex - 1) / 2;
                orders.Add(new IndexOrder { compactNodeID = NodeID(brushIndex), nodeOrder = nodeOrder });
                for (int i = 0; i < depth; i++)
                    touched.Add(touching);
                output.Resize(depth, NativeArrayOptions.ClearMemory);

                new CreateRoutingTableJob
                {
                    allUpdateBrushIndexOrders = orders,
                    brushesTouchedByBrushes   = touched,
                    compactTreeRef            = treeRef,
                    routingTableLookup        = output,
                    rowsPerNodeCopyLimit      = rowsPerNodeCopyLimit
                }.Execute(0);

                if (!output[nodeOrder].IsCreated)
                    return null;
                ref var table = ref output[nodeOrder].Value;
                var nodeOfLookup = new int[table.routingLookups.Length];
                for (int i = 0; i < nodeOfLookup.Length; i++)
                    nodeOfLookup[i] = -1;
                for (int id = 0; id < table.nodeIDToTableIndex.Length; id++)
                {
                    var lookup = table.nodeIDToTableIndex[id];
                    if (lookup >= 0)
                        nodeOfLookup[lookup] = id + table.nodeIDOffset;
                }
                var copy = new RoutedTable();
                for (int k = 0; k < table.routingLookups.Length; k++)
                {
                    var lookup = table.routingLookups[k];
                    var rows = new ushort[lookup.endIndex - lookup.startIndex][];
                    for (int r = 0; r < rows.Length; r++)
                    {
                        var row = table.routingRows[lookup.startIndex + r];
                        rows[r] = new ushort[CategoryRoutingRow.Length];
                        for (int c = 0; c < CategoryRoutingRow.Length; c++)
                            rows[r][c] = row[c];
                    }
                    copy.nodes.Add(nodeOfLookup[k]);
                    copy.rows.Add(rows);
                }
                return copy;
            }
            finally
            {
                for (int i = 0; i < output.Length; i++)
                    if (output[i].IsCreated) output[i].Dispose();
                output.Dispose();
                touched.Dispose();
                orders.Dispose();
                treeRef.Dispose();
                touching.Dispose();
                tree.Dispose();
            }
        }

        /// <summary>
        /// What the tree itself makes of a surface of the brush at <paramref name="brushIndex"/>, from the category it has
        /// against every other brush: the operation tables applied the way the tree nests them, and nothing of the
        /// routing table. Every node of a <see cref="NestedTree"/> touches, so no child is ever skipped.
        /// </summary>
        static int EvaluateNested(int depth, int brushIndex, int[] categoryOfNode, int branchIndex = 0)
        {
            var brush = branchIndex + 1;
            var left  = (brush == brushIndex) ? (int)CategoryIndex.SelfAligned : categoryOfNode[brush];
            if (branchIndex / 2 == depth - 1)
                return left;
            var right = EvaluateNested(depth, brushIndex, categoryOfNode, branchIndex + 2);
            return CategoryRoutingRow.kOperationTables[((int)CSGOperationType.Subtractive * CategoryRoutingRow.OperationStride) +
                                                       (left * CategoryRoutingRow.RowStride) + right];
        }

        /// <summary>What the routing table makes of the same surface: row 0 of the first node, one step per node.</summary>
        internal static int Route(RoutedTable table, int[] categoryOfNode)
        {
            var input = 0;
            for (int k = 0; k < table.rows.Count; k++)
            {
                if (input >= table.rows[k].Length)
                    return -1 - k;              // no such row: never a category, so it can only mismatch
                var node = table.nodes[k];
                var category = (node >= 0 && node < categoryOfNode.Length) ? categoryOfNode[node] : (int)CategoryIndex.Outside;
                input = table.rows[k][input][category];
            }
            return input;
        }

        /// <summary>
        /// Null when the table is minimal, what is wrong with it otherwise: a destination that leads nowhere, a row
        /// nothing leads to, or two rows of one node that are alike. With no node after it holding two alike rows, two
        /// rows of a node that are not alike route some input differently, so this finds every pair of equivalent rows.
        /// </summary>
        internal static string WhyNotMinimal(RoutedTable table)
        {
            var count = table.rows.Count;
            var reached = new bool[count][];
            for (int k = 0; k < count; k++)
                reached[k] = new bool[table.rows[k].Length];
            if (count == 0 || table.rows[0].Length == 0)
                return "the table is empty";
            reached[0][0] = true;
            for (int k = 0; k < count; k++)
            {
                for (int r = 0; r < table.rows[k].Length; r++)
                {
                    if (!reached[k][r])
                        return $"node {k} (brush {table.nodes[k]}): row {r} of {table.rows[k].Length} is never reached";
                    for (int s = 0; s < r; s++)
                    {
                        if (System.Linq.Enumerable.SequenceEqual(table.rows[k][s], table.rows[k][r]))
                            return $"node {k} (brush {table.nodes[k]}): rows {s} and {r} of {table.rows[k].Length} are alike";
                    }
                    if (k + 1 == count)
                        continue;
                    foreach (var destination in table.rows[k][r])
                    {
                        if (destination >= table.rows[k + 1].Length)
                            return $"node {k} (brush {table.nodes[k]}): row {r} leads to row {destination} of a node with {table.rows[k + 1].Length}";
                        reached[k + 1][destination] = true;
                    }
                }
            }
            foreach (var destination in table.rows[count - 1][0])
            {
                if (destination >= CategoryRoutingRow.Length)
                    return $"the last node ends in {destination}, which is not a category";
            }
            return null;
        }

        /// <summary>
        /// Null when the table routes every combination of categories the way the tree evaluates it, the first
        /// combination it gets wrong otherwise. Exhaustive while there are at most 6^5 combinations, a fixed sample of
        /// 4096 beyond that.
        /// </summary>
        internal static string WhereRoutingDiffersFromTheTree(int depth, int brushIndex, RoutedTable table)
        {
            var others = new System.Collections.Generic.List<int>();
            for (int i = 0; i < depth; i++)
                if ((i * 2) + 1 != brushIndex)
                    others.Add((i * 2) + 1);

            var categoryOfNode = new int[depth * 2];
            var combinations = System.Math.Pow(CategoryRoutingRow.Length, others.Count);
            var exhaustive = combinations <= 7776;
            var random = new System.Random(depth * 1000 + brushIndex);
            var total = exhaustive ? (int)combinations : 4096;
            for (int n = 0; n < total; n++)
            {
                var rest = n;
                foreach (var other in others)
                {
                    if (exhaustive)
                    {
                        categoryOfNode[other] = rest % CategoryRoutingRow.Length;
                        rest /= CategoryRoutingRow.Length;
                    } else
                        categoryOfNode[other] = random.Next(CategoryRoutingRow.Length);
                }
                var expected = EvaluateNested(depth, brushIndex, categoryOfNode);
                var routed   = Route(table, categoryOfNode);
                if (routed != expected)
                {
                    var text = new System.Text.StringBuilder();
                    foreach (var other in others)
                        text.Append(" brush ").Append(other).Append('=').Append((CategoryIndex)categoryOfNode[other]);
                    return $"routed to {routed}, the tree says {(CategoryIndex)expected}, for{text}";
                }
            }
            return null;
        }

        [TestCase(2)]
        [TestCase(4)]
        [TestCase(8)]
        [TestCase(16)]
        [TestCase(32)]
        public void ANestedChain_GivesEveryBrushAMinimalTable(int depth)
        {
            for (int brush = 1; brush < depth * 2; brush += 2)
            {
                var table = RouteNestedBrush(depth, brush);
                Assert.That(table, Is.Not.Null, $"{depth} levels, brush {brush}: no routing table was written");
                var problem = WhyNotMinimal(table);
                Assert.That(problem, Is.Null, $"{depth} levels, brush {brush}: {problem}");
            }
        }

        [TestCase(2)]
        [TestCase(4)]
        [TestCase(6)]
        [TestCase(8)]
        [TestCase(12)]
        [TestCase(16)]
        [TestCase(32)]
        public void ANestedChain_RoutesEveryBrushLikeTheTree(int depth)
        {
            for (int brush = 1; brush < depth * 2; brush += 2)
            {
                var table = RouteNestedBrush(depth, brush);
                Assert.That(table, Is.Not.Null, $"{depth} levels, brush {brush}: no routing table was written");
                var difference = WhereRoutingDiffersFromTheTree(depth, brush, table);
                Assert.That(difference, Is.Null, $"{depth} levels, brush {brush}: {difference}");
            }
        }

        [Test]
        public void TheJudges_CatchPlantedErrors()
        {
            const int depth = 4, brush = 3;
            var table = RouteNestedBrush(depth, brush);
            Assert.That(table, Is.Not.Null);
            Assert.That(WhyNotMinimal(table), Is.Null, "the unplanted table has to pass for the plants to mean anything");
            Assert.That(WhereRoutingDiffersFromTheTree(depth, brush, table), Is.Null, "the unplanted table has to pass for the plants to mean anything");

            // A wrong final category in a row that is reached
            var last = table.rows.Count - 1;
            var saved = table.rows[last][0][(int)CategoryIndex.Inside];
            table.rows[last][0][(int)CategoryIndex.Inside] = (ushort)((saved == (int)CategoryIndex.Outside) ? (int)CategoryIndex.Inside : (int)CategoryIndex.Outside);
            Assert.That(WhereRoutingDiffersFromTheTree(depth, brush, table), Is.Not.Null, "a wrong final category went unnoticed");
            table.rows[last][0][(int)CategoryIndex.Inside] = saved;

            // A row duplicated in the node that has the most
            var node = 0;
            for (int k = 1; k < table.rows.Count; k++)
                if (table.rows[k].Length > table.rows[node].Length) node = k;
            var rows = new System.Collections.Generic.List<ushort[]>(table.rows[node]) { (ushort[])table.rows[node][0].Clone() };
            var original = table.rows[node];
            table.rows[node] = rows.ToArray();
            Assert.That(WhyNotMinimal(table), Is.Not.Null, "two alike rows went unnoticed");
            table.rows[node] = original;

            // A row nothing leads to
            if (table.rows.Count > 1)
            {
                var extra = new System.Collections.Generic.List<ushort[]>(table.rows[last]) { new ushort[CategoryRoutingRow.Length] };
                for (int c = 0; c < CategoryRoutingRow.Length; c++)
                    extra[extra.Count - 1][c] = (ushort)CategoryIndex.Outside;
                original = table.rows[last];
                table.rows[last] = extra.ToArray();
                var problem = WhyNotMinimal(table);
                table.rows[last] = original;
                Assert.That(problem, Is.Not.Null, "an unreachable row went unnoticed");
            }
        }

        [Test]
        public void ANodeNeedingMoreRowsThanTheLimit_IsRefusedLoudly()
        {
            Assert.That(RouteNestedBrush(8, 1), Is.Not.Null, "the chain has to route at the job's own maximum for the refusal to mean anything");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("brush 1 is nested too deeply to route"));
            var table = RouteNestedBrush(8, 1, rowsPerNodeCopyLimit: 2);
            Assert.That(table, Is.Null, "a table was written although the routing needed more rows than allowed");
        }

        const int    kStopAtRows       = 8192;
        const double kStopAfterSeconds = 5.0;

        [Test, Explicit("Measurement: writes Library/ChiselMeasurements/RoutingRowsPerNode.txt")]
        public void MeasureRowsPerNodeAgainstNestingDepth()
        {
            var lines = new System.Text.StringBuilder();
            lines.AppendLine("depth  most rows for one node (any brush)  most rows in one table  seconds");
            foreach (var depth in new[] { 2, 4, 6, 8, 10, 12, 16, 20, 24, 32, 48, 64, 96, 128 })
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                int most = 0, mostInATable = 0;
                for (int brush = 1; brush < depth * 2; brush += 2)
                {
                    var table = RouteNestedBrush(depth, brush);
                    if (table == null)
                        continue;
                    var inTable = 0;
                    foreach (var rows in table.rows)
                    {
                        most = System.Math.Max(most, rows.Length);
                        inTable += rows.Length;
                    }
                    mostInATable = System.Math.Max(mostInATable, inTable);
                }
                watch.Stop();
                lines.AppendLine($"{depth,5}  {most,34}  {mostInATable,22}  {watch.Elapsed.TotalSeconds,7:0.000}");
                if (most > kStopAtRows || watch.Elapsed.TotalSeconds > kStopAfterSeconds)
                {
                    lines.AppendLine($"stopped after {depth} levels: growth is steep enough that deeper cases are not safe to run blind");
                    break;
                }
            }
            var directory = System.IO.Path.Combine("Library", "ChiselMeasurements");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "RoutingRowsPerNode.txt"), lines.ToString());
            TestContext.WriteLine(lines.ToString());
        }
    }
}

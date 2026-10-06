using System.Runtime.CompilerServices;

// This makes it possible for unity to pre-burst-compile these job types
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselBox>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselCapsule>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselCylinder>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselHemisphere>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselSphere>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.CreateBrushesJob<Chisel.Core.ChiselStadium>))]

[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchPrepareAndCountBrushesJob<Chisel.Core.ChiselExtrudedShape>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchAllocateBrushesJob<Chisel.Core.ChiselExtrudedShape>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchCreateBrushesJob<Chisel.Core.ChiselExtrudedShape>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.GeneratorBranchJobPool<Chisel.Core.ChiselExtrudedShape>.InitializeArraysJob))]

[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchPrepareAndCountBrushesJob<Chisel.Core.ChiselLinearStairs>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchAllocateBrushesJob<Chisel.Core.ChiselLinearStairs>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchCreateBrushesJob<Chisel.Core.ChiselLinearStairs>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.GeneratorBranchJobPool<Chisel.Core.ChiselLinearStairs>.InitializeArraysJob))]

[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchPrepareAndCountBrushesJob<Chisel.Core.ChiselPathedStairs>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchAllocateBrushesJob<Chisel.Core.ChiselPathedStairs>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchCreateBrushesJob<Chisel.Core.ChiselPathedStairs>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.GeneratorBranchJobPool<Chisel.Core.ChiselPathedStairs>.InitializeArraysJob))]

[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchPrepareAndCountBrushesJob<Chisel.Core.ChiselRevolvedShape>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchAllocateBrushesJob<Chisel.Core.ChiselRevolvedShape>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchCreateBrushesJob<Chisel.Core.ChiselRevolvedShape>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.GeneratorBranchJobPool<Chisel.Core.ChiselRevolvedShape>.InitializeArraysJob))]

[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchPrepareAndCountBrushesJob<Chisel.Core.ChiselSpiralStairs>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchAllocateBrushesJob<Chisel.Core.ChiselSpiralStairs>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchCreateBrushesJob<Chisel.Core.ChiselSpiralStairs>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.GeneratorBranchJobPool<Chisel.Core.ChiselSpiralStairs>.InitializeArraysJob))]

[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchPrepareAndCountBrushesJob<Chisel.Core.ChiselTorus>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchAllocateBrushesJob<Chisel.Core.ChiselTorus>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.BranchCreateBrushesJob<Chisel.Core.ChiselTorus>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.GeneratorBranchJobPool<Chisel.Core.ChiselTorus>.InitializeArraysJob))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.EnsureCapacityListReferenceJob<Chisel.Core.BrushIntersectionLoop>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.EnsureCapacityListForEachCountFromListJob<Chisel.Core.BrushData, Chisel.Core.IndexOrder>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeReferenceChildBlobAssetReferenceJob<Chisel.Core.CompactTree>))]

[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeArrayChildrenJob<Unity.Collections.LowLevel.Unsafe.UnsafeList<Chisel.Core.SubMeshSurface>>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeArrayChildrenJob<Unity.Collections.LowLevel.Unsafe.UnsafeList<Chisel.Core.BrushIntersectWith>>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeArrayChildrenJob<Unity.Collections.LowLevel.Unsafe.UnsafeList<Unity.Mathematics.float3>>))]

[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeListChildrenBlobAssetReferenceJob<Chisel.Core.InternalChiselSurfaceArray>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeListChildrenBlobAssetReferenceJob<Chisel.Core.BasePolygonsBlob>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeListChildrenBlobAssetReferenceJob<Chisel.Core.BrushTreeSpaceVerticesBlob>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeListChildrenBlobAssetReferenceJob<Chisel.Core.BrushesTouchedByBrush>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeListChildrenBlobAssetReferenceJob<Chisel.Core.RoutingTable>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeListChildrenBlobAssetReferenceJob<Chisel.Core.BrushTreeSpacePlanes>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeListChildrenBlobAssetReferenceJob<Chisel.Core.ChiselBrushRenderBuffer>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeListChildrenBlobAssetReferenceJob<Chisel.Core.SubMeshTriangleLookup>))]
[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeListChildrenBlobAssetReferenceJob<Chisel.Core.ExactBrush>))]

[assembly: Unity.Jobs.RegisterGenericJobType(typeof(Chisel.Core.DisposeListChildrenJob<Chisel.Core.CompactHierarchy>))]

[assembly: InternalsVisibleTo("com.chisel.unity.editor", AllInternalsVisible = true)]
[assembly: InternalsVisibleTo("com.chisel.unity.components", AllInternalsVisible = true)]
[assembly: InternalsVisibleTo("com.chisel.core.tests", AllInternalsVisible = true)]
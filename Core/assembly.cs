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

// TODO: figure out why this doesn't seem to work?
//[assembly: InternalsVisibleTo("com.chisel.unity.editor", AllInternalsVisible = true)]
//[assembly: InternalsVisibleTo("com.chisel.unity.components", AllInternalsVisible = true)]
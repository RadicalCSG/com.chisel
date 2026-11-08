using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Chisel.Core
{
    /// <summary>What every generator keeps to: a size can be negative (built the right way out), never 0</summary>
    public static class ChiselGeneratorLimits
    {
        /// <summary>The smallest a size can be, either way: a generator is never flat. A centimetre: a round generator
        /// (cylinder, capsule, sphere, hemisphere) a millimetre high isn't built. A box can be thinner (ChiselBox.kMinSize).</summary>
        public const float kMinSize = 0.01f;

        /// <summary>A size of 0 (or less than min either way) as the smallest size, keeping its sign</summary>
        public static float NotZero(float size, float min = kMinSize) => math.abs(size) >= min ? size : (size < 0 ? -min : min);
    }

    /// <summary>A generator that names its surfaces, for the inspector</summary>
    public interface IChiselSurfaceNames
    {
        /// <summary>The name of the surface at this place in the surface array, or null to show it numbered</summary>
        string GetSurfaceName(int index);
        /// <summary>Whether the generator, as it is set up now, has faces with the surface at this place</summary>
        bool IsSurfaceUsed(int index);
    }

    /// <summary>
    /// A generator made of several brushes that says what each is: a role ("Step", "Tread", "Side") per brush in build
    /// order, so something on a brush (a decal) keeps meaning the same part as the generator changes. A generator that
    /// says nothing has one role, its brushes in build order.
    /// </summary>
    public interface IChiselGeneratorParts
    {
        int PartRoleCount { get; }
        string GetPartRoleName(int role);
        /// <summary>The role of the brush at this place in build order, as the generator is set up now</summary>
        int GetPartRole(int brushIndex);
    }

    /// <summary>The words generators name their surfaces with, so the same kind of surface has the same name in each</summary>
    public static class ChiselSurfaceNames
    {
        public const string Top    = "Top";
        public const string Bottom = "Bottom";
        public const string Left   = "Left";
        public const string Right  = "Right";
        public const string Front  = "Front";
        public const string Back   = "Back";

        /// <summary>One of the sides around a round generator, counted from 1</summary>
        public static string Side(int number) { return "Side " + number; }
    }

    public interface IChiselNodeGenerator
    {
        int RequiredSurfaceCount { get; }
        /// <summary>The name of the surface at this place in the surface array, or null to show it numbered</summary>
        string GetSurfaceName(int index);
        /// <summary>Whether the generator, as it is set up now, has faces with the surface at this place</summary>
        bool IsSurfaceUsed(int index);
        void Reset();
        bool Validate();
        void UpdateSurfaces(ref ChiselSurfaceArray surfaceArray);
        /// <summary>
        /// A hash of the values this definition generates its nodes from, the same in every session for the same
        /// values. It is the input of the stage that makes brush meshes, so it hashes what was authored rather than
        /// anything generated from it.
        /// </summary>
        UnityEngine.Hash128 GetInputHash();

        void OnEdit(IChiselHandles handles);
		void GetMessages(IChiselMessageHandler messages);

        /// <summary>What its brushes are, or null when it doesn't say (one role, in build order)</summary>
        IChiselGeneratorParts Parts { get; }
    }

    public interface IBrushGenerator
    {
		BlobAssetReference<BrushMeshBlob> GenerateMesh(BlobAssetReference<InternalChiselSurfaceArray> internalSurfaceArrayBlob, Allocator allocator = Allocator.Persistent);// Indirect
        void Reset();
        int RequiredSurfaceCount { get; }
        void UpdateSurfaces(ref ChiselSurfaceArray surfaceArray);
        bool Validate();
		void GetMessages(IChiselMessageHandler messages);
    }

    [System.Serializable]
    public abstract class SerializedBrushGenerator<BrushGenerator> : IChiselNodeGenerator
        where BrushGenerator : unmanaged, IBrushGenerator
    {
        [HideFoldout] public BrushGenerator settings;

        public virtual int RequiredSurfaceCount { get { return settings.RequiredSurfaceCount; } }

        public virtual BrushGenerator GetBrushGenerator() { return settings; }

        public virtual string GetSurfaceName(int index) { return (settings is IChiselSurfaceNames names) ? names.GetSurfaceName(index) : null; }
        public virtual bool IsSurfaceUsed(int index) { return (settings is IChiselSurfaceNames names) ? names.IsSurfaceUsed(index) : true; }
        public virtual IChiselGeneratorParts Parts => settings as IChiselGeneratorParts;

        public virtual void Reset() { settings.Reset(); }

        public virtual void UpdateSurfaces(ref ChiselSurfaceArray surfaceArray) 
        {
            settings.UpdateSurfaces(ref surfaceArray);
        }

        public virtual bool Validate()
        {
            return settings.Validate();
        }
        
        public virtual void GetMessages(IChiselMessageHandler messages)
        {
            settings.GetMessages(messages);
        }

        public virtual UnityEngine.Hash128 GetInputHash()
        {
            var hash = new UnityEngine.Hash128();
            hash.Append(ref settings);
            return hash;
        }

        public abstract void OnEdit(IChiselHandles handles);
    }

    public struct GeneratedNode
    {
        public int                                parentIndex;    // -1 means root of generated node
        public CSGOperationType                   operation;
        public float4x4                           transformation;
        public BlobAssetReference<BrushMeshBlob>  brushMesh;      // Note: ignored if type is not Brush


		// Confirmed to dispose brushMesh
		public static GeneratedNode GenerateBrush(BlobAssetReference<BrushMeshBlob> brushMesh, CSGOperationType operation = CSGOperationType.Additive, int parentIndex = -1)
        {
            return GenerateBrush(brushMesh, float4x4.identity, operation, parentIndex);
        }

		// Confirmed to dispose brushMesh
		public static GeneratedNode GenerateBrush(BlobAssetReference<BrushMeshBlob> brushMesh, float4x4 transformation, CSGOperationType operation = CSGOperationType.Additive, int parentIndex = -1)
        {
            return new GeneratedNode
            {
                parentIndex     = parentIndex,
                transformation  = transformation,
                operation       = operation,
                brushMesh       = brushMesh
            };
        }

        public static GeneratedNode GenerateBranch(CSGOperationType operation = CSGOperationType.Additive, int parentIndex = -1)
        {
            return GenerateBranch(float4x4.identity, operation, parentIndex);
        }

        public static GeneratedNode GenerateBranch(float4x4 transformation, CSGOperationType operation = CSGOperationType.Additive, int parentIndex = -1)
        {
            return new GeneratedNode
            {
                parentIndex     = parentIndex,
                transformation  = transformation,
                operation       = operation,
                brushMesh       = BlobAssetReference<BrushMeshBlob>.Null
            };
        }
    }

    public interface IBranchGenerator
    {
        int PrepareAndCountRequiredBrushMeshes();
        bool GenerateNodes(BlobAssetReference<InternalChiselSurfaceArray> internalSurfaceArrayBlob, NativeList<GeneratedNode> nodes, Allocator allocator = Allocator.Persistent);// Indirect
		void Dispose();

        void Reset();
        int RequiredSurfaceCount { get; }
        void UpdateSurfaces(ref ChiselSurfaceArray surfaceArray);
        bool Validate();
        void GetWarningMessages(IChiselMessageHandler messages);
    }

    [System.Serializable]
    public abstract class SerializedBranchGenerator<BranchGenerator> : IChiselNodeGenerator
        where BranchGenerator : unmanaged, IBranchGenerator
    {
        [HideFoldout] public BranchGenerator settings;

        public virtual int RequiredSurfaceCount { get { return settings.RequiredSurfaceCount; } }

        public virtual BranchGenerator GetBranchGenerator() { return settings; }

        public virtual string GetSurfaceName(int index) { return (settings is IChiselSurfaceNames names) ? names.GetSurfaceName(index) : null; }
        public virtual bool IsSurfaceUsed(int index) { return (settings is IChiselSurfaceNames names) ? names.IsSurfaceUsed(index) : true; }
        public virtual IChiselGeneratorParts Parts => settings as IChiselGeneratorParts;

        public virtual void Reset()     { settings.Reset(); }
        public virtual bool Validate()  { return settings.Validate(); }

        public virtual void UpdateSurfaces(ref ChiselSurfaceArray surfaceArray)
        {
            settings.UpdateSurfaces(ref surfaceArray);
        }

        public virtual void GetMessages(IChiselMessageHandler messages)
        {
            settings.GetWarningMessages(messages);
        }

        public virtual UnityEngine.Hash128 GetInputHash()
        {
            var hash = new UnityEngine.Hash128();
            hash.Append(ref settings);
            return hash;
        }

        public abstract void OnEdit(IChiselHandles handles);
    }

}

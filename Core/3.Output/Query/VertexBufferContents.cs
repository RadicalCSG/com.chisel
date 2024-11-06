//#define RUN_IN_SERIAL
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Entities;

namespace Chisel.Core
{
    public struct RenderVertex
    {
        public float3 position;
        public float3 normal;
        public float4 tangent;
        public float2 uv0;
	}

	public struct SelectVertex
	{
		public float3 position;
		public Vector4 instanceID;
	}

	public struct SubMeshSection
    {
        public MeshQuery meshQuery;
        public int startIndex;
        public int endIndex;
        public int totalVertexCount;
        public int totalIndexCount;
    }

    public struct VertexBufferContents
    {
        public readonly static VertexAttributeDescriptor[] s_ColliderDescriptors = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,  dimension: 3, stream: 0)
        };

		public readonly static VertexAttributeDescriptor[] s_RenderDescriptors = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,  dimension: 3, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal,    dimension: 3, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.Tangent,   dimension: 4, stream: 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2, stream: 0),
        };

		public readonly static VertexAttributeDescriptor[] s_SelectDescriptors = new[]
		{
			new VertexAttributeDescriptor(VertexAttribute.Position,  dimension: 3, stream: 0),
			new VertexAttributeDescriptor(VertexAttribute.Color,     dimension: 4, stream: 0)
		};

        public NativeList<GeneratedMeshDescription> meshDescriptions;
        public NativeList<SubMeshSection>           subMeshSections;
        public NativeList<Mesh.MeshData>            meshes;
        public NativeList<BlobAssetReference<SubMeshTriangleLookup>> subMeshTriangleLookups;
		public NativeArray<VertexAttributeDescriptor> colliderDescriptors;
		public NativeArray<VertexAttributeDescriptor> renderDescriptors;
		public NativeArray<VertexAttributeDescriptor> selectDescriptors;

		public void EnsureInitialized()
        {
            if (!meshDescriptions.IsCreated) meshDescriptions = new NativeList<GeneratedMeshDescription>(Allocator.Persistent);
            else meshDescriptions.Clear();
            if (!subMeshSections.IsCreated) subMeshSections = new NativeList<SubMeshSection>(Allocator.Persistent);
            else subMeshSections.Clear();
            if (!meshes.IsCreated) meshes = new NativeList<Mesh.MeshData>(Allocator.Persistent);
            if (!subMeshTriangleLookups.IsCreated) subMeshTriangleLookups = new NativeList<BlobAssetReference<SubMeshTriangleLookup>>(Allocator.Persistent);

			if (!colliderDescriptors.IsCreated)
				colliderDescriptors = new NativeArray<VertexAttributeDescriptor>(s_ColliderDescriptors, Allocator.Persistent);
			if (!renderDescriptors.IsCreated)
                renderDescriptors = new NativeArray<VertexAttributeDescriptor>(s_RenderDescriptors, Allocator.Persistent);
			if (!selectDescriptors.IsCreated)
				selectDescriptors = new NativeArray<VertexAttributeDescriptor>(s_SelectDescriptors, Allocator.Persistent);
        }

        public void Clear()
        {
            if (meshDescriptions.IsCreated) meshDescriptions.Clear();
            if (subMeshSections.IsCreated) subMeshSections.Clear();
        }

        public bool IsCreated
        {
            get
            {
                return meshDescriptions.IsCreated &&
                        subMeshSections.IsCreated &&
                        subMeshTriangleLookups.IsCreated &&
                        meshes.IsCreated &&
						colliderDescriptors.IsCreated &&
                        renderDescriptors.IsCreated &&
						selectDescriptors.IsCreated;
			}
        }

        public JobHandle Dispose(JobHandle dependency) 
        {
            JobHandle lastJobHandle = default;
            if (meshDescriptions    .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, meshDescriptions       .Dispose(dependency));
            if (subMeshSections     .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, subMeshSections        .Dispose(dependency));
            if (meshes              .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, meshes                 .Dispose(dependency));
            if (subMeshTriangleLookups.IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, subMeshTriangleLookups.DisposeDeep(dependency));

			if (colliderDescriptors.IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, colliderDescriptors.Dispose(dependency));
			if (renderDescriptors  .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, renderDescriptors.Dispose(dependency));
			if (selectDescriptors  .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, selectDescriptors.Dispose(dependency));
			
            this = default;
            return lastJobHandle;
        }
    };
}
﻿using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    public struct BrushOutline : IDisposable
    {
        public UnsafeList<float3>   vertices;
        public UnsafeList<int>      visibleOuterLines;
        public UnsafeList<int>      surfaceVisibleOuterLines;
        public UnsafeList<int>      surfaceVisibleOuterLineRanges;
        public bool                 initialized;
        public uint                 hash;

        public static BrushOutline Create()
        {
            return new BrushOutline
            {
                initialized                   = true,
                vertices                      = new UnsafeList<float3>(512, Allocator.Persistent),
                visibleOuterLines             = new UnsafeList<int>(256, Allocator.Persistent),
                surfaceVisibleOuterLines      = new UnsafeList<int>(256, Allocator.Persistent),
                surfaceVisibleOuterLineRanges = new UnsafeList<int>(64, Allocator.Persistent)
            };
        }

        public bool IsCreated
        {
            get
            {
                return initialized && vertices.IsCreated && visibleOuterLines.IsCreated && surfaceVisibleOuterLines.IsCreated && surfaceVisibleOuterLineRanges.IsCreated;
            }
        }

        public void Dispose()
        {
            try
            {
                initialized = false;
                if (vertices                     .IsCreated) vertices                     .Dispose(); 
                if (visibleOuterLines            .IsCreated) visibleOuterLines            .Dispose(); 
                if (surfaceVisibleOuterLines     .IsCreated) surfaceVisibleOuterLines     .Dispose(); 
                if (surfaceVisibleOuterLineRanges.IsCreated) surfaceVisibleOuterLineRanges.Dispose(); 
            }
            catch(Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
            finally
            { 
                vertices = default; 
                visibleOuterLines = default; 
                surfaceVisibleOuterLines = default; 
                surfaceVisibleOuterLineRanges = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            vertices                     .Clear();
            visibleOuterLines            .Clear();
            surfaceVisibleOuterLines     .Clear();
            surfaceVisibleOuterLineRanges.Clear();
            hash = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void UpdateHash()
        {
            hash = math.hash(new uint4(vertices.Hash(),
                                       visibleOuterLines.Hash(),
                                       surfaceVisibleOuterLines.Hash(),
                                       surfaceVisibleOuterLineRanges.Hash()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Fill(ref BrushMeshBlob brushMesh)
        {
            if (!IsCreated)
            {
                UnityEngine.Debug.LogError($"Calling Fill on uninitialized Outline");
                return;
            }
            Reset();
            
            ref var polygons = ref brushMesh.polygons;
            ref var halfEdges = ref brushMesh.halfEdges;
            ref var localVertices = ref brushMesh.localVertices;

            vertices.AddRange(ref localVertices);

            surfaceVisibleOuterLines.Clear();
            if (surfaceVisibleOuterLines.Capacity < halfEdges.Length * 2)
                surfaceVisibleOuterLines.SetCapacity(halfEdges.Length * 2);

            visibleOuterLines.Clear();
            if (visibleOuterLines.Capacity < halfEdges.Length * 2)
                visibleOuterLines.SetCapacity(halfEdges.Length * 2);

            surfaceVisibleOuterLineRanges.Resize(polygons.Length, NativeArrayOptions.ClearMemory);
            UnityEngine.Debug.Assert(vertices.Length == localVertices.Length);
            for (int p = 0; p < polygons.Length; p++)
            {
                var firstEdge = polygons[p].firstEdge;
                var edgeCount = polygons[p].edgeCount;
                var lastEdge  = firstEdge + edgeCount;

                var vertexIndex0 = halfEdges[lastEdge - 1].vertexIndex;
                var vertexIndex1 = 0;
                for (int h = firstEdge; h < lastEdge; vertexIndex0 = vertexIndex1, h++)
                {
                    vertexIndex1 = halfEdges[h].vertexIndex;
                    if (vertexIndex0 > vertexIndex1) // avoid duplicate edges
                    {
                        visibleOuterLines.Add(vertexIndex0);
                        visibleOuterLines.Add(vertexIndex1);
                    }
                    surfaceVisibleOuterLines.Add(vertexIndex0);
                    surfaceVisibleOuterLines.Add(vertexIndex1);
                }
                surfaceVisibleOuterLineRanges[p] = surfaceVisibleOuterLines.Length;
            }
            UpdateHash();
        }
    }
}

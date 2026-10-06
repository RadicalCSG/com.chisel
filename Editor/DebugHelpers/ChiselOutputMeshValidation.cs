using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    static class ChiselOutputMeshValidation
    {
        const string kLogPrefix = "[Chisel.Manifold] ";

        [MenuItem("Chisel DEBUG/Validate Output Meshes")]
        static void ValidateOutputMeshes()
        {
            var models = Object.FindObjectsByType<ChiselModelComponent>(FindObjectsInactive.Include);
            if (models == null || models.Length == 0)
            {
                Debug.Log(kLogPrefix + "no Chisel models in the loaded scenes");
                return;
            }

            var meshes     = new List<Mesh>();
            var transforms = new List<Transform>();
            int checkedSurfaces = 0, closedSurfaces = 0;
            foreach (var model in models)
            {
                if (model == null || model.generated == null)
                    continue;

                CollectColliders(model, meshes, transforms);
                if (meshes.Count > 0)
                {
                    checkedSurfaces++;
                    if (Report(model.name + " collision surface", model, meshes, transforms))
                        closedSurfaces++;
                }

                CollectRenderables(model, meshes, transforms);
                if (meshes.Count > 0)
                {
                    checkedSurfaces++;
                    if (Report(model.name + " render surface", model, meshes, transforms))
                        closedSurfaces++;
                }
            }

            if (checkedSurfaces == 0)
                Debug.Log(kLogPrefix + "no generated meshes to check - has the scene been built?");
            else if (closedSurfaces == checkedSurfaces)
                Debug.Log($"{kLogPrefix}all {checkedSurfaces} surface(s) are closed manifolds");
            else
                Debug.LogError($"{kLogPrefix}{checkedSurfaces - closedSurfaces} of {checkedSurfaces} surface(s) are NOT closed manifolds - see the errors above");
        }

        static void CollectColliders(ChiselModelComponent model, List<Mesh> meshes, List<Transform> transforms)
        {
            meshes.Clear();
            transforms.Clear();
            var colliders = model.generated.colliders;
            if (colliders == null)
                return;
            foreach (var collider in colliders)
            {
                if (collider == null || collider.sharedMesh == null || collider.meshCollider == null)
                    continue;
                meshes.Add(collider.sharedMesh);
                transforms.Add(collider.meshCollider.transform);
            }
        }

        static void CollectRenderables(ChiselModelComponent model, List<Mesh> meshes, List<Transform> transforms)
        {
            meshes.Clear();
            transforms.Clear();
            var renderables = model.generated.renderables;
            if (renderables == null)
                return;
            foreach (var renderable in renderables)
            {
                if (renderable == null || renderable.sharedMesh == null || renderable.meshFilter == null)
                    continue;
                meshes.Add(renderable.sharedMesh);
                transforms.Add(renderable.meshFilter.transform);
            }
        }

        // Returns true when the surface is a closed manifold. Caller guarantees meshes is non-empty.
        static bool Report(string label, ChiselModelComponent model, List<Mesh> meshes, List<Transform> transforms)
        {
            Combine(model, meshes, transforms, out var vertices, out var indices);
            var report = MeshManifoldValidation.Classify(vertices, indices.AsArray());
            indices.Dispose();
            vertices.Dispose();

            if (report.triangleCount == 0)
                return true;

            var summary = $"{kLogPrefix}{label}: {report.triangleCount} triangles, {report.inputVertexCount} vertices" +
                          $" ({report.weldedVertexCount} after welding at {MeshManifoldValidation.kDefaultWeldEpsilon}), {report.edgeCount} edges";
            if (report.degenerateTriangleCount > 0)
                summary += $", {report.degenerateTriangleCount} degenerate triangles";

            if (report.IsClosedManifold)
            {
                Debug.Log(summary + " - CLOSED MANIFOLD", model);
                return true;
            }

            Debug.LogError(
                $"{summary}\n" +
                $"    NOT a closed manifold.\n" +
                $"    {report.boundaryEdgeCount} edge(s) used by ONE triangle - the surface stops there - total length {report.boundaryEdgeLength:F3}\n" +
                $"    {report.nonManifoldEdgeCount} edge(s) used by MORE THAN TWO - duplicated or overlapping surface - worst is {report.maxEdgeUseCount} triangles\n" +
                $"    longest single boundary edge {report.longestBoundaryEdgeLength:F4}, from {Format(report.longestBoundaryEdgeFrom)} to {Format(report.longestBoundaryEdgeTo)} in {model.name}'s local space\n" +
                $"    NOTE a boundary edge is not automatically a physical gap: a T-junction leaves three of them and is geometrically seamless.",
                model);
            return false;
        }

        static void Combine(ChiselModelComponent model, List<Mesh> meshes, List<Transform> transforms,
                            out NativeArray<float3> vertices, out NativeList<int> indices)
        {
            int vertexTotal = 0;
            for (int i = 0; i < meshes.Count; i++)
                vertexTotal += meshes[i].vertexCount;

            vertices = new NativeArray<float3>(vertexTotal, Allocator.Temp);
            indices  = new NativeList<int>(vertexTotal * 3, Allocator.Temp);

            var scratchVertices = new List<Vector3>();
            var scratchIndices  = new List<int>();
            var worldToModel    = model.transform.worldToLocalMatrix;
            int vertexOffset    = 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                var mesh    = meshes[i];
                var toModel = worldToModel * transforms[i].localToWorldMatrix;
                mesh.GetVertices(scratchVertices);
                for (int v = 0; v < scratchVertices.Count; v++)
                {
                    var position = toModel.MultiplyPoint3x4(scratchVertices[v]);
                    vertices[vertexOffset + v] = new float3(position.x, position.y, position.z);
                }
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                        continue;
                    mesh.GetTriangles(scratchIndices, subMesh);
                    for (int t = 0; t < scratchIndices.Count; t++)
                        indices.Add(vertexOffset + scratchIndices[t]);
                }
                vertexOffset += scratchVertices.Count;
            }
        }

        static string Format(float3 value)
        {
            return new Vector3(value.x, value.y, value.z).ToString("F3");
        }
    }
}

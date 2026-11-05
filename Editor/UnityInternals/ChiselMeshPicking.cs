using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Editors
{
    // Where a ray hits a mesh, any mesh, also one that isn't readable: Unity's HandleUtility.IntersectRayMesh, which is
    // internal
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class ChiselMeshPicking
    {
        const string kFeature = "placing on meshes that aren't Chisel's";

        delegate bool IntersectRayMeshDelegate(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit);
        static readonly IntersectRayMeshDelegate s_IntersectRayMesh = ChiselUnityInternals.Delegate<IntersectRayMeshDelegate>(typeof(HandleUtility), "IntersectRayMesh", kFeature);

        public static bool Available => s_IntersectRayMesh != null;

        public static bool IntersectRayMesh(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit)
        {
            hit = default;
            return s_IntersectRayMesh != null && mesh && s_IntersectRayMesh(ray, mesh, matrix, out hit);
        }

        // Where a ray hits a mesh and the plane of the triangle it hits there, facing the ray. The hit's own normal is
        // the mesh's vertex normals blended (smooth on a round mesh, anything on an imported one) and turned by the matrix
        // without its scale taken out, so a generator placed by it could lean or lie on its side
        public static bool IntersectRayMeshFace(Ray ray, Mesh mesh, Matrix4x4 matrix, out Vector3 point, out Vector3 normal)
        {
            point  = default;
            normal = default;
            if (!IntersectRayMesh(ray, mesh, matrix, out var hit))
                return false;
            point = hit.point;
            if (!TriangleNormal(mesh, hit.triangleIndex, matrix, out normal))
                normal = hit.normal.normalized;
            if (Vector3.Dot(normal, ray.direction) > 0)
                normal = -normal;
            return normal.sqrMagnitude > 0;
        }

        // The normal of a mesh's triangle in the space the matrix puts it in; triangles are counted over its sub-meshes
        // in order
        internal static bool TriangleNormal(Mesh mesh, int triangleIndex, Matrix4x4 matrix, out Vector3 normal)
        {
            normal = default;
            if (!mesh || triangleIndex < 0)
                return false;
            using var meshData = Mesh.AcquireReadOnlyMeshData(mesh);
            var data = meshData[0];
            for (int s = 0, first = 0; s < data.subMeshCount; s++)
            {
                var subMesh = data.GetSubMesh(s);
                if (subMesh.topology != MeshTopology.Triangles)
                    continue;
                var count = subMesh.indexCount / 3;
                if (triangleIndex >= first + count)
                {
                    first += count;
                    continue;
                }
                using var indices  = new NativeArray<int>(subMesh.indexCount, Allocator.Temp);
                using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
                data.GetIndices(indices, s);
                data.GetVertices(vertices);
                var t = (triangleIndex - first) * 3;
                var a = matrix.MultiplyPoint3x4(vertices[indices[t]]);
                var b = matrix.MultiplyPoint3x4(vertices[indices[t + 1]]);
                var c = matrix.MultiplyPoint3x4(vertices[indices[t + 2]]);
                normal = Vector3.Cross(b - a, c - a).normalized;
                return normal.sqrMagnitude > 0;
            }
            return false;
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // Where a ray hits a mesh, any mesh, also one that isn't readable: Unity's HandleUtility.IntersectRayMesh, which is
    // internal
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
    }
}

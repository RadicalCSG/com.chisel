using System;
using UnityEngine;

namespace Chisel.Components
{
    internal static class ChiselMeshUtility
    {
        public static void FlipNormals(Mesh mesh)
        {
            if (!mesh)
                return;

            var normals = mesh.normals;
            if (normals != null && normals.Length > 0)
            {
                for (int i = 0; i < normals.Length; i++)
                    normals[i] = -normals[i];
                mesh.normals = normals;
            }

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var indices = mesh.GetTriangles(s);
                Array.Reverse(indices);
                mesh.SetTriangles(indices, s);
            }
        }

        public static void SmoothNormals(Mesh mesh, float angle)
        {
            if (!mesh)
                return;
            mesh.RecalculateNormals(angle);
        }
    }
}

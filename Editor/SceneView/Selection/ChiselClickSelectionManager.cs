using Chisel.Components;
using Chisel.Core;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using UnityEditor;

using UnityEngine;

namespace Chisel.Editors
{
    // TODO: merge all "Intersection" types into one "Intersection" thing
	public class PlaneIntersection
    {
        public PlaneIntersection(Vector3 point, Plane plane) { this.point = point; this.plane = plane; }
        public PlaneIntersection(Vector3 point, Vector3 normal) { this.point = point; this.plane = new Plane(normal, point); }
        public PlaneIntersection(ChiselIntersection chiselIntersection)
        {
            this.point = chiselIntersection.worldPlaneIntersection;
            this.plane = chiselIntersection.worldPlane;
            this.node = chiselIntersection.treeNode;
            this.model = chiselIntersection.model;
        }

        public Vector3 point;
        public Plane plane;
        public Quaternion Orientation { get { return Quaternion.LookRotation(plane.normal); } }
        public ChiselNodeComponent node;
        public ChiselModelComponent model;
    }

	// TODO: merge with ChiselSelectionManager
	public static class ChiselClickSelectionManager
    {
        delegate bool IntersectRayMeshFunc(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit);
		readonly static IntersectRayMeshFunc s_IntersectRayMesh = typeof(HandleUtility).CreateDelegate<IntersectRayMeshFunc>("IntersectRayMesh");

        public static PlaneIntersection GetPlaneIntersection(Vector2 mousePosition)
        {
			var intersectionObject = ChiselSelectionManager.PickClosestGameObject(mousePosition, out ChiselIntersection brushIntersection);
			if (intersectionObject &&
                intersectionObject.activeInHierarchy)
            {
                if (brushIntersection.treeNode != null)
                    return new PlaneIntersection(brushIntersection);

                if (intersectionObject.TryGetComponent<MeshFilter>(out var meshFilter))
                {
                    var mesh = meshFilter.sharedMesh;
                    var mouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePosition);
					if (s_IntersectRayMesh(mouseRay, mesh, intersectionObject.transform.localToWorldMatrix, out RaycastHit hit))
					{
						if (intersectionObject.TryGetComponent<MeshRenderer>(out var meshRenderer) &&
							meshRenderer.enabled)
						{
							return new PlaneIntersection(hit.point, hit.normal);
						}
					}
				}
            }
            else
            {
                var gridPlane = Chisel.Editors.Grid.ActiveGrid.PlaneXZ;
                var mouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePosition);
				if (gridPlane.SignedRaycast(mouseRay, out float dist))
					return new PlaneIntersection(mouseRay.GetPoint(dist), gridPlane);
			}
            return null;
        }

		public static bool FindSurfaceReferences(Vector2 position, bool selectAllSurfaces, List<SurfaceReference> foundSurfaces, out ChiselIntersection intersection, out SurfaceReference surfaceReference)
        {
            intersection = ChiselIntersection.None;
            surfaceReference = null;
            try
			{
				var gameObject = ChiselSelectionManager.PickClosestGameObject(position, out intersection);
				if (!object.Equals(gameObject, null) &&
					(!intersection.model && !intersection.treeNode) || intersection.brushIntersection.surfaceIndex == -1)
					return false;

                var chiselNode = intersection.treeNode;
                if (!chiselNode)
                    return false;

                var brush = intersection.brushIntersection.brush;
                var surfaceID = intersection.brushIntersection.surfaceIndex;

                if (chiselNode && chiselNode.TopTreeNode.Valid)
                    FindSurfaceReference(chiselNode, chiselNode.TopTreeNode, brush, surfaceID, out surfaceReference);

                if (selectAllSurfaces)
                    return ChiselSurfaceSelectionManager.GetAllSurfaceReferences(chiselNode, brush, foundSurfaces);

                if (surfaceReference == null)
                    return false;

                foundSurfaces.Add(surfaceReference);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool FindSurfaceReference(ChiselNodeComponent chiselNode, CSGTreeNode node, CSGTreeBrush findBrush, int surfaceID, out SurfaceReference surfaceReference)
        {
            surfaceReference = null;
            switch (node.Type)
            {
                case CSGNodeType.Branch: return FindBranchSurfaceReference(chiselNode, (CSGTreeBranch)node, findBrush, surfaceID, out surfaceReference);
                case CSGNodeType.Brush: return FindBrushSurfaceReference(chiselNode, (CSGTreeBrush)node, findBrush, surfaceID, out surfaceReference);
                default: return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool FindBranchSurfaceReference(ChiselNodeComponent chiselNode, CSGTreeBranch branch, CSGTreeBrush findBrush, int surfaceID, out SurfaceReference surfaceReference)
        {
            surfaceReference = null;
            for (int i = 0; i < branch.Count; i++)
            {
                if (FindSurfaceReference(chiselNode, branch[i], findBrush, surfaceID, out surfaceReference))
                    return true;
            }
            return false;
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool FindBrushSurfaceReference(ChiselNodeComponent chiselNode, CSGTreeBrush brush, CSGTreeBrush findBrush, int surfaceID, out SurfaceReference surfaceReference)
        {
            surfaceReference = null;
            if (findBrush != brush)
                return false;

            var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(findBrush.BrushMesh.BrushMeshID);
            if (!brushMeshBlob.IsCreated)
                return true;

            ref var brushMesh = ref brushMeshBlob.Value;

            var surfaceIndex = surfaceID;
            if (surfaceIndex < 0 || surfaceIndex >= brushMesh.polygons.Length)
                return true;

            var descriptionIndex = brushMesh.polygons[surfaceIndex].descriptionIndex;

            surfaceReference = new SurfaceReference(chiselNode, descriptionIndex, brush, surfaceIndex);
            return true;
        }
    }
}

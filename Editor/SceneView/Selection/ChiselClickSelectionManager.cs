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
        public static PlaneIntersection GetPlaneIntersection(Vector2 mousePosition)
        {
            // whatever is nearest under the mouse. Chisel's picking finds only its own brushes, and Unity's never finds
            // them since the generated meshes are kept out of it: both are asked
            var mouseRay = UnityEditor.HandleUtility.GUIPointToWorldRay(mousePosition);

            PlaneIntersection onChisel = null;
            var intersectionObject = ChiselSelectionManager.PickClosestGameObject(mousePosition, out var brushIntersection);
            if (intersectionObject && intersectionObject.activeInHierarchy && brushIntersection.treeNode != null)
                onChisel = new PlaneIntersection(brushIntersection);

            var closest = UnityEditor.HandleUtility.PickGameObject(mousePosition, false);
            if (closest && closest.TryGetComponent<ChiselNodeComponent>(out _))
                closest = null;
            var onMesh = closest ? GetPlaneIntersection(closest, mouseRay) : null;

            if (onChisel != null || onMesh != null)
                return Nearest(mouseRay, onChisel, onMesh);
            return GetPlaneIntersection(null, mouseRay);
        }

        // the one nearer along the ray, either one when the other is missing
        internal static PlaneIntersection Nearest(Ray ray, PlaneIntersection a, PlaneIntersection b)
        {
            if (a == null) return b;
            if (b == null) return a;
            var distanceA = Vector3.Dot(a.point - ray.origin, ray.direction);
            var distanceB = Vector3.Dot(b.point - ray.origin, ray.direction);
            return distanceA <= distanceB ? a : b;
        }

        // Where the mouse ray meets an object under the mouse that isn't Chisel's: the triangle of its mesh it hits; the
        // grid when there is none
        internal static PlaneIntersection GetPlaneIntersection(GameObject closest, Ray mouseRay)
        {
			if (closest &&
                closest.activeInHierarchy)
            {
                if (closest.TryGetComponent<MeshFilter>(out var meshFilter) &&
                    closest.TryGetComponent<MeshRenderer>(out var meshRenderer) && meshRenderer.enabled &&
                    ChiselMeshPicking.IntersectRayMeshFace(mouseRay, meshFilter.sharedMesh, closest.transform.localToWorldMatrix, out var point, out var normal))
                    return new PlaneIntersection(point, normal);
                return null;
            }
            var gridPlane = Chisel.Editors.Grid.ActiveGrid.PlaneXZ;
			if (gridPlane.SignedRaycast(mouseRay, out float dist))
				return new PlaneIntersection(mouseRay.GetPoint(dist), gridPlane);
            return null;
        }

        // every surface of the node, of all the brushes a generator makes, not only of the one brush that was hit
        internal static bool GetAllSurfaces(ChiselNodeComponent chiselNode, List<SurfaceReference> foundSurfaces)
        {
            return ChiselSurfaceSelectionManager.GetAllSurfaceReferences(chiselNode, foundSurfaces);
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
                    return GetAllSurfaces(chiselNode, foundSurfaces);

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

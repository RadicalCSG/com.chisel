using Chisel.Core;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Pool;

namespace Chisel.Components
{
    public static class ChiselSceneQuery
    {
        public static ChiselIntersection Convert(CSGTreeBrushIntersection intersection)
        {
			var node  = Resources.InstanceIDToObject(intersection.brush.InstanceID) as ChiselNodeComponent;
            var model = Resources.InstanceIDToObject(intersection.tree.InstanceID) as ChiselModelComponent;

            var treeLocalToWorldMatrix  = model.transform.localToWorldMatrix;            
            
            var worldPlaneIntersection  = treeLocalToWorldMatrix.MultiplyPoint(intersection.surfaceIntersection.treePlaneIntersection);
            var worldPlane              = treeLocalToWorldMatrix.TransformPlane(intersection.surfaceIntersection.treePlane);
            
            return new ChiselIntersection()
            {
                treeNode                = node,
                model                   = model,
                worldPlane              = worldPlane,
                worldPlaneIntersection  = worldPlaneIntersection,
                brushIntersection       = intersection
            };
        }

		// TODO: move to CSGQueryManager?
		public static bool FindFirstWorldIntersection(List<ChiselIntersection> foundIntersections, Vector3 worldRayStart, Vector3 worldRayEnd, int visibleLayers = ~0, SurfaceDestinationFlags visibleLayerFlags = SurfaceDestinationFlags.Renderable, bool ignoreBackfaced = false, bool ignoreDiscarded = false, GameObject[] ignore = null, GameObject[] filter = null)
        {
            bool found = false;

            var ignoreInstanceIDs = HashSetPool<int>.Get();
            var filterInstanceIDs = HashSetPool<int>.Get();
            var ignoreNodes = ListPool<CSGTreeNode>.Get();
            var filterNodes = ListPool<CSGTreeNode>.Get();
            try
            {
                if (ignore != null)
                {
                    foreach (var go in ignore)
                    {
                        if (go.TryGetComponent<ChiselNodeComponent>(out var node))
                        {
                            ChiselNodeHierarchyManager.GetChildrenOfHierarchyItemNoAlloc(node.hierarchyItem, ignoreNodes);
                            ignoreInstanceIDs.Add(node.GetInstanceID());
                        }
                    }
                }
                if (filter != null)
                {
                    foreach (var go in filter)
					{
						if (go.TryGetComponent<ChiselNodeComponent>(out var node))
						{
                            ChiselNodeHierarchyManager.GetChildrenOfHierarchyItemNoAlloc(node.hierarchyItem, filterNodes);
                            filterInstanceIDs.Add(node.GetInstanceID());
                            if (node.hierarchyItem != null &&
                                node.hierarchyItem.Model)
                                filterInstanceIDs.Add(node.hierarchyItem.Model.GetInstanceID());
                        }
                    }
                }

				using var allTrees = new NativeList<CSGTree>(Allocator.Temp);
				CompactHierarchyManager.GetAllTrees(allTrees);
				for (var t = 0; t < allTrees.Length; t++)
				{
					var tree = allTrees[t];
					var model = Resources.InstanceIDToObject(tree.InstanceID) as ChiselModelComponent;
					if (!ChiselModelManager.Instance.IsSelectable(model))
						continue;

					if (((1 << model.gameObject.layer) & visibleLayers) == 0)
						continue;

					var modelInstanceID = model.GetInstanceID();
					if (ignoreInstanceIDs.Contains(modelInstanceID) ||
						(filterInstanceIDs.Count > 0 && !filterInstanceIDs.Contains(modelInstanceID)))
						continue;

					var query = ChiselMeshQueryManager.GetMeshQuery(model);
					var visibleQueries = ChiselMeshQueryManager.GetVisibleQueries(query, visibleLayerFlags);

					// We only accept RayCasts into this model if it's visible
					if (visibleQueries == null ||
						visibleQueries.Length == 0)
						return false;

					Vector3 treeRayStart;
					Vector3 treeRayEnd;

					var transform = model.transform;
					if (transform)
					{
						var worldToLocalMatrix = transform.worldToLocalMatrix;
						treeRayStart = worldToLocalMatrix.MultiplyPoint(worldRayStart);
						treeRayEnd = worldToLocalMatrix.MultiplyPoint(worldRayEnd);
					}
					else
					{
						treeRayStart = worldRayStart;
						treeRayEnd = worldRayEnd;
					}

					var treeIntersections = CSGQueryManager.RayCastMulti(visibleQueries, tree, treeRayStart, treeRayEnd, ignoreNodes, filterNodes, ignoreBackfaced, ignoreDiscarded);
					if (treeIntersections == null)
						continue;

					for (var i = 0; i < treeIntersections.Length; i++)
					{
						var intersection = treeIntersections[i];
						var brush = intersection.brush;
						var instanceID = brush.InstanceID;

						if ((filterInstanceIDs.Count > 0 && !filterInstanceIDs.Contains(instanceID)) ||
							ignoreInstanceIDs.Contains(instanceID))
							continue;

						foundIntersections.Add(Convert(intersection));
						found = true;
					}
				}
				return found;
			}
            finally
            {
                HashSetPool<int>.Release(ignoreInstanceIDs);
                HashSetPool<int>.Release(filterInstanceIDs);
                ListPool<CSGTreeNode>.Release(ignoreNodes);
                ListPool<CSGTreeNode>.Release(filterNodes);
            }
        }

        public static bool GetNodesInFrustum(Frustum frustum, int visibleLayers, SurfaceDestinationFlags visibleLayerFlags, ref HashSet<CSGTreeNode> rectFoundNodes)
        {
            rectFoundNodes.Clear();
            var planes = new Plane[6];
            Vector4 srcVector;

			using var allTrees = new NativeList<CSGTree>(Allocator.Temp);
			CompactHierarchyManager.GetAllTrees(allTrees);
			for (var t = 0; t < allTrees.Length; t++)
			{
				var tree = allTrees[t];
				var model = Resources.InstanceIDToObject(tree.InstanceID) as ChiselModelComponent;
				if (!ChiselModelManager.Instance.IsSelectable(model))
					continue;

				if (((1 << model.gameObject.layer) & visibleLayers) == 0)
					continue;

				var query = ChiselMeshQueryManager.GetMeshQuery(model);
				var visibleQueries = ChiselMeshQueryManager.GetVisibleQueries(query, visibleLayerFlags);

				// We only accept RayCasts into this model if it's visible
				if (visibleQueries == null ||
					visibleQueries.Length == 0)
					continue;

				// Transform the frustum into the space of the tree				
				var transform = model.transform;
				var worldToLocalMatrixInversed = transform.localToWorldMatrix;   // localToWorldMatrix == worldToLocalMatrix.inverse
				var worldToLocalMatrixInversedTransposed = worldToLocalMatrixInversed.transpose;
				for (int p = 0; p < 6; p++)
				{
					var srcPlane = frustum.Planes[p];
					srcVector.x = srcPlane.normal.x;
					srcVector.y = srcPlane.normal.y;
					srcVector.z = srcPlane.normal.z;
					srcVector.w = srcPlane.distance;

					srcVector = worldToLocalMatrixInversedTransposed * srcVector;

					planes[p].normal = srcVector;
					planes[p].distance = srcVector.w;
				}

				var treeNodesInFrustum = CSGQueryManager.GetNodesInFrustum(tree, visibleQueries, planes);
				if (treeNodesInFrustum == null)
					continue;

				for (int n = 0; n < treeNodesInFrustum.Length; n++)
				{
					var treeNode = treeNodesInFrustum[n];
					rectFoundNodes.Add(treeNode);
				}
			}
			return rectFoundNodes.Count > 0;
		}
    }
}

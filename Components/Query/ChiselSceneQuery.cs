using Chisel.Core;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Pool;
using System.Linq;
using System;
using Unity.Mathematics;

namespace Chisel.Components
{
	public enum VisibilityState
	{
		Unknown = 0,
		AllVisible = 1,
		AllInvisible = 2,
		Mixed = 3
	}

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

					var query = ChiselSceneQuery.GetMeshQuery(model);
					var visibleQueries = ChiselSceneQuery.GetVisibleQueries(query, visibleLayerFlags);

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

					var treeIntersections = RayCastMulti(visibleQueries, tree, treeRayStart, treeRayEnd, ignoreNodes, filterNodes, ignoreBackfaced, ignoreDiscarded);
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

				var query = ChiselSceneQuery.GetMeshQuery(model);
				var visibleQueries = ChiselSceneQuery.GetVisibleQueries(query, visibleLayerFlags);

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

				var treeNodesInFrustum = GetNodesInFrustum(tree, visibleQueries, planes);
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

		public static MeshQuery[] GetMeshQuery(ChiselModelComponent model)
		{
			// TODO: make this depended on the model settings / helper surface view settings
			if (model.CreateRenderComponents &&
				model.CreateColliderComponents)
				return MeshQuery.DefaultQueries;

			if (model.CreateRenderComponents)
				return MeshQuery.RenderOnly;
			else
				return MeshQuery.CollisionOnly;
		}

		public static MeshQuery[] GetVisibleQueries(MeshQuery[] queryArray, SurfaceDestinationFlags visibleLayerFlags)
		{
			var queryList = queryArray.ToList();
			for (int n = queryList.Count - 1; n >= 0; n--)
			{
				if ((visibleLayerFlags & queryList[n].LayerQuery) == visibleLayerFlags)
					continue;
				queryList.RemoveAt(n);
			}
			return queryList.ToArray();
		}
		
        static bool IsSurfaceVisible(MeshQuery[] meshQueries, ref ChiselSurfaceRenderBuffer surface)
        {
            // Compare surface with 'current' meshquery (is this surface even being rendered???)
            for (int n = 0; n < meshQueries.Length; n++)
            {
                var meshQuery = meshQueries[n];
                var destinationFlags = surface.destinationFlags;
                if ((destinationFlags & meshQuery.LayerQueryMask) == meshQuery.LayerQuery)
                    return true;
            }
            return false;
        }

        static bool IsPointInsideSurface(ref ChiselSurfaceRenderBuffer surface, float3 treeSpacePoint, out float3 treeSpaceNormal)
        {
            ref var triangles	= ref surface.indices;
            ref var vertices    = ref surface.colliderVertices;

            for (int i = 0, triangle_count = triangles.Length; i < triangle_count; i += 3)
	        {
                var v0 = vertices[triangles[i + 0]];
                var v1 = vertices[triangles[i + 1]];
                var v2 = vertices[triangles[i + 2]];

                if (MathExtensions.PointInTriangle(treeSpacePoint, v0, v2, v1)) 
                {
                    treeSpaceNormal = math.normalizesafe(math.cross(v2 - v0, v2 - v1));
                    return true;
                }
	        }

            treeSpaceNormal = float3.zero;
            return false;
        }
        
        // Requirement: out values only set when something is found, otherwise are not modified
        static bool BrushRayCast (MeshQuery[]       meshQueries,
                                  CSGTree           tree,
                                  CSGTreeBrush      brush,

                                  ref BlobArray<ChiselSurfaceRenderBuffer> surfaces,

						          Vector3           treeSpaceRayStart,
						          Vector3           treeSpaceRayEnd,

                                  bool              ignoreBackfaced,
                                  bool              ignoreDiscarded,

                                  List<CSGTreeBrushIntersection> foundIntersections)
        {
            var brushMeshInstanceID = brush.BrushMesh.brushMeshHash;
            var brushMeshBlob       = BrushMeshManager.GetBrushMeshBlob(brushMeshInstanceID);
            if (!brushMeshBlob.IsCreated)
                return false;

            ref var brushMesh = ref brushMeshBlob.Value;
            ref var planes = ref brushMesh.localPlanes;
            ref var planeCount = ref brushMesh.localPlaneCount;
            if (planeCount == 0)
		        return false;

            var treeToNodeSpace = (Matrix4x4)brush.TreeToNodeSpaceMatrix;
            var nodeToTreeSpace = (Matrix4x4)brush.NodeToTreeSpaceMatrix;

            var brushRayStart	= treeToNodeSpace.MultiplyPoint(treeSpaceRayStart);
            var brushRayEnd     = treeToNodeSpace.MultiplyPoint(treeSpaceRayEnd);
	        var brushRayDelta	= brushRayEnd - brushRayStart;

            var found		    = false;

            var brush_ray_start	= brushRayStart;
            var brush_ray_end	= brushRayEnd;
            for (var s = 0; s < planeCount; s++)
            {
                // Compare surface with 'current' meshquery (is this surface even being rendered???)
                if (ignoreDiscarded)
                {
                    if (surfaces[s].indices.Length == 0)
                        continue;

                    if (!IsSurfaceVisible(meshQueries, ref surfaces[s]))
                        continue;

                    Debug.Assert(surfaces[s].surfaceIndex == s);
                }

                var plane = new Plane(planes[s].xyz, planes[s].w);
                var s_dist = plane.GetDistanceToPoint(brush_ray_start);
                var e_dist = plane.GetDistanceToPoint(brush_ray_end);
                var length = s_dist - e_dist;

                var t = s_dist / length;
                if (!(t >= 0.0f && t <= 1.0f)) // NaN will return false on both comparisons, outer not is intentional
                    continue;

                var delta = brushRayDelta * t;

                var intersection = brush_ray_start + delta;

                // make sure the point is on the brush
                //if (!brushMesh.localBounds.Contains(intersection))
                //    continue;
                bool skipSurface = false;
                for (var s2 = 0; s2 < planeCount; s2++)
                {
                    if (s == s2)
                        continue;

                    var plane2 = new Plane(planes[s2].xyz, planes[s2].w);
                    var pl_dist = plane2.GetDistanceToPoint(intersection);
                    if (pl_dist > MathExtensions.kDistanceEpsilon) { skipSurface = true; break; }
                }

                if (skipSurface)
                    continue;
                
                var treeIntersection = nodeToTreeSpace.MultiplyPoint(intersection);
                if (!IsPointInsideSurface(ref surfaces[s], treeIntersection, out var treeSpaceNormal))
                {
                    if (ignoreDiscarded)
                        continue;
                }

                var localSpaceNormal = treeToNodeSpace.MultiplyVector(treeSpaceNormal);

                // Ignore backfaced culled triangles
                if (ignoreBackfaced && math.dot(localSpaceNormal, brushRayStart - intersection) < 0)
                    continue;

                found = true;
                var result_isReversed		    = math.dot(localSpaceNormal, plane.normal) < 0;
                var result_localPlane           = plane;
                var result_surfaceIndex         = s;
                var result_localIntersection    = intersection;

                var treePlane               = nodeToTreeSpace.TransformPlane(result_localPlane);

                var result_dist             = delta.sqrMagnitude;
                var result_treeIntersection = nodeToTreeSpace.MultiplyPoint(result_localIntersection);
                var result_treePlane        = result_isReversed ? treePlane.flipped : treePlane;

                foundIntersections.Add(new CSGTreeBrushIntersection()
                { 
                    tree            = tree,
                    brush           = brush,

                    surfaceIndex    = result_surfaceIndex,

                    surfaceIntersection = new ChiselSurfaceIntersection()
                    {
                        treePlane               = result_treePlane,
                        treePlaneIntersection   = result_treeIntersection,
			            distance			    = result_dist,
                    }
                });
            }

	        return found;
        }



		// TODO:	problem with RayCastMulti is that this code is too slow
		//			solution:	1.	replace RayCastMulti with a way to 'simply' changing the in_rayStart 
		//							and only finding the closest brush.
		//						2.	cache the categorization-table per brush (the generation is the slow part)
		//							in a way that we only need to regenerate it if it doesn't touch an 'ignored' brush
		//							(in fact, we could still cache the generated table w/ ignored brushes while moving the mouse)
		//						3.	have a ray-brush acceleration data structure so that we reduce the number of brushes to 'try'
		//							to only the ones that the ray actually intersects with (instead of -all- brushes)
		
        // TODO: make this non allocating
		public static CSGTreeBrushIntersection[] RayCastMulti(MeshQuery[]       meshQueries, 
                                                              CSGTree           tree,
                                                              Vector3           treeSpaceRayStart,
                                                              Vector3           treeSpaceRayEnd,
                                                              List<CSGTreeNode> ignoreNodes = null,
                                                              List<CSGTreeNode> filterNodes = null,
                                                              bool              ignoreBackfaced = true, 
                                                              bool              ignoreDiscarded = true)
        {
            if (!tree.Valid)
                return null;
			
            var ignoreNodeIndices = HashSetPool<CSGTreeNode>.Get();
			var filterNodeIndices = HashSetPool<CSGTreeNode>.Get();
			var foundIntersections = ListPool<CSGTreeBrushIntersection>.Get();
			try
			{ 
                ignoreNodeIndices.Clear();
                if (ignoreNodes != null)
                {
                    for (var i = 0; i < ignoreNodes.Count; i++)
                    {
                        if (!ignoreNodes[i].Valid ||
                            ignoreNodes[i].Type != CSGNodeType.Brush)
                            continue;

                        ignoreNodeIndices.Add(ignoreNodes[i]);
                    }
                }
                filterNodeIndices.Clear();
                if (filterNodes != null)
                {
                    for (var i = 0; i < filterNodes.Count; i++)
                    {
                        if (!filterNodes[i].Valid ||
                            filterNodes[i].Type != CSGNodeType.Brush)
                            continue; 

                        filterNodeIndices.Add(filterNodes[i]);
                    }
                }

                foundIntersections.Clear();

			    using var treeBrushes = new NativeList<CompactNodeID>(Allocator.Temp);
			    CompactHierarchyManager.GetHierarchy(tree).GetTreeNodes(default, treeBrushes);

			    var brushCount = treeBrushes.Length;
			    if (brushCount == 0)
				    return null;

			    var treeSpaceRay = new Ray(treeSpaceRayStart, treeSpaceRayEnd - treeSpaceRayStart);
			    var brushRenderBufferLookup = ChiselTreeLookup.Value[tree].brushRenderBufferLookup;

			    for (int i = 0; i < brushCount; i++)
			    {
				    var brush = CSGTreeBrush.Find(treeBrushes[i]);
    #if UNITY_EDITOR
				    if (!brush.IsSelectable)
					    continue;
    #endif
				    var minMaxAABB = brush.Bounds;
				    if (minMaxAABB.IsEmpty())
					    continue;
				    if (ignoreNodeIndices.Contains(brush) ||
					    (filterNodeIndices.Count > 0 && !filterNodeIndices.Contains(brush)))
					    continue;

				    var brushCompactNodeID = CompactHierarchyManager.GetCompactNodeID(brush);
				    if (!brushRenderBufferLookup.TryGetValue(brushCompactNodeID, out var brushRenderBuffer))
					    continue;

				    var bounds = new Bounds((minMaxAABB.Max + minMaxAABB.Min) / 2, minMaxAABB.Max - minMaxAABB.Min);
				    if (!bounds.IntersectRay(treeSpaceRay))
					    continue;

				    BrushRayCast(meshQueries, tree, brush,

								    ref brushRenderBuffer.Value.surfaces,

								    treeSpaceRayStart,
								    treeSpaceRayEnd,

								    ignoreBackfaced,
								    ignoreDiscarded,

								    foundIntersections);
			    }

			    if (foundIntersections.Count == 0)
				    return null;

			    return foundIntersections.ToArray();
            }
            finally
			{
				ListPool<CSGTreeBrushIntersection>.Release(foundIntersections);
				HashSetPool<CSGTreeNode>.Release(ignoreNodeIndices);
				HashSetPool<CSGTreeNode>.Release(filterNodeIndices);
			}
		}

        public static CSGTreeNode[] GetNodesInFrustum(CSGTree       tree,
                                                      MeshQuery[]   meshQueries, // TODO: add meshquery support here
                                                      Plane[]       planes)
        {
            if (planes == null)
                throw new ArgumentNullException("planes");

            if (planes.Length != 6)
                throw new ArgumentException("planes requires to be an array of length 6", "planes");

            for (int p = 0; p < 6; p++)
            {
                var plane       = planes[p];
                var normal      = plane.normal;
                var distance    = plane.distance;
                var n = normal.x + normal.y + normal.z + distance;
                if (float.IsInfinity(n) || float.IsNaN(n))
                    return null;
            }

			using var treeBrushes = new NativeList<CompactNodeID>(Allocator.Temp);
			CompactHierarchyManager.GetHierarchy(tree).GetTreeNodes(default, treeBrushes);

			var brushCount = treeBrushes.Length;
			if (brushCount == 0)
				return null;

            var foundNodes = ListPool<CSGTreeNode>.Get();
			try
			{
			    var brushRenderBufferLookup = ChiselTreeLookup.Value[tree].brushRenderBufferLookup;

			    for (int i = 0; i < brushCount; i++)
			    {
				    var brush = CSGTreeBrush.Find(treeBrushes[i]);
    #if UNITY_EDITOR
				    if (!brush.IsSelectable)
					    continue;
    #endif
				    var minMaxAABB = brush.Bounds;
				    if (minMaxAABB.IsEmpty())
					    continue;

				    var bounds = new Bounds((minMaxAABB.Max + minMaxAABB.Min) / 2, minMaxAABB.Max - minMaxAABB.Min);
				    // TODO: take transformations into account? (frustum is already in tree space)

				    bool intersectsFrustum = false;
				    for (int p = 0; p < 6; p++)
				    {
					    if (planes[p].IsOutside(bounds))
						    goto SkipBrush;

					    if (!planes[p].IsInside(bounds))
						    intersectsFrustum = true;
				    }

				    if (intersectsFrustum)
				    {
					    var brushCompactNodeID = CompactHierarchyManager.GetCompactNodeID(brush);
					    if (!brushRenderBufferLookup.TryGetValue(brushCompactNodeID, out var brushRenderBuffers) ||
						    !brushRenderBuffers.IsCreated)
						    continue;

					    ref var surfaceRenderBuffers = ref brushRenderBuffers.Value.surfaces;

					    bool haveVisibleSurfaces = false;

					    // Double check if the vertices of the brush are inside the frustum
					    for (int s = 0; s < surfaceRenderBuffers.Length; s++)
					    {
						    ref var surfaceRenderBuffer = ref surfaceRenderBuffers[s];

						    // Compare surface with 'current' meshquery (is this surface even being rendered???)
						    if (!IsSurfaceVisible(meshQueries, ref surfaceRenderBuffer))
							    goto SkipSurface;

						    ref var vertices = ref surfaceRenderBuffer.colliderVertices;
						    for (int p = 0; p < 6; p++)
						    {
							    var plane = planes[p];
							    for (int v = 0; v < vertices.Length; v++)
							    {
								    var distance = plane.GetDistanceToPoint(vertices[v]);
								    if (distance > MathExtensions.kFrustumDistanceEpsilon)
									    // If we have a visible surface that is outside the frustum, we skip the brush
									    // (we only want brushes that are completely inside the frustum)
									    goto SkipBrush;
							    }
						    }
						    // Make sure we find at least one single visible surface inside the frustum
						    haveVisibleSurfaces = true;
					    SkipSurface:
						    ;
					    }

					    // If we haven't found a single visible surface inside the frustum, skip the brush
					    if (!haveVisibleSurfaces)
						    goto SkipBrush;
				    }

				    // TODO: handle generators, where we only select a generator when ALL of it's brushes are selected

				    foundNodes.Add((CSGTreeNode)brush);
			    SkipBrush:
				    ;
			    }

			    if (foundNodes.Count == 0)
				    return null;

			    return foundNodes.ToArray();
            }
            finally
			{
				ListPool<CSGTreeNode>.Release(foundNodes);
			}
		}
	}
}

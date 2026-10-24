using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;

namespace Chisel.Core
{
	[BurstCompile(FloatPrecision.High, FloatMode.Strict, CompileSynchronously = true)]
	struct GenerateSurfaceTrianglesJob : IJobParallelForDefer
	{
		// Read
		// 'Required' for scheduling with index count
		[NoAlias, ReadOnly] public NativeList<IndexOrder> allUpdateBrushIndexOrders;

		[NoAlias, ReadOnly] public NativeList<BlobAssetReference<BasePolygonsBlob>> basePolygonCache;
		[NoAlias, ReadOnly] public NativeList<NodeTransformations> transformationCache;

		[NoAlias, ReadOnly] public NativeStream.Reader input;
		[NoAlias, ReadOnly] public NativeArray<MeshQuery> meshQueries;
		[NoAlias, ReadOnly] public CompactHierarchyManagerInstance.ReadOnlyEntityIDLookup entityIDLookup;
		[NoAlias, ReadOnly] public bool subtractiveWorkflow;
		[NoAlias, ReadOnly] public float normalSmoothingAngle;
		// Decals, lowest first. Empty when the tree has none.
		[NoAlias, ReadOnly] public NativeArray<DecalVolume> decalVolumes;
		// The surfaces decals are limited to (DecalVolume.targetStart/targetCount)
		[NoAlias, ReadOnly] public NativeArray<ChiselDecalTarget> decalTargets;
		// Hotspot layouts (ChiselHotspotStore.Gather): every layout's tiles, and where a layout's are (by id - 1)
		[NoAlias, ReadOnly] public NativeArray<HotspotTileData>   hotspotTiles;
		[NoAlias, ReadOnly] public NativeArray<HotspotLayoutData> hotspotLayouts;
		// the materials layouts with variants are drawn with (HotspotLayoutData.variantStart)
		[NoAlias, ReadOnly] public NativeArray<ulong>             hotspotVariants;

		// Write
		[NativeDisableParallelForRestriction]
		[NoAlias] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>> brushRenderBufferCache;

		struct CompareSortByBasePlaneIndex : System.Collections.Generic.IComparer<ChiselQuerySurface>
		{
			public readonly int Compare(ChiselQuerySurface x, ChiselQuerySurface y)
			{
				var diff = x.surfaceParameter.CompareTo(y.surfaceParameter);
				if (diff != 0)
					return diff;
				return x.surfaceIndex - y.surfaceIndex;
			}
		}
		readonly static CompareSortByBasePlaneIndex kCompareSortByBasePlaneIndex = new();

		// After a surface's triangles have been registered: normal smoothing, UVs, tangents, lightmap coordinates, the decals on
		// it and its render buffer.
		unsafe void FinishSurface(int surf, int brushNodeOrder, ulong entityID, ref BlobArray<BaseSurface> baseSurfaces, float4x4 treeToPlane, float4x4 nodeToTree, float3 finalFaceNormal,
						   NativeList<int> surfaceIndexList, ref UniqueVertexMapper uniqueVertexMapper,
						   BlobBuilder builder, BlobBuilderArray<ChiselSurfaceRenderBuffer> surfaceBuffers,
						   ref DecalSurfaceBuilder decalBuilder, NativeList<int> decalCandidates, NativeList<byte> decalTargeted,
						   NativeArray<int> decalCandidateStart, SlotLayout slots)
		{
			var baseSurfaceCount = slots.baseSurfaceCount;
			if (normalSmoothingAngle > 0.0001f)
			{
				var renderVertices = uniqueVertexMapper.surfaceRenderVertices;
				var positions      = uniqueVertexMapper.surfaceColliderVertices;
				float smoothingCos = math.cos(math.radians(normalSmoothingAngle));

				var alignedPlanes = new NativeList<float4>(64, Allocator.Temp);
				for (int otherBrushIdx = 0; otherBrushIdx < basePolygonCache.Length; otherBrushIdx++)
				{
					if (!basePolygonCache[otherBrushIdx].IsCreated) continue;
					ref var otherSurfaces = ref basePolygonCache[otherBrushIdx].Value.surfaces;
					// Hoisted out of the vertex loop - this only varies per brush.
					var otherNodeToTreeInvTrans = math.transpose(transformationCache[otherBrushIdx].treeToNode);

					for (int otherSurf = 0; otherSurf < otherSurfaces.Length; otherSurf++)
					{
						// Skip ourselves (same brush, same surface)
						if (otherBrushIdx == brushNodeOrder && otherSurf == surf) continue;

						float4 otherPlaneTree = math.mul(otherNodeToTreeInvTrans, otherSurfaces[otherSurf].localPlane);

						// If subtractive workflow, we must flip the neighbour normal effectively
						// to compare "Inwards vs Inwards" rather than "Inwards vs Outwards"
						float3 comparisonNormal = subtractiveWorkflow ? -otherPlaneTree.xyz : otherPlaneTree.xyz;
						if (math.dot(finalFaceNormal, comparisonNormal) < smoothingCos)
							continue;

						// Kept RAW (normal + D as cached): the per-vertex test below needs the
						// raw plane for the geometric distance, and negates it when accumulating.
						alignedPlanes.Add(otherPlaneTree);
					}
				}

				for (int v = 0; v < renderVertices.Length; v++)
				{
					float3 vertPos        = positions[v];
					float3 smoothedNormal = finalFaceNormal;

					for (int a = 0; a < alignedPlanes.Length; a++)
					{
						var otherPlaneTree = alignedPlanes[a];
						// distance = dot(N_raw, P) + D_raw
						float dist = math.dot(otherPlaneTree.xyz, vertPos) + otherPlaneTree.w;
						if (math.abs(dist) < 0.005f)
							smoothedNormal += subtractiveWorkflow ? -otherPlaneTree.xyz : otherPlaneTree.xyz;
					}

					var rv = renderVertices[v];
					rv.normal = math.normalizesafe(smoothedNormal, finalFaceNormal);
					renderVertices[v] = rv;
				}

				alignedPlanes.Dispose();
			}

			var flags = baseSurfaces[surf].destinationFlags;
			var outputFlags = baseSurfaces[surf].outputFlags;
			var parms = baseSurfaces[surf].destinationParameters;
			var UV0 = baseSurfaces[surf].UV0;
			var uvMat = math.mul(UV0.ToFloat4x4(), treeToPlane);
			// A surface whose material has a hotspot layout takes its uvs from the tile that fits what it draws
			var hotspot = baseSurfaces[surf].hotspotLayout;
			var sectionInput  = default(HotspotSectionInput);
			var hotspotInfo   = default(HotspotSurfaceInfo);
			var hotspotSource = default(HotspotSurfaceSource);
			if (hotspot > 0 && hotspot <= hotspotLayouts.Length && hotspotLayouts[hotspot - 1].tileCount > 0)
			{
				var layout  = hotspotLayouts[hotspot - 1];
				// the faces round a pole, close enough to a flat circle, take one radial tile together, as their first
				// face's settings ask
				using var capFaces   = new NativeList<int>(16, Allocator.Temp);
				using var capCorners = new NativeList<float3>(64, Allocator.Temp);
				var cap  = HotspotCaps.Find(ref basePolygonCache[brushNodeOrder].Value, surf, capFaces, capCorners, out var capNormal);
				var lead = cap ? capFaces[0] : surf;
				var hotspotRight = float3.zero; var hotspotUp = float3.zero; var shape = default(HotspotShape);
				// locked to the object, its units: how many of the brush's own units a unit of the tree is (its scale)
				var ownUnits = 1.0f;
				if (cap)
				{
					if (math.dot(capNormal, finalFaceNormal) < 0)
						capNormal = -capNormal;
					var localNormal = math.mul(math.transpose(new float3x3(nodeToTree)), capNormal);
					// locked to the object, in the brush's own space and units; otherwise the tree's
					if (baseSurfaces[lead].textureInWorld)
						HotspotMatcher.Frame(capNormal, baseSurfaces[lead].hotspotRotation, baseSurfaces[lead].hotspotMirror, out hotspotRight, out hotspotUp);
					else
					{
						HotspotMatcher.Frame(capNormal, localNormal, nodeToTree, baseSurfaces[lead].hotspotRotation, baseSurfaces[lead].hotspotMirror, out hotspotRight, out hotspotUp);
						ownUnits = HotspotMatcher.OwnUnits(capNormal, math.normalizesafe(localNormal), math.inverse(nodeToTree), baseSurfaces[lead].hotspotRotation, baseSurfaces[lead].hotspotMirror);
					}
					shape = HotspotMatcher.ShapeOf(capCorners.AsArray(), hotspotRight, hotspotUp);
					cap = shape.radial;
				}
				if (!cap)
				{
					lead = surf;
					if (baseSurfaces[surf].textureInWorld)
						HotspotMatcher.Frame(finalFaceNormal, baseSurfaces[surf].hotspotRotation, baseSurfaces[surf].hotspotMirror, out hotspotRight, out hotspotUp);
					else
					{
						// the brush's own normal, facing as the drawn face does (inwards in the subtractive workflow)
						var ownNormal = math.normalizesafe(baseSurfaces[surf].localPlane.xyz);
						var toTree = math.transpose(math.inverse(nodeToTree));
						if (math.dot(math.mul(toTree, new float4(ownNormal, 0)).xyz, finalFaceNormal) < 0)
							ownNormal = -ownNormal;
						HotspotMatcher.Frame(finalFaceNormal, baseSurfaces[surf].localPlane.xyz, nodeToTree, baseSurfaces[surf].hotspotRotation, baseSurfaces[surf].hotspotMirror, out hotspotRight, out hotspotUp);
						ownUnits = HotspotMatcher.OwnUnits(finalFaceNormal, ownNormal, math.inverse(nodeToTree), baseSurfaces[surf].hotspotRotation, baseSurfaces[surf].hotspotMirror);
					}
					shape = HotspotMatcher.ShapeOf(uniqueVertexMapper.surfaceColliderVertices.AsArray(), hotspotRight, hotspotUp);
					HotspotMatcher.MaskOf(ref shape, uniqueVertexMapper.surfaceColliderVertices.AsArray(), surfaceIndexList.AsArray(), hotspotRight, hotspotUp);
				}
				// measured in the brush's own units when locked: a scaled brush's texture scales with it
				var density = (baseSurfaces[lead].hotspotDensity > 0 ? baseSurfaces[lead].hotspotDensity : 1) * ownUnits;
				var seed   = math.hash(new uint2(baseSurfaces[lead].hotspotSeed, (uint)baseSurfaces[lead].descriptionIndex));
				var choice = HotspotMatcher.Choose(hotspotTiles, layout, shape, seed, baseSurfaces[lead].hotspotPinned, density: density,
													pinnedSheet: baseSurfaces[lead].hotspotPinnedSheet);
				// coplanar neighbours with this layout and frame, of any brush, are matched again together (HotspotGroups)
				if (!cap)
					hotspotSource = new HotspotSurfaceSource { layout = hotspot, right = hotspotRight, up = hotspotUp, normal = finalFaceNormal, seed = seed,
															   pinned = baseSurfaces[surf].hotspotPinned, pinnedSheet = baseSurfaces[surf].hotspotPinnedSheet,
															   density = density, offset = baseSurfaces[surf].hotspotOffset,
															   world = baseSurfaces[surf].textureInWorld };
				if (choice.tile >= 0)
				{
					// the tile's own sheet (another sheet drawn with the layout, its own texture and material)
					var sheet = HotspotMatcher.SheetOf(hotspotTiles[choice.tile], hotspotLayouts, layout);
					var onOtherSheet = hotspotTiles[choice.tile].sheet > 0;
					layout = sheet;
					uvMat = HotspotMatcher.Mapping(hotspotTiles[choice.tile], layout, shape, choice, hotspotRight, hotspotUp);
					// a tile that repeats, has a border, or whose repeats are moved along (a cap is mapped whole): what is drawn is cut
					// where its sections meet, after the model's weld (OutputHotspotSections)
					if (!cap && (hotspotTiles[choice.tile].border > 0 || choice.Repeated || math.any(baseSurfaces[surf].hotspotOffset != 0) ||
								 (baseSurfaces[surf].textureInWorld && (hotspotTiles[choice.tile].flags & HotspotTileFlags.PartialRepeats) != 0)))
						sectionInput = HotspotSections.Input(hotspotTiles[choice.tile], layout, shape, choice, hotspotRight, hotspotUp, density, baseSurfaces[surf].hotspotOffset,
															 baseSurfaces[surf].textureInWorld);
					var pinnedId = baseSurfaces[lead].hotspotPinned;
					hotspotInfo = HotspotDiagnostics.Info(hotspotTiles[choice.tile], layout, choice, sectionInput,
														  pinnedId != 0 && hotspotTiles[choice.tile].id == pinnedId &&
														  hotspotTiles[choice.tile].sheetIndex == baseSurfaces[lead].hotspotPinnedSheet, density);
					// the other sheet's material, or one of the materials drawn on the layout, as the seed picked it
					if (onOtherSheet)
					{
						if (layout.material != 0)
							parms.parameter1 = layout.material;
					} else if (layout.variantCount > 1 && layout.variantStart + choice.variant < hotspotVariants.Length)
						parms.parameter1 = hotspotVariants[layout.variantStart + choice.variant];
				}
			}
			
			MeshAlgorithms.ComputeUVs(uniqueVertexMapper.surfaceRenderVertices, uvMat);
			MeshAlgorithms.ComputeTangents(uniqueVertexMapper.surfaceRenderVertices, finalFaceNormal, uvMat);
			MeshAlgorithms.ComputeLightmapCoordinates(uniqueVertexMapper.surfaceRenderVertices, finalFaceNormal);

			// Decals on this surface, each in its own slot
			var candidateStart = decalCandidateStart[surf];
			var candidateCount = decalCandidateStart[surf + 1] - candidateStart;
			var decalCut = false;
			if (candidateCount > 0)
			{
				var candidates = new UnsafeList<int>(decalCandidates.GetUnsafeReadOnlyPtr() + candidateStart, candidateCount);
				var targeted   = new UnsafeList<byte>(decalTargeted.GetUnsafeReadOnlyPtr() + candidateStart, candidateCount);
				decalCut = decalBuilder.Build(surfaceIndexList, uniqueVertexMapper.surfaceRenderVertices,
											  uniqueVertexMapper.surfaceSelectVertices, decalVolumes, candidates, targeted, flags);
				for (int k = 0; k < candidateCount; k++)
				{
					var decalOutput = decalBuilder.decals[k];
					if (decalOutput.IsEmpty)
						continue;
					var volume     = decalVolumes[decalCandidates[candidateStart + k]];
					var decalParms = parms;
					decalParms.parameter1 = volume.renderMaterial;
					if (volume.IsTransparent)
						decalParms.parameter2 = 0;
					MeshAlgorithms.ComputeTangents(decalOutput.indices, decalOutput.renderVertices);
					MeshAlgorithms.ComputeLightmapCoordinates(decalOutput.renderVertices, finalFaceNormal);
					var slot = baseSurfaceCount + candidateStart + k;
					surfaceBuffers[slot].Construct(builder, decalOutput.indices, decalOutput.colliderVertices,
												   decalOutput.selectVertices, decalOutput.renderVertices,
												   slot, surf, volume.GetDestinationFlags(flags), decalParms, outputFlags);
					surfaceBuffers[slot].decalEntityID = volume.entityID;
					// the volumes stack lowest first
					if (volume.IsTransparent)
						surfaceBuffers[slot].decalStack = decalCandidates[candidateStart + k] + 1;
				}
			}

			ref var buf = ref surfaceBuffers[surf];
			if (decalCut)
			{
				// An opaque decal took part of this surface: the rest of it is drawn instead
				var remaining = decalBuilder.surface;
				MeshAlgorithms.ComputeUVs(remaining.renderVertices, uvMat);
				MeshAlgorithms.ComputeTangents(remaining.renderVertices, finalFaceNormal, uvMat);
				MeshAlgorithms.ComputeLightmapCoordinates(remaining.renderVertices, finalFaceNormal);
				buf.Construct(builder, remaining.indices, remaining.colliderVertices,
							  remaining.selectVertices, remaining.renderVertices,
							  surf, surf, flags & ~SurfaceDestinationFlags.Collidable, parms, outputFlags);

				// and the collider keeps the whole surface, which the weld moves as it moves a decal's pieces
				if ((flags & SurfaceDestinationFlags.Collidable) != 0)
				{
					var copySlot = slots.FirstCopySlot + surf;
					ref var copy = ref surfaceBuffers[copySlot];
					copy.Construct(builder, surfaceIndexList,
								   uniqueVertexMapper.surfaceColliderVertices,
								   uniqueVertexMapper.surfaceSelectVertices,
								   uniqueVertexMapper.surfaceRenderVertices,
								   copySlot, surf, SurfaceDestinationFlags.Collidable, parms, outputFlags);
					copy.decalEntityID = CuttingDecal(decalCandidates, candidateStart, candidateCount);
				}
			} else
			buf.Construct(builder, surfaceIndexList,
						uniqueVertexMapper.surfaceColliderVertices,
						uniqueVertexMapper.surfaceSelectVertices,
						uniqueVertexMapper.surfaceRenderVertices,
						surf, surf, flags, parms, outputFlags);
			buf.sectionInput = sectionInput;
			buf.hotspot       = hotspotInfo;
			buf.hotspotSource = hotspotSource;

			if (HasBackFace(flags, outputFlags))
				FinishBackFace(surf, entityID, flags, parms, outputFlags, surfaceIndexList, ref uniqueVertexMapper, builder, surfaceBuffers, slots);
		}

		// A double sided surface that is drawn: its back is drawn too (SurfaceOutputFlags.DoubleSided)
		static bool HasBackFace(SurfaceDestinationFlags flags, SurfaceOutputFlags outputFlags)
		{
			return (outputFlags & SurfaceOutputFlags.DoubleSided) != 0 && (flags & SurfaceDestinationFlags.Renderable) != 0;
		}

		static bool AnyBackFaces(ref BlobArray<BaseSurface> baseSurfaces, int baseSurfaceCount)
		{
			for (int s = 0; s < baseSurfaceCount; s++)
			{
				if (HasBackFace(baseSurfaces[s].destinationFlags, baseSurfaces[s].outputFlags))
					return true;
			}
			return false;
		}

		// The back of a double sided surface, in its own slot: its triangles turned over, normal and tangent the other way.
		// It is only drawn, never collides. Marked with the brush's entity: like a decal's pieces it follows the surface
		// through the weld, and picking it picks the brush. It is drawn with the surface's own uvs (no decals, no hotspot).
		void FinishBackFace(int surf, ulong entityID, SurfaceDestinationFlags flags, SurfaceDestinationParameters parms, SurfaceOutputFlags outputFlags,
							NativeList<int> surfaceIndexList, ref UniqueVertexMapper uniqueVertexMapper, BlobBuilder builder,
							BlobBuilderArray<ChiselSurfaceRenderBuffer> surfaceBuffers, SlotLayout layout)
		{
			if (surfaceIndexList.Length < 3)
				return;
			using var indices          = new NativeList<int>(surfaceIndexList.Length, Allocator.Temp);
			using var colliderVertices = new NativeList<float3>(uniqueVertexMapper.surfaceColliderVertices.Length, Allocator.Temp);
			using var selectVertices   = new NativeList<SelectVertex>(uniqueVertexMapper.surfaceSelectVertices.Length, Allocator.Temp);
			using var renderVertices   = new NativeList<RenderVertex>(uniqueVertexMapper.surfaceRenderVertices.Length, Allocator.Temp);
			for (int i = 0; i + 2 < surfaceIndexList.Length; i += 3)
			{
				indices.Add(surfaceIndexList[i]);
				indices.Add(surfaceIndexList[i + 2]);
				indices.Add(surfaceIndexList[i + 1]);
			}
			colliderVertices.AddRange(uniqueVertexMapper.surfaceColliderVertices.AsArray());
			selectVertices.AddRange(uniqueVertexMapper.surfaceSelectVertices.AsArray());
			for (int v = 0; v < uniqueVertexMapper.surfaceRenderVertices.Length; v++)
			{
				var vertex = uniqueVertexMapper.surfaceRenderVertices[v];
				vertex.normal  = -vertex.normal;
				vertex.tangent = new float4(vertex.tangent.xyz, -vertex.tangent.w);
				renderVertices.Add(vertex);
			}
			var slot = layout.FirstBackSlot + surf;
			ref var back = ref surfaceBuffers[slot];
			back.Construct(builder, indices, colliderVertices, selectVertices, renderVertices,
						   slot, surf, flags & ~SurfaceDestinationFlags.Collidable, parms, outputFlags & ~SurfaceOutputFlags.DoubleSided);
			back.decalEntityID = entityID;
		}

		// A brush's slots: its surfaces, a slot per decal candidate, and when it has candidates, a slot per surface for
		// the uncut collider copy of a surface an opaque decal cut
		// and, while the Discarded debug visualization is shown and the CSG removed something, a slot per surface for that;
		// and with a double sided surface, a slot per surface for the back faces
		struct SlotLayout
		{
			public int  baseSurfaceCount;
			public int  candidateCount;
			public bool discarded;
			public bool backFaces;

			public readonly int FirstCopySlot      => baseSurfaceCount + candidateCount;
			public readonly int FirstDiscardedSlot => FirstCopySlot + (candidateCount > 0 ? baseSurfaceCount : 0);
			public readonly int FirstBackSlot      => FirstDiscardedSlot + (discarded ? baseSurfaceCount : 0);
			public readonly int Count              => FirstBackSlot + (backFaces ? baseSurfaceCount : 0);
		}

		// The category ExactCSGJob writes the removed part of a face with
		const CategoryIndex kDiscardedPart = CategoryIndex.Inside;

		// What the CSG removed of a surface, in its own slot, only for the Discarded debug visualization. Marked with the
		// brush's own entity: like a decal's pieces it is drawn over the output, so the weld and hotspot mapping leave it
		// alone, and picking it picks the brush.
		void FinishDiscarded(int surf, ref BlobArray<BaseSurface> baseSurfaces, NativeList<int4> parts, NativeList<int> partIndices,
							 UnsafeList<float3> vertices, float3 finalFaceNormal, ulong entityID, NativeList<int> surfaceIndexList,
							 ref UniqueVertexMapper uniqueVertexMapper, BlobBuilder builder,
							 BlobBuilderArray<ChiselSurfaceRenderBuffer> surfaceBuffers, SlotLayout layout)
		{
			for (int p = 0; p < parts.Length; p++)
			{
				var part = parts[p];
				if (part.x != surf || part.w < 3 || part.y != (int)kDiscardedPart)
					continue;
				var prevCount = surfaceIndexList.Length;
				for (int t = part.z; t + 2 < part.z + part.w; t += 3)
				{
					surfaceIndexList.Add(partIndices[t]);
					surfaceIndexList.Add(subtractiveWorkflow ? partIndices[t + 2] : partIndices[t + 1]);
					surfaceIndexList.Add(subtractiveWorkflow ? partIndices[t + 1] : partIndices[t + 2]);
				}
				uniqueVertexMapper.RegisterVertices(surfaceIndexList, prevCount, vertices, finalFaceNormal, entityID, CategoryIndex.ValidAligned);
			}
			if (surfaceIndexList.Length == 0)
				return;
			var slot = layout.FirstDiscardedSlot + surf;
			ref var buffer = ref surfaceBuffers[slot];
			buffer.Construct(builder, surfaceIndexList,
							 uniqueVertexMapper.surfaceColliderVertices,
							 uniqueVertexMapper.surfaceSelectVertices,
							 uniqueVertexMapper.surfaceRenderVertices,
							 slot, surf, SurfaceDestinationFlags.Discarded, baseSurfaces[surf].destinationParameters, baseSurfaces[surf].outputFlags);
			buffer.decalEntityID = entityID;
		}

		// The topmost opaque decal on the surface: the collider copy is marked as the decal's, like its pieces
		ulong CuttingDecal(NativeList<int> decalCandidates, int candidateStart, int candidateCount)
		{
			for (int k = candidateCount - 1; k > 0; k--)
			{
				var volume = decalVolumes[decalCandidates[candidateStart + k]];
				if (!volume.IsTransparent)
					return volume.entityID;
			}
			return decalVolumes[decalCandidates[candidateStart]].entityID;
		}

		// After every surface: the empty decal slots, the mesh queries, and the brush's render buffer in the cache.
		void FinishBrush(IndexOrder brushIndexOrder, int brushNodeOrder, BlobBuilder builder, ref ChiselBrushRenderBuffer root,
						 BlobBuilderArray<ChiselSurfaceRenderBuffer> surfaceBuffers, NativeArray<int> decalCandidateStart, SlotLayout layout)
		{
			var baseSurfaceCount = layout.baseSurfaceCount;
			// Decal slots nothing was drawn in still say which surface they belong to
			for (int s = 0; s < baseSurfaceCount; s++)
			{
				for (int c = decalCandidateStart[s]; c < decalCandidateStart[s + 1]; c++)
				{
					ref var emptySlot = ref surfaceBuffers[baseSurfaceCount + c];
					if (emptySlot.vertexCount != 0)
						continue;
					emptySlot.surfaceIndex     = baseSurfaceCount + c;
					emptySlot.baseSurfaceIndex = s;
				}
			}
			var firstCopySlot      = layout.FirstCopySlot;
			var firstDiscardedSlot = layout.FirstDiscardedSlot;
			var firstBackSlot      = layout.FirstBackSlot;
			for (int s = firstCopySlot; s < surfaceBuffers.Length; s++)
			{
				ref var emptySlot = ref surfaceBuffers[s];
				if (emptySlot.vertexCount != 0)
					continue;
				emptySlot.surfaceIndex     = s;
				emptySlot.baseSurfaceIndex = (s - firstCopySlot) % baseSurfaceCount;
			}

			using var queryList = new NativeList<ChiselQuerySurface>(surfaceBuffers.Length, Allocator.Temp);
			var queryArr = builder.Allocate(ref root.querySurfaces, meshQueries.Length);
			for (int t = 0; t < meshQueries.Length; t++)
			{
				var meshQuery = meshQueries[t];
				var layerQueryMask = meshQuery.LayerQueryMask;
				var layerQuery = meshQuery.LayerQuery;
				// a collider copy only goes to colliders, and what the CSG removed only to the Discarded visualization, never
				// to the hidden surfaces
				var colliderQuery  = (layerQuery & SurfaceDestinationFlags.Collidable) != 0;
				var discardedQuery = (layerQuery & SurfaceDestinationFlags.Discarded) != 0;
				var surfaceParameterIndex = (meshQuery.LayerParameterIndex >= SurfaceParameterIndex.Parameter1 &&
												meshQuery.LayerParameterIndex <= SurfaceParameterIndex.MaxParameterIndex) ?
												(int)meshQuery.LayerParameterIndex - 1 : -1;

				queryList.Clear();
				for (int s = 0; s < surfaceBuffers.Length; s++)
				{
					ref var buffer = ref surfaceBuffers[s];
					// A slot that has no triangles is left out: a surface that was skipped above was never
					// constructed, so its surfaceIndex is 0 and would make its entry read surface 0.
					if (buffer.vertexCount == 0 || buffer.indexCount == 0)
						continue;
					var destinationFlags = buffer.destinationFlags;
					if ((destinationFlags & layerQueryMask) != layerQuery)
						continue;
					if (s >= firstCopySlot && s < firstDiscardedSlot && !colliderQuery)
						continue;
					if (s >= firstDiscardedSlot && s < firstBackSlot && !discardedQuery)
						continue;

					queryList.AddNoResize(new ChiselQuerySurface
					{
						surfaceIndex      = s,
						surfaceParameter  = surfaceParameterIndex < 0 ? 0 : buffer.destinationParameters.parameters[surfaceParameterIndex],
						vertexCount       = buffer.vertexCount,
						indexCount        = buffer.indexCount,
						surfaceHashValue  = buffer.surfaceHashValue,
						geometryHashValue = buffer.geometryHashValue
					});
				}
				queryList.Sort(kCompareSortByBasePlaneIndex);

				builder.Construct(ref queryArr[t].surfaces, queryList);
				queryArr[t].brushNodeID = brushIndexOrder.compactNodeID;
			}


			root.surfaceOffset = 0;
			root.surfaceCount = surfaceBuffers.Length;

			var brushRenderBuffer = builder.CreateBlobAssetReference<ChiselBrushRenderBuffer>(Allocator.Persistent);
			if (brushRenderBufferCache[brushNodeOrder].IsCreated)
			{
				brushRenderBufferCache[brushNodeOrder].Dispose();
				brushRenderBufferCache[brushNodeOrder] = default;
			}
			brushRenderBufferCache[brushNodeOrder] = brushRenderBuffer;
		}

		// The frame a surface is textured in, and the normal its vertices get.
		void SurfaceFrame(ref BlobArray<BaseSurface> baseSurfaces, int surf, float4x4 treeToNode, float4x4 nodeToTreeInvTrans,
						  out float4x4 treeToPlane, out float3 finalFaceNormal)
		{
			var plane = baseSurfaces[surf].localPlane;
			var planeNormalMap = math.mul(nodeToTreeInvTrans, plane);
			// uvs relative to the object (locked), or to the world: from the plane as it lies in the tree
			if (baseSurfaces[surf].textureInWorld)
			{
				var treePlane = planeNormalMap / math.length(planeNormalMap.xyz);
				treeToPlane = MathExtensions.GenerateLocalToPlaneSpaceMatrix(treePlane);
			} else
			{
				var localToPlane = MathExtensions.GenerateLocalToPlaneSpaceMatrix(plane);
				treeToPlane = math.mul(localToPlane, treeToNode);
			}
			var map3DTo2D = new Map3DTo2D(planeNormalMap.xyz);
			finalFaceNormal = map3DTo2D.normal;
			if (subtractiveWorkflow)
				finalFaceNormal = -finalFaceNormal;   // lighting for the "inside"
		}

		public unsafe void Execute(int index)
		{
			var count = input.BeginForEachIndex(index);
			if (count == 0)
				return;
			var brushIndexOrder = input.Read<IndexOrder>();
			var brushNodeOrder  = brushIndexOrder.nodeOrder;
			var surfaceCount    = input.Read<int>();

			// All parts of all surfaces: vertices concatenated, indices already offset into them.
			var vertices = new UnsafeList<float3>(64, Allocator.Temp);
			NativeList<int> partIndices;
			using var _partIndices = partIndices = new NativeList<int>(256, Allocator.Temp);
			NativeList<int4> parts;     // (surface, category, first index, index count)
			using var _parts = parts = new NativeList<int4>(surfaceCount * 2, Allocator.Temp);
			DecalSurfaceBuilder decalBuilder = default;
			try
			{
				for (int s = 0; s < surfaceCount; s++)
				{
					var partCount = input.Read<int>();
					for (int p = 0; p < partCount; p++)
					{
						var category     = input.Read<int>();
						var vertexCount  = input.Read<int>();
						var vertexOffset = vertices.Length;
						for (int v = 0; v < vertexCount; v++)
							vertices.Add(input.Read<float3>());
						var indexCount = input.Read<int>();
						var first      = partIndices.Length;
						for (int i = 0; i < indexCount; i++)
							partIndices.Add(vertexOffset + input.Read<int>());
						parts.Add(new int4(s, category, first, indexCount));
					}
				}
				input.EndForEachIndex();

				if (!basePolygonCache[brushNodeOrder].IsCreated)
					return;

				ulong entityID = entityIDLookup.SafeGetNodeEntityID(brushIndexOrder.compactNodeID);
				ref var baseSurfaces = ref basePolygonCache[brushNodeOrder].Value.surfaces;
				var transform = transformationCache[brushNodeOrder];
				var treeToNode = transform.treeToNode;
				var nodeToTreeInvTrans = math.transpose(treeToNode);
				var baseSurfaceCount = math.min(surfaceCount, baseSurfaces.Length);

				NativeList<int> decalCandidates;
				using var _decalCandidates = decalCandidates = new NativeList<int>(16, Allocator.Temp);
				NativeList<byte> decalTargeted;
				using var _decalTargeted = decalTargeted = new NativeList<byte>(16, Allocator.Temp);
				NativeArray<int> decalCandidateStart;
				using var _decalCandidateStart = decalCandidateStart = new NativeArray<int>(baseSurfaceCount + 1, Allocator.Temp);
				float3 brushMin = float3.zero, brushMax = float3.zero;
				if (vertices.Length > 0)
				{
					brushMin = brushMax = vertices[0];
					for (int v = 1; v < vertices.Length; v++)
					{
						brushMin = math.min(brushMin, vertices[v]);
						brushMax = math.max(brushMax, vertices[v]);
					}
				}
				FindDecalCandidates(ref baseSurfaces, nodeToTreeInvTrans, vertices.Length > 0, brushMin, brushMax, entityID, decalCandidates, decalTargeted, decalCandidateStart);
				if (decalCandidates.Length > 0)
					decalBuilder = DecalSurfaceBuilder.Create(Allocator.Temp);

				UniqueVertexMapper uniqueVertexMapper;
				using var _uniqueVertexMapper = uniqueVertexMapper = new()
				{
					indexRemap = new NativeArray<int>(math.max(1, vertices.Length), Allocator.Temp),
					surfaceColliderVertices = new NativeList<float3>(vertices.Length, Allocator.Temp),
					surfaceSelectVertices = new NativeList<SelectVertex>(vertices.Length, Allocator.Temp),
					surfaceRenderVertices = new NativeList<RenderVertex>(vertices.Length, Allocator.Temp)
				};

				NativeList<int> surfaceIndexList;
				using var _surfaceIndexList = surfaceIndexList = new NativeList<int>(partIndices.Length, Allocator.Temp);

				using var builder = new BlobBuilder(Allocator.Temp, 4096);
				ref var root = ref builder.ConstructRoot<ChiselBrushRenderBuffer>();
				var anyDiscarded = false;
				for (int p = 0; p < parts.Length && !anyDiscarded; p++)
					anyDiscarded = parts[p].y == (int)kDiscardedPart;
				var layout = new SlotLayout { baseSurfaceCount = baseSurfaceCount, candidateCount = decalCandidates.Length, discarded = anyDiscarded,
											  backFaces = AnyBackFaces(ref baseSurfaces, baseSurfaceCount) };
				var surfaceBuffers = builder.Allocate(ref root.surfaces, layout.Count);

				for (int surf = 0; surf < baseSurfaceCount; surf++)
				{
					uniqueVertexMapper.Reset();
					surfaceIndexList.Clear();
					SurfaceFrame(ref baseSurfaces, surf, treeToNode, nodeToTreeInvTrans, out var treeToPlane, out var finalFaceNormal);
					if (anyDiscarded)
					{
						FinishDiscarded(surf, ref baseSurfaces, parts, partIndices, vertices, finalFaceNormal, entityID, surfaceIndexList, ref uniqueVertexMapper,
										builder, surfaceBuffers, layout);
						uniqueVertexMapper.Reset();
						surfaceIndexList.Clear();
					}
					for (int p = 0; p < parts.Length; p++)
					{
						var part = parts[p];
						if (part.x != surf || part.w < 3 || part.y == (int)kDiscardedPart)
							continue;
						var interiorCat = (CategoryIndex)part.y;
						bool flip = (interiorCat == CategoryIndex.ValidAligned) == subtractiveWorkflow;
						var prevCount = surfaceIndexList.Length;
						for (int t = part.z; t + 2 < part.z + part.w; t += 3)
						{
							surfaceIndexList.Add(partIndices[t]);
							surfaceIndexList.Add(flip ? partIndices[t + 2] : partIndices[t + 1]);
							surfaceIndexList.Add(flip ? partIndices[t + 1] : partIndices[t + 2]);
						}
						uniqueVertexMapper.RegisterVertices(surfaceIndexList, prevCount, vertices, finalFaceNormal, entityID, interiorCat);
					}
					if (surfaceIndexList.Length == 0)
						continue;
					FinishSurface(surf, brushNodeOrder, entityID, ref baseSurfaces, treeToPlane, transform.nodeToTree, finalFaceNormal, surfaceIndexList, ref uniqueVertexMapper,
								  builder, surfaceBuffers, ref decalBuilder, decalCandidates, decalTargeted, decalCandidateStart, layout);
				}
				FinishBrush(brushIndexOrder, brushNodeOrder, builder, ref root, surfaceBuffers, decalCandidateStart, layout);
			}
			finally
			{
				decalBuilder.Dispose();
				vertices.Dispose();
			}
		}

		static void BoundsOf(NativeArray<float3> vertices, out float3 min, out float3 max)
		{
			min = max = float3.zero;
			if (vertices.Length == 0)
				return;
			min = max = vertices[0];
			for (int v = 1; v < vertices.Length; v++)
			{
				min = math.min(min, vertices[v]);
				max = math.max(max, vertices[v]);
			}
		}

		unsafe void FindDecalCandidates(ref BlobArray<BaseSurface> baseSurfaces, float4x4 nodeToTreeInvTrans, bool hasVertices, float3 brushMin, float3 brushMax,
										ulong brushEntityID, NativeList<int> candidates, NativeList<byte> targeted, NativeArray<int> start)
		{
			candidates.Clear();
			targeted.Clear();
			for (int s = 0; s < start.Length; s++)
				start[s] = 0;
			if (!decalVolumes.IsCreated || decalVolumes.Length == 0 || !hasVertices)
				return;

			var volumes = (DecalVolume*)decalVolumes.GetUnsafeReadOnlyPtr();

			// The decals near the brush, once for all of its surfaces (a map has thousands of decals)
			using var nearBrush = new NativeList<int>(math.min(decalVolumes.Length, 64), Allocator.Temp);
			for (int d = 0; d < decalVolumes.Length; d++)
			{
				if (volumes[d].Overlaps(brushMin, brushMax))
					nearBrush.Add(d);
			}
			if (nearBrush.Length == 0)
				return;

			var surfaceCount = start.Length - 1;
			for (int s = 0; s < surfaceCount; s++)
			{
				start[s] = candidates.Length;
				if (s >= baseSurfaces.Length ||
					(baseSurfaces[s].destinationFlags & SurfaceDestinationFlags.Renderable) == 0)
					continue;
				var plane  = (double4)math.mul(nodeToTreeInvTrans, baseSurfaces[s].localPlane);
				var length = math.length(plane.xyz);
				if (!(length > 0))
					continue;
				plane /= length;
				for (int i = 0; i < nearBrush.Length; i++)
				{
					var d = nearBrush[i];
					ref var volume = ref volumes[d];
					// Does the surface's plane pass through the decal's bounds?
					var center   = ((double3)volume.boundsMin + (double3)volume.boundsMax) * 0.5;
					var extents  = ((double3)volume.boundsMax - (double3)volume.boundsMin) * 0.5;
					var distance = math.dot(plane.xyz, center) + plane.w;
					var reach    = math.dot(math.abs(plane.xyz), extents);
					if (math.abs(distance) > reach + DecalVolume.kPlaneEpsilon)
						continue;
					var isTargeted = volume.Targets(decalTargets, brushEntityID, baseSurfaces[s].descriptionIndex);
					if (!isTargeted && volume.IsTransparent)
						continue;
					candidates.Add(d);
					targeted.Add(isTargeted ? (byte)1 : (byte)0);
				}
			}
			start[surfaceCount] = candidates.Length;
		}
	}
}

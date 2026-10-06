# Machinery nothing calls

**Generated** by `scratchpad/dead_machinery.py`. Re-run it and diff; do not hand-edit.

Walking the pipeline turned up the same thing four separate times: the careful implementation of something exists and is not called, while a worse inline version is. `BrushMesh.Optimize`'s `SnapPolygonVerticesToItsPlanes`, `GetVertexFromIntersectingPlanes` and `CenterAndSnapPlanes`; `CSGMath`'s `SqrDistance`, `VerticesEqual`, `EdgePlaneCrossing` and `PlaneIntersection` — `EdgePlaneCrossing` computes in double and **five inline float copies of it are live**; `MergeTouchingBrushVerticesJob` in its entirety. Four found by hand is enough reason to enumerate the rest.

## What this is and is not

This is a regular expression, not a C# compiler. It cannot resolve overloads, generics, interface dispatch or reflection. Every entry is a **candidate** to check, and the counts are an upper bound on deadness. Names that Unity, the job system or the language calls for you are excluded outright (`Execute`, `Dispose`, `Compare`, `GetHashCode`, …), because a caller search cannot see those callers — which also means anything reached only through an interface is wrongly listed here.

1494 method declarations in Core's 196 production files, searched for callers across 379 production files and 70 test files in Core, Components and Editor.

- **11** have no production caller but **are covered by tests** — the dangerous class, because the suite reads as green over code that cannot run in anger.
- **47** have no caller anywhere.

## No production caller, but tests exercise it

Read these first. A passing test here proves the function works, not that the pipeline uses it.

| method | declared | test call sites |
|---|---|---|
| `IsTreeUpdateSkipped` | 2.Processing/Managers/CSGManager.SkipUnchangedTrees.cs:34 | 13 |
| `EdgeStraddlesSegmentPlanes` | 2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:149 | 4 |
| `EdgePlaneCrossing` | 2.Processing/CSGMath.cs:35 | 3 |
| `GetPolygonCenter` | 1.Input/BrushMesh/BrushMesh.Utility.cs:2001 | 3 |
| `LineLineIntersection` | ExtensionMethods/Geometry/MathExtensions.cs:635 | 3 |
| `VerticesEqual` | 2.Processing/CSGMath.cs:28 | 2 |
| `CenterAndSnapPlanes` | 1.Input/BrushMesh/BrushMesh.Optimize.cs:120 | 1 |
| `ClearDecals` | 1.Input/Decals/ChiselDecal.cs:205 | 1 |
| `InverseTransform` | ExtensionMethods/Geometry/MathExtensions.cs:239; ExtensionMethods/Geometry/MathExtensions.cs:249; ExtensionMethods/Geometry/MathExtensions.cs:258 | 1 |
| `PlanePlaneIntersection` | ExtensionMethods/Geometry/PlaneExtensions.cs:157 | 1 |
| `SqrDistance` | 2.Processing/CSGMath.cs:24 | 1 |

## No caller anywhere

| method | declared |
|---|---|
| `CSGTree` | 1.Input/Nodes/CSGTreeNode.cs:162 |
| `CheckCapacityInRange` | 2.Processing/Containers/HashedVertices.cs:452 |
| `Choose` | 2.Processing/Containers/WeldIncidence.cs:54 |
| `Common` | ExtensionMethods/Dev/SystemHashSetExtensions.cs:58 |
| `Compact` | 1.Input/CompactHierarchy/CompactHierarchy.public.cs:369 |
| `ContainsAny` | ExtensionMethods/Dev/SystemHashSetExtensions.cs:151 |
| `CreateBrushWireframe` | 3.Output/OutputMeshes/Wireframe/ChiselWireframe.Internal.cs:99 |
| `CreateFromPoints` | 1.Input/GeneratorBase/BrushFactory.Utility.cs:1162 |
| `CreateSurfaceWireframe` | 3.Output/OutputMeshes/Wireframe/ChiselWireframe.Internal.cs:71 |
| `DeleteChildFromParentAt` | 1.Input/CompactHierarchy/CompactHierarchy.public.cs:429 |
| `DeleteChildFromParentRecursiveAt` | 1.Input/CompactHierarchy/CompactHierarchy.public.cs:440 |
| `DeleteChildrenFromParentAt` | 1.Input/CompactHierarchy/CompactHierarchy.public.cs:451 |
| `DeleteChildrenFromParentRecursiveAt` | 1.Input/CompactHierarchy/CompactHierarchy.public.cs:462 |
| `EnsureConstantSizeAndClear` | ExtensionMethods/Jobs/ScratchpadHelpers.cs:121 |
| `EnsureCreatedAndClear` | ExtensionMethods/Jobs/ScratchpadHelpers.cs:111 |
| `ExtractRotation` | ExtensionMethods/Geometry/MathExtensions.cs:372 |
| `FindNoErrors` | 1.Input/Nodes/CSGTree.cs:90; 1.Input/Nodes/CSGTreeBranch.cs:91; 1.Input/Nodes/CSGTreeBrush.cs:91; 1.Input/Nodes/CSGTreeNode.cs:71 |
| `FindOrAppendInProject` | 1.Input/Contents/ChiselContentsList.cs:211 |
| `FreeIndex` | 2.Processing/Managers/SlotIndexMap.cs:601 |
| `Generate` | 1.Input/Generators/Brush/ChiselBrushDefinition.cs:142 |
| `GetCenter` | ExtensionMethods/Dev/BoundsExtensions.cs:142; ExtensionMethods/Dev/BoundsExtensions.cs:145; ExtensionMethods/Dev/BoundsExtensions.cs:148 |
| `GetChildAt` | 1.Input/CompactHierarchy/CompactHierarchy.public.cs:362 |
| `GetChildIDAtInternalNoError` | 1.Input/CompactHierarchy/CompactHierarchy.Internal.cs:531 |
| `GetHashcode` | 1.Input/CompactHierarchy/CompactHierarchy.public.cs:123 |
| `GetHierarchyID` | 2.Processing/Managers/CompactHierarchyManager.cs:552 |
| `GetMatrices` | 1.Input/GeneratorBase/ChiselPathBlob.cs:97 |
| `GetSectionEnd` | 2.Processing/Managers/SectionManager.cs:42 |
| `GetSectionStart` | 2.Processing/Managers/SectionManager.cs:38 |
| `GetSize` | ExtensionMethods/Dev/BoundsExtensions.cs:151; ExtensionMethods/Dev/BoundsExtensions.cs:154; ExtensionMethods/Dev/BoundsExtensions.cs:157 |
| `InsertChildNode` | 2.Processing/Managers/CompactHierarchyManager.cs:1621; 2.Processing/Managers/CompactHierarchyManager.cs:2425 |
| `IntersectionFirst` | ExtensionMethods/Geometry/PlaneExtensions.cs:175 |
| `IntersectionSecond` | ExtensionMethods/Geometry/PlaneExtensions.cs:186 |
| `InvalidFinalCategory` | 2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:62 |
| `InvertWhenInsideOut` | 1.Input/BrushMesh/BrushMesh.Optimize.cs:386 |
| `IsPointInPolygon` | 2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:103 |
| `OnAssemblyReload` | 1.Input/GeneratorBase/GeneratorJobPool.cs:50 |
| `RemoveChildNodeAt` | 2.Processing/Managers/CompactHierarchyManager.cs:1834; 2.Processing/Managers/CompactHierarchyManager.cs:2437 |
| `RemoveChildNodeRange` | 2.Processing/Managers/CompactHierarchyManager.cs:1863; 2.Processing/Managers/CompactHierarchyManager.cs:2441 |
| `SetInvalid` | 1.Input/Nodes/CSGTree.cs:131; 1.Input/Nodes/CSGTreeBranch.cs:132; 1.Input/Nodes/CSGTreeBrush.cs:132; 1.Input/Nodes/CSGTreeNode.cs:113 |
| `ShouldDeallocate` | 2.Processing/Containers/HashedVertices.cs:551 |
| `SmoothingGroup` | 1.Input/Surfaces/SurfaceDetails.cs:13 |
| `SnapPolygonVerticesToItsPlanes` | 1.Input/BrushMesh/BrushMesh.Optimize.cs:28 |
| `SnapToExistingVertex` | 2.Processing/Containers/HashedVertices.cs:27 |
| `ToMatrix4x4` | 1.Input/Surfaces/UVMatrix.cs:54 |
| `ToNativeList` | ExtensionMethods/Dev/NativeCollectionExtensions.cs:694; ExtensionMethods/Dev/NativeCollectionExtensions.cs:705; ExtensionMethods/Dev/NativeCollectionExtensions.cs:716 |
| `UnregisterParameter` | 2.Processing/Jobs/JobData/ChiselLayerParameters.cs:31 |
| `uint` | 1.Input/Surfaces/SurfaceDetails.cs:12 |


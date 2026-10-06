# Tolerance inventory

**Generated** by `scratchpad/tolerance_inventory.py`. Do not hand-edit — re-run it, and diff.

196 source files under `Core/` (tests excluded): **57 declared tolerances** across **36 distinct names**, **307 uses**, and **24 inline literals** with no named constant.

The declaration count is cross-checked against a second, independently written detector; the script refuses to write this file if they disagree.

**Known imprecision, stated rather than hidden:** where one file declares the same name twice - `BrushMesh.Optimize.cs` has `kDistanceEpsilon` at class scope and again inside a method - the use counts cannot tell which declaration a bare mention refers to, so both are credited with all of them. The use LISTS are still complete; only the per-declaration split is wrong, and only for the names in the section above.

## Names declared in more than one file

Each is a separate tolerance that happens to share a name, and they do not have to agree. Any inventory keyed by name alone silently collapses these — which is how the first version of this script reported 35 where there are more.

- `kBoundsDistanceEpsilon` — 2×: 2.Processing/CSGConstants.cs:15, 2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:530
- `kDistanceEpsilon` — 5×: 1.Input/BrushMesh/BrushMesh.Optimize.cs:12, 1.Input/BrushMesh/BrushMesh.Optimize.cs:607, 1.Input/GeneratorBase/BrushFactory.Utility.cs:684, 2.Processing/Thirdparty/BayazitDecomposerBursted.cs:31, ExtensionMethods/Geometry/MathExtensions.cs:19
- `kEpsilon` — 10×: 1.Input/Generators/LinearStairs/ChiselLinearStairsBrushFactory.cs:1084, 1.Input/Generators/PathedStairs/ChiselPathedStairsBrushFactory.cs:196, 1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:14, 1.Input/Generators/SpiralStairs/ChiselSpiralStairsBrushFactory.cs:18, 1.Input/Generators/SpiralStairs/ChiselSpiralStairsDefinition.cs:80, 2.Processing/Decals/DecalClipping.cs:135, ExtensionMethods/Geometry/MathExtensions.cs:148, ExtensionMethods/Geometry/MathExtensions.cs:374, ExtensionMethods/Geometry/PlaneExtensions.cs:47, ExtensionMethods/Geometry/PlaneExtensions.cs:119
- `kFatPlaneWidthEpsilon` — 5×: 2.Processing/CSGConstants.cs:17, 2.Processing/Jobs/CreateIntersectionLoopsJob.cs:19, 2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:20, 2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:27, 2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:34
- `kNormalDotAlignEpsilon` — 2×: 2.Processing/CSGConstants.cs:24, 2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:29
- `kOnPlane` — 2×: 2.Processing/Containers/CanonicalVertices.cs:162, 2.Processing/Containers/WeldIncidence.cs:28
- `kSqrVertexEqualEpsilon` — 2×: 2.Processing/CSGConstants.cs:22, 2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:19

## 1. input / generators

| constant | declared | uses | in comments | declaration |
|---|---|---|---|---|
| `kDistanceEpsilon` | 1.Input/BrushMesh/BrushMesh.Optimize.cs:12 | 14 | 0 | `const float kDistanceEpsilon = 0.00001f;` |
| `kEqualityEpsilon` | 1.Input/BrushMesh/BrushMesh.Optimize.cs:13 | 0 | 0 | `const float kEqualityEpsilon = 0.0001f;` |
| `kDistanceEpsilon` | 1.Input/BrushMesh/BrushMesh.Optimize.cs:607 | 14 | 0 | `const float kDistanceEpsilon = 0.0001f; // TODO: why??` |
| `kDistanceEpsilon` | 1.Input/GeneratorBase/BrushFactory.Utility.cs:684 | 1 | 0 | `const float kDistanceEpsilon = 0.0000001f;` |
| `kHeightEpsilon` | 1.Input/Generators/Capsule/ChiselCapsuleDefinition.cs:48 | 3 | 0 | `const float kHeightEpsilon = 0.001f;` |
| `kEpsilon` | 1.Input/Generators/LinearStairs/ChiselLinearStairsBrushFactory.cs:1084 | 8 | 0 | `const float kEpsilon = 0.001f;` |
| `kEpsilon` | 1.Input/Generators/PathedStairs/ChiselPathedStairsBrushFactory.cs:196 | 5 | 0 | `const float kEpsilon = 0.0001f;` |
| `kEpsilon` | 1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:14 | 12 | 0 | `const float kEpsilon = CSGConstants.kFatPlaneWidthEpsilon;` |
| `kEpsilon` | 1.Input/Generators/SpiralStairs/ChiselSpiralStairsBrushFactory.cs:18 | 7 | 0 | `const float kEpsilon = 0.001f;` |
| `kEpsilon` | 1.Input/Generators/SpiralStairs/ChiselSpiralStairsDefinition.cs:80 | 6 | 0 | `const float kEpsilon = 0.001f;` |
| `kNoCenterEpsilon` | 1.Input/Generators/Stadium/ChiselStadiumDefinition.cs:39 | 3 | 0 | `internal const float kNoCenterEpsilon = 0.0001f;` |
| `kSnapEpsilon` | 1.Input/Surfaces/UVMatrix.cs:18 | 0 | 0 | `const double kSnapEpsilon       = kScaleStep / 10.0f;` |

## 2. broad phase

| constant | declared | uses | in comments | declaration |
|---|---|---|---|---|
| `kBoundsDistanceEpsilon` | 2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:530 | 8 | 0 | `public const double kBoundsDistanceEpsilon = CSGConstants.kBoundsDistanceEpsilon;` |

## 3. planes

| constant | declared | uses | in comments | declaration |
|---|---|---|---|---|
| `kEpsilon` | ExtensionMethods/Geometry/PlaneExtensions.cs:47 | 14 | 2 | `const double kEpsilon = 0.0006f;` |
| `kEpsilon` | ExtensionMethods/Geometry/PlaneExtensions.cs:119 | 14 | 2 | `const double kEpsilon = 0.00001f;` |

## 4. pair preparation

| constant | declared | uses | in comments | declaration |
|---|---|---|---|---|
| `kFatPlaneWidthEpsilon` | 2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:27 | 12 | 0 | `const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;` |
| `kPlaneWAlignEpsilon` | 2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:28 | 3 | 0 | `const float kPlaneWAlignEpsilon         = CSGConstants.kPlaneDAlignEpsilon;` |
| `kNormalDotAlignEpsilon` | 2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:29 | 6 | 1 | `const float kNormalDotAlignEpsilon      = CSGConstants.kNormalDotAlignEpsilon;` |
| `kPlaneVertexAlignEpsilon` | 2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:328 | 2 | 0 | `const float kPlaneVertexAlignEpsilon = 0.01f;   // < kVertexEqualEpsilon (0.0125), below w` |

## 5. intersection loops

| constant | declared | uses | in comments | declaration |
|---|---|---|---|---|
| `kFatPlaneWidthEpsilon` | 2.Processing/Jobs/CreateIntersectionLoopsJob.cs:19 | 15 | 0 | `const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;` |

## 6. loop overlap / splitting

| constant | declared | uses | in comments | declaration |
|---|---|---|---|---|
| `kSqrVertexEqualEpsilon` | 2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:19 | 15 | 0 | `const float kSqrVertexEqualEpsilon      = CSGConstants.kSqrVertexEqualEpsilon;` |
| `kFatPlaneWidthEpsilon` | 2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:20 | 20 | 0 | `const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;` |

## 7. welding

| constant | declared | uses | in comments | declaration |
|---|---|---|---|---|
| `kOnPlane` | 2.Processing/Containers/CanonicalVertices.cs:162 | 5 | 0 | `internal const double kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;` |
| `kSameVertex` | 2.Processing/Containers/CanonicalVertices.cs:168 | 1 | 0 | `internal const float kSameVertex    = 0.00001f;` |
| `kCellSize` | 2.Processing/Containers/HashedVertices.cs:405 | 13 | 0 | `internal const float    kCellSize       = CSGConstants.kVertexEqualEpsilon * 2.5f;` |
| `kOnPlane` | 2.Processing/Containers/WeldIncidence.cs:28 | 6 | 0 | `const float kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;` |
| `kOffPlane` | 2.Processing/Containers/WeldIncidence.cs:35 | 2 | 0 | `const float kOffPlane = kOnPlane * 2;` |

## 8. csg

| constant | declared | uses | in comments | declaration |
|---|---|---|---|---|
| `kFatPlaneWidthEpsilon` | 2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:34 | 11 | 0 | `const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;` |
| `kSameRegionEpsilon` | 2.Processing/Jobs/PerformCSGJob.cs:177 | 1 | 1 | `const float kSameRegionEpsilon = CSGConstants.kVertexEqualEpsilon * 2;` |
| `kCanonicalSameRegionEpsilon` | 2.Processing/Jobs/PerformCSGJob.cs:182 | 1 | 0 | `const float kCanonicalSameRegionEpsilon = CSGConstants.kFatPlaneWidthEpsilon;` |

## 9. triangulation / output

| constant | declared | uses | in comments | declaration |
|---|---|---|---|---|
| `kNormalizeEpsilon` | 2.Processing/Algorithms/MeshAlgorithms.cs:697 | 1 | 0 | `const double kNormalizeEpsilon = 1e-6;` |
| `kConvexTestEpsilon` | 2.Processing/Thirdparty/BayazitDecomposerBursted.cs:30 | 1 | 0 | `const float kConvexTestEpsilon = 0.00001f;` |
| `kDistanceEpsilon` | 2.Processing/Thirdparty/BayazitDecomposerBursted.cs:31 | 1 | 0 | `const float kDistanceEpsilon = 0.0006f;` |
| `epsilon` | 2.Processing/Thirdparty/ConvexHullCalculator.cs:319 | 3 | 1 | `const float epsilon = 0.001f;` |
| `kDefaultWeldEpsilon` | 3.Output/OutputMeshes/MeshManifoldValidation.cs:49 | 1 | 0 | `public const float kDefaultWeldEpsilon = 0.0001f;` |

## 10. decals

| constant | declared | uses | in comments | declaration |
|---|---|---|---|---|
| `kEpsilon` | 2.Processing/Decals/DecalClipping.cs:135 | 11 | 0 | `const double kEpsilon = DecalVolume.kPlaneEpsilon;` |
| `kMinArea` | 2.Processing/Decals/DecalClipping.cs:137 | 5 | 0 | `public const double kMinArea = 1e-10;` |
| `kPlaneEpsilon` | 2.Processing/Decals/DecalVolume.cs:61 | 9 | 0 | `public const double kPlaneEpsilon = 1e-4;` |

## 11. shared math

| constant | declared | uses | in comments | declaration |
|---|---|---|---|---|
| `kPlaneDAlignEpsilonDouble` | 2.Processing/CSGConstants.cs:7 | 1 | 0 | `const double        kPlaneDAlignEpsilonDouble       = 0.0006;` |
| `kNormalDotAlignEpsilonDouble` | 2.Processing/CSGConstants.cs:8 | 1 | 0 | `const double        kNormalDotAlignEpsilonDouble    = 0.9999;` |
| `kBoundsDistanceEpsilonDouble` | 2.Processing/CSGConstants.cs:10 | 1 | 0 | `const double        kBoundsDistanceEpsilonDouble    = 0.0006;` |
| `kEdgeDistanceEpsilonDouble` | 2.Processing/CSGConstants.cs:11 | 2 | 0 | `const double        kEdgeDistanceEpsilonDouble	    = 0.0006;` |
| `kVertexEqualEpsilonDouble` | 2.Processing/CSGConstants.cs:12 | 1 | 0 | `const double        kVertexEqualEpsilonDouble	    = 0.005;` |
| `kFatPlaneWidthEpsilonDouble` | 2.Processing/CSGConstants.cs:13 | 1 | 0 | `const double        kFatPlaneWidthEpsilonDouble	    = 0.0006;` |
| `kBoundsDistanceEpsilon` | 2.Processing/CSGConstants.cs:15 | 7 | 0 | `public const float  kBoundsDistanceEpsilon	    = (float)kBoundsDistanceEpsilonDouble;` |
| `kFatPlaneWidthEpsilon` | 2.Processing/CSGConstants.cs:17 | 8 | 0 | `public const float  kFatPlaneWidthEpsilon	    = (float)kFatPlaneWidthEpsilonDouble;` |
| `kEdgeIntersectionEpsilon` | 2.Processing/CSGConstants.cs:18 | 5 | 0 | `public const float  kEdgeIntersectionEpsilon    = (float)kEdgeDistanceEpsilonDouble;` |
| `kSqrEdgeDistanceEpsilon` | 2.Processing/CSGConstants.cs:19 | 6 | 0 | `public const float  kSqrEdgeDistanceEpsilon	    = (float)(kEdgeDistanceEpsilonDouble * kEd` |
| `kVertexEqualEpsilon` | 2.Processing/CSGConstants.cs:21 | 3 | 2 | `public const float  kVertexEqualEpsilon	        = (float)(kVertexEqualEpsilonDouble * 2.5f` |
| `kSqrVertexEqualEpsilon` | 2.Processing/CSGConstants.cs:22 | 12 | 0 | `public const float  kSqrVertexEqualEpsilon	    = kVertexEqualEpsilon * kVertexEqualEpsilon` |
| `kNormalDotAlignEpsilon` | 2.Processing/CSGConstants.cs:24 | 5 | 0 | `public const float  kNormalDotAlignEpsilon		= (float)kNormalDotAlignEpsilonDouble;` |
| `kPlaneDAlignEpsilon` | 2.Processing/CSGConstants.cs:25 | 2 | 0 | `public const float  kPlaneDAlignEpsilon	        = (float)kPlaneDAlignEpsilonDouble;` |
| `kDivideMinimumEpsilon` | 2.Processing/CSGConstants.cs:27 | 3 | 0 | `public const double kDivideMinimumEpsilon       = 0.000001;` |
| `kDistanceEpsilon` | ExtensionMethods/Geometry/MathExtensions.cs:19 | 7 | 0 | `public const float kDistanceEpsilon         = 0.00001f;` |
| `kFrustumDistanceEpsilon` | ExtensionMethods/Geometry/MathExtensions.cs:22 | 0 | 0 | `public const float kFrustumDistanceEpsilon  = kDistanceEpsilon * 100;` |
| `kEpsilon` | ExtensionMethods/Geometry/MathExtensions.cs:148 | 16 | 2 | `const float kEpsilon = 0.0001f;` |
| `kEpsilon` | ExtensionMethods/Geometry/MathExtensions.cs:374 | 16 | 2 | `const float kEpsilon = 1.0e-9f;` |

## Every use

### `kDistanceEpsilon` (1.Input/BrushMesh/BrushMesh.Optimize.cs:12) — 14 use(s)

- `1.Input/BrushMesh/BrushMesh.Optimize.cs:12` — `const float kDistanceEpsilon = 0.00001f;`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:292` — `if (distance < -kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:292` — `if (distance < -kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:299` — `if (distance > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:299` — `if (distance > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:371` — `if (distance < -kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:371` — `if (distance < -kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:375` — `if (distance > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:375` — `if (distance > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:464` — `if (Mathf.Abs(plane.GetDistanceToPoint(vertex)) > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:464` — `if (Mathf.Abs(plane.GetDistanceToPoint(vertex)) > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:607` — `const float kDistanceEpsilon = 0.0001f; // TODO: why??`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:640` — `if (math.lengthsq(vertexV0 - vertexV1) >= kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:640` — `if (math.lengthsq(vertexV0 - vertexV1) >= kDistanceEpsilon)`

### `kEqualityEpsilon` (1.Input/BrushMesh/BrushMesh.Optimize.cs:13) — **declared and never used**

### `kDistanceEpsilon` (1.Input/BrushMesh/BrushMesh.Optimize.cs:607) — 14 use(s)

- `1.Input/BrushMesh/BrushMesh.Optimize.cs:12` — `const float kDistanceEpsilon = 0.00001f;`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:292` — `if (distance < -kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:292` — `if (distance < -kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:299` — `if (distance > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:299` — `if (distance > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:371` — `if (distance < -kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:371` — `if (distance < -kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:375` — `if (distance > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:375` — `if (distance > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:464` — `if (Mathf.Abs(plane.GetDistanceToPoint(vertex)) > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:464` — `if (Mathf.Abs(plane.GetDistanceToPoint(vertex)) > kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:607` — `const float kDistanceEpsilon = 0.0001f; // TODO: why??`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:640` — `if (math.lengthsq(vertexV0 - vertexV1) >= kDistanceEpsilon)`
- `1.Input/BrushMesh/BrushMesh.Optimize.cs:640` — `if (math.lengthsq(vertexV0 - vertexV1) >= kDistanceEpsilon)`

### `kDistanceEpsilon` (1.Input/GeneratorBase/BrushFactory.Utility.cs:684) — 1 use(s)

- `1.Input/GeneratorBase/BrushFactory.Utility.cs:689` — `if (magnitude < kDistanceEpsilon)`

### `kHeightEpsilon` (1.Input/Generators/Capsule/ChiselCapsuleDefinition.cs:48) — 3 use(s)

- `1.Input/Generators/Capsule/ChiselCapsuleDefinition.cs:50` — `public bool	        HaveRoundedTop		{ get { return topSegments > 0 && topHeight > kHeightEpsilon; } }`
- `1.Input/Generators/Capsule/ChiselCapsuleDefinition.cs:51` — `public bool	        HaveRoundedBottom	{ get { return bottomSegments > 0 && bottomHeight > kHeightEpsilon; } }`
- `1.Input/Generators/Capsule/ChiselCapsuleDefinition.cs:52` — `public bool	        HaveCylinder		{ get { return CylinderHeight > kHeightEpsilon; } }`

### `kEpsilon` (1.Input/Generators/LinearStairs/ChiselLinearStairsBrushFactory.cs:1084) — 8 use(s)

- `1.Input/Generators/LinearStairs/ChiselLinearStairsBrushFactory.cs:1086` — `this.treadHeight     = (treadHeight < kEpsilon) ? 0 : treadHeight;`
- `1.Input/Generators/LinearStairs/ChiselLinearStairsBrushFactory.cs:1154` — `this.offsetZ             = (this.stepDepthOffset < kEpsilon) ? 0 : this.stepDepthOffset;`
- `1.Input/Generators/LinearStairs/ChiselLinearStairsBrushFactory.cs:1160` — `this.haveTread           = (this.treadHeight >= kEpsilon);`
- `1.Input/Generators/LinearStairs/ChiselLinearStairsBrushFactory.cs:1161` — `this.haveTopSide         = (sideHeight > kEpsilon);`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`

### `kEpsilon` (1.Input/Generators/PathedStairs/ChiselPathedStairsBrushFactory.cs:196) — 5 use(s)

- `1.Input/Generators/PathedStairs/ChiselPathedStairsBrushFactory.cs:200` — `if (f >= -kEpsilon && f <= kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`

### `kEpsilon` (1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:14) — 12 use(s)

- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:25` — `if (x < -kEpsilon) { negativeSide++; if (positiveSide > 0) break; }`
- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:26` — `if (x >  kEpsilon) { positiveSide++; if (negativeSide > 0) break; }`
- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:44` — `if (!(x_a <= kEpsilon && x_b <= kEpsilon) &&`
- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:45` — `!(x_a >= -kEpsilon && x_b >= -kEpsilon))`
- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:82` — `if (p.x < -kEpsilon)`
- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:84` — `if (p.x > kEpsilon)`
- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:97` — `if (p.x > kEpsilon)`
- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:99` — `if (p.x < -kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`

### `kEpsilon` (1.Input/Generators/SpiralStairs/ChiselSpiralStairsBrushFactory.cs:18) — 7 use(s)

- `1.Input/Generators/SpiralStairs/ChiselSpiralStairsBrushFactory.cs:21` — `var treadHeight		= (nosingDepth < kEpsilon) ? 0 : definition.treadHeight;`
- `1.Input/Generators/SpiralStairs/ChiselSpiralStairsBrushFactory.cs:22` — `var haveTread		= (treadHeight >= kEpsilon);`
- `1.Input/Generators/SpiralStairs/ChiselSpiralStairsBrushFactory.cs:25` — `var haveInnerCyl	= (innerDiameter >= kEpsilon);`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`

### `kEpsilon` (1.Input/Generators/SpiralStairs/ChiselSpiralStairsDefinition.cs:80) — 6 use(s)

- `1.Input/Generators/SpiralStairs/ChiselSpiralStairsDefinition.cs:82` — `internal bool HaveInnerCylinder => (innerDiameter >= kEpsilon);`
- `1.Input/Generators/SpiralStairs/ChiselSpiralStairsDefinition.cs:83` — `internal bool HaveTread => (nosingDepth < kEpsilon) ? false : (treadHeight >= kEpsilon);`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`

### `kNoCenterEpsilon` (1.Input/Generators/Stadium/ChiselStadiumDefinition.cs:39) — 3 use(s)

- `1.Input/Generators/Stadium/ChiselStadiumBrushFactory.cs:29` — `var haveCenter = (length - ((haveRoundedTop ? topLength : 0) + (haveRoundedBottom ? bottomLength : 0))) >= ChiselStadium.kNoCenterEpsilon;`
- `1.Input/Generators/Stadium/ChiselStadiumBrushFactory.cs:60` — `var haveCenter			= (length - ((haveRoundedTop ? topLength : 0) + (haveRoundedBottom ? bottomLength : 0))) >= ChiselStadium.kNoCenterEpsilon;`
- `1.Input/Generators/Stadium/ChiselStadiumDefinition.cs:49` — `internal bool				HaveCenter			{ get { return (length - ((HaveRoundedTop ? topLength : 0) + (HaveRoundedBottom ? bottomLength : 0))) >= kNoCenterEpsilo`

### `kSnapEpsilon` (1.Input/Surfaces/UVMatrix.cs:18) — **declared and never used**

### `kEpsilon` (2.Processing/Decals/DecalClipping.cs:135) — 11 use(s)

- `2.Processing/Decals/DecalClipping.cs:173` — `var pOutside = dp < -kEpsilon;`
- `2.Processing/Decals/DecalClipping.cs:174` — `var qOutside = dq < -kEpsilon;`
- `2.Processing/Decals/DecalClipping.cs:189` — `var tolerance = kEpsilon / length;`
- `2.Processing/Decals/DecalClipping.cs:260` — `if (dc >= -kEpsilon)`
- `2.Processing/Decals/DecalClipping.cs:262` — `if ((dc < -kEpsilon && dn > kEpsilon) ||`
- `2.Processing/Decals/DecalClipping.cs:263` — `(dc > kEpsilon && dn < -kEpsilon))`
- `2.Processing/Decals/DecalClipping.cs:275` — `var epsilonSqr = kEpsilon * kEpsilon;`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`

### `kMinArea` (2.Processing/Decals/DecalClipping.cs:137) — 5 use(s)

- `2.Processing/Decals/DecalClipping.cs:327` — `if (TriangleArea(triangle.v0.position, triangle.v1.position, triangle.v2.position) > kMinArea)`
- `2.Processing/Decals/DecalClipping.cs:346` — `if (TriangleArea(center.position, a.position, b.position) <= kMinArea)`
- `2.Processing/Decals/DecalClipping.cs:365` — `math.length(AreaVector(ref polygon)) * 0.5 <= kMinArea)`
- `2.Processing/Decals/DecalClipping.cs:424` — `if (inner.Count < 3 || innerArea <= kMinArea)`
- `2.Processing/Decals/DecalClipping.cs:448` — `if (math.abs(ringArea + innerArea - outerArea) > 1e-6 * outerArea + kMinArea)`

### `kPlaneEpsilon` (2.Processing/Decals/DecalVolume.cs:61) — 9 use(s)

- `2.Processing/Decals/DecalClipping.cs:135` — `const double kEpsilon = DecalVolume.kPlaneEpsilon;`
- `2.Processing/Decals/DecalVolume.cs:226` — `boundsMin       = (float3)(boundsMin - kPlaneEpsilon),`
- `2.Processing/Decals/DecalVolume.cs:227` — `boundsMax       = (float3)(boundsMax + kPlaneEpsilon)`
- `2.Processing/Decals/DecalVolume.cs:256` — `if (Distance(i, point) < -kPlaneEpsilon)`
- `2.Processing/Decals/DecalVolume.cs:273` — `if (Distance(i, a) < -kPlaneEpsilon &&`
- `2.Processing/Decals/DecalVolume.cs:274` — `Distance(i, b) < -kPlaneEpsilon &&`
- `2.Processing/Decals/DecalVolume.cs:275` — `Distance(i, c) < -kPlaneEpsilon)`
- `2.Processing/Decals/DecalVolume.cs:305` — `if (math.dot(surfaceNormal, facingPoint - surfacePoint) < -kPlaneEpsilon)`
- `2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:1112` — `if (math.abs(distance) > reach + DecalVolume.kPlaneEpsilon)`

### `kPlaneDAlignEpsilonDouble` (2.Processing/CSGConstants.cs:7) — 1 use(s)

- `2.Processing/CSGConstants.cs:25` — `public const float  kPlaneDAlignEpsilon	        = (float)kPlaneDAlignEpsilonDouble;`

### `kNormalDotAlignEpsilonDouble` (2.Processing/CSGConstants.cs:8) — 1 use(s)

- `2.Processing/CSGConstants.cs:24` — `public const float  kNormalDotAlignEpsilon		= (float)kNormalDotAlignEpsilonDouble;`

### `kBoundsDistanceEpsilonDouble` (2.Processing/CSGConstants.cs:10) — 1 use(s)

- `2.Processing/CSGConstants.cs:15` — `public const float  kBoundsDistanceEpsilon	    = (float)kBoundsDistanceEpsilonDouble;`

### `kEdgeDistanceEpsilonDouble` (2.Processing/CSGConstants.cs:11) — 2 use(s)

- `2.Processing/CSGConstants.cs:18` — `public const float  kEdgeIntersectionEpsilon    = (float)kEdgeDistanceEpsilonDouble;`
- `2.Processing/CSGConstants.cs:19` — `public const float  kSqrEdgeDistanceEpsilon	    = (float)(kEdgeDistanceEpsilonDouble * kEdgeDistanceEpsilonDouble);`

### `kVertexEqualEpsilonDouble` (2.Processing/CSGConstants.cs:12) — 1 use(s)

- `2.Processing/CSGConstants.cs:21` — `public const float  kVertexEqualEpsilon	        = (float)(kVertexEqualEpsilonDouble * 2.5f);`

### `kFatPlaneWidthEpsilonDouble` (2.Processing/CSGConstants.cs:13) — 1 use(s)

- `2.Processing/CSGConstants.cs:17` — `public const float  kFatPlaneWidthEpsilon	    = (float)kFatPlaneWidthEpsilonDouble;`

### `kBoundsDistanceEpsilon` (2.Processing/CSGConstants.cs:15) — 7 use(s)

- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:51` — `if (!BrushBoundsSweep.FindRange(ref brushBoundsSweep, in bounds1, IntersectionUtility.kBoundsDistanceEpsilon, out var first, out var last))`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:66` — `if (!entry.bounds.Intersects(bounds1, IntersectionUtility.kBoundsDistanceEpsilon))`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:100` — `if (!entry.bounds.Intersects(bounds1, IntersectionUtility.kBoundsDistanceEpsilon))`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:223` — `if (!BrushBoundsSweep.FindRange(ref brushBoundsSweep, in bounds1, IntersectionUtility.kBoundsDistanceEpsilon, out var first, out var last))`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:237` — `if (!entry.bounds.Intersects(bounds1, IntersectionUtility.kBoundsDistanceEpsilon))`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:530` — `public const double kBoundsDistanceEpsilon = CSGConstants.kBoundsDistanceEpsilon;`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:662` — `if (!bounds0.Intersects(bounds1, IntersectionUtility.kBoundsDistanceEpsilon))`

### `kFatPlaneWidthEpsilon` (2.Processing/CSGConstants.cs:17) — 8 use(s)

- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:14` — `const float kEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Containers/CanonicalVertices.cs:162` — `internal const double kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Containers/WeldIncidence.cs:28` — `const float kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:19` — `const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:20` — `const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/PerformCSGJob.cs:182` — `const float kCanonicalSameRegionEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:27` — `const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:34` — `const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`

### `kEdgeIntersectionEpsilon` (2.Processing/CSGConstants.cs:18) — 5 use(s)

- `2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:434` — `selfMin -= CSGConstants.kEdgeIntersectionEpsilon;`
- `2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:435` — `selfMax += CSGConstants.kEdgeIntersectionEpsilon;`
- `2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:513` — `loopMin -= CSGConstants.kEdgeIntersectionEpsilon;`
- `2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:514` — `loopMax += CSGConstants.kEdgeIntersectionEpsilon;`
- `2.Processing/Jobs/LoopVerticesCacheJobs.cs:165` — `var influence = 2.0f * math.max(HashedVertices.kCellSize, CSGConstants.kEdgeIntersectionEpsilon);`

### `kSqrEdgeDistanceEpsilon` (2.Processing/CSGConstants.cs:19) — 6 use(s)

- `2.Processing/Algorithms/MeshAlgorithms.cs:198` — `const float kSqrEps = CSGConstants.kSqrEdgeDistanceEpsilon;`
- `2.Processing/Jobs/CreateBlobPolygonsBlobsJob.cs:59` — `if (distance <= CSGConstants.kSqrEdgeDistanceEpsilon)`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:465` — `LoopEdgeSplitter.SplitEdgesAtVertices(ref splitEdges, in splitPositions, in splitCandidates, candidateCount, CSGConstants.kSqrEdgeDistanceEpsilon);`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:992` — `if (!MathExtensions.IsPointOnLineSegment(otherVertex, vertex0, vertex1, CSGConstants.kSqrVertexEqualEpsilon, CSGConstants.kSqrEdgeDistanceEpsilon))`
- `2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:537` — `if (MathExtensions.IsPointOnLineSegmentButNotOnVertex(w, a, b, CSGConstants.kSqrEdgeDistanceEpsilon))`
- `2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:557` — `LoopEdgeSplitter.SplitEdgesAtVertices(ref edges, in posArr, in candArr, candArr.Length, CSGConstants.kSqrEdgeDistanceEpsilon);`

### `kVertexEqualEpsilon` (2.Processing/CSGConstants.cs:21) — 3 use(s)

- `2.Processing/CSGConstants.cs:22` — `public const float  kSqrVertexEqualEpsilon	    = kVertexEqualEpsilon * kVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:405` — `internal const float    kCellSize       = CSGConstants.kVertexEqualEpsilon * 2.5f;`
- `2.Processing/Jobs/PerformCSGJob.cs:177` — `const float kSameRegionEpsilon = CSGConstants.kVertexEqualEpsilon * 2;`

### `kSqrVertexEqualEpsilon` (2.Processing/CSGConstants.cs:22) — 12 use(s)

- `2.Processing/CSGMath.cs:29` — `=> math.lengthsq((double3)a - (double3)b) < CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:53` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:99` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:146` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:205` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:262` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:309` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:364` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:19` — `const float kSqrVertexEqualEpsilon      = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:992` — `if (!MathExtensions.IsPointOnLineSegment(otherVertex, vertex0, vertex1, CSGConstants.kSqrVertexEqualEpsilon, CSGConstants.kSqrEdgeDistanceEpsilon))`
- `2.Processing/Jobs/MergeTouchingBrushVerticesJob.cs:246` — `else if (math.distancesq(claimFirst[ci], vertices[i]) > CSGConstants.kSqrVertexEqualEpsilon)`
- `2.Processing/Jobs/PerformCSGJob.cs:1632` — `if (ia != ib && math.distancesq(hashedTreeSpaceVertices[ia], hashedTreeSpaceVertices[ib]) < CSGConstants.kSqrVertexEqualEpsilon)`

### `kNormalDotAlignEpsilon` (2.Processing/CSGConstants.cs:24) — 5 use(s)

- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:363` — `if (math.abs(math.dot(plane2.xyz, plane0.xyz)) >= CSGConstants.kNormalDotAlignEpsilon ||`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:364` — `math.abs(math.dot(plane2.xyz, plane1.xyz)) >= CSGConstants.kNormalDotAlignEpsilon ||`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:365` — `math.abs(math.dot(plane0.xyz, plane1.xyz)) >= CSGConstants.kNormalDotAlignEpsilon)`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:592` — `if (math.abs(math.dot(facePlane.xyz, planes1[q].xyz)) < CSGConstants.kNormalDotAlignEpsilon)`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:29` — `const float kNormalDotAlignEpsilon      = CSGConstants.kNormalDotAlignEpsilon;`

### `kPlaneDAlignEpsilon` (2.Processing/CSGConstants.cs:25) — 2 use(s)

- `2.Processing/Containers/InternedPlanes.cs:173` — `if (math.abs(Distance(stored, points[i])) > CSGConstants.kPlaneDAlignEpsilon)`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:28` — `const float kPlaneWAlignEpsilon         = CSGConstants.kPlaneDAlignEpsilon;`

### `kDivideMinimumEpsilon` (2.Processing/CSGConstants.cs:27) — 3 use(s)

- `2.Processing/Containers/InternedPlanes.cs:138` — `return (length > (float)CSGConstants.kDivideMinimumEpsilon) ? (plane / length) : plane;`
- `2.Processing/Jobs/InternBrushPlanesJob.cs:99` — `if (length <= (float)CSGConstants.kDivideMinimumEpsilon)`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:38` — `if (math.isnan(E.y) || E.y > -CSGConstants.kDivideMinimumEpsilon && E.y < CSGConstants.kDivideMinimumEpsilon)`

### `kDistanceEpsilon` (ExtensionMethods/Geometry/MathExtensions.cs:19) — 7 use(s)

- `ExtensionMethods/Geometry/MathExtensions.cs:22` — `public const float kFrustumDistanceEpsilon  = kDistanceEpsilon * 100;`
- `ExtensionMethods/Geometry/MathExtensions.cs:133` — `return (distance < -kDistanceEpsilon);`
- `ExtensionMethods/Geometry/MathExtensions.cs:143` — `return (distance > kDistanceEpsilon);`
- `ExtensionMethods/Geometry/MathExtensions.cs:313` — `if (forward > kDistanceEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:320` — `if (backward < -kDistanceEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:334` — `if (forward > kDistanceEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:341` — `if (backward < -kDistanceEpsilon)`

### `kFrustumDistanceEpsilon` (ExtensionMethods/Geometry/MathExtensions.cs:22) — **declared and never used**

### `kEpsilon` (ExtensionMethods/Geometry/MathExtensions.cs:148) — 16 use(s)

- `ExtensionMethods/Geometry/MathExtensions.cs:148` — `const float kEpsilon = 0.0001f;`
- `ExtensionMethods/Geometry/MathExtensions.cs:152` — `if (f >= -kEpsilon && f <= kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:152` — `if (f >= -kEpsilon && f <= kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:374` — `const float kEpsilon = 1.0e-9f;`
- `ExtensionMethods/Geometry/MathExtensions.cs:388` — `(1.0f / Mathf.Abs(Vector3.Dot(O0, I0) + Vector3.Dot(O1, I1) + Vector3.Dot(O2, I2)) + kEpsilon);`
- `ExtensionMethods/Geometry/MathExtensions.cs:388` — `(1.0f / Mathf.Abs(Vector3.Dot(O0, I0) + Vector3.Dot(O1, I1) + Vector3.Dot(O2, I2)) + kEpsilon);`
- `ExtensionMethods/Geometry/MathExtensions.cs:390` — `if (w < kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:390` — `if (w < kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`

### `kEpsilon` (ExtensionMethods/Geometry/MathExtensions.cs:374) — 16 use(s)

- `ExtensionMethods/Geometry/MathExtensions.cs:148` — `const float kEpsilon = 0.0001f;`
- `ExtensionMethods/Geometry/MathExtensions.cs:152` — `if (f >= -kEpsilon && f <= kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:152` — `if (f >= -kEpsilon && f <= kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:374` — `const float kEpsilon = 1.0e-9f;`
- `ExtensionMethods/Geometry/MathExtensions.cs:388` — `(1.0f / Mathf.Abs(Vector3.Dot(O0, I0) + Vector3.Dot(O1, I1) + Vector3.Dot(O2, I2)) + kEpsilon);`
- `ExtensionMethods/Geometry/MathExtensions.cs:388` — `(1.0f / Mathf.Abs(Vector3.Dot(O0, I0) + Vector3.Dot(O1, I1) + Vector3.Dot(O2, I2)) + kEpsilon);`
- `ExtensionMethods/Geometry/MathExtensions.cs:390` — `if (w < kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:390` — `if (w < kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`

### `kBoundsDistanceEpsilon` (2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:530) — 8 use(s)

- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:51` — `if (!BrushBoundsSweep.FindRange(ref brushBoundsSweep, in bounds1, IntersectionUtility.kBoundsDistanceEpsilon, out var first, out var last))`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:66` — `if (!entry.bounds.Intersects(bounds1, IntersectionUtility.kBoundsDistanceEpsilon))`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:100` — `if (!entry.bounds.Intersects(bounds1, IntersectionUtility.kBoundsDistanceEpsilon))`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:223` — `if (!BrushBoundsSweep.FindRange(ref brushBoundsSweep, in bounds1, IntersectionUtility.kBoundsDistanceEpsilon, out var first, out var last))`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:237` — `if (!entry.bounds.Intersects(bounds1, IntersectionUtility.kBoundsDistanceEpsilon))`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:576` — `int side = WhichSide(ref brushVertices1, plane0, kBoundsDistanceEpsilon);`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:604` — `int side = WhichSide(ref brushVertices0, plane1, kBoundsDistanceEpsilon);`
- `2.Processing/Jobs/FindAllBrushIntersectionPairsJob.cs:662` — `if (!bounds0.Intersects(bounds1, IntersectionUtility.kBoundsDistanceEpsilon))`

### `kEpsilon` (ExtensionMethods/Geometry/PlaneExtensions.cs:47) — 14 use(s)

- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:47` — `const double kEpsilon = 0.0006f;`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:56` — `if (w > -kEpsilon && w < kEpsilon)`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:56` — `if (w > -kEpsilon && w < kEpsilon)`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:119` — `const double kEpsilon = 0.00001f;`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:122` — `if (w > -kEpsilon && w < kEpsilon)`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:122` — `if (w > -kEpsilon && w < kEpsilon)`

### `kEpsilon` (ExtensionMethods/Geometry/PlaneExtensions.cs:119) — 14 use(s)

- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:469` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:484` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:861` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/MathExtensions.cs:875` — `if (length > Vector3.kEpsilon)`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:47` — `const double kEpsilon = 0.0006f;`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:56` — `if (w > -kEpsilon && w < kEpsilon)`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:56` — `if (w > -kEpsilon && w < kEpsilon)`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:119` — `const double kEpsilon = 0.00001f;`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:122` — `if (w > -kEpsilon && w < kEpsilon)`
- `ExtensionMethods/Geometry/PlaneExtensions.cs:122` — `if (w > -kEpsilon && w < kEpsilon)`

### `kFatPlaneWidthEpsilon` (2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:27) — 12 use(s)

- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:14` — `const float kEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Containers/CanonicalVertices.cs:162` — `internal const double kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Containers/WeldIncidence.cs:28` — `const float kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:19` — `const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:20` — `const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/PerformCSGJob.cs:182` — `const float kCanonicalSameRegionEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:115` — `if (forward > kFatPlaneWidthEpsilon) // closest point is outside`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:128` — `if (backward < -kFatPlaneWidthEpsilon) // closest point is inside`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:139` — `onCount += (distance >= -kFatPlaneWidthEpsilon && distance <= kFatPlaneWidthEpsilon) ? 1 : 0;`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:143` — `if ((minDistance > kFatPlaneWidthEpsilon || maxDistance < -kFatPlaneWidthEpsilon))`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:392` — `canonicalAlignment ? kFatPlaneWidthEpsilon : kPlaneVertexAlignEpsilon);`
- `2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:34` — `const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`

### `kPlaneWAlignEpsilon` (2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:28) — 3 use(s)

- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:341` — `if (math.abs(math.dot(facePlane, vertex)) > kPlaneWAlignEpsilon)`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:390` — `bool coincident = math.abs(localPlane1.w - localPlane2.w) < kPlaneWAlignEpsilon`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:469` — `if (distance >= -kPlaneWAlignEpsilon && distance <= kPlaneWAlignEpsilon) // Note: this is false on NaN/Infinity, so don't invert`

### `kNormalDotAlignEpsilon` (2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:29) — 6 use(s)

- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:363` — `if (math.abs(math.dot(plane2.xyz, plane0.xyz)) >= CSGConstants.kNormalDotAlignEpsilon ||`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:364` — `math.abs(math.dot(plane2.xyz, plane1.xyz)) >= CSGConstants.kNormalDotAlignEpsilon ||`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:365` — `math.abs(math.dot(plane0.xyz, plane1.xyz)) >= CSGConstants.kNormalDotAlignEpsilon)`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:592` — `if (math.abs(math.dot(facePlane.xyz, planes1[q].xyz)) < CSGConstants.kNormalDotAlignEpsilon)`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:384` — `bool sameFacing = normalDot >= kNormalDotAlignEpsilon;`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:385` — `if (!sameFacing && normalDot > -kNormalDotAlignEpsilon)`

### `kPlaneVertexAlignEpsilon` (2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:328) — 2 use(s)

- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:335` — `float epsilon = kPlaneVertexAlignEpsilon)`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:392` — `canonicalAlignment ? kFatPlaneWidthEpsilon : kPlaneVertexAlignEpsilon);`

### `kFatPlaneWidthEpsilon` (2.Processing/Jobs/CreateIntersectionLoopsJob.cs:19) — 15 use(s)

- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:14` — `const float kEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Containers/CanonicalVertices.cs:162` — `internal const double kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Containers/WeldIncidence.cs:28` — `const float kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:78` — `if (!math.all(distance <= kFatPlaneWidthEpsilon))`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:86` — `if (!(distance <= kFatPlaneWidthEpsilon))`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:383` — `if (math.abs(math.dot(plane2, edgeVertex0)) <= kFatPlaneWidthEpsilon &&`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:384` — `math.abs(math.dot(plane2, edgeVertex1)) <= kFatPlaneWidthEpsilon)`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:579` — `if (math.abs(math.dot(facePlane, candidate)) > kFatPlaneWidthEpsilon)`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:608` — `if (math.abs(math.dot(facePlane, vertex)) > kFatPlaneWidthEpsilon)`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:615` — `if (math.dot(planes1[q], vertex) > kFatPlaneWidthEpsilon)`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:628` — `if (math.abs(math.dot(facePlane, new float4(treeSpaceVertex, 1))) > kFatPlaneWidthEpsilon)`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:20` — `const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/PerformCSGJob.cs:182` — `const float kCanonicalSameRegionEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:27` — `const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:34` — `const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`

### `kSqrVertexEqualEpsilon` (2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:19) — 15 use(s)

- `2.Processing/CSGMath.cs:29` — `=> math.lengthsq((double3)a - (double3)b) < CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:53` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:99` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:146` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:205` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:262` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:309` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Containers/HashedVertices.cs:364` — `double closestDistance = CSGConstants.kSqrVertexEqualEpsilon;`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:693` — `if ((math.lengthsq(vertex0 - identityVertex) <= kSqrVertexEqualEpsilon && weldFilter.Allows(identityVertex, vertex0)) ||`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:694` — `(math.lengthsq(vertex1 - identityVertex) <= kSqrVertexEqualEpsilon && weldFilter.Allows(identityVertex, vertex1)))`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:849` — `if ((math.lengthsq(vertex0 - identityVertex) <= kSqrVertexEqualEpsilon && weldFilter.Allows(identityVertex, vertex0)) ||`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:850` — `(math.lengthsq(vertex1 - identityVertex) <= kSqrVertexEqualEpsilon && weldFilter.Allows(identityVertex, vertex1)))`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:992` — `if (!MathExtensions.IsPointOnLineSegment(otherVertex, vertex0, vertex1, CSGConstants.kSqrVertexEqualEpsilon, CSGConstants.kSqrEdgeDistanceEpsilon))`
- `2.Processing/Jobs/MergeTouchingBrushVerticesJob.cs:246` — `else if (math.distancesq(claimFirst[ci], vertices[i]) > CSGConstants.kSqrVertexEqualEpsilon)`
- `2.Processing/Jobs/PerformCSGJob.cs:1632` — `if (ia != ib && math.distancesq(hashedTreeSpaceVertices[ia], hashedTreeSpaceVertices[ib]) < CSGConstants.kSqrVertexEqualEpsilon)`

### `kFatPlaneWidthEpsilon` (2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:20) — 20 use(s)

- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:14` — `const float kEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Containers/CanonicalVertices.cs:162` — `internal const double kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Containers/WeldIncidence.cs:28` — `const float kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:19` — `const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:637` — `if (distance1 <=  kFatPlaneWidthEpsilon ||`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:638` — `distance0 >= -kFatPlaneWidthEpsilon) continue;`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:641` — `if (distance1 >= -kFatPlaneWidthEpsilon ||`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:642` — `distance0 <=  kFatPlaneWidthEpsilon) continue;`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:674` — `if (distance > kFatPlaneWidthEpsilon)`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:684` — `if (distance > kFatPlaneWidthEpsilon)`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:795` — `if (distance1 <=  kFatPlaneWidthEpsilon ||`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:796` — `distance0 >= -kFatPlaneWidthEpsilon) continue;`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:799` — `if (distance1 >= -kFatPlaneWidthEpsilon ||`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:800` — `distance0 <=  kFatPlaneWidthEpsilon) continue;`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:831` — `if (distance > kFatPlaneWidthEpsilon)`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:840` — `if (distance > kFatPlaneWidthEpsilon)`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:933` — `if (distance > kFatPlaneWidthEpsilon)`
- `2.Processing/Jobs/PerformCSGJob.cs:182` — `const float kCanonicalSameRegionEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:27` — `const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:34` — `const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`

### `kOnPlane` (2.Processing/Containers/CanonicalVertices.cs:162) — 5 use(s)

- `2.Processing/Containers/CanonicalVertices.cs:176` — `internal const double kMaxMove = kOnPlane * 4;`
- `2.Processing/Containers/CanonicalVertices.cs:294` — `if (Distance(brushPlanes[f], point) > kOnPlane)`
- `2.Processing/Containers/CanonicalVertices.cs:302` — `if (distance <= kOnPlane)`
- `2.Processing/Containers/CanonicalVertices.cs:304` — `else if (distance <= kOnPlane * 2)`
- `2.Processing/Containers/CanonicalVertices.cs:417` — `var tau   = CanonicalVertices.kOnPlane;`

### `kSameVertex` (2.Processing/Containers/CanonicalVertices.cs:168) — 1 use(s)

- `2.Processing/Containers/CanonicalVertices.cs:169` — `internal const float kSqrSameVertex = kSameVertex * kSameVertex;`

### `kCellSize` (2.Processing/Containers/HashedVertices.cs:405) — 13 use(s)

- `2.Processing/Containers/HashedVertices.cs:29` — `var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCe`
- `2.Processing/Containers/HashedVertices.cs:75` — `var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCe`
- `2.Processing/Containers/HashedVertices.cs:122` — `var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCe`
- `2.Processing/Containers/HashedVertices.cs:181` — `var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCe`
- `2.Processing/Containers/HashedVertices.cs:238` — `var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCe`
- `2.Processing/Containers/HashedVertices.cs:285` — `var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCe`
- `2.Processing/Containers/HashedVertices.cs:342` — `var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCe`
- `2.Processing/Containers/HashedVertices.cs:523` — `var centerIndex     = new int3((int)(vertex.x / kCellSize), (int)(vertex.y / kCellSize), (int)(vertex.z / kCellSize));`
- `2.Processing/Containers/HashedVertices.cs:754` — `var centerIndex = new int3((int)(vertex.x / kCellSize), (int)(vertex.y / kCellSize), (int)(vertex.z / kCellSize));`
- `2.Processing/Containers/HashedVertices.cs:773` — `var centerIndex = new int3((int)(vertex.x / kCellSize), (int)(vertex.y / kCellSize), (int)(vertex.z / kCellSize));`
- `2.Processing/Jobs/LoopVerticesCacheJobs.cs:165` — `var influence = 2.0f * math.max(HashedVertices.kCellSize, CSGConstants.kEdgeIntersectionEpsilon);`
- `2.Processing/Jobs/MergeTouchingBrushVerticesJob.cs:180` — `mergeMin -= HashedVertices.kCellSize;`
- `2.Processing/Jobs/MergeTouchingBrushVerticesJob.cs:181` — `mergeMax += HashedVertices.kCellSize;`

### `kOnPlane` (2.Processing/Containers/WeldIncidence.cs:28) — 6 use(s)

- `2.Processing/Containers/CanonicalVertices.cs:417` — `var tau   = CanonicalVertices.kOnPlane;`
- `2.Processing/Containers/WeldIncidence.cs:35` — `const float kOffPlane = kOnPlane * 2;`
- `2.Processing/Containers/WeldIncidence.cs:111` — `if (math.dot(plane, a4) > kOnPlane) aInside = false;`
- `2.Processing/Containers/WeldIncidence.cs:112` — `if (math.dot(plane, b4) > kOnPlane) bInside = false;`
- `2.Processing/Containers/WeldIncidence.cs:122` — `if (aInside && distanceA <= kOnPlane && distanceB > kOffPlane) return false;`
- `2.Processing/Containers/WeldIncidence.cs:123` — `if (bInside && distanceB <= kOnPlane && distanceA > kOffPlane) return false;`

### `kOffPlane` (2.Processing/Containers/WeldIncidence.cs:35) — 2 use(s)

- `2.Processing/Containers/WeldIncidence.cs:122` — `if (aInside && distanceA <= kOnPlane && distanceB > kOffPlane) return false;`
- `2.Processing/Containers/WeldIncidence.cs:123` — `if (bInside && distanceB <= kOnPlane && distanceA > kOffPlane) return false;`

### `kFatPlaneWidthEpsilon` (2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:34) — 11 use(s)

- `1.Input/Generators/RevolvedShape/ChiselRevolvedShapeBrushFactory.cs:14` — `const float kEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Containers/CanonicalVertices.cs:162` — `internal const double kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Containers/WeldIncidence.cs:28` — `const float kOnPlane = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/CreateIntersectionLoopsJob.cs:19` — `const float kFatPlaneWidthEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/FindLoopOverlapIntersectionJob.cs:20` — `const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/PerformCSGJob.cs:182` — `const float kCanonicalSameRegionEpsilon = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/PrepareBrushPairIntersectionsJob.cs:27` — `const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;`
- `2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:82` — `if (!(distance <= kFatPlaneWidthEpsilon))`
- `2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:96` — `if (!(distance <= kFatPlaneWidthEpsilon))`
- `2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:174` — `if (!(CSGMath.SignedDistance(planePtr[planesOffset + n], localVertex) < -kFatPlaneWidthEpsilon))`
- `2.Processing/Jobs/JobData/BooleanEdgesUtility.cs:184` — `if (CSGMath.SignedDistance(planePtr[planesOffset + n], localVertex) > kFatPlaneWidthEpsilon)`

### `kSameRegionEpsilon` (2.Processing/Jobs/PerformCSGJob.cs:177) — 1 use(s)

- `2.Processing/Jobs/PerformCSGJob.cs:338` — `: kSameRegionEpsilon;`

### `kCanonicalSameRegionEpsilon` (2.Processing/Jobs/PerformCSGJob.cs:182) — 1 use(s)

- `2.Processing/Jobs/PerformCSGJob.cs:337` — `var sameRegionEpsilon = canonicalVertexStage >= CanonicalVertexStage.Everywhere ? kCanonicalSameRegionEpsilon`

### `kNormalizeEpsilon` (2.Processing/Algorithms/MeshAlgorithms.cs:697) — 1 use(s)

- `2.Processing/Algorithms/MeshAlgorithms.cs:698` — `if (tangentMagnitude <= kNormalizeEpsilon || binormalMagnitude <= kNormalizeEpsilon)`

### `kConvexTestEpsilon` (2.Processing/Thirdparty/BayazitDecomposerBursted.cs:30) — 1 use(s)

- `2.Processing/Thirdparty/BayazitDecomposerBursted.cs:381` — `return math.abs(value1) <= kConvexTestEpsilon;`

### `kDistanceEpsilon` (2.Processing/Thirdparty/BayazitDecomposerBursted.cs:31) — 1 use(s)

- `2.Processing/Thirdparty/BayazitDecomposerBursted.cs:432` — `if (math.abs(denom) < kDistanceEpsilon)`

### `epsilon` (2.Processing/Thirdparty/ConvexHullCalculator.cs:319) — 3 use(s)

- `2.Processing/Thirdparty/ConvexHullCalculator.cs:329` — `if (Vector3.Distance(a, b) < epsilon || Vector3.Distance(a, c) < epsilon || Vector3.Distance(b, c) < epsilon)`
- `2.Processing/Thirdparty/ConvexHullCalculator.cs:335` — `if (Vector3.Distance(p.normal, Vector3.zero) < epsilon && math.abs(p.distance) < epsilon)`
- `2.Processing/Thirdparty/ConvexHullCalculator.cs:344` — `if (Vector3.Distance(p.normal, cuttingPlane.xyz) < epsilon && math.abs(p.distance - cuttingPlane.w) < epsilon)`

### `kDefaultWeldEpsilon` (3.Output/OutputMeshes/MeshManifoldValidation.cs:49) — 1 use(s)

- `3.Output/OutputMeshes/MeshManifoldValidation.cs:120` — `=> Classify(vertices, indices, kDefaultWeldEpsilon);`

## Inline literals with no named constant

A search for the named constants cannot find these; the first version of the exact-predicates plan missed the category entirely.

- `1.Input/GeneratorBase/BrushFactory.Utility.cs:324` — `0.0001` in `var equals03 = math.lengthsq(v0 - v3) < 0.0001f;`
- `1.Input/GeneratorBase/BrushFactory.Utility.cs:325` — `0.0001` in `var equals12 = math.lengthsq(v1 - v2) < 0.0001f;`
- `1.Input/GeneratorBase/BrushFactory.Utility.cs:341` — `0.001` in `if (math.abs(dist) < 0.001f)`
- `1.Input/GeneratorBase/BrushFactory.Utility.cs:742` — `0.001` in `if (math.abs(dist) < 0.001f)`
- `1.Input/GeneratorBase/BrushFactory.Utility.cs:1146` — `1e-5` in `if (math.abs(length - 1) > 1e-5f)`
- `2.Processing/Algorithms/MeshAlgorithms.cs:238` — `1e-12` in `if (dlen2 < 1e-12) continue;                 // neighbours coincide`
- `2.Processing/Containers/CanonicalVertices.cs:374` — `1e-6` in `internal const int kDisplacement = 8;   // 8 bins: 0, <1e-6, <1e-5, <1e-4, <τ, <2τ, <4τ, >=4τ`
- `2.Processing/Containers/CanonicalVertices.cs:374` — `1e-5` in `internal const int kDisplacement = 8;   // 8 bins: 0, <1e-6, <1e-5, <1e-4, <τ, <2τ, <4τ, >=4τ`
- `2.Processing/Containers/CanonicalVertices.cs:374` — `1e-4` in `internal const int kDisplacement = 8;   // 8 bins: 0, <1e-6, <1e-5, <1e-4, <τ, <2τ, <4τ, >=4τ`
- `2.Processing/Containers/CanonicalVertices.cs:419` — `1e-6` in `int bin = moved == 0 ? 0 : moved < 1e-6 ? 1 : moved < 1e-5 ? 2 : moved < 1e-4 ? 3`
- `2.Processing/Containers/CanonicalVertices.cs:419` — `1e-5` in `int bin = moved == 0 ? 0 : moved < 1e-6 ? 1 : moved < 1e-5 ? 2 : moved < 1e-4 ? 3`
- `2.Processing/Containers/CanonicalVertices.cs:419` — `1e-4` in `int bin = moved == 0 ? 0 : moved < 1e-6 ? 1 : moved < 1e-5 ? 2 : moved < 1e-4 ? 3`
- `2.Processing/Containers/CanonicalVertices.cs:469` — `1e-6` in `.Append(", <1e-6 ").Append(V(kDisplacement + 1))`
- `2.Processing/Containers/CanonicalVertices.cs:470` — `1e-5` in `.Append(", <1e-5 ").Append(V(kDisplacement + 2))`
- `2.Processing/Containers/CanonicalVertices.cs:471` — `1e-4` in `.Append(", <1e-4 ").Append(V(kDisplacement + 3))`
- `2.Processing/Decals/DecalClipping.cs:448` — `1e-6` in `if (math.abs(ringArea + innerArea - outerArea) > 1e-6 * outerArea + kMinArea)`
- `2.Processing/Decals/DecalVolume.cs:122` — `1e-12` in `if (math.abs(math.determinant(decalToTree)) < 1e-12)`
- `2.Processing/Decals/DecalVolume.cs:169` — `1e-12` in `if (!(length > 1e-12))`
- `2.Processing/Decals/DecalVolume.cs:289` — `1e-12` in `if (depth > 1e-12)`
- `2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:145` — `1e-12` in `if (L2 <= 1e-12)`
- `2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:467` — `1e-9` in `if (planeLen > 1e-9f) planeTree /= planeLen;`
- `2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:844` — `0.0001` in `if (normalSmoothingAngle > 0.0001f)`
- `2.Processing/Jobs/GenerateSurfaceTrianglesJob.cs:892` — `0.005` in `if (math.abs(dist) < 0.005f)`
- `2.Processing/Jobs/PerformCSGJob.cs:233` — `1e-12` in `if (lengthSq <= 1e-12f)`


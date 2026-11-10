using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;

namespace Chisel.Core
{
    public sealed partial class BrushMeshFactory
    {
        public static bool GenerateSpiralStairs(NativeList<BlobAssetReference<BrushMeshBlob>>     brushMeshes, // Confirmed to dispose
												ref ChiselSpiralStairs                            definition, 
                                                in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob,
                                                Allocator                                         allocator = Allocator.Persistent)// Indirect
		{
            const bool fitToBounds = false;
            const float kEpsilon = 0.001f;

            var nosingDepth		= definition.nosingDepth;
            var treadHeight		= (nosingDepth < kEpsilon) ? 0 : definition.treadHeight;
            var haveTread		= (treadHeight >= kEpsilon);

            var innerDiameter	= definition.innerDiameter;
            var haveInnerCyl	= (innerDiameter >= kEpsilon);

            var riserType		= definition.riserType;
            var haveRiser		= riserType != StairsRiserType.None;

            if (!haveRiser && !haveTread)
                return false;
            

            var origin		    = definition.origin;

            var startAngle	    = math.radians(definition.startAngle);
            var anglePerStep    = math.radians(definition.AnglePerStep);

            var nosingWidth	    = definition.nosingWidth;
            var outerDiameter   = definition.outerDiameter;

            var p0 = new float2(math.sin(0                  ), math.cos(0                  ));
            var p1 = new float2(math.sin(anglePerStep       ), math.cos(anglePerStep       ));
            var pm = new float2(math.sin(anglePerStep * 0.5f), math.cos(anglePerStep * 0.5f));
            var pn = (p0 + p1) * 0.5f;

            // Widened past outerDiameter so the straight outer edge of a step reaches the circle; 1.25 makes the step fit
            var stepOuterDiameter		= outerDiameter + ((math.length(pm) - math.length(pn)) * (outerDiameter * 1.25f));
            var stepOuterRadius			= stepOuterDiameter * 0.5f;
            var outerNosingWidth        = nosingWidth;
            // with a guard rail on the outside the steps' own shapes end inside it, their outer edges halfway in: shapes that
            // reach through it, even where they're trimmed away, split its outer face along their outlines
            if (definition.HaveOuterSide)
            {
                var railApothem = outerDiameter * 0.5f * math.cos(math.PI / definition.outerSegments);
                var railMiddle  = railApothem - definition.sideWidth * 0.5f;
                stepOuterRadius = math.min(railApothem - 0.001f, railMiddle / math.cos(math.abs(anglePerStep) * 0.5f));
                outerNosingWidth = 0;
            }
            var stepInnerRadius			= 0.0f;
            // with a wall around an open middle the steps start inside that wall instead of at the middle: steps that
            // reach through it, even where they're cut away, split the wall's face along their outlines. The wall has the
            // straight sides of the inner cylinder, so the steps start on the same shape, halfway into the wall; this is
            // how far its sides are from the middle (see InnerRadiusAt)
            if (definition.HaveInnerSide && !definition.HaveColumn && definition.innerDiameter > 0)
            {
                var innerApothem = definition.innerDiameter * 0.5f * math.cos(math.PI / definition.innerSegments);
                stepInnerRadius = math.max(0, innerApothem - definition.sideWidth * 0.5f);
            }
            var stepHeight				= definition.stepHeight;
            var height					= definition.height;
            var stepCount				= definition.StepCount;

            if (height < 0)
            {
                origin.y += height;
                height = -height;
            }
            // raised above the object's position, the first step is solid down to it, as are the walls and the cylinders
            var ground = math.min(0, origin.y);

            var smoothSubDivisions		= 3;

            var cylinderSubMeshCount	= haveInnerCyl ? 2 : 1;
            var subMeshPerRiser			= (riserType == StairsRiserType.None  ) ? 0 : 
                                          (riserType == StairsRiserType.Smooth) ? (2 * smoothSubDivisions)
                                                                                : 1;
            // raised above the object's position, the first step stands on a base of its own down to the ground
            var haveBase                = definition.HaveBase;
            var riserSubMeshCount		= (stepCount * subMeshPerRiser) + ((riserType == StairsRiserType.None  ) ? 0 : cylinderSubMeshCount);
            var treadSubMeshCount		= (haveTread ? stepCount + cylinderSubMeshCount : 0);
            var subMeshCount			= (treadSubMeshCount + riserSubMeshCount);

            var treadStart		= !haveRiser ? 0 : riserSubMeshCount;
            var innerSides		= definition.innerSegments;
            var outerSides		= definition.outerSegments;
            var riserDepth		= definition.riserDepth;
            var undersideSmoothingGroup = definition.bottomSmoothingGroup;

            using (var outerCylinderSurfaceDefinitionBlob = BrushMeshManager.BuildInternalSurfaceArrayBlob(outerSides + 2, Allocator.Temp))
            using (var innerCylinderSurfaceDefinitionBlob = BrushMeshManager.BuildInternalSurfaceArrayBlob(innerSides + 2, Allocator.Temp))
            {
                ref var outerCylinderSurfaces = ref outerCylinderSurfaceDefinitionBlob.Value.surfaces;
                for (int i = 0; i < outerCylinderSurfaces.Length; i++)
                    outerCylinderSurfaces[i] = surfaceDefinitionBlob.Value.surfaces[(int)StairsSurface.StepSides];

                ref var innerCylinderSurfaces = ref innerCylinderSurfaceDefinitionBlob.Value.surfaces;
                for (int i = 0; i < innerCylinderSurfaces.Length; i++)
                    innerCylinderSurfaces[i] = surfaceDefinitionBlob.Value.surfaces[(int)StairsSurface.StepSides];


                if (haveRiser)
                {
                    if (riserType == StairsRiserType.ThinRiser)
                    {
                        var minY = origin.y;
                        var maxY = origin.y + stepHeight - treadHeight;
                        float2 o0, o1;
                        float angle = startAngle;
                        var c1 = math.sin(angle) * stepOuterRadius;
                        var s1 = math.cos(angle) * stepOuterRadius;
                        for (int i = 0; i < stepCount; i++)
                        {
                            var c0 = c1;
                            var s0 = s1;
                            angle += anglePerStep;
                            c1 = math.sin(angle) * stepOuterRadius;
                            s1 = math.cos(angle) * stepOuterRadius;

                            o0 = new float2(origin.x + c0, origin.z + s0);
                            o1 = new float2(origin.x     , origin.z     );

                            var riserVector = math.normalize(new float2((c0 - c1), (s0 - s1))) * riserDepth;

                            var i0 = o0 - riserVector;
                            var i1 = o1 - riserVector;

                            //brushMesh.halfEdges = (anglePerStep > 0) ? invertedBoxHalfEdges.ToArray() : boxHalfEdges.ToArray();
                            brushMeshes[i] = Classified(CreateBox(new float3(i0.x, maxY, i0.y), // 0
                                                       new float3(i1.x, maxY, i1.y), // 1
                                                       new float3(o1.x, maxY, o1.y), // 2
                                                       new float3(o0.x, maxY, o0.y), // 3  

                                                       // the first one from the ground, unless it has a base under it
                                                       new float3(i0.x, (i == 0 && !haveBase) ? ground : minY, i0.y), // 4
                                                       new float3(i1.x, (i == 0 && !haveBase) ? ground : minY, i1.y), // 5
                                                       new float3(o1.x, (i == 0 && !haveBase) ? ground : minY, o1.y), // 6
                                                       new float3(o0.x, (i == 0 && !haveBase) ? ground : minY, o0.y), // 7
                                                       in surfaceDefinitionBlob,
                                                       allocator), StairsPart.Step, origin, anglePerStep, in surfaceDefinitionBlob);// Indirect
							if (i == 0)
                                minY -= treadHeight;

                            minY += stepHeight;
                            maxY += stepHeight;
                        }
                    } else
                    if (riserType == StairsRiserType.Smooth)
                    {
                        //var stepY = stepHeight;
                        var minY  = origin.y;
                        var maxY  = origin.y + stepHeight - treadHeight;
                        var maxY2 = origin.y + (stepHeight * 2) - treadHeight;
                        float angle = startAngle;
                        var c1 = math.sin(angle);
                        var s1 = math.cos(angle);
                        angle += anglePerStep;
                        var c2 = math.sin(angle);
                        var s2 = math.cos(angle);

                        // one set of pieces per step; the cylinders after them have their own places
                        for (int i = 0; i < stepCount * subMeshPerRiser; i += subMeshPerRiser)
                        {
                            var c0 = c1;
                            var s0 = s1;
                            c1 = c2;
                            s1 = s2;
                            angle += anglePerStep;
                            c2 = math.sin(angle);
                            s2 = math.cos(angle);
                            // this step runs from two steps back to one step back
                            var middleAngle = angle - 1.5f * anglePerStep;

                            var c0o = c0 * stepOuterRadius;
                            var c1o = c1 * stepOuterRadius;
                            var s0o = s0 * stepOuterRadius;
                            var s1o = s1 * stepOuterRadius;
                        
                            var o0 = new float2(origin.x + c0o, origin.z + s0o);
                            var o1 = new float2(origin.x + c1o, origin.z + s1o);

                            var i0 = o0;
                            var i1 = o1;

                            int subMeshIndex = i;
                            // the first step from the ground, unless it has a base under it
                            var stepMin = (i == 0 && !haveBase) ? ground : minY;
                            for (int subDiv = 1; subDiv < smoothSubDivisions; subDiv++)
                            {
                                float stepMidRadius;
                                stepMidRadius = (((outerDiameter * 0.5f) * (1.0f / (smoothSubDivisions + 1))) * ((smoothSubDivisions - 1) - (subDiv - 1)));
                                if (subDiv == (smoothSubDivisions - 1))
                                {
                                    var innerRadius = (innerDiameter * 0.5f) - 0.1f;
                                    stepMidRadius = (innerRadius < 0.1f) ? stepMidRadius : innerRadius;
                                }
                                var stepMidRadius0 = stepMidRadius;
                                var stepMidRadius1 = stepMidRadius;
                                // the innermost ring inside the wall around the middle, see stepInnerRadius
                                if (subDiv == (smoothSubDivisions - 1) && stepInnerRadius > 0)
                                {
                                    stepMidRadius0 = InnerRadiusAt(angle - 2 * anglePerStep, stepInnerRadius, innerSides);
                                    stepMidRadius1 = InnerRadiusAt(angle - anglePerStep, stepInnerRadius, innerSides);
                                }

                                var c0i = c0 * stepMidRadius0;
                                var c1i = c1 * stepMidRadius1;
                                var s0i = s0 * stepMidRadius0;
                                var s1i = s1 * stepMidRadius1;

                                i0 = new float2(origin.x + c0i, origin.z + s0i);
                                i1 = new float2(origin.x + c1i, origin.z + s1i);

                                {                  
                                    //brushMesh.halfEdges = (anglePerStep > 0) ? invertedSquarePyramidHalfEdges.ToArray() : squarePyramidHalfEdges.ToArray();
                                    brushMeshes[subMeshIndex] = SmoothUnderside(Classified(CreateSquarePyramidAssetPolygons(new float3(i0.x, maxY, i0.y), // 0
                                                                                                 new float3(i0.x, stepMin, i0.y), // 1
                                                                                                 new float3(o0.x, stepMin, o0.y), // 2  
                                                                                                 new float3(o0.x, maxY, o0.y), // 3
                                                                                                 new float3(o1.x, maxY, o1.y), // 4
                                                                                                 in surfaceDefinitionBlob,
                                                                                                 allocator), StairsPart.Step, origin, anglePerStep, in surfaceDefinitionBlob), undersideSmoothingGroup, origin, middleAngle, stepOuterRadius);// Indirect
									subMeshIndex++;
                                }

                                {
                                    //brushMesh.halfEdges = (anglePerStep > 0) ? invertedTriangularPyramidHalfEdges.ToArray() : triangularPyramidHalfEdges.ToArray();
                                    brushMeshes[subMeshIndex] = SmoothUnderside(Classified(CreateTriangularPyramidAssetPolygons(new float3(i0.x, stepMin, i0.y), // 1
                                                                                                     new float3(i0.x, maxY, i0.y), // 0
                                                                                                     new float3(i1.x, maxY, i1.y), // 2
                                                                                                     new float3(o1.x, maxY, o1.y), // 3,
                                                                                                     in surfaceDefinitionBlob, 
                                                                                                     allocator), StairsPart.Step, origin, anglePerStep, in surfaceDefinitionBlob), undersideSmoothingGroup, origin, middleAngle, stepOuterRadius);// Indirect
									subMeshIndex++;
                                }
                            
                                o0 = i0;
                                o1 = i1;
                            }

                            // the pieces towards the middle meet at its center, or, with a wall around an open middle, just
                            // inside the innermost ring so they stay inside that wall
                            var apex = new float2(origin.x, origin.z);
                            if (stepInnerRadius > 0)
                            {
                                var chordMiddle = (i0 + i1) * 0.5f;
                                apex = chordMiddle - math.normalizesafe(chordMiddle - apex) * 0.01f;
                            }
                            {
                                //brushMesh.halfEdges = (anglePerStep > 0) ? invertedTriangularPyramidHalfEdges.ToArray() : triangularPyramidHalfEdges.ToArray();
                                brushMeshes[subMeshIndex] = SmoothUnderside(Classified(CreateTriangularPyramidAssetPolygons(new float3(     i1.x, maxY,     i1.y), // 2
                                                                                                 new float3(     i0.x, maxY,     i0.y), // 0
                                                                                                 new float3(     i0.x, stepMin,     i0.y), // 1
                                                                                                 new float3(   apex.x, stepMin,   apex.y), // 3
                                                                                                 in surfaceDefinitionBlob,
                                                                                                 allocator), StairsPart.Step, origin, anglePerStep, in surfaceDefinitionBlob), undersideSmoothingGroup, origin, middleAngle, stepOuterRadius);// Indirect
								subMeshIndex++;
                            }

                            {
                                //brushMesh.halfEdges = (anglePerStep > 0) ? invertedTriangularPyramidHalfEdges.ToArray() : triangularPyramidHalfEdges.ToArray();
                                brushMeshes[subMeshIndex] = SmoothUnderside(Classified(CreateTriangularPyramidAssetPolygons(new float3(     i0.x, maxY,     i0.y), // 0
                                                                                                 new float3(     i1.x, maxY,     i1.y), // 2
                                                                                                 new float3(   apex.x, maxY,   apex.y), // 1
                                                                                                 new float3(   apex.x, stepMin,   apex.y), // 3
                                                                                                 in surfaceDefinitionBlob, 
                                                                                                 allocator), StairsPart.Step, origin, anglePerStep, in surfaceDefinitionBlob), undersideSmoothingGroup, origin, middleAngle, stepOuterRadius);// Indirect
								subMeshIndex++;
                            }

                            if (i == 0)
                                minY -= treadHeight;

                            minY += stepHeight;
                            maxY += stepHeight;
                            maxY2 += stepHeight;
                        }
                    } else
                    {
                        var minY = origin.y;
                        var maxY = origin.y + stepHeight - treadHeight;
                        float2 o0, o1;
                        float angle = startAngle;
                        var c1 = math.sin(angle) * stepOuterRadius;
                        var s1 = math.cos(angle) * stepOuterRadius;
                        for (int i = 0; i < stepCount; i++)
                        {
                            var c0 = c1;
                            var s0 = s1;
                            angle += anglePerStep;
                            c1 = math.sin(angle) * stepOuterRadius;
                            s1 = math.cos(angle) * stepOuterRadius;

                            o0 = new float2(origin.x + c0, origin.z + s0);
                            o1 = new float2(origin.x + c1, origin.z + s1);

                            // the first step without a base under it, and every step solid down to the floor, from the ground
                            var bottom = ((i == 0 && !haveBase) || riserType == StairsRiserType.FillDown) ? ground : minY;
                            if (stepInnerRadius > 0)
                            {
                                var scale0 = InnerRadiusAt(angle - anglePerStep, stepInnerRadius, innerSides) / stepOuterRadius;
                                var scale1 = InnerRadiusAt(angle, stepInnerRadius, innerSides) / stepOuterRadius;
                                var i0 = new float2(origin.x + c0 * scale0, origin.z + s0 * scale0);
                                var i1 = new float2(origin.x + c1 * scale1, origin.z + s1 * scale1);
                                brushMeshes[i] = AlignedToFront(Classified(CreateBox(new float3(i1.x, maxY, i1.y),
                                                                          new float3(i0.x, maxY, i0.y),
                                                                          new float3(o0.x, maxY, o0.y),
                                                                          new float3(o1.x, maxY, o1.y),

                                                                          new float3(i1.x, bottom, i1.y),
                                                                          new float3(i0.x, bottom, i0.y),
                                                                          new float3(o0.x, bottom, o0.y),
                                                                          new float3(o1.x, bottom, o1.y),
                                                                          in surfaceDefinitionBlob,
                                                                          allocator), StairsPart.Step, origin, anglePerStep, in surfaceDefinitionBlob), i0, o0);// Indirect
                            } else
                            brushMeshes[i] = AlignedToFront(Classified(CreateWedgeAssetPolygons(new float3( origin.x, bottom, origin.z), // 0
                                                                      new float3(     o1.x, bottom,     o1.y), // 1
                                                                      new float3(     o0.x, bottom,     o0.y), // 2

                                                                      new float3( origin.x, maxY, origin.z), // 3
                                                                      new float3(     o1.x, maxY,     o1.y), // 4
                                                                      new float3(     o0.x, maxY,     o0.y), // 5
                                                                      in surfaceDefinitionBlob,
                                                                      allocator), StairsPart.Step, origin, anglePerStep, in surfaceDefinitionBlob), new float2(origin.x, origin.z), o0);// Indirect
							if (i == 0)
                                minY -= treadHeight;

                            if (riserType != StairsRiserType.FillDown)
                                minY += stepHeight;
                            maxY += stepHeight;
                        }
                    }
                
                    {
                        var subMeshIndex = treadStart - cylinderSubMeshCount;


                        if (!GenerateCylinderSubMesh(definition.StepsOuterDiameter, ground, origin.y + height, 0, outerSides, fitToBounds: fitToBounds,
                                                     in outerCylinderSurfaceDefinitionBlob,
                                                     out var brushMeshBlob,
                                                     allocator))// Indirect
							return false;
                        // the last step's top is on the cylinder's top, which shows instead: it starts at that step's front
                        var lastAngle = startAngle + (stepCount - 1) * anglePerStep;
                        var lastDir   = new float2(math.sin(lastAngle), math.cos(lastAngle));
                        var center    = new float2(origin.x, origin.z);
                        // its sides are the wall's inside, the other way round
                        if (definition.HaveOuterSide)
                            brushMeshBlob = WithSidePlanes(brushMeshBlob, origin, OuterRailInside(in definition), outerSides);
                        brushMeshes[subMeshIndex] = LastTopRunsOn(AlignedToFront(Classified(brushMeshBlob, StairsPart.Step, origin, anglePerStep, in surfaceDefinitionBlob),
                                                                   center + lastDir * InnerRadiusAt(lastAngle, stepInnerRadius, innerSides), center + lastDir * stepOuterRadius),
                                                                   origin, lastDir, center, anglePerStep, stepHeight, in surfaceDefinitionBlob);
                        //brushContainer.operations[subMeshIndex] = CSGOperationType.Intersecting;
                    }

                    if (haveInnerCyl)
                    {
                        var subMeshIndex = treadStart - 1;



                        if (!GenerateCylinderSubMesh(innerDiameter, ground, origin.y + height, 0, innerSides, fitToBounds: fitToBounds,
                                                     in innerCylinderSurfaceDefinitionBlob,
                                                     out var brushMeshBlob,
                                                     allocator))// Indirect
							return false;
                        brushMeshes[subMeshIndex] = Classified(brushMeshBlob, StairsPart.Step, origin, anglePerStep, in surfaceDefinitionBlob);
                        //brushContainer.operations[subMeshIndex] = CSGOperationType.Subtractive;
                    }

                }

                if (haveTread)
                {
                    var minY = origin.y + stepHeight - treadHeight;
                    var maxY = origin.y + stepHeight;
                    float2 i0, i1, o0, o1;
                    float angle = startAngle;
                    var c1 = math.sin(angle);
                    var s1 = math.cos(angle);
                    var startIndex = treadStart;
                    for (int n = 0, i = startIndex; n < stepCount; n++, i++)
                    {
                        var c0 = c1;
                        var s0 = s1;
                        angle += anglePerStep;
                        c1 = math.sin(angle);
                        s1 = math.cos(angle);

                        var inner0 = InnerRadiusAt(angle - anglePerStep, stepInnerRadius, innerSides);
                        var inner1 = InnerRadiusAt(angle, stepInnerRadius, innerSides);
                        i0 = new float2(origin.x + (c0 * inner0), origin.z + (s0 * inner0));
                        i1 = new float2(origin.x + (c1 * inner1), origin.z + (s1 * inner1));
                        o0 = new float2(origin.x + (c0 * (stepOuterRadius + outerNosingWidth)), origin.z + (s0 * (stepOuterRadius + outerNosingWidth)));
                        o1 = new float2(origin.x + (c1 * (stepOuterRadius + outerNosingWidth)), origin.z + (s1 * (stepOuterRadius + outerNosingWidth)));

                        var noseSizeDeep = math.normalize(new float2((c0 - c1), (s0 - s1))) * nosingDepth;
                        i0 += noseSizeDeep;
                        o0 += noseSizeDeep;

                        //brushMesh.halfEdges = (anglePerStep > 0) ? invertedBoxHalfEdges.ToArray() : boxHalfEdges.ToArray();
                        brushMeshes[i] = AlignedToFront(Classified(CreateBox(new float3(i1.x, maxY, i1.y), // 1
                                                   new float3(i0.x, maxY, i0.y), // 0
                                                   new float3(o0.x, maxY, o0.y), // 3
                                                   new float3(o1.x, maxY, o1.y), // 2

                                                   new float3(i1.x, minY, i1.y), // 5
                                                   new float3(i0.x, minY, i0.y), // 4
                                                   new float3(o0.x, minY, o0.y), // 7
                                                   new float3(o1.x, minY, o1.y), // 6
                                                   in surfaceDefinitionBlob,
                                                   allocator), StairsPart.Tread, origin, anglePerStep, in surfaceDefinitionBlob), i0, o0);// Indirect

						minY += stepHeight;
                        maxY += stepHeight;
                    }
                }


                // the treads' cylinders, only with treads: without, these places are the risers' cylinders, which would be
                // left without being disposed
                if (haveTread)
                {
                    var subMeshIndex = subMeshCount - cylinderSubMeshCount;
                    // the treads stop at a guard rail on the outside, rather than poking through it
                    if (!GenerateCylinderSubMesh(definition.HaveOuterSide ? definition.StepsOuterDiameter : outerDiameter + nosingWidth, ground, origin.y + height, 0, outerSides, fitToBounds: fitToBounds,
                                                 in outerCylinderSurfaceDefinitionBlob,
                                                 out var brushMeshBlob, allocator))// Indirect
						return false;
                    // the last tread's top is on the cylinder's top, which shows instead: it starts at that tread's nose
                    var lastAngle = startAngle + (stepCount - 1) * anglePerStep;
                    var lastDir   = new float2(math.sin(lastAngle), math.cos(lastAngle));
                    var nextDir   = new float2(math.sin(lastAngle + anglePerStep), math.cos(lastAngle + anglePerStep));
                    var nose      = math.normalizesafe(lastDir - nextDir) * nosingDepth;
                    var center    = new float2(origin.x, origin.z);
                    if (definition.HaveOuterSide)
                        brushMeshBlob = WithSidePlanes(brushMeshBlob, origin, OuterRailInside(in definition), outerSides);
                    brushMeshes[subMeshIndex] = LastTopRunsOn(AlignedToFront(Classified(brushMeshBlob, StairsPart.Step, origin, anglePerStep, in surfaceDefinitionBlob),
                                                               center + lastDir * InnerRadiusAt(lastAngle, stepInnerRadius, innerSides) + nose,
                                                               center + lastDir * (stepOuterRadius + outerNosingWidth) + nose),
                                                               origin, lastDir, center + nose, anglePerStep, stepHeight, in surfaceDefinitionBlob);
                    //brushContainer.operations[subMeshIndex] = CSGOperationType.Intersecting;
                }

                if (haveTread && haveInnerCyl)
                {
                    var subMeshIndex = subMeshCount - 1;
                    if (!GenerateCylinderSubMesh(innerDiameter - nosingWidth, ground, origin.y + height, 0, innerSides, fitToBounds: fitToBounds,
                                                 in innerCylinderSurfaceDefinitionBlob,
                                                 out var brushMeshBlob, allocator))// Indirect
						return false;
                    brushMeshes[subMeshIndex] = Classified(brushMeshBlob, StairsPart.Step, origin, anglePerStep, in surfaceDefinitionBlob);
                    //brushContainer.operations[subMeshIndex] = CSGOperationType.Subtractive;
                }

                // the steps' fronts and backs start their texture at the bottom of each step, and at the middle across
                for (int i = 0; i < subMeshCount && i < brushMeshes.Length; i++)
                    brushMeshes[i] = AlignedUpright(brushMeshes[i], origin, aroundOrigin: true, stepHeight, topRunsOnFromFront: anglePerStep < 0 ? -1 : 1);

                // the column and the sides come after the steps, so the cylinders that trim the steps leave them whole
                var index = subMeshCount;
                if (definition.HaveColumn)
                {
                    if (!GenerateCylinderSubMesh(innerDiameter, ground, origin.y + height, 0, innerSides, fitToBounds: fitToBounds,
                                                 in innerCylinderSurfaceDefinitionBlob,
                                                 out var columnBlob, allocator))// Indirect
                        return false;
                    brushMeshes[index++] = MapRoundFaces(SmoothRoundFaces(Classified(columnBlob, StairsPart.Column, origin, anglePerStep, in surfaceDefinitionBlob), definition.columnSmoothingGroup, origin), innerDiameter * 0.5f, innerSides);
                    if (definition.HaveColumnHole)
                    {
                        if (!GenerateCylinderSubMesh(definition.ColumnHoleDiameter, ground, origin.y + height, 0, innerSides, fitToBounds: fitToBounds,
                                                     in innerCylinderSurfaceDefinitionBlob,
                                                     out var holeBlob, allocator))// Indirect
                            return false;
                        brushMeshes[index++] = MapRoundFaces(SmoothRoundFaces(Classified(holeBlob, StairsPart.Column, origin, anglePerStep, in surfaceDefinitionBlob), definition.columnSmoothingGroup, origin), definition.ColumnHoleDiameter * 0.5f, innerSides);
                    }
                }

                // a wall reaches a little into what it stands against, so the two overlap instead of touching face to
                // face, which would leave edges where both their outlines meet
                const float kSideOverlap = 0.01f;
                var sideWidth = definition.sideWidth;
                if (definition.HaveOuterSide)
                {
                    // inside the outer cylinder, on the steps' outside, as thick as set inwards
                    var apothem = outerDiameter * 0.5f * math.cos(math.PI / outerSides);
                    GenerateSide(brushMeshes, ref index, definition.outerSide, OuterRailInside(in definition), apothem, outerSides, in definition, origin, startAngle, anglePerStep, in surfaceDefinitionBlob, allocator);
                }
                if (definition.HaveInnerSide)
                {
                    // against the open middle, as thick as set inwards; with a column there it stands beside the column,
                    // as thick as set outwards
                    var apothem = innerDiameter * 0.5f * math.cos(math.PI / innerSides);
                    if (definition.HaveColumn)
                        GenerateSide(brushMeshes, ref index, definition.innerSide, apothem - kSideOverlap, apothem + sideWidth, innerSides, in definition, origin, startAngle, anglePerStep, in surfaceDefinitionBlob, allocator);
                    else
                        GenerateSide(brushMeshes, ref index, definition.innerSide, math.max(0, apothem - sideWidth), apothem + kSideOverlap, innerSides, in definition, origin, startAngle, anglePerStep, in surfaceDefinitionBlob, allocator);
                }

                if (haveBase)
                    GenerateBase(brushMeshes, ref index, in definition, origin, ground, startAngle, anglePerStep, in surfaceDefinitionBlob, allocator);
                return true;
            }
        }

        // A wall along the steps between two distances from the middle, along the same straight pieces as the cylinder the
        // steps are trimmed by, following them round and up. Its height is measured from the line through the fronts of
        // the treads: above it for a wall above the steps, below it (under the steps) for one below, never below the floor.
        // There is one piece for each straight side of the cylinder (and one more where it reaches the floor), so each side
        // shows a single face; along it the wall's top and bottom are straight, a little off the steps' line in between.
        // Each piece is one brush, its top and bottom two triangles where they lean two ways
        static void GenerateSide(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, ref int index, StairsSideType sideType,
                                 float innerApothem, float outerApothem, int segments, in ChiselSpiralStairs definition, float3 origin,
                                 float startAngle, float anglePerStep,
                                 in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, Allocator allocator)
        {
            var stepHeight  = definition.stepHeight;
            var above       = (sideType == StairsSideType.Up   || sideType == StairsSideType.DownAndUp) ? definition.sideHeight : 0;
            // steps that are solid down to the floor have walls that go down to the floor too
            var below       = definition.SideBelowSteps(sideType);
            var floor      = math.min(0, origin.y);
            // how far along the wall each piece starts, on its inner and its outer face, so the texture runs on across them
            var innerLength = 0.0f;
            var outerLength = 0.0f;
            var middleLength = 0.0f;

            var end   = startAngle + anglePerStep * definition.StepCount;
            var split = definition.SideFloorAngle(startAngle, anglePerStep, sideType);
            // raised above the object's position, a wall below the steps stands on it beside the first step, as that
            // step does (its pieces there end where that step does, see SideFloorAngle)
            var onGround  = below > 0 && origin.y > floor;
            var firstStep = startAngle + anglePerStep;
            const float kToTheGround = 1e6f;
            var a0    = startAngle;
            {
                // In front of the first step, as far out as asked: straight on from the wall's first piece, its top and
                // bottom flat
                if (definition.sideFrontOffset > 0 && a0 < end - ChiselSpiralStairs.kAngleEpsilon)
                {
                    var first   = ChiselSpiralStairs.NextSidePieceEnd(a0, end, segments, split);
                    var middle  = (a0 + first) * 0.5f;
                    var in1     = Around(origin, ChiselSpiralStairs.RadiusOnSide(a0, middle, innerApothem, segments), a0);
                    var out1    = Around(origin, ChiselSpiralStairs.RadiusOnSide(a0, middle, outerApothem, segments), a0);
                    var inNext  = Around(origin, ChiselSpiralStairs.RadiusOnSide(first, middle, innerApothem, segments), first);
                    var outNext = Around(origin, ChiselSpiralStairs.RadiusOnSide(first, middle, outerApothem, segments), first);
                    var back    = math.normalizesafe(((in1 - inNext) + (out1 - outNext)) * 0.5f);
                    var in0     = in1  + back * definition.sideFrontOffset;
                    var out0    = out1 + back * definition.sideFrontOffset;

                    var line1   = origin.y + stepHeight;
                    var bottom  = onGround ? floor : math.max(floor, line1 - below);
                    var top1    = line1 + above;
                    var top0    = top1;

                    var smoothing = definition.sideSmoothingGroup;
                    var endsFrom  = origin;
                    innerLength = -math.distance(in0, in1);
                    outerLength = -math.distance(out0, out1);
                    var frontStart = -math.distance((in0 + out0) * 0.5f, (in1 + out1) * 0.5f);
                    float2 inUV0 = new float2(0, frontStart), outUV0 = new float2(math.distance(in0, out0), frontStart);
                    float2 inUV1 = new float2(0, 0),          outUV1 = new float2(math.distance(in1, out1), 0);
                    // where it meets the first piece both brushes have the one plane
                    var piece = WithPlane(Classified(SidePiece(in0, out0, in1, out1, top0, top1, bottom, bottom, in surfaceDefinitionBlob, allocator), StairsPart.Wall, origin, anglePerStep, in surfaceDefinitionBlob),
                                          RadialPlane(origin, a0));
                    piece = WallFace(WallFace(piece, smoothing, in0, in1, innerLength), smoothing, out0, out1, outerLength);
                    brushMeshes[index++] = TopFromCorners(AlignedUpright(piece, endsFrom, aroundOrigin: true, 0), in0, inUV0, out0, outUV0, in1, inUV1, out1, outUV1);
                    innerLength = 0;
                    outerLength = 0;
                }

                while (a0 < end - ChiselSpiralStairs.kAngleEpsilon)
                {
                    var a1 = ChiselSpiralStairs.NextSidePieceEnd(a0, end, segments, split);
                    // the line through the fronts of the treads rises a step per step
                    var line0 = origin.y + stepHeight * (1 + (a0 - startAngle) / anglePerStep);
                    var line1 = origin.y + stepHeight * (1 + (a1 - startAngle) / anglePerStep);

                    // both ends on the same straight piece of the round side
                    var middle = (a0 + a1) * 0.5f;
                    var in0  = Around(origin, ChiselSpiralStairs.RadiusOnSide(a0, middle, innerApothem, segments), a0);
                    var out0 = Around(origin, ChiselSpiralStairs.RadiusOnSide(a0, middle, outerApothem, segments), a0);
                    var in1  = Around(origin, ChiselSpiralStairs.RadiusOnSide(a1, middle, innerApothem, segments), a1);
                    var out1 = Around(origin, ChiselSpiralStairs.RadiusOnSide(a1, middle, outerApothem, segments), a1);

                    var smoothing = definition.sideSmoothingGroup;
                    // its ends, pointing out from the middle, have their texture from the middle out and from the bottom of the
                    // step beside them up, as that step's front and back
                    var stepBeside = math.clamp((int)math.floor(((a0 + a1) * 0.5f - startAngle) / anglePerStep), 0, definition.StepCount - 1);
                    var endsFrom   = new float3(origin.x, origin.y + stepBeside * stepHeight, origin.z);
                    var pieceBelow = (onGround && a1 <= firstStep + ChiselSpiralStairs.kAngleEpsilon) ? kToTheGround : below;
                    // the tops: u across the wall from its inside, v along its middle
                    var middleNext = middleLength + math.distance((in0 + out0) * 0.5f, (in1 + out1) * 0.5f);
                    float2 inUV0 = new float2(0, middleLength), outUV0 = new float2(math.distance(in0, out0), middleLength);
                    float2 inUV1 = new float2(0, middleNext),   outUV1 = new float2(math.distance(in1, out1), middleNext);
                    // where brushes meet (a piece and the next, the wall's inside and the steps' trimming cylinder) each has
                    // the one plane, the other way round, so the CSG finds them exactly on each other
                    SidePlanes(origin, middle, innerApothem, outerApothem, segments, out var innerSide, out var outerSide);
                    var bottom0 = math.max(floor, line0 - pieceBelow);
                    var bottom1 = math.max(floor, line1 - pieceBelow);
                    var piece   = WithPlanes(Classified(SidePiece(in0, out0, in1, out1, line0 + above, line1 + above, bottom0, bottom1, in surfaceDefinitionBlob, allocator), StairsPart.Wall, origin, anglePerStep, in surfaceDefinitionBlob),
                                             RadialPlane(origin, a0), RadialPlane(origin, a1), innerSide, outerSide);
                    piece = WallFace(WallFace(piece, smoothing, in0, in1, innerLength), smoothing, out0, out1, outerLength);
                    brushMeshes[index++] = TopFromCorners(AlignedUpright(piece, endsFrom, aroundOrigin: true, 0), in0, inUV0, out0, outUV0, in1, inUV1, out1, outUV1);
                    innerLength += math.distance(in0, in1);
                    outerLength += math.distance(out0, out1);
                    middleLength = middleNext;
                    a0 = a1;
                }
            }
        }

        // The base under the first step, from the ground up to the steps: a piece for each straight side of the outer
        // cylinder under that step, out to that side, from the middle or from the open middle's cylinder
        static void GenerateBase(NativeList<BlobAssetReference<BrushMeshBlob>> brushMeshes, ref int index, in ChiselSpiralStairs definition,
                                 float3 origin, float ground, float startAngle, float anglePerStep,
                                 in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, Allocator allocator)
        {
            var outerSides   = definition.outerSegments;
            var innerSides   = definition.innerSegments;
            var outerApothem = definition.StepsOuterDiameter * 0.5f * math.cos(math.PI / outerSides);
            var innerApothem = definition.innerDiameter * 0.5f * math.cos(math.PI / innerSides);
            var openMiddle   = definition.BaseHasOpenMiddle;
            var center       = new float2(origin.x, origin.z);
            var top          = origin.y;
            var low          = math.min(startAngle, startAngle + anglePerStep);
            var high         = math.max(startAngle, startAngle + anglePerStep);
            var a0           = low;
            for (int count = 0; a0 < high - ChiselSpiralStairs.kAngleEpsilon && count < outerSides + 2; count++)
            {
                var a1     = ChiselSpiralStairs.NextSideCorner(a0, high, outerSides);
                var middle = (a0 + a1) * 0.5f;
                var out0   = Around(origin, ChiselSpiralStairs.RadiusOnSide(a0, middle, outerApothem, outerSides), a0);
                var out1   = Around(origin, ChiselSpiralStairs.RadiusOnSide(a1, middle, outerApothem, outerSides), a1);
                if (openMiddle)
                {
                    var in0 = Around(origin, InnerRadiusAt(a0, innerApothem, innerSides), a0);
                    var in1 = Around(origin, InnerRadiusAt(a1, innerApothem, innerSides), a1);
                    brushMeshes[index++] = BasePiece(FlatBottomWedge(in0, top, out0, top, out1, top, ground, in surfaceDefinitionBlob, allocator), origin, anglePerStep, center, out0, in surfaceDefinitionBlob);
                    brushMeshes[index++] = BasePiece(FlatBottomWedge(in0, top, out1, top, in1, top, ground, in surfaceDefinitionBlob, allocator), origin, anglePerStep, center, out0, in surfaceDefinitionBlob);
                } else
                    brushMeshes[index++] = BasePiece(FlatBottomWedge(center, top, out0, top, out1, top, ground, in surfaceDefinitionBlob, allocator), origin, anglePerStep, center, out0, in surfaceDefinitionBlob);
                a0 = a1;
            }
        }

        static BlobAssetReference<BrushMeshBlob> BasePiece(BlobAssetReference<BrushMeshBlob> brushMesh, float3 origin, float anglePerStep, float2 center, float2 front,
                                                          in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob)
        {
            // its texture as the first step's, from the ground up
            return AlignedUpright(AlignedToFront(Classified(brushMesh, StairsPart.Base, origin, anglePerStep, in surfaceDefinitionBlob), center, front),
                                  new float3(origin.x, math.min(0, origin.y), origin.z), aroundOrigin: true, 0);
        }

        static float2 Around(float3 origin, float radius, float angle)
        {
            return new float2(origin.x + math.sin(angle) * radius, origin.z + math.cos(angle) * radius);
        }

        // A wedge standing on a triangle, from one flat bottom up to each corner's own top
        static BlobAssetReference<BrushMeshBlob> FlatBottomWedge(float2 p0, float top0, float2 p1, float top1, float2 p2, float top2, float bottom,
                                                                 in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, Allocator allocator)
        {
            var d1 = p1 - p0;
            var d2 = p2 - p0;
            if ((d1.x * d2.y - d1.y * d2.x) < 0)
            {
                var p = p1; p1 = p2; p2 = p;
                var top = top1; top1 = top2; top2 = top;
            }
            return CreateWedgeAssetPolygons(new float3(p0.x, bottom, p0.y),
                                            new float3(p1.x, bottom, p1.y),
                                            new float3(p2.x, bottom, p2.y),
                                            new float3(p0.x, top0, p0.y),
                                            new float3(p1.x, top1, p1.y),
                                            new float3(p2.x, top2, p2.y),
                                            in surfaceDefinitionBlob, allocator);// Indirect
        }

        // A piece of a wall as one convex brush, standing on the straight lines from in0 to in1 (its inside) and out0 to
        // out1 (its outside), its top from top0 at the first end to top1 at the second, its bottom likewise. A top or
        // bottom that leans two ways at once is two triangles, cut along the diagonal that keeps the brush convex
        static BlobAssetReference<BrushMeshBlob> SidePiece(float2 in0, float2 out0, float2 in1, float2 out1, float top0, float top1, float bottom0, float bottom1,
                                                          in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob, Allocator allocator)
        {
            ref var surfaces = ref surfaceDefinitionBlob.Value.surfaces;
            if (surfaces.Length < 1)
                return BlobAssetReference<BrushMeshBlob>.Null;

            // a wall thicker than the middle it stands round has its inside on the middle itself: a wedge from there, its
            // top and bottom each one flat triangle (only at the middle can its two ends meet on its inside). Its two
            // ends rise to different heights on the middle; there it reaches from the lower bottom to the higher top
            const float kOnePoint = 1e-5f;
            var wedge = math.lengthsq(in1 - in0) < kOnePoint * kOnePoint;

            // 0..3 the bottom, 4..7 the top, each: inside first end, outside first end, outside second end, inside second end
            // (a wedge has no inside second end: 0..2 the bottom, 3..5 the top, each: inside, outside first end, outside second end)
            var vertices = new NativeArray<float3>(wedge ? 6 : 8, Allocator.Temp);
            var loops    = new NativeList<int>(32, Allocator.Temp);
            var counts   = new NativeList<int>(8, Allocator.Temp);
            try
            {
                if (wedge)
                {
                    vertices[0] = new float3(in0.x,  math.min(bottom0, bottom1), in0.y);  vertices[1] = new float3(out0.x, bottom0, out0.y);
                    vertices[2] = new float3(out1.x, bottom1, out1.y);
                    vertices[3] = new float3(in0.x,  math.max(top0, top1), in0.y);        vertices[4] = new float3(out0.x, top0,    out0.y);
                    vertices[5] = new float3(out1.x, top1,    out1.y);
                } else
                {
                    vertices[0] = new float3(in0.x,  bottom0, in0.y);  vertices[1] = new float3(out0.x, bottom0, out0.y);
                    vertices[2] = new float3(out1.x, bottom1, out1.y); vertices[3] = new float3(in1.x,  bottom1, in1.y);
                    vertices[4] = new float3(in0.x,  top0,    in0.y);  vertices[5] = new float3(out0.x, top0,    out0.y);
                    vertices[6] = new float3(out1.x, top1,    out1.y); vertices[7] = new float3(in1.x,  top1,    in1.y);
                }
                var middle = float3.zero;
                for (int v = 0; v < vertices.Length; v++)
                    middle += vertices[v];
                middle /= vertices.Length;

                if (wedge)
                {
                    AddFace(ref loops, ref counts, 1, 2, 5, 4);     // outside
                    AddFace(ref loops, ref counts, 0, 1, 4, 3);     // first end
                    AddFace(ref loops, ref counts, 0, 2, 5, 3);     // second end
                    AddFace(ref loops, ref counts, 3, 4, 5);        // top
                    AddFace(ref loops, ref counts, 0, 1, 2);        // bottom
                } else
                {
                    AddFace(ref loops, ref counts, 0, 3, 7, 4);     // inside
                    AddFace(ref loops, ref counts, 1, 2, 6, 5);     // outside
                    AddFace(ref loops, ref counts, 0, 1, 5, 4);     // first end
                    AddFace(ref loops, ref counts, 3, 2, 6, 7);     // second end
                    AddConvexQuad(ref loops, ref counts, vertices, middle, 4, 5, 6, 7);
                    AddConvexQuad(ref loops, ref counts, vertices, middle, 0, 1, 2, 3);
                }
                return FromFaces(vertices, loops, counts, middle, ref surfaces, allocator);
            }
            finally
            {
                vertices.Dispose();
                loops.Dispose();
                counts.Dispose();
            }
        }

        static void AddFace(ref NativeList<int> loops, ref NativeList<int> counts, int a, int b, int c, int d = -1)
        {
            loops.Add(a); loops.Add(b); loops.Add(c);
            if (d >= 0) loops.Add(d);
            counts.Add(d >= 0 ? 4 : 3);
        }

        // A quad, or where it isn't flat the two triangles with the fourth corner inside the first's plane
        static void AddConvexQuad(ref NativeList<int> loops, ref NativeList<int> counts, NativeArray<float3> vertices, float3 middle, int a, int b, int c, int d)
        {
            const float kFlat = 0.00001f;
            var normal = math.normalizesafe(math.cross(vertices[b] - vertices[a], vertices[d] - vertices[a]));
            if (math.dot(normal, vertices[a] - middle) < 0)
                normal = -normal;
            var away = math.dot(normal, vertices[c] - vertices[a]);
            if (math.abs(away) < kFlat)
                AddFace(ref loops, ref counts, a, b, c, d);
            else if (away < 0)
            {
                AddFace(ref loops, ref counts, a, b, d);
                AddFace(ref loops, ref counts, b, c, d);
            } else
            {
                AddFace(ref loops, ref counts, a, b, c);
                AddFace(ref loops, ref counts, a, c, d);
            }
        }

        // A brush from its corners and the corners of each face (either way round: each is turned to face away from the
        // middle), its edges paired up between the faces
        static BlobAssetReference<BrushMeshBlob> FromFaces(NativeArray<float3> vertices, NativeList<int> loops, NativeList<int> counts, float3 middle,
                                                          ref BlobArray<InternalChiselSurface> surfaces, Allocator allocator)
        {
            for (int f = 0, first = 0; f < counts.Length; first += counts[f], f++)
            {
                var normal = float3.zero;
                var center = float3.zero;
                for (int i = 0; i < counts[f]; i++)
                {
                    var a = vertices[loops[first + i]];
                    normal += math.cross(a, vertices[loops[first + (i + 1) % counts[f]]]);
                    center += a;
                }
                center /= counts[f];
                // a polygon's corners turn so its plane (Newell's) faces out
                if (math.dot(normal, center - middle) >= 0)
                    continue;
                for (int i = 0, j = counts[f] - 1; i < j; i++, j--)
                {
                    var swap = loops[first + i];
                    loops[first + i] = loops[first + j];
                    loops[first + j] = swap;
                }
            }

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BrushMeshBlob>();
            var localVertices          = builder.Allocate(ref root.localVertices, vertices.Length);
            var halfEdges              = builder.Allocate(ref root.halfEdges, loops.Length);
            var halfEdgePolygonIndices = builder.Allocate(ref root.halfEdgePolygonIndices, loops.Length);
            var polygons               = builder.Allocate(ref root.polygons, counts.Length);
            var localPlanes            = builder.Allocate(ref root.localPlanes, counts.Length);
            root.localPlaneCount = polygons.Length;
            for (int v = 0; v < vertices.Length; v++)
                localVertices[v] = vertices[v];

            for (int f = 0, first = 0; f < counts.Length; first += counts[f], f++)
            {
                polygons[f] = new BrushMeshBlob.Polygon { firstEdge = first, edgeCount = counts[f], descriptionIndex = 0, surface = surfaces[0] };
                for (int i = 0; i < counts[f]; i++)
                {
                    halfEdges[first + i] = new BrushMeshBlob.HalfEdge { vertexIndex = loops[first + i], twinIndex = -1 };
                    halfEdgePolygonIndices[first + i] = f;
                }
            }
            // an edge ends in its corner, coming from the corner before it; its twin runs the other way
            for (int f = 0, first = 0; f < counts.Length; first += counts[f], f++)
            {
                for (int i = 0; i < counts[f]; i++)
                {
                    var to   = loops[first + i];
                    var from = loops[first + (i + counts[f] - 1) % counts[f]];
                    for (int g = 0, other = 0; g < counts.Length && halfEdges[first + i].twinIndex < 0; other += counts[g], g++)
                    {
                        if (g == f)
                            continue;
                        for (int j = 0; j < counts[g]; j++)
                        {
                            if (loops[other + j] == from && loops[other + (j + counts[g] - 1) % counts[g]] == to)
                            {
                                halfEdges[first + i] = new BrushMeshBlob.HalfEdge { vertexIndex = to, twinIndex = other + j };
                                break;
                            }
                        }
                    }
                }
            }
            CalculatePlanes(ref localPlanes, in polygons, in halfEdges, in localVertices);
            root.localBounds = CalculateBounds(in localVertices);
            return builder.CreateBlobAssetReference<BrushMeshBlob>(allocator);
        }

        // The CSG snaps every plane before it finds the corners where they meet, so two brushes only meet exactly where
        // they have the one plane, the other way round: a plane snaps to exactly the opposite of its opposite. Planes
        // made from each brush's own corners differ a little, and can snap apart. These give a place's plane once, for
        // every brush that has a face there.

        // How far out the inside of a guard rail on the outside is: the steps' trimming cylinders' sides are there too
        static float OuterRailInside(in ChiselSpiralStairs definition)
        {
            return math.max(0, definition.outerDiameter * 0.5f * math.cos(math.PI / definition.outerSegments) - definition.sideWidth);
        }

        // The upright plane through the middle of the stairs, along one of their angles
        static float4 RadialPlane(float3 origin, float angle)
        {
            var normal = new float3(math.cos(angle), 0, -math.sin(angle));
            return new float4(normal, -(normal.x * origin.x + normal.z * origin.z));
        }

        // Which straight side of a cylinder of so many sides an angle of the stairs is on, counted from 0
        static int SideOf(float angle, int segments)
        {
            var segmentAngle = (math.PI * 2) / segments;
            var offset       = ((segments & 1) == 1) ? 0.0f : segmentAngle * 0.5f;
            var t            = (math.PI * 0.5f) - angle;
            var side         = (int)math.floor((t - offset) / segmentAngle);
            return ((side % segments) + segments) % segments;
        }

        // The plane of a straight side of a cylinder around the origin, this far out, facing out. A cylinder has its corners
        // at (cos t, sin t), so a side's middle is at (cos e, sin e)
        static float4 SidePlane(float3 origin, int side, float apothem, int segments)
        {
            var segmentAngle = (math.PI * 2) / segments;
            var offset       = ((segments & 1) == 1) ? 0.0f : segmentAngle * 0.5f;
            var middle       = offset + (side + 0.5f) * segmentAngle;
            var normal       = new float3(math.cos(middle), 0, math.sin(middle));
            return new float4(normal, -(apothem + normal.x * origin.x + normal.z * origin.z));
        }

        // The planes of a wall's inside and outside along the side an angle of the stairs is on
        static void SidePlanes(float3 origin, float angle, float innerApothem, float outerApothem, int segments, out float4 inner, out float4 outer)
        {
            var side = SideOf(angle, segments);
            inner = SidePlane(origin, side, innerApothem, segments);
            outer = SidePlane(origin, side, outerApothem, segments);
        }

        // Every side of a cylinder this far out gets the plane its side has
        static BlobAssetReference<BrushMeshBlob> WithSidePlanes(BlobAssetReference<BrushMeshBlob> brushMesh, float3 origin, float apothem, int segments)
        {
            for (int side = 0; side < segments; side++)
                brushMesh = WithPlane(brushMesh, SidePlane(origin, side, apothem, segments));
            return brushMesh;
        }

        // The brush's face on a plane, either way round, gets that exact plane (or its opposite)
        static BlobAssetReference<BrushMeshBlob> WithPlane(BlobAssetReference<BrushMeshBlob> brushMesh, float4 plane)
        {
            if (!brushMesh.IsCreated)
                return brushMesh;
            const float kSameDirection = 0.9999f;
            const float kOnIt          = 0.001f;
            ref var mesh = ref brushMesh.Value;
            for (int p = 0; p < mesh.localPlanes.Length; p++)
            {
                var own    = mesh.localPlanes[p];
                var length = math.length(own.xyz);
                if (!(length > 0))
                    continue;
                own /= length;
                var facing = math.dot(own.xyz, plane.xyz);
                if (facing > kSameDirection && math.abs(own.w - plane.w) < kOnIt)
                    mesh.localPlanes[p] = plane;
                else if (facing < -kSameDirection && math.abs(own.w + plane.w) < kOnIt)
                    mesh.localPlanes[p] = -plane;
            }
            return brushMesh;
        }

        static BlobAssetReference<BrushMeshBlob> WithPlanes(BlobAssetReference<BrushMeshBlob> brushMesh, float4 a, float4 b, float4 c, float4 d)
        {
            return WithPlane(WithPlane(WithPlane(WithPlane(brushMesh, a), b), c), d);
        }

        // The round faces of a column or a wall, the upright ones facing towards or away from the center, share a smoothing
        // group so the light runs over them as over one curved surface; their tops, bottoms and ends stay sharp
        static BlobAssetReference<BrushMeshBlob> SmoothRoundFaces(BlobAssetReference<BrushMeshBlob> brushMesh, uint smoothingGroup, float3 center)
        {
            if (smoothingGroup == 0 || !brushMesh.IsCreated)
                return brushMesh;
            ref var mesh = ref brushMesh.Value;
            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                var normal = mesh.localPlanes[p].xyz;
                if (math.abs(normal.y) > 0.1f)
                    continue;
                ref var polygon = ref mesh.polygons[p];
                var middle = float3.zero;
                for (int e = 0; e < polygon.edgeCount; e++)
                    middle += mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex];
                middle /= math.max(1, polygon.edgeCount);
                var outwards = new float2(middle.x - center.x, middle.z - center.z);
                if (math.lengthsq(outwards) < 1e-8f)
                    continue;
                if (math.abs(math.dot(math.normalize(outwards), math.normalize(normal.xz))) > 0.7f)
                    polygon.surface.details.smoothingGroup = smoothingGroup;
            }
            return brushMesh;
        }

        // A wall piece's top, of one or two faces: its texture spread over each from the uvs at the piece's four corners
        // (found by where they stand), so the tops of the pieces along a wall run on into each other
        static BlobAssetReference<BrushMeshBlob> TopFromCorners(BlobAssetReference<BrushMeshBlob> brushMesh, float2 q0, float2 r0, float2 q1, float2 r1,
                                                               float2 q2, float2 r2, float2 q3, float2 r3)
        {
            if (!brushMesh.IsCreated)
                return brushMesh;
            const float kEpsilon = 0.0001f;
            ref var mesh = ref brushMesh.Value;
            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                if (mesh.localPlanes[p].y < 0.1f)
                    continue;
                ref var polygon = ref mesh.polygons[p];
                if (polygon.edgeCount < 3 || (polygon.surface.details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) != 0)
                    continue;
                // three of its corners and their uvs
                float3 c0 = default, c1 = default, c2 = default;
                float2 uv0 = default, uv1 = default, uv2 = default;
                var found = 0;
                for (int e = 0; e < polygon.edgeCount && found < 3; e++)
                {
                    var vertex = mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex];
                    float2 uv;
                    if      (math.lengthsq(vertex.xz - q0) < kEpsilon) uv = r0;
                    else if (math.lengthsq(vertex.xz - q1) < kEpsilon) uv = r1;
                    else if (math.lengthsq(vertex.xz - q2) < kEpsilon) uv = r2;
                    else if (math.lengthsq(vertex.xz - q3) < kEpsilon) uv = r3;
                    else break;
                    if (found == 0)      { c0 = vertex; uv0 = uv; }
                    else if (found == 1) { c1 = vertex; uv1 = uv; }
                    else                 { c2 = vertex; uv2 = uv; }
                    found++;
                }
                if (found < 3)
                    continue;

                // the linear function in the face's plane that is uv0, uv1 and uv2 at its corners
                var e1 = c1 - c0;
                var e2 = c2 - c0;
                float a11 = math.dot(e1, e1), a12 = math.dot(e1, e2), a22 = math.dot(e2, e2);
                var det = a11 * a22 - a12 * a12;
                if (math.abs(det) < 1e-12f)
                    continue;
                var du1 = uv1 - uv0;
                var du2 = uv2 - uv0;
                // per uv axis: gradient = alpha * e1 + beta * e2, with gradient . e1 = d1 and gradient . e2 = d2
                var alpha = (du1 * a22 - du2 * a12) / det;
                var beta  = (du2 * a11 - du1 * a12) / det;
                var gu = alpha.x * e1 + beta.x * e2;
                var gv = alpha.y * e1 + beta.y * e2;
                var bu = uv0.x - math.dot(gu, c0);
                var bv = uv0.y - math.dot(gv, c0);

                var toPlane  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(mesh.localPlanes[p]);
                var rotation = new float3x3(toPlane);
                var shift    = toPlane.c3.xyz;
                var ru = math.mul(rotation, gu);
                var rv = math.mul(rotation, gv);
                var u = new float4(ru, bu - math.dot(ru, shift));
                var v = new float4(rv, bv - math.dot(rv, shift));

                ref var uvs = ref polygon.surface.details.UV0;
                float4 givenU = uvs.U, givenV = uvs.V;
                uvs.U = givenU.x * u + givenU.y * v + new float4(0, 0, 0, givenU.w);
                uvs.V = givenV.x * u + givenV.y * v + new float4(0, 0, 0, givenV.w);
            }
            return brushMesh;
        }

        // A wall piece's face that stands on the line from one point to another, seen from above: it gets the smoothing
        // group, and a texture that runs on from the pieces before it, by the length along the wall, and up by height. It
        // is not mirrored seen from the side it faces, and the surface's own uv settings are applied to it; a surface
        // mapped in world space keeps its own
        static BlobAssetReference<BrushMeshBlob> WallFace(BlobAssetReference<BrushMeshBlob> brushMesh, uint smoothingGroup, float2 from, float2 to, float startLength)
        {
            if (!brushMesh.IsCreated)
                return brushMesh;
            const float kEpsilon = 0.0001f;
            ref var mesh = ref brushMesh.Value;
            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                ref var polygon = ref mesh.polygons[p];
                var along = polygon.edgeCount > 2;
                for (int e = 0; e < polygon.edgeCount && along; e++)
                {
                    var vertex = mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex];
                    along = math.lengthsq(vertex.xz - from) < kEpsilon || math.lengthsq(vertex.xz - to) < kEpsilon;
                }
                if (!along)
                    continue;

                if (smoothingGroup != 0)
                    polygon.surface.details.smoothingGroup = smoothingGroup;

                if ((polygon.surface.details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) != 0)
                    continue;
                var length = math.distance(from, to);
                if (length < kEpsilon)
                    continue;

                // u = the length along the wall, v = the height, both as a flat mapping of the face in the brush's space
                var normal    = math.normalizesafe(mesh.localPlanes[p].xyz);
                var direction = new float3(to.x - from.x, 0, to.y - from.y) / length;
                // u runs to the right seen from the side the face faces: cross(normal, up) in a left handed space
                var side      = math.dot(direction, math.cross(normal, new float3(0, 1, 0))) < 0 ? -1.0f : 1.0f;
                var start     = new float3(from.x, 0, from.y);
                var alongU    = direction * side;
                var atU       = side * (startLength - math.dot(direction, start));
                var upV       = new float3(0, 1, 0);
                var atV       = 0.0f;

                // the same in the plane's space, which the surface's uv settings work in
                var toPlane  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(mesh.localPlanes[p]);
                var rotation = new float3x3(toPlane);
                var shift    = toPlane.c3.xyz;
                var u = new float4(math.mul(rotation, alongU), atU - math.dot(math.mul(rotation, alongU), shift));
                var v = new float4(math.mul(rotation, upV),    atV - math.dot(math.mul(rotation, upV),    shift));

                ref var uv0 = ref polygon.surface.details.UV0;
                float4 givenU = uv0.U, givenV = uv0.V;
                uv0.U = givenU.x * u + givenU.y * v + new float4(0, 0, 0, givenU.w);
                uv0.V = givenV.x * u + givenV.y * v + new float4(0, 0, 0, givenV.w);
            }
            return brushMesh;
        }

        // A column's round side: the texture runs round it by the length along its sides, starting again where it began,
        // and up by height. Like a wall's faces it is not mirrored seen from the side a face faces, and the surface's own uv
        // settings are applied to it; a surface mapped in world space keeps its own. The cylinder is around the brush's
        // center, with its corners at (cos t, sin t)
        static BlobAssetReference<BrushMeshBlob> MapRoundFaces(BlobAssetReference<BrushMeshBlob> brushMesh, float radius, int segments)
        {
            if (!brushMesh.IsCreated || radius <= 0 || segments < 3)
                return brushMesh;
            var segmentAngle = (math.PI * 2) / segments;
            var offset       = ((segments & 1) == 1) ? 0.0f : segmentAngle * 0.5f;
            var sideLength   = 2 * radius * math.sin(segmentAngle * 0.5f);
            ref var mesh = ref brushMesh.Value;
            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                ref var polygon = ref mesh.polygons[p];
                var normal = math.normalizesafe(mesh.localPlanes[p].xyz);
                if (math.abs(normal.y) > 0.1f)
                    continue;
                if ((polygon.surface.details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) != 0)
                    continue;

                // which side this is, from the middle of its corners
                var middle = float3.zero;
                for (int e = 0; e < polygon.edgeCount; e++)
                    middle += mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex];
                middle /= math.max(1, polygon.edgeCount);
                var angle = math.atan2(middle.z, middle.x);
                var side  = (int)math.floor((angle - offset) / segmentAngle);
                side = ((side % segments) + segments) % segments;
                var t0    = offset + side * segmentAngle;
                var start = new float3(math.cos(t0), 0, math.sin(t0)) * radius;
                var end   = new float3(math.cos(t0 + segmentAngle), 0, math.sin(t0 + segmentAngle)) * radius;
                var direction = math.normalizesafe(end - start);

                var flip   = math.dot(direction, math.cross(normal, new float3(0, 1, 0))) < 0 ? -1.0f : 1.0f;
                var alongU = direction * flip;
                var atU    = flip * (side * sideLength - math.dot(direction, start));
                var upV    = new float3(0, 1, 0);

                var toPlane  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(mesh.localPlanes[p]);
                var rotation = new float3x3(toPlane);
                var shift    = toPlane.c3.xyz;
                var u = new float4(math.mul(rotation, alongU), atU - math.dot(math.mul(rotation, alongU), shift));
                var v = new float4(math.mul(rotation, upV),    0 - math.dot(math.mul(rotation, upV),    shift));

                ref var uv0 = ref polygon.surface.details.UV0;
                float4 givenU = uv0.U, givenV = uv0.V;
                uv0.U = givenU.x * u + givenU.y * v + new float4(0, 0, 0, givenU.w);
                uv0.V = givenV.x * u + givenV.y * v + new float4(0, 0, 0, givenV.w);
            }
            return brushMesh;
        }

        // How far from the middle the steps start at an angle, on a shape with the inner cylinder's straight sides, its sides
        // an apothem away from the middle; 0 when they start at the middle
        static float InnerRadiusAt(float angle, float apothem, int segments)
        {
            if (apothem <= 0)
                return 0;
            return ChiselSpiralStairs.RadiusOnSide(angle, angle, apothem, segments);
        }

        // A step's upright front and back: the texture starts at the bottom of the step, and across at a fixed place (the
        // stairs' side for straight stairs, the middle for spiral ones, where each front and back points away from). Only
        // the faces running across the stairs: for straight stairs those facing along them, for spiral stairs those
        // pointing out from the middle. Not mirrored seen from the side a face faces, and the surface's own uv settings
        // are applied to it; a surface mapped in world space keeps its own
        // With a way the steps go (for spiral stairs the sign of the angle per step), a step's top runs on from its front
        // without a seam: the same way across, and from the front's top edge into the step
        static BlobAssetReference<BrushMeshBlob> AlignedUpright(BlobAssetReference<BrushMeshBlob> brushMesh, float3 origin, bool aroundOrigin, float stepHeight, float topRunsOnFromFront = 0)
        {
            if (!brushMesh.IsCreated)
                return brushMesh;
            ref var mesh = ref brushMesh.Value;
            // the bottom of the step this piece belongs to (its top is in that step, a tread's top is the step's top), so
            // a tread's front and the riser under it run on as one; steps are a whole number of steps up from the origin
            var top = float.NegativeInfinity;
            for (int i = 0; i < mesh.localVertices.Length; i++)
                top = math.max(top, mesh.localVertices[i].y);
            // without a step height the texture starts at the origin's height itself
            var bottom = origin.y;
            if (stepHeight > 0)
                bottom = origin.y + (math.ceil((top - origin.y) / stepHeight - 0.001f) - 1) * stepHeight;
            var start = new float3(origin.x, bottom, origin.z);
            var up    = new float3(0, 1, 0);

            // its front, a corner on it and the uv settings of its surface, for the top to run on from
            var frontNormal = float3.zero;
            var onFront     = float3.zero;
            float4 frontU = default, frontV = default;
            var haveFront   = false;
            var frontFromMiddle = -1.0f; // a tiny face right at the middle can pass for one: the one reaching furthest out is it
            for (int p = 0; topRunsOnFromFront != 0 && p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                var normal = math.normalizesafe(mesh.localPlanes[p].xyz);
                if (math.abs(normal.y) > 0.1f)
                    continue;
                ref var polygon = ref mesh.polygons[p];
                var middle = float3.zero;
                for (int e = 0; e < polygon.edgeCount; e++)
                    middle += mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex];
                middle /= math.max(1, polygon.edgeCount);
                if (aroundOrigin)
                {
                    var outwards = new float2(middle.x - origin.x, middle.z - origin.z);
                    var fromMiddle = math.length(outwards);
                    if (fromMiddle < 1e-4f || fromMiddle <= frontFromMiddle)
                        continue;
                    outwards /= fromMiddle;
                    if (math.abs(math.dot(outwards, normal.xz)) > 0.1f)
                        continue;
                    // up the stairs, around the middle; the front looks the other way
                    var goingUp = new float2(outwards.y, -outwards.x) * topRunsOnFromFront;
                    if (math.dot(normal.xz, goingUp) >= 0)
                        continue;
                    frontFromMiddle = fromMiddle;
                } else
                if (normal.z < 0.9f || haveFront)
                    continue;
                frontNormal = normal;
                onFront     = mesh.localVertices[mesh.halfEdges[polygon.firstEdge].vertexIndex];
                frontU      = polygon.surface.details.UV0.U;
                frontV      = polygon.surface.details.UV0.V;
                haveFront   = true;
            }

            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                var normal = math.normalizesafe(mesh.localPlanes[p].xyz);
                if (math.abs(normal.y) > 0.1f)
                    continue;
                ref var polygon = ref mesh.polygons[p];
                if (aroundOrigin)
                {
                    var middle = float3.zero;
                    for (int e = 0; e < polygon.edgeCount; e++)
                        middle += mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex];
                    middle /= math.max(1, polygon.edgeCount);
                    var outwards = new float2(middle.x - origin.x, middle.z - origin.z);
                    if (math.lengthsq(outwards) < 1e-8f || math.abs(math.dot(math.normalize(outwards), normal.xz)) > 0.1f)
                        continue;
                } else
                if (math.abs(normal.z) < 0.9f)
                    continue;
                if ((polygon.surface.details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) != 0)
                    continue;

                // u to the right seen from in front (left handed space), v up
                var u3 = math.normalizesafe(math.cross(normal, up));
                var v3 = up;

                var toPlane  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(mesh.localPlanes[p]);
                var rotation = new float3x3(toPlane);
                var shift    = toPlane.c3.xyz;
                var ru = math.mul(rotation, u3);
                var rv = math.mul(rotation, v3);
                var u = new float4(ru, -math.dot(u3, start) - math.dot(ru, shift));
                var v = new float4(rv, -math.dot(v3, start) - math.dot(rv, shift));

                ref var uv0 = ref polygon.surface.details.UV0;
                float4 givenU = uv0.U, givenV = uv0.V;
                uv0.U = givenU.x * u + givenU.y * v + new float4(0, 0, 0, givenU.w);
                uv0.V = givenV.x * u + givenV.y * v + new float4(0, 0, 0, givenV.w);
            }

            if (!haveFront)
                return brushMesh;
            return TopRunsOn(brushMesh, frontNormal, start, onFront, top - bottom, frontU, frontV);
        }

        // The last step's top is on the top of the cylinder that trims it, which has no front of its own
        static BlobAssetReference<BrushMeshBlob> LastTopRunsOn(BlobAssetReference<BrushMeshBlob> brushMesh, float3 origin, float2 lastDir, float2 onFront, float anglePerStep, float stepHeight,
                                                               in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob)
        {
            var goingUp = new float2(lastDir.y, -lastDir.x) * (anglePerStep < 0 ? -1 : 1);
            ref var uv0 = ref surfaceDefinitionBlob.Value.surfaces[(int)StairsSurface.RiserFront].details.UV0;
            return TopRunsOn(brushMesh, new float3(-goingUp.x, 0, -goingUp.y), origin, new float3(onFront.x, 0, onFront.y), stepHeight, uv0.U, uv0.V);
        }

        // A flat top going on from the top edge of a front: the same way across, into the step from there, with the front's uv settings
        static BlobAssetReference<BrushMeshBlob> TopRunsOn(BlobAssetReference<BrushMeshBlob> brushMesh, float3 frontNormal, float3 start, float3 onFront, float rise, float4 frontU, float4 frontV)
        {
            if (!brushMesh.IsCreated)
                return brushMesh;
            ref var mesh = ref brushMesh.Value;
            var up = new float3(0, 1, 0);
            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                var normal = math.normalizesafe(mesh.localPlanes[p].xyz);
                if (normal.y < 0.9f)
                    continue;
                ref var polygon = ref mesh.polygons[p];
                if ((polygon.surface.details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) != 0)
                    continue;

                var u3 = math.normalizesafe(math.cross(frontNormal, up));
                var v3 = -frontNormal;

                var toPlane  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(mesh.localPlanes[p]);
                var rotation = new float3x3(toPlane);
                var shift    = toPlane.c3.xyz;
                var ru = math.mul(rotation, u3);
                var rv = math.mul(rotation, v3);
                var u = new float4(ru, -math.dot(u3, start) - math.dot(ru, shift));
                var v = new float4(rv, -math.dot(v3, onFront) + rise - math.dot(rv, shift));

                // with the uv settings the front has, or the two wouldn't meet
                ref var uv0 = ref polygon.surface.details.UV0;
                uv0.U = frontU.x * u + frontU.y * v + new float4(0, 0, 0, frontU.w);
                uv0.V = frontV.x * u + frontV.y * v + new float4(0, 0, 0, frontV.w);
            }
            return brushMesh;
        }

        // A step's flat top and bottom: the texture starts at the step's front edge, running along it, and into the step
        // from there, so every step shows it the same way. Not mirrored seen from the side a face faces, and the surface's
        // own uv settings are applied to it; a surface mapped in world space keeps its own
        static BlobAssetReference<BrushMeshBlob> AlignedToFront(BlobAssetReference<BrushMeshBlob> brushMesh, float2 frontStart, float2 frontEnd)
        {
            if (!brushMesh.IsCreated)
                return brushMesh;
            var along = frontEnd - frontStart;
            var length = math.length(along);
            if (length < 1e-6f)
                return brushMesh;
            var alongU = new float3(along.x, 0, along.y) / length;
            var start  = new float3(frontStart.x, 0, frontStart.y);
            ref var mesh = ref brushMesh.Value;
            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                var normal = math.normalizesafe(mesh.localPlanes[p].xyz);
                if (math.abs(normal.y) < 0.9f)
                    continue;
                ref var polygon = ref mesh.polygons[p];
                if ((polygon.surface.details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) != 0)
                    continue;

                // v is turned a quarter from u so u runs to the right seen from this face (left handed space)
                var intoV = math.cross(alongU, normal);
                var u3 = alongU;
                var v3 = intoV;

                var toPlane  = MathExtensions.GenerateLocalToPlaneSpaceMatrix(mesh.localPlanes[p]);
                var rotation = new float3x3(toPlane);
                var shift    = toPlane.c3.xyz;
                var ru = math.mul(rotation, u3);
                var rv = math.mul(rotation, v3);
                var u = new float4(ru, -math.dot(u3, start) - math.dot(ru, shift));
                var v = new float4(rv, -math.dot(v3, start) - math.dot(rv, shift));

                ref var uv0 = ref polygon.surface.details.UV0;
                float4 givenU = uv0.U, givenV = uv0.V;
                uv0.U = givenU.x * u + givenU.y * v + new float4(0, 0, 0, givenU.w);
                uv0.V = givenV.x * u + givenV.y * v + new float4(0, 0, 0, givenV.w);
            }
            return brushMesh;
        }

        // The sloped underside is made of flat pieces: the ones facing down share a smoothing group, so the light runs over
        // them as over one curved surface, and a texture that runs on across them: around by the length along the outer
        // edge, and outwards by the distance from the center. Each piece is a triangle, so its corners get exactly those
        // values and the pieces meet without a seam. Not mirrored seen from below, and the surface's own uv settings are
        // applied to it; a surface mapped in world space keeps its own
        static BlobAssetReference<BrushMeshBlob> SmoothUnderside(BlobAssetReference<BrushMeshBlob> brushMesh, uint smoothingGroup,
                                                                 float3 center, float middleAngle, float outerRadius)
        {
            if (!brushMesh.IsCreated)
                return brushMesh;
            ref var mesh = ref brushMesh.Value;
            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                if (mesh.localPlanes[p].y >= -0.2f)
                    continue;
                ref var polygon = ref mesh.polygons[p];
                if (smoothingGroup != 0)
                    polygon.surface.details.smoothingGroup = smoothingGroup;
                if ((polygon.surface.details.detailFlags & SurfaceDetailFlags.TextureIsInWorldSpace) != 0 || polygon.edgeCount < 3)
                    continue;

                var toPlane = MathExtensions.GenerateLocalToPlaneSpaceMatrix(mesh.localPlanes[p]);
                float2 q0 = default, q1 = default, q2 = default;
                float2 t0 = default, t1 = default, t2 = default;
                for (int e = 0; e < 3; e++)
                {
                    var vertex = mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex];
                    var q      = math.mul(toPlane, new float4(vertex, 1)).xy;
                    var offset = new float2(vertex.x - center.x, vertex.z - center.z);
                    var radius = math.length(offset);
                    // the angle as the steps measure it, unwrapped around the middle of this step; at the center itself
                    // there is none, so the middle is used
                    var angle  = middleAngle;
                    if (radius > 1e-5f)
                    {
                        var difference = math.atan2(offset.x, offset.y) - middleAngle;
                        difference -= math.round(difference / (2 * math.PI)) * (2 * math.PI);
                        angle += difference;
                    }
                    var t = new float2(angle * outerRadius, radius);
                    if (e == 0) { q0 = q; t0 = t; } else if (e == 1) { q1 = q; t1 = t; } else { q2 = q; t2 = t; }
                }

                // the flat mapping in the plane's space that puts those values on the three corners
                var edge1 = q1 - q0;
                var edge2 = q2 - q0;
                var det   = edge1.x * edge2.y - edge1.y * edge2.x;
                if (math.abs(det) < 1e-12f)
                    continue;
                var du = new float2(t1.x - t0.x, t2.x - t0.x);
                var dv = new float2(t1.y - t0.y, t2.y - t0.y);
                var gradU = new float2(du.x * edge2.y - du.y * edge1.y, du.y * edge1.x - du.x * edge2.x) / det;
                var gradV = new float2(dv.x * edge2.y - dv.y * edge1.y, dv.y * edge1.x - dv.x * edge2.x) / det;
                // seen from the side it faces the texture isn't mirrored
                var flip  = (gradU.x * gradV.y - gradU.y * gradV.x) < 0 ? -1.0f : 1.0f;
                var u = new float4(gradU.x, gradU.y, 0, t0.x - math.dot(gradU, q0)) * flip;
                var v = new float4(gradV.x, gradV.y, 0, t0.y - math.dot(gradV, q0));

                ref var uv0 = ref polygon.surface.details.UV0;
                float4 givenU = uv0.U, givenV = uv0.V;
                uv0.U = givenU.x * u + givenU.y * v + new float4(0, 0, 0, givenU.w);
                uv0.V = givenV.x * u + givenV.y * v + new float4(0, 0, 0, givenV.w);
            }
            return brushMesh;
        }

        // Gives each face the stairs' surface for the way it faces. Seen from the middle, a face pointing to or away from it
        // is a step's side; a step's front points back down the stairs, against the way the steps go round
        static BlobAssetReference<BrushMeshBlob> Classified(BlobAssetReference<BrushMeshBlob> brushMesh, StairsPart part, float3 origin, float anglePerStep,
                                                            in BlobAssetReference<InternalChiselSurfaceArray> surfaceDefinitionBlob)
        {
            if (!brushMesh.IsCreated)
                return brushMesh;
            ref var mesh = ref brushMesh.Value;
            ref var surfaces = ref surfaceDefinitionBlob.Value.surfaces;
            for (int p = 0; p < mesh.polygons.Length && p < mesh.localPlanes.Length; p++)
            {
                ref var polygon = ref mesh.polygons[p];
                var normal  = mesh.localPlanes[p].xyz;
                var middle  = float3.zero;
                for (int e = 0; e < polygon.edgeCount; e++)
                    middle += mesh.localVertices[mesh.halfEdges[polygon.firstEdge + e].vertexIndex];
                middle /= math.max(1, polygon.edgeCount);

                // in the steps' own terms: x along the radius, z along the way down the stairs
                var outwards = math.normalizesafe(new float2(middle.x - origin.x, middle.z - origin.z), new float2(0, 1));
                var around   = new float2(outwards.y, -outwards.x) * (anglePerStep < 0 ? -1 : 1);
                var local    = new float3(math.dot(normal.xz, outwards), normal.y, -math.dot(normal.xz, around));

                var surface = StairsSurfaces.Classify(part, local);
                polygon.descriptionIndex = (int)surface;
                polygon.surface          = surfaces[(int)surface];
            }
            return brushMesh;
        }
    }
}
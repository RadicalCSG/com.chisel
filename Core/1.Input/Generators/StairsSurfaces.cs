using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    // The surfaces both the linear and the spiral stairs have, in the order their surface arrays keep them
    public enum StairsSurface : byte
    {
        [InspectorName("Tread Top")]
        TreadTop    = 0,    // the tops of the treads, or of the steps where there are none
        [InspectorName("Tread Bottom")]
        TreadBottom = 1,    // under the treads
        [InspectorName("Riser Front")]
        RiserFront  = 2,    // the fronts of the steps
        [InspectorName("Riser Back")]
        RiserBack   = 3,    // the backs of the steps
        Underside   = 4,    // under the steps, on the floor too
        [InspectorName("Step Sides")]
        StepSides   = 5,    // the sides of the steps
        [InspectorName("Step Nose")]
        StepNose    = 6,    // the fronts of the treads
        Wall        = 7,    // the side walls
        [InspectorName("Guard Rail Top")]
        WallTop     = 8,    // the tops of the side walls
        [InspectorName("Guard Rail Bottom")]
        WallBottom  = 9,    // under the side walls
        Column      = 10,   // the column in the middle of spiral stairs
        Base        = 11,   // the solid part under the lowest step, when it starts higher up

        TotalSurfaces
    }

    public enum StairsPart : byte
    {
        Step,
        Tread,
        Wall,
        Column,
        Base
    }

    public static class StairsSurfaces
    {
        public const int kCount = (int)StairsSurface.TotalSurfaces;

        static readonly string[] kNames = { "Tread Top", "Tread Bottom", "Riser Front", "Riser Back", "Underside", "Step Sides", "Step Nose", "Guard Rail", "Guard Rail Top", "Guard Rail Bottom", "Column", "Base" };

        public static string Name(int index) { return (index >= 0 && index < kNames.Length) ? kNames[index] : null; }

        // Whether stairs with these parts have faces with this surface
        public static bool IsUsed(int index, bool haveTread, bool haveRiser, bool haveWall, bool haveColumn, bool haveBase)
        {
            switch ((StairsSurface)index)
            {
                case StairsSurface.TreadTop:    return haveTread || haveRiser;
                case StairsSurface.TreadBottom:
                case StairsSurface.StepNose:    return haveTread;
                case StairsSurface.RiserFront:
                case StairsSurface.RiserBack:
                case StairsSurface.Underside:   return haveRiser;
                case StairsSurface.StepSides:   return haveTread || haveRiser;
                case StairsSurface.Wall:
                case StairsSurface.WallTop:
                case StairsSurface.WallBottom:  return haveWall;
                case StairsSurface.Column:      return haveColumn;
                case StairsSurface.Base:        return haveBase;
                default:                        return false;
            }
        }

        // The parts every kind of stairs has a material for, used or not, so their materials are the same: the base,
        // which is there only when the stairs start above the floor
        public static bool IsAlwaysAPart(int index) { return (StairsSurface)index == StairsSurface.Base; }

        // A face's surface from what it is part of and the way it faces, where a step's front faces +z and its sides
        // face along x
        public static StairsSurface Classify(StairsPart part, float3 normal)
        {
            if (part == StairsPart.Column)
                return StairsSurface.Column;
            if (part == StairsPart.Base)
                return StairsSurface.Base;
            if (part == StairsPart.Wall)
                return normal.y > 0.5f ? StairsSurface.WallTop : normal.y < -0.5f ? StairsSurface.WallBottom : StairsSurface.Wall;
            if (normal.y < -0.5f)
                return part == StairsPart.Tread ? StairsSurface.TreadBottom : StairsSurface.Underside;
            if (normal.y > 0.5f)
                return StairsSurface.TreadTop;
            if (math.abs(normal.x) > math.abs(normal.z))
                return StairsSurface.StepSides;
            if (normal.z <= 0)
                return StairsSurface.RiserBack;
            return part == StairsPart.Tread ? StairsSurface.StepNose : StairsSurface.RiserFront;
        }

        public static void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition)
        {
            var surfaces = surfaceDefinition.surfaces;
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i].HasMaterial)
                    continue;
                switch ((StairsSurface)i)
                {
                    case StairsSurface.TreadTop:    surfaces[i].SetMaterial(ChiselProjectSettings.DefaultTreadMaterial); break;
                    case StairsSurface.TreadBottom: surfaces[i].SetMaterial(ChiselProjectSettings.DefaultTreadBottomMaterial); break;
                    case StairsSurface.RiserFront:  surfaces[i].SetMaterial(ChiselProjectSettings.DefaultRiserMaterial); break;
                    case StairsSurface.StepNose:    surfaces[i].SetMaterial(ChiselProjectSettings.DefaultStepNoseMaterial); break;
                    case StairsSurface.StepSides:   surfaces[i].SetMaterial(ChiselProjectSettings.DefaultStepSidesMaterial); break;
                    case StairsSurface.Column:      surfaces[i].SetMaterial(ChiselProjectSettings.DefaultColumnMaterial); break;
                    case StairsSurface.Base:        surfaces[i].SetMaterial(ChiselProjectSettings.DefaultFloorMaterial); break;
                    default:                        surfaces[i].SetMaterial(ChiselProjectSettings.DefaultWallMaterial); break;
                }
            }
        }
    }
}

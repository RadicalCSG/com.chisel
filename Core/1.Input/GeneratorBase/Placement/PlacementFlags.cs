using System;

namespace Chisel.Core
{
    [Flags]
    public enum PlacementFlags
    {
        [ToggleFlag("size_from_bottom",   "Extrude from the bottom",
                    "size_from_center",   "Extrude it from the center")]
        GenerateFromCenterY     = 1,

        
        [ToggleFlag("drag_to_height",     "Drag to extrude distance",
                    "auto_height",       "Extrude distance is determined by base size")]
        HeightEqualsXZ          = 2,

        
        [ToggleFlag("drag_to_height",     "Drag to extrude distance",
                    "auto_height",       "Extrude distance is determined by base size")]
        HeightEqualsHalfXZ      = 4,

        
        [ToggleFlag("rectangular_base",  "Base width and depth can be sized independently", 
                    "square_base",       "Base width and depth are identical in size")]
        SameLengthXZ            = 8,


        [ToggleFlag("size_base_from_corner", "Base is sized from corner",
                    "size_base_from_center", "Base is sized from center")]
        GenerateFromCenterXZ    = 16,

        [ToggleFlag(ignore: true)] None = 0,
        [ToggleFlag(ignore: true)] AlwaysFaceUp         = 32,
        [ToggleFlag(ignore: true)] AlwaysFaceCameraXZ   = 64,
        [ToggleFlag(ignore: true)] UseLastHeight        = 128,
    }
}

using UnityEngine;

namespace Chisel.Editors
{
    // The outline colors, as set in Chisel's preferences
    public static class ColorManager
    {
        public static Color UnselectedOutlineColor    => ChiselColors.UnselectedOutline.Value;
        public static Color PreSelectedOutlineColor   => ChiselColors.PreSelectedOutline.Value;
        public static Color SelectedHoverOutlineColor => ChiselColors.SelectedHoverOutline.Value;
        public static Color SelectedOutlineColor      => ChiselColors.SelectedOutline.Value;
    }
}

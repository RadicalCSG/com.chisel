using UnityEngine;

namespace Chisel.Editors
{
    public static class ColorManager
    {
		// TODO: create proper color management
		//static readonly Color kUnselectedOutlineColor			= new(255.0f / 255.0f, 102.0f / 255.0f, 55.0f / 255.0f, 128.0f / 255.0f);
		public readonly static Color kUnselectedOutlineColor	= new( 94.0f / 255.0f, 119.0f / 255.0f, 155.0f / 255.0f, 255.0f / 255.0f);
		public readonly static Color kPreSelectedOutlineColor	= new(201.0f / 255.0f, 200.0f / 255.0f, 144.0f / 255.0f, 255.0f / 255.0f);
		public readonly static Color kSelectedHoverOutlineColor	= new(246.0f / 255.0f, 242.0f / 255.0f,  50.0f / 255.0f, 255.0f / 255.0f);
		public readonly static Color kSelectedOutlineColor		= new(255.0f / 255.0f, 102.0f / 255.0f,   0.0f / 255.0f, 255.0f / 255.0f);

    }
}

using System;
using System.Collections.Generic;
using Chisel.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Pool;
using System.Runtime.CompilerServices;

namespace Chisel.Components
{
	[Serializable]
	public class BrushPickingMaterial
	{
		static Shader	s_BrushPickingShader;
		static Material s_BrushPickingMaterial;
		static int		s_ScenePickingPassIndex;
		static int		s_PickingOffsetPropertyID;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetState()
		{
			s_BrushPickingShader = null;
			s_BrushPickingMaterial = null;
			s_ScenePickingPassIndex = -1;
			s_PickingOffsetPropertyID = -1;
		}

		static void Initialize()
		{
			if (s_BrushPickingShader != null)
				return;
			
			s_BrushPickingShader = Shader.Find("Hidden/Chisel/Brush-Picking");
			s_BrushPickingMaterial = new Material(s_BrushPickingShader) { hideFlags = HideFlags.HideAndDontSave };
			s_ScenePickingPassIndex = s_BrushPickingMaterial.FindPass("ScenePickingPass");
			s_PickingOffsetPropertyID = Shader.PropertyToID("_Offset");
		}

		public static bool SetScenePickingPass(int offset)
		{
			Initialize();
			s_BrushPickingMaterial.SetInteger(s_PickingOffsetPropertyID, offset);
			return s_BrushPickingMaterial.SetPass(s_ScenePickingPassIndex);
		}
	}
}
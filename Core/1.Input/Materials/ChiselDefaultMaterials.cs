using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Core
{
	public sealed class ChiselDefaultMaterials : ScriptableObject
	{
		#region Instance
		static ChiselDefaultMaterials _instance;
		public static ChiselDefaultMaterials Instance
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				if (_instance)
					return _instance;

				_instance = ScriptableObject.CreateInstance<ChiselDefaultMaterials>();
				_instance.hideFlags = HideFlags.HideAndDontSave;
				return _instance;
			}
		}
		#endregion

		// NOTE: We use the default values that are set in the inspector when selecting this .cs file within unity 
		[SerializeField] private ChiselPipelineMaterialsSetObject builtin;
		[SerializeField] private ChiselPipelineMaterialsSetObject HDRP;
		[SerializeField] private ChiselPipelineMaterialsSetObject URP;

		public static ChiselPipelineMaterialsSet Defaults
		{
			get
			{
#if USING_URP
				return Instance.URP.Set;
#elif USING_HDRP
				return Instance.HDRP.Set;
#else
				return Instance.builtin.Set;
#endif
			}
		}
	}
}

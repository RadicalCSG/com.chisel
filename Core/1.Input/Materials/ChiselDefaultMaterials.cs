using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chisel.Core
{
#if UNITY_6000_7_OR_NEWER
	[Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
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

		[SerializeField] private ChiselPipelineMaterialsSetObject URP;

		public static ChiselPipelineMaterialsSet Defaults
		{
			get
			{ 
				return Instance.URP.Set;
			}
		}
	}
}

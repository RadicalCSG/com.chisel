using System;

using UnityEngine;

namespace Chisel.Core
{
#if UNITY_EDITOR
	[Serializable]
	[UnityEditor.FilePath(kSettingsFilePath, UnityEditor.FilePathAttribute.Location.ProjectFolder)]
#endif
	public sealed class ChiselProjectSettings
#if UNITY_EDITOR
		: UnityEditor.ScriptableSingleton<ChiselProjectSettings>
#else
		: ScriptableObject
#endif
	{
		public const string kMaterialsName = nameof(materials);

		[SerializeField] private ChiselPipelineMaterialsSet materials;
		public static ChiselPipelineMaterialsSet Materials
		{
			get
			{
				instance.materials ??= ChiselDefaultMaterials.Defaults;
				return instance.materials;
			}
			set { instance.materials = value; }
		}


		public static Material DefaultMaterial                 { get { return DefaultWallMaterial; } }

		public static Material DefaultFloorMaterial		       { get { return Materials.defaultFloorMaterial; } set { Materials.defaultFloorMaterial = value; } }
        public static Material DefaultStepMaterial		       { get { return Materials.defaultStepMaterial; } set { Materials.defaultStepMaterial = value; } }
		public static Material DefaultTreadMaterial		       { get { return Materials.defaultTreadMaterial; } set { Materials.defaultTreadMaterial = value; } }
		public static Material DefaultWallMaterial		       { get { return Materials.defaultWallMaterial; } set { Materials.defaultWallMaterial = value; } }
		public static PhysicsMaterial DefaultPhysicsMaterial   { get { return Materials.defaultPhysicsMaterial; } set { Materials.defaultPhysicsMaterial = value; } }

		public static Material UserHiddenSurfacesMaterial      { get { return Materials.userHiddenSurfacesMaterial; } set { Materials.userHiddenSurfacesMaterial = value; } }
		public static Material DiscardedSurfacesMaterial	   { get { return Materials.discardedSurfacesMaterial; } set { Materials.discardedSurfacesMaterial = value; } }
		public static Material ShadowCastingSurfacesMaterial   { get { return Materials.shadowCastingSurfacesMaterial; } set { Materials.shadowCastingSurfacesMaterial = value; } }
		public static Material ShadowOnlySurfacesMaterial      { get { return Materials.shadowOnlySurfacesMaterial; } set { Materials.shadowOnlySurfacesMaterial = value; } }
		public static Material ShadowReceivingSurfacesMaterial { get { return Materials.shadowReceivingSurfacesMaterial; } set { Materials.shadowReceivingSurfacesMaterial = value; } }
		public static Material CollisionSurfacesMaterial       { get { return Materials.collisionSurfacesMaterial; } set { Materials.collisionSurfacesMaterial = value; } }
		public static Material[] DebugVisualizationMaterials   { get { return Materials.DebugVisualizationMaterials; } }

		public void ResetMaterials() 
		{ 
			materials = ChiselDefaultMaterials.Defaults;
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(this);
#endif
		}

#if UNITY_EDITOR
		public const string kSettingsFilePath = "ProjectSettings/Packages/com.chisel/ChiselSettings.asset";
		public void Reset() { ResetMaterials(); }
		public void OnValidate() { if (materials == null) { Reset(); } }
		public void Save() { Save(true); }
#else
		private static ChiselProjectSettings s_Instance;

		public static ChiselProjectSettings instance
		{
			get
			{
				if ((UnityEngine.Object)s_Instance == (UnityEngine.Object)null)
				{
					s_Instance = ScriptableObject.CreateInstance<ChiselProjectSettings>();
					s_Instance.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
					s_Instance.materials ??= ChiselDefaultMaterials.Defaults;
				}

				return s_Instance;
			}
		}
#endif
	}
}

using System;
using UnityEngine;

namespace Chisel.Core
{
	//[CreateAssetMenu(fileName = "ChiselMaterialSet.asset", menuName = "ScriptableObjects/ChiselMaterialSet", order = 1)]
	public class ChiselPipelineMaterialsSetObject : ScriptableObject
	{
		public ChiselPipelineMaterialsSet Set;
	}

	[Serializable]
	public class ChiselPipelineMaterialsSet
	{
		public const string kDefaultFloorMaterialName	= nameof(defaultFloorMaterial);
		public const string kDefaultStepMaterialName	= nameof(defaultStepMaterial);
		public const string kDefaultTreadMaterialName	= nameof(defaultTreadMaterial);
		public const string kDefaultWallMaterialName	= nameof(defaultWallMaterial);
		public const string kDefaultPhysicMaterialName	= nameof(defaultPhysicsMaterial);

		public const string kUserHiddenSurfacesMaterialName		 = nameof(userHiddenSurfacesMaterial);
		public const string kShadowCastingSurfacesMaterialName	 = nameof(shadowCastingSurfacesMaterial);
		public const string kShadowOnlySurfacesMaterialName		 = nameof(shadowOnlySurfacesMaterial);
		public const string kShadowReceivingSurfacesMaterialName = nameof(shadowReceivingSurfacesMaterial);
		public const string kCollisionSurfacesMaterialName		 = nameof(collisionSurfacesMaterial);
		public const string kDiscardedSurfacesMaterialName		 = nameof(discardedSurfacesMaterial);

		public PhysicsMaterial defaultPhysicsMaterial;

		public Material defaultFloorMaterial;
		public Material defaultStepMaterial;
		public Material defaultTreadMaterial;
		public Material defaultWallMaterial;

		public Material collisionSurfacesMaterial;
		public Material shadowCastingSurfacesMaterial;
		public Material shadowOnlySurfacesMaterial;
		public Material shadowReceivingSurfacesMaterial;
		public Material discardedSurfacesMaterial;
		public Material userHiddenSurfacesMaterial;
				

		readonly Material[] debugVisualizationMaterials = new Material[6];
		public Material[] DebugVisualizationMaterials
		{
			get
			{
				// See ChiselGeneratedObjects.cs
				// Uses same indices as kGeneratedVisualizationRendererNames / kGeneratedVisualizationShowFlags

				// SurfaceDestinationFlags.Collidable / DrawModeFlags.ShowColliders
				debugVisualizationMaterials[4] = collisionSurfacesMaterial;

				// SurfaceDestinationFlags.RenderShadowReceiveAndCasting / DrawModeFlags.ShowShadowCasters
				debugVisualizationMaterials[1] = shadowCastingSurfacesMaterial;

				// SurfaceDestinationFlags.ShadowCasting / DrawModeFlags.ShowShadowOnly
				debugVisualizationMaterials[2] = shadowOnlySurfacesMaterial;

				// SurfaceDestinationFlags.RenderShadowsReceiving / DrawModeFlags.ShowShadowReceivers
				debugVisualizationMaterials[3] = shadowReceivingSurfacesMaterial;

				// SurfaceDestinationFlags.Discarded / DrawModeFlags.ShowDiscarded
				debugVisualizationMaterials[5] = discardedSurfacesMaterial;

				// SurfaceDestinationFlags.None / DrawModeFlags.ShowUserHidden
				debugVisualizationMaterials[0] = userHiddenSurfacesMaterial;

				return debugVisualizationMaterials;
			}
		}
	}
}

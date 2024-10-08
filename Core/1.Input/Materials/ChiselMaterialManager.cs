using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Chisel.Core
{
    public static class ChiselMaterialManager
    {
        readonly static Dictionary<int, Material> idToMaterial = new();
        readonly static Dictionary<Material, int> materialToID = new();
        readonly static Dictionary<int, PhysicsMaterial> idToPhysicMaterial = new();
        readonly static Dictionary<PhysicsMaterial, int> physicMaterialToID = new();

#if UNITY_EDITOR
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		static int ToID(Material material)
        {
            var objectID = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(material);
            return objectID.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		static int ToID(PhysicsMaterial physicMaterial)
        {
            var objectID = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(physicMaterial);
            return objectID.GetHashCode();
        }
#else
        // TODO: Implement a runtime solution
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int ToID(Material material)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
       static  int ToID(PhysicMaterial physicMaterial)
        {
            throw new NotImplementedException();
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetID(Material material)
        {
            if (!material)
                return 0;
            if (!materialToID.TryGetValue(material, out var id))
            {
                id = ToID(material);
                materialToID[material] = id;
                idToMaterial[id] = material;
            }
            return id;
        }

        public static Material GetMaterial(int id)
        {
            if (idToMaterial.TryGetValue(id, out var material))
            {
                if (material)
                    return material;
                idToMaterial.Remove(id);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetID(PhysicsMaterial physicMaterial)
        {
            if (!physicMaterial)
            {
                physicMaterial = ChiselProjectSettings.DefaultPhysicsMaterial;
				if (physicMaterial != null)
                    return GetID(physicMaterial);
				return 0;
            }
            if (!physicMaterialToID.TryGetValue(physicMaterial, out var id))
            {
                id = ToID(physicMaterial);
                physicMaterialToID[physicMaterial] = id;
                idToPhysicMaterial[id] = physicMaterial;
            }
            return id;
        }

        public static PhysicsMaterial GetPhysicMaterial(int id)
        {
            if (idToPhysicMaterial.TryGetValue(id, out var physicMaterial))
            {
                if (physicMaterial)
                    return physicMaterial;
                idToPhysicMaterial.Remove(id);
            }
            return null;
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
	[GenerateTestsForBurstCompatibility]
	public struct BrushWireframeManager : IDisposable
    {
        [NoAlias] NativeParallelHashMap<CompactNodeID, BlobAssetReference<NativeWireframeBlob>> outlineLookup;
		
		public readonly bool IsCreated
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return outlineLookup.IsCreated; }
		}

		public static BrushWireframeManager Create(Allocator allocator)
		{
            return new BrushWireframeManager()
            {
				outlineLookup = new NativeParallelHashMap<CompactNodeID, BlobAssetReference<NativeWireframeBlob>>(1024, allocator)
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetWireframe(CompactNodeID compactNodeID, BlobAssetReference<NativeWireframeBlob> nativeOutline)
		{
			Debug.Assert(IsCreated);
			if (outlineLookup.TryGetValue(compactNodeID, out var oldOutline))
			{
				oldOutline.Dispose();
				outlineLookup.Remove(compactNodeID);
			}
			outlineLookup[compactNodeID] = nativeOutline;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public BlobAssetReference<NativeWireframeBlob> GetOutline(CompactNodeID compactNodeID)
		{
			Debug.Assert(IsCreated);
			if (outlineLookup.TryGetValue(compactNodeID, out var nativeOutline))
				return nativeOutline;
			return BlobAssetReference<NativeWireframeBlob>.Null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void FreeOutline(CompactNodeID compactNodeID) 
        {
			Debug.Assert(IsCreated);
			if (!outlineLookup.TryGetValue(compactNodeID, out var nativeOutline))
				return;
			nativeOutline.Dispose();
			outlineLookup.Remove(compactNodeID);
		}

		public void Dispose()
		{
			// Confirmed to get disposed
			if (!outlineLookup.IsCreated)
				return;

			try
			{
				var values = outlineLookup.GetValueArray(Allocator.Temp);
				try
				{
					foreach (var value in values)
						value.Dispose();
				}
				finally
				{
					values.Dispose();
				}
			}
			finally
			{ 
				outlineLookup.Dispose();
				outlineLookup = default;
			}
		}
	}
}

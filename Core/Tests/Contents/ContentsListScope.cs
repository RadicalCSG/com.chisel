using System;

namespace Chisel.Core.Tests
{
	public sealed class ContentsListScope : IDisposable
	{
		// The list the generators draw their types from
		public const int kGlass = 1, kGrate = 2, kWater = 3;
		public static readonly string[] kDefaultNames = { ChiselContentsList.kSolidName, "Glass", "Grate", "Water" };

		readonly ChiselContentsList previous;
		ChiselContentsList installed;

		public ContentsListScope() : this(kDefaultNames) { }

		public ContentsListScope(params string[] names)
		{
			previous = ChiselContentsList.Instance;
			Install(names);
		}

		public void Install(params string[] names)
		{
			var old = installed;
			installed = ChiselContentsList.Create(names);
			ChiselContentsList.Instance = installed;
			CompactHierarchyManager.ApplyContentsList();
			if (old != null)
				UnityEngine.Object.DestroyImmediate(old);
		}

		public void Dispose()
		{
			ChiselContentsList.Instance = previous;
			CompactHierarchyManager.ApplyContentsList();
			if (installed != null)
				UnityEngine.Object.DestroyImmediate(installed);
			installed = null;
		}
	}
}

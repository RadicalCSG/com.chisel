using NUnit.Framework;

namespace Chisel.Core.Tests
{
	// The contents list itself: how entries are found, which is how an importer maps its own types onto the
	// project's list.
	[TestFixture]
	[Category("Contents")]
	public class ContentsListTests
	{
		[Test]
		public void IndexOf_FindsAnEntryByName_IgnoringCaseAndSpaces()
		{
			var list = ChiselContentsList.Create(ChiselContentsList.kSolidName, "Glass", "Water");
			try
			{
				Assert.That(list.IndexOf("Solid"),     Is.EqualTo(ChiselContentsList.kSolidIndex));
				Assert.That(list.IndexOf("glass"),     Is.EqualTo(1));
				Assert.That(list.IndexOf("  Water "),  Is.EqualTo(2));
				Assert.That(list.IndexOf("Grate"),     Is.EqualTo(-1));
				Assert.That(list.IndexOf(""),          Is.EqualTo(-1));
				Assert.That(list.IndexOf(null),        Is.EqualTo(-1));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(list);
			}
		}

		// A list handed in (by a test, or by the settings page) is the one in force until it is replaced, and
		// putting the previous one back restores it
		[Test]
		public void AnAssignedList_IsTheOneInForce()
		{
			var previous = ChiselContentsList.Instance;
			var list = ChiselContentsList.Create(ChiselContentsList.kSolidName, "Glass");
			try
			{
				ChiselContentsList.Instance = list;
				Assert.That(ChiselContentsList.Instance, Is.SameAs(list));
				Assert.That(ChiselContentsList.Instance.IsDefault, Is.False);
			}
			finally
			{
				ChiselContentsList.Instance = previous;
				UnityEngine.Object.DestroyImmediate(list);
			}
			Assert.That(ChiselContentsList.Instance, Is.SameAs(previous));
		}
	}
}

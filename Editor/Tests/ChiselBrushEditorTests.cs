using NUnit.Framework;

namespace Chisel.Editors.Tests
{
    // The brush editor commits the changes its handles make once the operation is over. Committing replaces the editable
    // outlines, so a commit while a handle holds the mouse would take away what its drag works from: a rotate or scale
    // handle then drew at the brush's origin and its drag did nothing, after any change marked the brush modified.
    public sealed class ChiselBrushEditorTests
    {
        const int kHandle = 1234;

        // a handle takes the mouse in the event its drag starts, which can already be a change
        [Test]
        public void AChangeWhileAHandleHoldsTheMouse_IsntCommitted()
        {
            Assert.That(ChiselBrushEditor.ShouldCommit(modified: true, hotControl: kHandle), Is.False);
        }

        [Test]
        public void AChange_IsCommittedOnceNoHandleHoldsTheMouse()
        {
            Assert.That(ChiselBrushEditor.ShouldCommit(modified: true, hotControl: 0), Is.True);
        }

        [Test]
        public void WithoutAChange_NothingIsCommitted()
        {
            Assert.That(ChiselBrushEditor.ShouldCommit(modified: false, hotControl: 0), Is.False);
        }
    }
}

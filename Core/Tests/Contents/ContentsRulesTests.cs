using NUnit.Framework;

namespace Chisel.Core.Tests
{
    // The contents rules layer, exhaustively: every category, every ordered pair of the 32 types, and both
    // carving flags, against references written straight from the design's two rules and its tie order.
    [TestFixture]
    [Category("Contents")]
    public class ContentsRulesTests
    {
        const int kTypes = ChiselContentsList.kMaxEntries;
        const int kSolid = ChiselContentsList.kSolidIndex;

        // Is a face of type a removed where it lies inside a brush of type b? Only its own type and Solid
        // remove it.
        static bool FaceRemovedInside(int a, int b) => a == b || b == kSolid;

        // Does b's face win a coplanar tie against a's? Solid first, then the order of the list.
        static bool TieGoesTo(int b, int a)
        {
            if (a == b)      return false;
            if (b == kSolid) return true;
            if (a == kSolid) return false;
            return b < a;
        }

        static CategoryIndex ExpectedRewrite(CategoryIndex computed, int a, bool carvingA, int b, bool carvingB, bool intersectingB, bool bIsLater)
        {
            if (a == b)
                return computed;
            var bCarvesA = carvingB && !carvingA && bIsLater;
            var aCarvesB = carvingA && !carvingB && !bIsLater;
            switch (computed)
            {
                case CategoryIndex.Inside:
                    return (carvingB || b == kSolid) ? computed : CategoryIndex.Outside;
                case CategoryIndex.ReverseAligned:
                    if (carvingB)
                        return (bCarvesA && !intersectingB) ? CategoryIndex.Outside : computed;
                    return (!aCarvesB && b == kSolid) ? computed : CategoryIndex.Outside;
                case CategoryIndex.Aligned:
                    if (carvingB)
                        return bCarvesA ? CategoryIndex.Inside : computed;
                    return (!aCarvesB && TieGoesTo(b, a)) ? CategoryIndex.Inside : CategoryIndex.Outside;
                default:
                    return computed;
            }
        }

        [Test]
        public void Rewrite_FollowsTheRules_ForEveryPairOfTypes([Values] bool carvingA, [Values] bool carvingB, [Values] bool intersectingB, [Values] bool bIsLater)
        {
            for (int category = 0; category <= (int)CategoryIndex.LastCategory; category++)
            {
                var computed = (CategoryIndex)category;
                for (int a = 0; a < kTypes; a++)
                {
                    for (int b = 0; b < kTypes; b++)
                    {
                        var expected = ExpectedRewrite(computed, a, carvingA, b, carvingB, intersectingB, bIsLater);
                        var actual   = ContentsRules.Rewrite(computed, (byte)a, carvingA, (byte)b, carvingB, intersectingB, bIsLater);
                        if (actual != expected)
                            Assert.Fail($"{computed} of type {a}{(carvingA ? " (carving)" : "")} against type {b}{(carvingB ? (intersectingB ? " (intersecting)" : " (carving)") : "")}{(bIsLater ? " after it" : " before it")}: {actual}, expected {expected}");
                    }
                }
            }
        }

        [Test]
        public void MatchingTypes_ChangeNothing([Values] bool carvingA, [Values] bool carvingB, [Values] bool intersectingB, [Values] bool bIsLater)
        {
            for (int category = 0; category <= (int)CategoryIndex.LastCategory; category++)
            {
                for (int t = 0; t < kTypes; t++)
                {
                    Assert.That(ContentsRules.Rewrite((CategoryIndex)category, (byte)t, carvingA, (byte)t, carvingB, intersectingB, bIsLater),
                                Is.EqualTo((CategoryIndex)category));
                }
            }
        }

        // Two coplanar faces of different types facing the same way: exactly one is drawn
        [Test]
        public void AlignedFacesOfDifferentTypes_KeepExactlyOne()
        {
            for (int a = 0; a < kTypes; a++)
            {
                for (int b = 0; b < kTypes; b++)
                {
                    if (a == b)
                        continue;
                    var keptA = ContentsRules.Rewrite(CategoryIndex.Aligned, (byte)a, false, (byte)b, carvingB: false, intersectingB: false, bIsLater: true)  == CategoryIndex.Outside;
                    var keptB = ContentsRules.Rewrite(CategoryIndex.Aligned, (byte)b, false, (byte)a, carvingB: false, intersectingB: false, bIsLater: false) == CategoryIndex.Outside;
                    Assert.That(keptA != keptB, Is.True, $"types {a} and {b}");
                }
            }
        }

        [Test]
        public void CarvingBrush_LeavesFacesOnATypeItCarvesToThatType()
        {
            for (int a = 0; a < kTypes; a++)
            {
                for (int b = 0; b < kTypes; b++)
                {
                    if (a == b)
                        continue;
                    // a carves b, which comes first
                    Assert.That(ContentsRules.Rewrite(CategoryIndex.Aligned,        (byte)a, carvingA: true,  (byte)b, carvingB: false, intersectingB: false, bIsLater: false), Is.EqualTo(CategoryIndex.Outside), $"carving type {a} on type {b}");
                    Assert.That(ContentsRules.Rewrite(CategoryIndex.ReverseAligned, (byte)a, carvingA: true,  (byte)b, carvingB: false, intersectingB: false, bIsLater: false), Is.EqualTo(CategoryIndex.Outside), $"carving type {a} against type {b}");
                    Assert.That(ContentsRules.Rewrite(CategoryIndex.Aligned,        (byte)b, carvingA: false, (byte)a, carvingB: true,  intersectingB: false, bIsLater: true),  Is.EqualTo(CategoryIndex.Inside),  $"type {b} on carving type {a}");
                    Assert.That(ContentsRules.Rewrite(CategoryIndex.ReverseAligned, (byte)b, carvingA: false, (byte)a, carvingB: true,  intersectingB: false, bIsLater: true),  Is.EqualTo(CategoryIndex.Outside), $"type {b} against subtracting type {a}");
                    Assert.That(ContentsRules.Rewrite(CategoryIndex.ReverseAligned, (byte)b, carvingA: false, (byte)a, carvingB: true,  intersectingB: true,  bIsLater: true),  Is.EqualTo(CategoryIndex.ReverseAligned), $"type {b} against intersecting type {a}");
                    // b comes after the carve: nothing of it is carved
                    Assert.That(ContentsRules.Rewrite(CategoryIndex.Aligned,        (byte)b, carvingA: false, (byte)a, carvingB: true,  intersectingB: false, bIsLater: false), Is.EqualTo(CategoryIndex.Aligned),        $"type {b} after carving type {a}");
                    Assert.That(ContentsRules.Rewrite(CategoryIndex.ReverseAligned, (byte)b, carvingA: false, (byte)a, carvingB: true,  intersectingB: false, bIsLater: false), Is.EqualTo(CategoryIndex.ReverseAligned), $"type {b} after carving type {a}, back to back");
                }
            }
        }

        [Test]
        public void Ties_AreDecidedOnce_AndSolidWinsEvery()
        {
            for (int a = 0; a < kTypes; a++)
            {
                Assert.That(ContentsRules.WinsTie((byte)a, (byte)a), Is.False, $"type {a} against itself");
                if (a != kSolid)
                {
                    Assert.That(ContentsRules.WinsTie(ContentsRules.kSolid, (byte)a), Is.True,  $"Solid against {a}");
                    Assert.That(ContentsRules.WinsTie((byte)a, ContentsRules.kSolid), Is.False, $"{a} against Solid");
                }
                for (int b = 0; b < kTypes; b++)
                {
                    if (a == b)
                        continue;
                    Assert.That(ContentsRules.WinsTie((byte)a, (byte)b) != ContentsRules.WinsTie((byte)b, (byte)a), Is.True, $"types {a} and {b}");
                    Assert.That(ContentsRules.WinsTie((byte)b, (byte)a), Is.EqualTo(TieGoesTo(b, a)), $"types {b} against {a}");
                }
            }
        }

        [Test]
        public void RemovesWhenInside_FollowsTheTwoRules([Values] bool carvingB)
        {
            for (int a = 0; a < kTypes; a++)
            {
                for (int b = 0; b < kTypes; b++)
                {
                    Assert.That(ContentsRules.RemovesWhenInside((byte)a, (byte)b, carvingB),
                                Is.EqualTo(carvingB || FaceRemovedInside(a, b)),
                                $"type {a} inside type {b}{(carvingB ? " (carving)" : "")}");
                }
            }
        }

        // An index past the end of the list builds as Solid
        [Test]
        public void Resolve_BuildsAnIndexOutsideTheListAsSolid()
        {
            Assert.That(ContentsRules.Resolve(0, 1), Is.EqualTo(0));
            Assert.That(ContentsRules.Resolve(3, 4), Is.EqualTo(3));
            Assert.That(ContentsRules.Resolve(4, 4), Is.EqualTo(0), "one past the end");
            Assert.That(ContentsRules.Resolve(1, 1), Is.EqualTo(0), "a list with only Solid");
            Assert.That(ContentsRules.Resolve(-1, 4), Is.EqualTo(0), "negative");
            Assert.That(ContentsRules.Resolve(kTypes, kTypes + 8), Is.EqualTo(0), "past the largest list there can be");
            Assert.That(ContentsRules.Resolve(kTypes - 1, kTypes), Is.EqualTo(kTypes - 1));
        }
    }
}

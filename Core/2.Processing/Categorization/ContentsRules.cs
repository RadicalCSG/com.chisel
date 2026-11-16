using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    static class ContentsRules
    {
        public const byte kSolid = (byte)ChiselContentsList.kSolidIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CategoryIndex Rewrite(CategoryIndex category, byte contentsA, bool carvingA, byte contentsB, bool carvingB, bool intersectingB, bool bIsLater)
        {
            if (contentsA == contentsB)
                return category;

            if (carvingB)
            {
                if (carvingA || !bIsLater)
                    return category;
                switch (category)
                {
                    case CategoryIndex.Aligned:        return CategoryIndex.Inside;
                    case CategoryIndex.ReverseAligned: return intersectingB ? category : CategoryIndex.Outside;
                    default:                           return category;
                }
            }

            var aCarvesB = carvingA && !bIsLater;
            switch (category)
            {
                // Inside another type: only Solid removes the piece
                case CategoryIndex.Inside:
                    return contentsB == kSolid ? category : CategoryIndex.Outside;

                // Against another type, back to back: only Solid removes the piece. A carving brush leaves no
                // wall on a brush it carves; that brush keeps its own face.
                case CategoryIndex.ReverseAligned:
                    if (aCarvesB)
                        return CategoryIndex.Outside;
                    return contentsB == kSolid ? category : CategoryIndex.Outside;

                // Coplanar and facing the same way: the pair alone does not decide which face survives, as the
                // winner's face may not be drawn there (GivesWay); each face is built as if the other were not there.
                case CategoryIndex.Aligned:
                    return CategoryIndex.Outside;

                default:
                    return category;
            }
        }

        // Whether A's face gives way to a coplanar face of B, facing the same way, where B's face is drawn: when B's type
        // wins the tie, whichever brush comes later, so another type never removes a face of Solid. A carving brush
        // leaves it to the brush it carves, and a carve's face is decided by Rewrite.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GivesWay(byte contentsA, bool carvingA, byte contentsB, bool carvingB, bool bIsLater)
        {
            if (contentsA == contentsB || carvingB)
                return false;
            if (carvingA && !bIsLater)
                return false;
            return WinsTie(contentsB, contentsA);
        }

        // Whether brush B still removes the faces of a brush A that lies entirely inside it, which is what
        // routes A as AllInside rather than AllOutside.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemovesWhenInside(byte contentsA, byte contentsB, bool carvingB)
        {
            return contentsA == contentsB || carvingB || contentsB == kSolid;
        }

        // Which of two types draws a coplanar face both have: Solid always wins, and between two other types the
        // earlier entry in the list does. Equal types don't win against each other.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WinsTie(byte contents, byte against)
        {
            if (contents == against) return false;
            if (contents == kSolid)  return true;
            if (against  == kSolid)  return false;
            return contents < against;
        }

        // The type a brush builds as: an index past the end of the list, or out of range, builds as Solid
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Resolve(int contents, int listCount)
        {
            if (contents <= kSolid || contents >= listCount || contents >= ChiselContentsList.kMaxEntries)
                return kSolid;
            return (byte)contents;
        }
    }
}

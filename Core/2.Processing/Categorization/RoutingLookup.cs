using System.Runtime.CompilerServices;
using Unity.Burst;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    struct RoutingLookup
    {
        public int startIndex;
        public int endIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryGetRoute([NoAlias, ReadOnly] ref RoutingTable table, byte inputIndex, out CategoryRoutingRow routingRow)
        {
            var tableIndex = startIndex + (int)inputIndex;
            if (tableIndex < startIndex || tableIndex >= endIndex)
            {
                routingRow = new CategoryRoutingRow(inputIndex);
                return false;
            }

            routingRow = table.routingRows[tableIndex];
            return true;
        }
    }
}

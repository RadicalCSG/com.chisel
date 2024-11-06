using Unity.Jobs;
using Unity.Collections;

namespace Chisel.Core
{
    public delegate int FinishMeshUpdate(CSGTree tree, ChiselMeshUpdates meshUpdates, JobHandle dependencies);
}

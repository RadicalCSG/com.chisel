using System.Reflection;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace Chisel.Core
{
    // Sizes and offsets of the structs every brush blob is made of. Native code compiled for
    // another layout than the managed one writes blobs that managed code misreads.
    public struct BrushMeshBlobLayout
    {
        public int polygonSize;
        public int polygonFirstEdge;
        public int polygonEdgeCount;
        public int polygonDescriptionIndex;
        public int polygonSurface;

        public int surfaceSize;
        public int surfaceDetails;
        public int surfaceParameters;
        public int surfaceDestinationFlags;
        public int surfaceOutputFlags;
        public int surfaceHotspotLayout;

        public int detailsSize;

        public static unsafe BrushMeshBlobLayout Measure()
        {
            var polygon = default(BrushMeshBlob.Polygon);
            var p = (byte*)&polygon;
            var s = (byte*)&polygon.surface;
            return new BrushMeshBlobLayout
            {
                polygonSize             = UnsafeUtility.SizeOf<BrushMeshBlob.Polygon>(),
                polygonFirstEdge        = (int)((byte*)&polygon.firstEdge - p),
                polygonEdgeCount        = (int)((byte*)&polygon.edgeCount - p),
                polygonDescriptionIndex = (int)((byte*)&polygon.descriptionIndex - p),
                polygonSurface          = (int)(s - p),

                surfaceSize             = UnsafeUtility.SizeOf<InternalChiselSurface>(),
                surfaceDetails          = (int)((byte*)&polygon.surface.details - s),
                surfaceParameters       = (int)((byte*)&polygon.surface.parameters - s),
                surfaceDestinationFlags = (int)((byte*)&polygon.surface.destinationFlags - s),
                surfaceOutputFlags      = (int)((byte*)&polygon.surface.outputFlags - s),
                surfaceHotspotLayout    = (int)((byte*)&polygon.surface.hotspotLayout - s),

                detailsSize             = UnsafeUtility.SizeOf<SurfaceDetails>()
            };
        }

        [BurstCompile(CompileSynchronously = true)]
        struct MeasureJob : IJob
        {
            [WriteOnly] public NativeReference<BrushMeshBlobLayout> layout;
            public void Execute() { layout.Value = Measure(); }
        }

        // Measured by the job system: Burst-compiled unless Burst is off
        public static BrushMeshBlobLayout MeasureCompiled()
        {
            using var layout = new NativeReference<BrushMeshBlobLayout>(Allocator.TempJob);
            new MeasureJob { layout = layout }.Run();
            return layout.Value;
        }

        // The fields that differ, as "name managed/compiled"; empty when none do
        public static string Differences(in BrushMeshBlobLayout managed, in BrushMeshBlobLayout compiled)
        {
            var text = new StringBuilder();
            object a = managed, b = compiled;
            foreach (var field in typeof(BrushMeshBlobLayout).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var x = (int)field.GetValue(a);
                var y = (int)field.GetValue(b);
                if (x != y)
                    text.Append(text.Length > 0 ? ", " : "").Append(field.Name).Append(' ').Append(x).Append('/').Append(y);
            }
            return text.ToString();
        }

        // Logs an error when the layouts differ
        internal static bool Check(in BrushMeshBlobLayout managed, in BrushMeshBlobLayout compiled)
        {
            var differences = Differences(managed, compiled);
            if (differences.Length == 0)
                return true;
            Debug.LogError("Chisel: compiled code has another brush blob layout than managed code (managed/compiled: " + differences +
                           "); every brush blob it writes is misread");
            return false;
        }

        static bool s_Checked;

        // Once per domain, before the first brush registration
        public static bool CheckOnce()
        {
            if (s_Checked)
                return true;
            s_Checked = true;
            return Check(Measure(), MeasureCompiled());
        }
    }
}

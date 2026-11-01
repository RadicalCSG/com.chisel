using System.Collections.Generic;
using Chisel.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Components
{
    /// <summary>
    /// Transparent decals that fade with distance (<see cref="ChiselDecalSettings.fadeDistance"/>). Each one is a mesh
    /// of its own (<see cref="ChiselDecalMeshes.FadeChunkOf"/>), and its renderer is cross-faded by a LODGroup: whole
    /// from the camera up to the start distance, gone past the end distance. A LODGroup goes by the size the renderer
    /// takes up on screen, so the distances hold at <see cref="kFieldOfView"/> and scale with the camera's field of view.
    /// The material's shader has to support LOD cross-fading, as URP's Lit does, with LOD Cross Fade on in the pipeline asset.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class ChiselDecalFades
    {
        public const float kFieldOfView = 60.0f;

        // the fade distances of the fading decals, by the chunk of their mesh
        static readonly Dictionary<uint, float2> s_Distances = new();

        internal static void Register(in ChiselDecalInstance decal)
        {
            if (decal.settings.Fades)
                s_Distances[ChiselDecalMeshes.FadeChunkOf(decal.entityID)] = decal.settings.fadeDistance;
        }

        public static bool TryGetDistances(uint chunk, out float2 distances) => s_Distances.TryGetValue(chunk, out distances);

        /// <summary>The height on screen, relative to the screen's, at which something this big this far away is</summary>
        public static float ScreenHeightAt(float size, float distance)
        {
            return size / (2.0f * math.max(distance, 1e-4f) * math.tan(math.radians(kFieldOfView) * 0.5f));
        }

        internal static void Apply(ChiselRenderObjects renderable)
        {
            if (renderable == null || renderable.surfaceParameter != ChiselDecalMeshes.kTransparentDecals ||
                !s_Distances.TryGetValue(renderable.chunk, out var distances) || !renderable.HasGeometry || !renderable.meshRenderer)
                return;

            var bounds   = renderable.sharedMesh.bounds;
            var scale    = renderable.container.transform.lossyScale;
            var size     = math.max(math.cmax(bounds.size), 1e-4f);
            var world    = size * math.cmax(math.abs((float3)scale));
            var end      = math.max(distances.y, 1e-3f);
            var start    = math.clamp(distances.x, 0, end);
            var gone     = math.min(ScreenHeightAt(world, end), 0.999f);
            var whole    = math.min(ScreenHeightAt(world, start), 1.0f);
            // the cross-fade runs over this part of the way from the LOD's height up to the whole screen
            var width    = gone < 1 ? math.saturate((whole - gone) / (1 - gone)) : 0;

            var group = renderable.container.GetComponent<LODGroup>();
            if (group == null)
                group = renderable.container.AddComponent<LODGroup>();
            group.fadeMode            = LODFadeMode.CrossFade;
            group.animateCrossFading  = false;
            group.localReferencePoint = bounds.center;
            group.size                = size;
            group.SetLODs(new[] { new LOD(gone, new Renderer[] { renderable.meshRenderer }) { fadeTransitionWidth = width } });
        }
    }
}

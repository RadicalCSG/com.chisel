using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // A material whose main texture or shader is swapped, a texture imported at another size, or a shader changed: the
    // generators using a material whose texture scale (ChiselTextureScale) or lightmapping (ChiselMaterialLightmaps)
    // changed are built again
    [InitializeOnLoad]
    static class ChiselMaterialWatcher
    {
        static ChiselMaterialWatcher()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
            EditorApplication.projectChanged -= Refresh;
            EditorApplication.projectChanged += Refresh;
        }

        static void OnChangesPublished(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) == ObjectChangeKind.ChangeAssetObjectProperties)
                {
                    Refresh();
                    return;
                }
            }
        }

        internal static void Refresh()
        {
            var changed = new List<Material>();
            ChiselTextureScale.Refresh(changed);
            ChiselMaterialLightmaps.Refresh(changed);
            if (changed.Count == 0)
                return;
            var materials = new HashSet<Material>(changed);
            foreach (var generator in Object.FindObjectsByType<ChiselGeneratorComponent>(FindObjectsInactive.Include))
            {
                var surfaces = generator.SurfaceDefinition?.surfaces;
                if (surfaces == null)
                    continue;
                foreach (var surface in surfaces)
                {
                    if (surface != null && materials.Contains(surface.RenderMaterial))
                    {
                        generator.SetDirty();
                        break;
                    }
                }
            }
        }
    }
}

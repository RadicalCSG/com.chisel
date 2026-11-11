using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // A selected decal's gizmo, in the Decal Handles color: its volume, or for a polygon decal its outline where its
    // surface has it now. Only when selected: an imported map has thousands of decals. A trim decal's box means nothing:
    // its editor draws its outline instead
    static class ChiselDecalGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void DrawSelected(ChiselDecalComponent decal, GizmoType gizmoType)
        {
            if (decal.Shape == ChiselDecalShape.Box)
                DrawVolume(decal);
            else if (decal.Shape == ChiselDecalShape.Polygon)
                DrawPolygon(decal);
        }

        static void DrawPolygon(ChiselDecalComponent decal)
        {
            var corners = new List<Vector2>(decal.Polygon.Count);
            ChiselSurfaceShapes.Resolve(decal, out var frame, corners);
            Gizmos.color  = ChiselColors.DecalHandles.Value;
            Gizmos.matrix = frame;
            for (int i = 0; i < corners.Count; i++)
                Gizmos.DrawLine(corners[i], corners[(i + 1) % corners.Count]);
            Gizmos.matrix = Matrix4x4.identity;
        }

        static void DrawVolume(ChiselDecalComponent decal)
        {
            var settings = decal.Settings;
            Gizmos.color = ChiselColors.DecalHandles.Value;
            if (settings.projection == ChiselDecalProjection.Omni)
            {
                // how far it reaches; the image lies on each surface it reaches
                Gizmos.matrix = decal.transform.localToWorldMatrix;
                var reach = math.abs(settings.size.z);
                if (settings.reach == ChiselDecalReach.Cube)
                    Gizmos.DrawWireCube(settings.center, new Vector3(reach, reach, reach));
                else
                    Gizmos.DrawWireSphere(settings.center, reach * 0.5f);
                Gizmos.matrix = Matrix4x4.identity;
                return;
            }
            Gizmos.matrix = decal.transform.localToWorldMatrix * (Matrix4x4)ChiselDecalSettings.SkewMatrix(settings.skew);
            var corners = new Vector3[8];
            decal.GetCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                var next = (i + 1) % 4;
                Gizmos.DrawLine(corners[i], corners[next]);
                Gizmos.DrawLine(corners[4 + i], corners[4 + next]);
                Gizmos.DrawLine(corners[i], corners[4 + i]);
            }
            // Which way the image goes, and which way is up in it
            var center = (Vector3)settings.center;
            var depth  = math.abs(settings.size.z) * 0.5f;
            Gizmos.DrawLine(center, center + new Vector3(0, 0, depth));
            var height = math.abs(settings.size.y) * 0.5f;
            Gizmos.DrawLine(center, center + new Vector3(0, height * 0.5f, 0));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}

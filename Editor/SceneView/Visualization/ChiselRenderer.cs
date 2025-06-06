﻿using System.Collections.Generic;
using Chisel.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Editors
{
    internal static class GUIConstants
    {
        public const float kDefaultLineScale		= 1.0f;
        public const float kVisibleOuterLineDots	= 2.0f;
        public const float kVisibleInnerLineDots	= 1.0f;

        public const float kInvisibleOuterLineDots	= 2.0f;
        public const float kInvisibleInnerLineDots	= 1.0f;

        public const float kInvalidLineDots			= 2.0f;

        public const float kUnselectedFactor		= 0.65f;
        public const float kInnerFactor				= 0.70f;
        public const float kOccludedFactor			= 0.80f;
    }

    public sealed class ChiselRenderer
    {
		readonly LineMeshManager    zTestLinesManager	= new();
		readonly LineMeshManager    noZTestLinesManager	= new();
		readonly PointMeshManager   pointManager		= new();
		readonly PolygonMeshManager	polygonManager		= new();

        public void Destroy()
        {
            zTestLinesManager.Destroy();
            noZTestLinesManager.Destroy();
            pointManager.Destroy();
            polygonManager.Destroy();
        }

        public void DrawLine(Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLine(UnityEditor.Handles.matrix, from, to, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }

        public void DrawLine(Vector3 from, Vector3 to, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLine(UnityEditor.Handles.matrix, from, to, color, lineMode, thickness, dashSize);
        }

        public void DrawLine(Matrix4x4 transformation, Vector3 from, Vector3 to, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLine(transformation, from, to, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }

        public void DrawLine(Matrix4x4 transformation, Vector3 from, Vector3 to, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            switch (lineMode)
            {
                case LineMode.ZTest:   zTestLinesManager  .DrawLine(transformation, from, to, color, thickness, dashSize); break;
                case LineMode.NoZTest: noZTestLinesManager.DrawLine(transformation, from, to, color, thickness, dashSize); break;
            }
        }


        public void DrawLines(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLines(UnityEditor.Handles.matrix, points, startIndex, length, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }

        public void DrawLines(Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLines(UnityEditor.Handles.matrix, points, startIndex, length, color, lineMode, thickness, dashSize);
        }

        public void DrawLines(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLines(transformation, points, startIndex, length, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }

        public void DrawLines(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            switch (lineMode)
            {
                case LineMode.ZTest:	zTestLinesManager  .DrawLines(transformation, points, startIndex, length, color, thickness, dashSize); break;
                case LineMode.NoZTest:	noZTestLinesManager.DrawLines(transformation, points, startIndex, length, color, thickness, dashSize); break;
            }
        }



        public void DrawContinuousLines(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawContinuousLines(UnityEditor.Handles.matrix, points, startIndex, length, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawContinuousLines(UnityEditor.Handles.matrix, points, startIndex, length, color, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawContinuousLines(transformation, points, startIndex, length, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            switch (lineMode)
            {
                case LineMode.ZTest:   zTestLinesManager  .DrawContinuousLines(transformation, points, startIndex, length, color, thickness, dashSize); break;
                case LineMode.NoZTest: noZTestLinesManager.DrawContinuousLines(transformation, points, startIndex, length, color, thickness, dashSize); break;
            }
        }


        public void DrawContinuousLines(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawContinuousLines(UnityEditor.Handles.matrix, points, startIndex, length, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(float3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawContinuousLines(UnityEditor.Handles.matrix, points, startIndex, length, color, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(float4x4 transformation, float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawContinuousLines(transformation, points, startIndex, length, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }

        public void DrawContinuousLines(float4x4 transformation, float3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            switch (lineMode)
            {
                case LineMode.ZTest:    zTestLinesManager.DrawContinuousLines(transformation, points, startIndex, length, color, thickness, dashSize); break;
                case LineMode.NoZTest:  noZTestLinesManager.DrawContinuousLines(transformation, points, startIndex, length, color, thickness, dashSize); break;
            }
        }

        public void DrawPolygon(Matrix4x4 transformation, Vector3[] points, int[] indices, Color color)
        {
            polygonManager.DrawPolygon(transformation, points, indices, color);
        }

        public void DrawPolygon(Matrix4x4 transformation, Vector3[] points, Color color)
        {
            polygonManager.DrawPolygon(transformation, points, color);
        }

        public void DrawPolygon(Matrix4x4 transformation, List<Vector3> points, Color color)
        {
            polygonManager.DrawPolygon(transformation, points, color);
        }

        public void DrawPolygon(float4x4 transformation, float3[] points, int[] indices, Color color)
        {
            polygonManager.DrawPolygon(transformation, points, indices, color);
        }

        public void DrawPolygon(float4x4 transformation, float3[] points, Color color)
        {
            polygonManager.DrawPolygon(transformation, points, color);
        }

        public void DrawPolygon(float4x4 transformation, List<float3> points, Color color)
        {
            polygonManager.DrawPolygon(transformation, points, color);
        }


        public void DrawLineLoop(Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLineLoop(UnityEditor.Handles.matrix, points, startIndex, length, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }

        public void DrawLineLoop(List<Vector3> points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLineLoop(UnityEditor.Handles.matrix, points, startIndex, length, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }

        public void DrawLineLoop(Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLineLoop(UnityEditor.Handles.matrix, points, startIndex, length, color, lineMode, thickness, dashSize);
        }

        public void DrawLineLoop(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLineLoop(transformation, points, startIndex, length, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }


        public void DrawLineLoop(float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLineLoop(UnityEditor.Handles.matrix, points, startIndex, length, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }

        public void DrawLineLoop(float3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLineLoop(UnityEditor.Handles.matrix, points, startIndex, length, color, lineMode, thickness, dashSize);
        }

        public void DrawLineLoop(float4x4 transformation, float3[] points, int startIndex, int length, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            DrawLineLoop(transformation, points, startIndex, length, UnityEditor.Handles.color, lineMode, thickness, dashSize);
        }


        public void DrawLineLoop(Matrix4x4 transformation, List<Vector3> points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            switch (lineMode)
            {
                case LineMode.ZTest:    zTestLinesManager  .DrawLineLoop(transformation, points, startIndex, length, color, thickness, dashSize); break;
                case LineMode.NoZTest:  noZTestLinesManager.DrawLineLoop(transformation, points, startIndex, length, color, thickness, dashSize); break;
            }
        }

        public void DrawLineLoop(Matrix4x4 transformation, Vector3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            switch (lineMode)
            {
                case LineMode.ZTest:	zTestLinesManager  .DrawLineLoop(transformation, points, startIndex, length, color, thickness, dashSize); break;
                case LineMode.NoZTest:	noZTestLinesManager.DrawLineLoop(transformation, points, startIndex, length, color, thickness, dashSize); break;
            }
        }

        public void DrawLineLoop(Matrix4x4 transformation, float3[] points, int startIndex, int length, Color color, LineMode lineMode = LineMode.NoZTest, float thickness = 1.0f, float dashSize = 0.0f)
        {
            switch (lineMode)
            {
                case LineMode.ZTest:    zTestLinesManager.DrawLineLoop(transformation, points, startIndex, length, color, thickness, dashSize); break;
                case LineMode.NoZTest:  noZTestLinesManager.DrawLineLoop(transformation, points, startIndex, length, color, thickness, dashSize); break;
            }
        }


        public void DrawSimpleOutlines(Matrix4x4 transformation, ChiselWireframe wireframe, Color color)
        {
            if (wireframe == null ||
                wireframe.Vertices == null ||
                wireframe.Vertices.Length == 0 ||

                (wireframe.VisibleOuterLines == null //&&
                 //wireframe.InvisibleOuterLines == null &&
                 //wireframe.VisibleInnerLines == null &&
                 //wireframe.InvisibleInnerLines == null &&
                 //wireframe.InvalidLines == null
                 ))
                return;

            color.a *= 0.5f;

            var vertices = wireframe.Vertices;
            var indices  = wireframe.VisibleOuterLines;
            if (indices != null &&
                indices.Length > 0 &&
                (indices.Length & 1) == 0)
            {
                zTestLinesManager.DrawLines(transformation, vertices, indices, color, thickness: 0.5f);
            }
            
            if (indices != null &&
                indices.Length > 0 &&
                (indices.Length & 1) == 0)
            {
                noZTestLinesManager.DrawLines(transformation, vertices, indices, color, thickness: 0.5f, dashSize: GUIConstants.kInvisibleInnerLineDots);
            }
        }
        
        public void DrawOutlines(Matrix4x4 transformation, ChiselWireframe wireframe, Color wireframeColor, float thickness = -1, bool onlyInnerLines = false, bool showInnerLines = true)
        { 
            Color outerColor		 = wireframeColor;
            Color innerColor		 = outerColor;
            innerColor.a *= GUIConstants.kInnerFactor;

            Color outerOccludedColor = wireframeColor;// * GUIConstants.occludedFactor;
            Color innerOccludedColor = wireframeColor;// * GUIConstants.occludedFactor;
            
            outerOccludedColor.a *= 0.5f;
            innerOccludedColor.a *= 0.5f * GUIConstants.kInnerFactor;;

            DrawOutlines(transformation, wireframe, outerColor, outerOccludedColor, innerColor, innerOccludedColor, thickness, onlyInnerLines, showInnerLines);
        }
            

        public void DrawOutlines(Matrix4x4 transformation, ChiselWireframe wireframe, 
                                 Color outerColor, Color outerOccludedColor, 
                                 Color innerColor, Color innerOccludedColor, 
                                 float thickness = -1, bool onlyInnerLines = false, bool showInnerLines = true)
        {
            if (wireframe == null || 
                wireframe.Vertices == null ||
                wireframe.Vertices.Length == 0 ||

                (wireframe.VisibleOuterLines	== null //&&
                 //wireframe.InvisibleOuterLines	== null &&
                 //wireframe.VisibleInnerLines	== null &&
                 //wireframe.InvisibleInnerLines	== null &&
                 //wireframe.InvalidLines			== null
                 ))
                return;

            if (thickness <= 0)
                thickness = GUIConstants.kDefaultLineScale;

            if (!onlyInnerLines && wireframe.VisibleOuterLines != null && wireframe.VisibleOuterLines.Length > 0)
                zTestLinesManager.DrawLines(transformation, wireframe.Vertices, wireframe.VisibleOuterLines, outerColor, thickness: thickness);
            
            //if (showInnerLines && wireframe.VisibleInnerLines != null && wireframe.VisibleInnerLines.Length > 0)
            //    zTestLinesManager.DrawLines(transformation, wireframe.Vertices, wireframe.VisibleInnerLines, innerColor, thickness: thickness);
            
            //if (!onlyInnerLines && wireframe.InvisibleOuterLines != null && wireframe.InvisibleOuterLines.Length > 0)
            //    zTestLinesManager.DrawLines(transformation, wireframe.Vertices, wireframe.InvisibleOuterLines, outerOccludedColor, dashSize: GUIConstants.invisibleInnerLineDots, thickness: GUIConstants.defaultLineScale);
            
            if (!onlyInnerLines && wireframe.VisibleOuterLines != null && wireframe.VisibleOuterLines.Length > 0)
                noZTestLinesManager.DrawLines(transformation, wireframe.Vertices, wireframe.VisibleOuterLines, innerOccludedColor, thickness: thickness, dashSize: GUIConstants.kInvisibleInnerLineDots);

            //if (showInnerLines && wireframe.VisibleInnerLines != null && wireframe.VisibleInnerLines.Length > 0)
            //    noZTestLinesManager.DrawLines(transformation, wireframe.Vertices, wireframe.VisibleInnerLines, innerOccludedColor, thickness: thickness, dashSize: GUIConstants.invisibleInnerLineDots);
            
            //if (showInnerLines && !onlyInnerLines && wireframe.InvisibleOuterLines != null && wireframe.InvisibleOuterLines.Length > 0)
            //    noZTestLinesManager.DrawLines(transformation, wireframe.Vertices, wireframe.InvisibleOuterLines, outerOccludedColor, dashSize: GUIConstants.invisibleInnerLineDots);

            //if (showInnerLines && wireframe.InvisibleInnerLines != null && wireframe.InvisibleInnerLines.Length > 0)
            //    noZTestLinesManager.DrawLines(transformation, wireframe.Vertices, wireframe.InvisibleInnerLines, innerOccludedColor, dashSize: GUIConstants.invisibleInnerLineDots);

#if TEST_ENABLED
            if (outline.invalidLines != null && outline.invalidLines.Length > 0)
                _noZTestLinesManager.DrawDottedLines(transformation, outline.vertices, outline.invalidLines, Color.red, dashSize: GUIConstants.invalidLineDots);
#endif
        }

        public void Begin()
        {
            zTestLinesManager.Begin();			
            noZTestLinesManager.Begin();
            pointManager.Begin();
            polygonManager.Begin();
        }
        
        public void End()
        {
            zTestLinesManager.End();		
            noZTestLinesManager.End();
            pointManager.End();
            polygonManager.End();
        }

        public void Clear()
        {
            zTestLinesManager.Clear();		
            noZTestLinesManager.Clear();
            pointManager.Clear();
            polygonManager.Clear();
        }
        
        public void RenderAll(Camera camera)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var zTestGenericLineMaterial    = SceneHandleMaterialManager.ZTestGenericLine;
            var noZTestGenericLineMaterial  = SceneHandleMaterialManager.NoZTestGenericLine;
            var coloredPolygonMaterial		= SceneHandleMaterialManager.ColoredPolygonMaterial;
            var customDotMaterial			= SceneHandleMaterialManager.CustomDotMaterial;
            var surfaceNoDepthMaterial		= SceneHandleMaterialManager.SurfaceNoDepthMaterial;

            polygonManager.Render(camera, coloredPolygonMaterial);

			SceneHandleMaterialManager.LineDashMultiplier = 1.0f;
			SceneHandleMaterialManager.LineThicknessMultiplier = GUIConstants.kDefaultLineScale;
            noZTestLinesManager.Render(camera, noZTestGenericLineMaterial);

			SceneHandleMaterialManager.LineDashMultiplier = 1.0f;
			SceneHandleMaterialManager.LineThicknessMultiplier = GUIConstants.kDefaultLineScale;
            zTestLinesManager.Render(camera, zTestGenericLineMaterial);

			SceneHandleMaterialManager.LineAlphaMultiplier = 1.0f;

            pointManager.Render(camera, customDotMaterial, surfaceNoDepthMaterial);
        }
    }
}

using UnityEditor;
using UnityEngine;
using Chisel.Core;

namespace Chisel.Editors
{
    public static class GridRenderer
    {
        const int kPlaneSize = 100;
        static Mesh s_GridMesh;		
        internal static Mesh GridMesh
        {
            get
            {
                if (!s_GridMesh)
                {
                    var vertices = new Vector3[kPlaneSize * kPlaneSize];
                    var indices  = new int[(kPlaneSize -1) * (kPlaneSize-1)*6];
                    var vertex	 = new Vector3();
                    for (int y = 0, n = 0; y < kPlaneSize; y++)
                    {
                        vertex.y = (2.0f * (y / (float)(kPlaneSize - 1))) - 1.0f;
                        for (int x = 0; x < kPlaneSize; x++, n++)
                        {
                            vertex.x = (2.0f * (x / (float)(kPlaneSize - 1))) - 1.0f;
                            vertices[n] = vertex;
                        }
                    }

                    for (int y = 0, n = 0; y < kPlaneSize - 1; y++)
                    {
                        var y0 = y;
                        var y1 = y + 1;
                        for (int x = 0; x < kPlaneSize - 1; x++, n += 6)
                        {
                            var x0 = x;
                            var x1 = x + 1;

                            var n00 = (y0 * kPlaneSize) + x0; var n10 = (y0 * kPlaneSize) + x1;
                            var n01 = (y1 * kPlaneSize) + x0; var n11 = (y1 * kPlaneSize) + x1;

                            indices[n + 0] = n00;
                            indices[n + 1] = n10;
                            indices[n + 2] = n01;

                            indices[n + 3] = n10;
                            indices[n + 4] = n01;
                            indices[n + 5] = n11;
                        }
                    }

                    s_GridMesh = new Mesh()
                    {
                        name = "Plane",
                        vertices  = vertices,
                        triangles = indices,
                        hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset
                    };
                    s_GridMesh.bounds = new Bounds(Vector3.zero, new Vector3(float.MaxValue, 0.1f, float.MaxValue));
                }
                return s_GridMesh;
            }
        }

        static Material s_GridMaterial;
        internal static Material GridMaterial
        {
            get
            {
                if (!s_GridMaterial)
                {
                    s_GridMaterial = SceneHandleMaterialManager.GenerateDebugMaterial(SceneHandleMaterialManager.kShaderNameHandlesRoot + "Grid");
                    s_GridMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    s_GridMaterial.SetInt("_ZWrite", 0);   
                    s_GridMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual); 
                }
                return s_GridMaterial;
            }
        }
        
        static MaterialPropertyBlock s_MaterialProperties = null;
        

        internal static float	s_PrevOrthoInterpolation	= 0;
        internal static float	s_PrevSceneViewSize		= 0;
        internal static Vector3 s_PrevGridSpacing			= Vector3.zero;
        internal static Color	s_PrevCenterColor;
        internal static Color	s_PrevGridColor;
        internal static Color	s_CenterColor;
        internal static Color	s_GridColor;
        internal static int		s_Counter = 0;

        public static float Opacity { get; set; } = 1.0f;


        public static void Render(this Grid grid, SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (!sceneView)
                return;

            var renderMode = sceneView.cameraMode.drawMode;
            if (renderMode != DrawCameraMode.Textured &&
                renderMode != DrawCameraMode.TexturedWire &&
                renderMode != DrawCameraMode.Normal)
                return;
             
            var camera		= sceneView.camera;
            if (!camera)
                return;
            
            var gridMaterial	= GridMaterial;
            var gridMesh		= GridMesh;
            var gridSpacing		= grid.Spacing;
            var sceneViewSize	= sceneView.size;

            Vector3 swizzledGridSpacing;
            swizzledGridSpacing.x = gridSpacing.x;
            swizzledGridSpacing.y = gridSpacing.z;
            swizzledGridSpacing.z = gridSpacing.y;

            float orthoInterpolation; // hack to get SceneView.m_Ortho.faded
            {
                const float kOneOverSqrt2 = 0.707106781f;
                const float kMinOrtho = 0.2f;
                const float kMaxOrtho = 0.95f;
                orthoInterpolation = ((Mathf.Atan(Mathf.Tan(camera.fieldOfView / (2 * Mathf.Rad2Deg)) * Mathf.Sqrt(camera.aspect) / kOneOverSqrt2) / (0.5f * Mathf.Deg2Rad)) / 90.0f);
                orthoInterpolation = Mathf.Clamp01((orthoInterpolation - kMinOrtho) / (kMaxOrtho - kMinOrtho));
            }

            s_Counter--;
            if (s_Counter <= 0)
            {
                var opacity = Opacity;

                // this code is slow and creates garbage, but unity doesn't give us a nice efficient mechanism to get these standard colors
                s_CenterColor = ColorUtility.GetPreferenceColor("Scene/Center Axis", new Color(.8f, .8f, .8f, .93f));
                s_CenterColor.a = opacity * 0.5f;
                s_GridColor = ColorUtility.GetPreferenceColor("Scene/Grid", new Color(.5f, .5f, .5f, .4f));
                s_GridColor.a = opacity;
                s_Counter = 10;
            }

            if (renderMode == DrawCameraMode.TexturedWire)
            {
                // if we don't use DrawMeshNow Unity will draw the wireframe of the grid :(

                gridMaterial.SetColor("_GridColor",			 s_GridColor);
                gridMaterial.SetColor("_CenterColor",		 s_CenterColor);
                gridMaterial.SetFloat("_OrthoInterpolation", orthoInterpolation);
                gridMaterial.SetFloat("_ViewSize",			 sceneViewSize);
                gridMaterial.SetVector("_GridSpacing",		 swizzledGridSpacing);
                gridMaterial.SetPass(0);
                Graphics.DrawMeshNow(gridMesh, grid.GridToWorldSpace, 0);
            } else
            {
                // TODO: store this for each SceneView
                if (s_MaterialProperties == null)
                    s_MaterialProperties = new MaterialPropertyBlock();

                if (s_PrevGridColor != s_GridColor)
                {
                    s_MaterialProperties.SetColor("_GridColor", s_GridColor);
                    s_PrevGridColor = s_GridColor;
                }

                if (s_PrevCenterColor != s_CenterColor)
                {
                    s_MaterialProperties.SetColor("_CenterColor", s_CenterColor);
                    s_PrevCenterColor = s_CenterColor;
                }

                if (s_PrevOrthoInterpolation != orthoInterpolation)
                {
                    s_MaterialProperties.SetFloat("_OrthoInterpolation", orthoInterpolation);
                    s_PrevOrthoInterpolation = orthoInterpolation;
                }

                if (s_PrevSceneViewSize != sceneViewSize)
                {
                    s_MaterialProperties.SetFloat("_ViewSize", sceneViewSize);
                    s_PrevSceneViewSize = sceneViewSize;
                }

                if (s_PrevGridSpacing != gridSpacing)
                {
                    s_MaterialProperties.SetVector("_GridSpacing", swizzledGridSpacing);
                    s_PrevGridSpacing = gridSpacing;
                }

                Graphics.DrawMesh(gridMesh, grid.GridToWorldSpace, gridMaterial, 0, camera, 0, s_MaterialProperties, false, false);
            } 
        }
    }
}

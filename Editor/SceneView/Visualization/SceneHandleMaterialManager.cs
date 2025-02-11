using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Chisel.Editors
{
    public class SceneHandleMaterialManager : ScriptableObject
    {
        internal const string kShaderNameHandlesRoot	= "Hidden/Chisel/internal/";
        
        static bool s_ShadersInitialized		= false;
        static int	s_PixelsPerPointId			= -1;
        static int	s_LineThicknessMultiplierId	= -1; 
        static int	s_LineDashMultiplierId		= -1; 
        static int	s_LineAlphaMultiplierId		= -1;

        static void ShaderInit()
        {
            s_ShadersInitialized = true;
    
            s_PixelsPerPointId			= Shader.PropertyToID("_pixelsPerPoint");
            s_LineThicknessMultiplierId	= Shader.PropertyToID("_thicknessMultiplier");
            s_LineDashMultiplierId		= Shader.PropertyToID("_dashMultiplier");
            s_LineAlphaMultiplierId		= Shader.PropertyToID("_alphaMultiplier");
        }
        
        static Material s_DefaultMaterial;
        public static Material DefaultMaterial
        {
            get
            {
                // TODO: make this work with HDRP
                if (!s_DefaultMaterial)
                    s_DefaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
                return s_DefaultMaterial;
            }
        }
        
        public static void InitGenericLineMaterial(Material genericLineMaterial)
        {
            if (!genericLineMaterial)
                return;
            
            if (!s_ShadersInitialized) ShaderInit();
            if (s_PixelsPerPointId != -1)
            {
                genericLineMaterial.SetFloat(s_PixelsPerPointId, EditorGUIUtility.pixelsPerPoint);
            }
            if (s_LineThicknessMultiplierId != -1) genericLineMaterial.SetFloat(s_LineThicknessMultiplierId, s_LineThicknessMultiplier * EditorGUIUtility.pixelsPerPoint);
            if (s_LineDashMultiplierId      != -1) genericLineMaterial.SetFloat(s_LineDashMultiplierId,      s_LineDashMultiplier);
            if (s_LineAlphaMultiplierId	    != -1) genericLineMaterial.SetFloat(s_LineAlphaMultiplierId,     s_LineAlphaMultiplier);
        }
        

        static float s_LineThicknessMultiplier = 1.0f;
        public static float LineThicknessMultiplier		{ get { return s_LineThicknessMultiplier; } set { if (Mathf.Abs(s_LineThicknessMultiplier - value) < 0.0001f) return; s_LineThicknessMultiplier = value; } }

        static float s_LineDashMultiplier = 1.0f;
        public static float LineDashMultiplier			{ get { return s_LineDashMultiplier; } set { if (Mathf.Abs(s_LineDashMultiplier - value) < 0.0001f) return; s_LineDashMultiplier = value; } }

        static float s_LineAlphaMultiplier = 1.0f;
        public static float LineAlphaMultiplier			{ get { return s_LineAlphaMultiplier; } set { if (Mathf.Abs(s_LineAlphaMultiplier - value) < 0.0001f) return; s_LineAlphaMultiplier = value; } }
        
        static Material s_ZTestGenericLine;
        public static Material ZTestGenericLine			{ get { if (!s_ZTestGenericLine) s_ZTestGenericLine = GenerateDebugMaterial(kShaderNameHandlesRoot + "ZTestGenericLine"); return s_ZTestGenericLine; } }

        static Material s_NoZTestGenericLine;
        public static Material NoZTestGenericLine		{ get { if (!s_NoZTestGenericLine) s_NoZTestGenericLine = GenerateDebugMaterial(kShaderNameHandlesRoot + "NoZTestGenericLine"); return s_NoZTestGenericLine; } }

        static Material s_ColoredPolygonMaterial;
        public static Material ColoredPolygonMaterial	{ get { if (!s_ColoredPolygonMaterial) s_ColoredPolygonMaterial = GenerateDebugMaterial(kShaderNameHandlesRoot + "customSurface"); return s_ColoredPolygonMaterial; } }

        static Material s_CustomDotMaterial;
        public static Material CustomDotMaterial		{ get { if (!s_CustomDotMaterial) s_CustomDotMaterial = GenerateDebugMaterial(kShaderNameHandlesRoot + "customDot"); return s_CustomDotMaterial; } }

        static Material s_SurfaceNoDepthMaterial;
        public static Material SurfaceNoDepthMaterial	{ get { if (!s_SurfaceNoDepthMaterial) s_SurfaceNoDepthMaterial = GenerateDebugMaterial(kShaderNameHandlesRoot + "customNoDepthSurface"); return s_SurfaceNoDepthMaterial; } }

        static Material s_GridMaterial;
        public static Material GridMaterial				{ get { if (!s_GridMaterial) s_GridMaterial = GenerateDebugMaterial(kShaderNameHandlesRoot + "Grid"); return s_GridMaterial; } }



		readonly static Dictionary<string, Material> s_EditorMaterials = new();
		internal static Material GenerateDebugMaterial(string shaderName)
        {
			var name = shaderName;
			if (s_EditorMaterials.TryGetValue(name, out Material material))
            {
                // just in case one of many unity bugs destroyed the material
                if (!material)
                {
                    s_EditorMaterials.Remove(name);
                } else
                    return material;
            }

            var materialName = name.Replace(':', '_');


            var shader = Shader.Find(shaderName);
            if (!shader)
            {
                Debug.LogWarning("Could not find internal shader: " + shaderName);
                return null;
            }

            material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave
            };
            s_EditorMaterials.Add(name, material);
            return material;
        }	
    }
}

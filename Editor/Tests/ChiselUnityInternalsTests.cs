using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors.Tests
{
    // Every Unity member Chisel looks up by name still exists, in the shape Chisel calls it
    public sealed class ChiselUnityInternalsTests
    {
        const BindingFlags kAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        static Type EditorType(string name) => typeof(Editor).Assembly.GetType(name);

        static bool HasMember(Type type, string name) => type != null && type.GetMember(name, kAll).Length > 0;

        static bool HasMethod(Type type, string name, params Type[] parameters)
        {
            return type != null && type.GetMethods(kAll).Any(m => m.Name == name &&
                       m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
        }

        static readonly (string, Func<bool>)[] kMembers =
        {
            ("SceneViewGrid.gridOpacity",                      () => HasMember(EditorType("UnityEditor.SceneViewGrid"), "gridOpacity")),
            ("SceneViewGrid.gridAxis",                         () => HasMember(EditorType("UnityEditor.SceneViewGrid"), "gridAxis")),
            ("GridSettings.size",                              () => HasMember(EditorType("UnityEditor.GridSettings"), "size")),
            ("RendererLightingSettings.LightmapParametersGUI", () => HasMethod(EditorType("UnityEditor.RendererLightingSettings"), "LightmapParametersGUI", typeof(SerializedProperty), typeof(GUIContent))),
            ("InternalMeshUtil.GetCachedMeshSurfaceArea",      () => HasMethod(EditorType("UnityEditor.InternalMeshUtil"), "GetCachedMeshSurfaceArea", typeof(MeshRenderer))),
            ("Lightmapping.HasClampedResolution",              () => HasMethod(typeof(Lightmapping), "HasClampedResolution", typeof(Renderer))),
            ("Lightmapping.HasUVOverlaps",                     () => HasMethod(typeof(Lightmapping), "HasUVOverlaps", typeof(Renderer))),
            ("ShaderUtil.HasInstancing",                       () => HasMethod(typeof(ShaderUtil), "HasInstancing", typeof(Shader))),
            ("EditorGUIUtility.contextWidth",                  () => HasMember(typeof(EditorGUIUtility), "contextWidth")),
            ("EditorGUIUtility.GetHelpIcon",                   () => HasMethod(typeof(EditorGUIUtility), "GetHelpIcon", typeof(MessageType))),
            ("EditorGUIUtility.s_LastControlID",               () => HasMember(typeof(EditorGUIUtility), "s_LastControlID")),
            ("EditorGUI.s_RecycledEditor",                     () => HasMember(typeof(EditorGUI), "s_RecycledEditor")),
            ("EditorGUI.DoFloatField",                         () => typeof(EditorGUI).GetMethods(kAll).Any(m => m.Name == "DoFloatField" && m.GetParameters().Length == 8)),
            ("Editor.DrawPropertiesExcluding",                 () => HasMethod(typeof(Editor), "DrawPropertiesExcluding", typeof(SerializedObject), typeof(string[]))),
            ("ColorPicker.Show",                               () => EditorType("UnityEditor.ColorPicker")?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                                        .Any(m => m.Name == "Show" && m.GetParameters().Length >= 4 &&
                                                                                  m.GetParameters()[0].ParameterType == typeof(Action<Color>)) == true),
            ("SceneView.m_RectSelection",                      () => HasMember(typeof(SceneView), "m_RectSelection")),
            ("RectSelection.k_RectSelectionID",                () => HasMember(EditorType("UnityEditor.RectSelection"), "k_RectSelectionID")),
            ("RectSelection.m_StartPoint",                     () => HasMember(EditorType("UnityEditor.RectSelection"), "m_StartPoint")),
            ("RectSelection.m_IsNearestControl",               () => HasMember(EditorType("UnityEditor.RectSelection"), "m_IsNearestControl")),
            ("RectSelection.m_SelectionStart",                 () => HasMember(EditorType("UnityEditor.RectSelection"), "m_SelectionStart")),
            ("RectSelection.m_LastSelection",                  () => HasMember(EditorType("UnityEditor.RectSelection"), "m_LastSelection")),
            ("RectSelection.m_CurrentSelection",               () => HasMember(EditorType("UnityEditor.RectSelection"), "m_CurrentSelection")),
            ("RectSelection.m_SelectMousePoint",               () => HasMember(EditorType("UnityEditor.RectSelection"), "m_SelectMousePoint")),
            ("RectSelection.UpdateSelection",                  () => HasMethod(EditorType("UnityEditor.RectSelection"), "UpdateSelection", typeof(UnityEngine.Object[]), typeof(UnityEngine.Object[]),
                                                                               EditorType("UnityEditor.RectSelection+SelectionType"), typeof(bool))),
            ("RectSelection.SelectionType.Additive",           () => EditorType("UnityEditor.RectSelection+SelectionType") is Type t && Enum.IsDefined(t, "Additive")),
        };

        [Test]
        public void EveryUnityMemberChiselLooksUp_StillExists()
        {
            var missing = kMembers.Where(member => !member.Item2()).Select(member => member.Item1).ToArray();
            Assert.That(missing, Is.Empty);
        }
    }
}

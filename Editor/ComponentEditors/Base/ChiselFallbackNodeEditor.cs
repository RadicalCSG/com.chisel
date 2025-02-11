using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
using System.Collections.Generic;
using Chisel;
using Chisel.Core;
using Chisel.Components;

namespace Chisel.Editors
{
    [CustomEditor(typeof(ChiselNodeComponent), isFallback = true)]
    public sealed class ChiselFallbackNodeEditor : ChiselNodeEditor<ChiselNodeComponent>
    {
        protected override void OnEditSettingsGUI(SceneView sceneView) { }
    }
}

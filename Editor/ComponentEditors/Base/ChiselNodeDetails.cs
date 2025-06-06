﻿using UnityEngine;
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
    public interface IChiselNodeDetails
    {
        GUIContent GetHierarchyIconForGenericNode(ChiselNodeComponent node);
    }

    public abstract class ChiselNodeDetails<T> : IChiselNodeDetails
        where T : ChiselNodeComponent
    {
        GUIContent IChiselNodeDetails.GetHierarchyIconForGenericNode(ChiselNodeComponent node) { return GetHierarchyIcon((T)node); }
        public abstract GUIContent GetHierarchyIcon(T node);
    }
}

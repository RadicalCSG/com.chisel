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
    public static class ChiselNodeDetailsManager
    {
		static readonly Dictionary<Type, IChiselNodeDetails> s_NodeDetailsLookup = new();
        static readonly IChiselNodeDetails s_GeneratorDefaultDetails = new ChiselDefaultGeneratorDetails();

        [InitializeOnLoadMethod]
        static void InitializeNodeDetails()
        {
            ReflectionExtensions.Initialize();
            foreach (var type in ReflectionExtensions.AllNonAbstractClasses)
            {
                var baseType = type.GetGenericBaseClass(typeof(ChiselNodeDetails<>));
                if (baseType == null)
                    continue;

                var typeParameters = baseType.GetGenericArguments();
                var instance = (IChiselNodeDetails)Activator.CreateInstance(type);
                s_NodeDetailsLookup.Add(typeParameters[0], instance);
            }
        }

        public static IChiselNodeDetails GetNodeDetails(ChiselNodeComponent node)
        {
            if (s_NodeDetailsLookup.TryGetValue(node.GetType(), out IChiselNodeDetails nodeDetails))
                return nodeDetails;
            return s_GeneratorDefaultDetails;
        }

        public static IChiselNodeDetails GetNodeDetails(Type type)
        {
            if (s_NodeDetailsLookup.TryGetValue(type, out IChiselNodeDetails nodeDetails))
                return nodeDetails;
            return s_GeneratorDefaultDetails;
        }

        public static GUIContent GetHierarchyIcon(ChiselNodeComponent node)
        {
            if (s_NodeDetailsLookup.TryGetValue(node.GetType(), out IChiselNodeDetails nodeDetails))
            {
                return nodeDetails.GetHierarchyIconForGenericNode(node);
            }
            return s_GeneratorDefaultDetails.GetHierarchyIconForGenericNode(node);
        }

        class HierarchyMessageHandler : IChiselMessageHandler
		{
			public MessageDestination Destination
			{
				get { return MessageDestination.Hierarchy; }
			}

			static System.Text.StringBuilder warningStringBuilder = new System.Text.StringBuilder();

			public void SetTitle(string name, UnityEngine.Object reference)
			{
			}

			// TODO: how to handle these kind of message in the hierarchy? cannot show buttons, 
			//       but still want to show a coherent message
			public void Warning(string message, Action buttonAction, string buttonText)
            {
            }

            public void Warning(string message)
            {
                if (warningStringBuilder.Length > 0)
                    warningStringBuilder.AppendLine();
                warningStringBuilder.Append(message);
            }

            public void Clear() { warningStringBuilder.Clear(); }
            public int Length { get { return warningStringBuilder.Length; } }
            public override string ToString() { return warningStringBuilder.ToString(); }
		}

        static HierarchyMessageHandler hierarchyMessageHandler = new HierarchyMessageHandler();

        public static GUIContent GetHierarchyIcon(ChiselNodeComponent node, out bool hasValidState)
        {
            hierarchyMessageHandler.Clear();
            node.GetMessages(hierarchyMessageHandler);
            string nodeMessage;
            if (hierarchyMessageHandler.Length != 0)
            {
                hasValidState = false;
                nodeMessage = hierarchyMessageHandler.ToString();
            } else
            {
                hasValidState = true;
                nodeMessage = string.Empty;
            }
            var hierarchyIcon = GetHierarchyIcon(node);
            hierarchyIcon.tooltip = nodeMessage;
            return hierarchyIcon;
        }
    }
}

using UnityEngine;
using Chisel.Core;
using Unity.Mathematics;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    [Icon(kIconBasePath + "box" + kIconExtension)]
    public sealed class ChiselBoxComponent : ChiselBrushGeneratorComponent<ChiselBoxDefinition, Core.ChiselBox>
    {
        public const string kNodeTypeName = Core.ChiselBoxDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public AABB Bounds
        {
            get { return definition.settings.bounds; }
            set { if (math.all(value.Min == definition.settings.bounds.Min) && math.all(value.Max == definition.settings.bounds.Max)) return; definition.settings.bounds = value; OnValidateState(); }
        }

        public float3 Min
        {
            get { return definition.settings.Min; }
            set { if (math.all(value == definition.settings.Min)) return; definition.settings.Min = value; OnValidateState(); }
        }

        public float3 Max
        {
            get { return definition.settings.Max; }
            set { if (math.all(value == definition.settings.Max)) return; definition.settings.Max = value; OnValidateState(); }
        }

        public float3 Center
        {
            get { return definition.settings.Center; }
            set { if (math.all(value == definition.settings.Center)) return; definition.settings.Center = value; OnValidateState(); }
        }

        public float3 Size
        {
            get { return definition.settings.Size; }
            set { if (math.all(value == definition.settings.Size)) return; definition.settings.Size = value; OnValidateState(); }
        }
        #endregion

        public override bool SupportsDisplacement => true;

        protected override Core.ChiselBox OwnBrushGenerator(Core.ChiselBox settings)
        {
            var min = math.min(settings.Min, settings.Max);
            var max = math.max(settings.Min, settings.Max);
            // in the order of BrushMeshFactory.BoxSides
            max.y -= DisplacedInset(0);
            min.y += DisplacedInset(1);
            max.x -= DisplacedInset(2);
            min.x += DisplacedInset(3);
            min.z += DisplacedInset(4);
            max.z -= DisplacedInset(5);
            settings.bounds = new MinMaxAABB { Min = min, Max = max };
            return settings;
        }

        protected override bool GetDisplacementOutline(System.Collections.Generic.List<float4> planes, System.Collections.Generic.List<int> surfaces)
        {
            var min = math.min(definition.settings.Min, definition.settings.Max);
            var max = math.max(definition.settings.Min, definition.settings.Max);
            // in the order of BrushMeshFactory.BoxSides, the surface each is
            planes.Add(new float4( 0,  1,  0, -max.y)); surfaces.Add(0);
            planes.Add(new float4( 0, -1,  0,  min.y)); surfaces.Add(1);
            planes.Add(new float4( 1,  0,  0, -max.x)); surfaces.Add(2);
            planes.Add(new float4(-1,  0,  0,  min.x)); surfaces.Add(3);
            planes.Add(new float4( 0,  0, -1,  min.z)); surfaces.Add(4);
            planes.Add(new float4( 0,  0,  1, -max.z)); surfaces.Add(5);
            return true;
        }
    }
}
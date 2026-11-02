using Chisel.Core;
using UnityEngine;

namespace Chisel.Components
{
    [ExecuteInEditMode, HelpURL(kDocumentationBaseURL + kNodeTypeName + kDocumentationExtension)]
    [DisallowMultipleComponent, AddComponentMenu("Chisel/" + kNodeTypeName)]
    [Icon(kIconBasePath + "boxOutline" + kIconExtension)]
    public sealed class ChiselBrushComponent : ChiselNodeGeneratorComponent<ChiselBrushDefinition>
    {
        public const string kNodeTypeName = ChiselBrushDefinition.kNodeTypeName;
        public override string ChiselNodeTypeName { get { return kNodeTypeName; } }

        #region Properties
        public BrushMesh BrushMesh
        {
            get { return definition.BrushOutline; }
            set { if (value == definition.BrushOutline) return; definition.BrushOutline = value; OnValidateState(); }
        }
        #endregion

        protected override bool EnsureTopNodeCreatedInternal(in CSGTree tree, ref CSGTreeNode node, UnityEngine.EntityId entityId)
        {
			if (!OnValidateDefinition())
				return false;

            EnsureOwnBrush(in tree, ref node, entityId);
            return true;
        }

        protected override void UpdateGeneratorNodesInternal(in CSGTree tree, ref CSGTreeNode node)
        {
            CSGTreeNode brush = OwnBrush;
            ChiselNodeHierarchyManager.OnBrushMeshUpdate(this, ref brush);
        }

        public override bool SupportsDisplacement => true;

        /// <summary>The outline the brush itself is made as: its definition's, with its displaced surfaces moved in
        /// (<see cref="ChiselGeneratorComponent.DisplacedInset"/>)</summary>
        internal BrushMesh OwnBrushOutline
        {
            get
            {
                var outline = definition.BrushOutline;
                if (outline?.planes == null || outline.polygons == null || outline.vertices == null ||
                    outline.planes.Length != outline.polygons.Length || !IsDisplaced)
                    return outline;
                var planes = (Unity.Mathematics.float4[])outline.planes.Clone();
                var moved  = false;
                for (int p = 0; p < planes.Length; p++)
                {
                    var inset = DisplacedInset(outline.polygons[p].descriptionIndex);
                    if (inset <= 0)
                        continue;
                    planes[p].w += inset * Unity.Mathematics.math.length(planes[p].xyz);
                    moved = true;
                }
                if (!moved)
                    return outline;
                var bounds = new Bounds(outline.vertices[0], Vector3.zero);
                foreach (var vertex in outline.vertices)
                    bounds.Encapsulate(vertex);
                bounds.Expand(1);
                var surfaces = new ChiselSurfaceArray();
                BrushMeshFactory.CreateFromPlanes(planes, bounds, ref surfaces, out var inside);
                if (inside?.polygons == null)
                    return outline;
                // CreateFromPlanes numbers a face by its plane: each keeps the surface its plane's polygon had
                for (int p = 0; p < inside.polygons.Length; p++)
                {
                    var plane = inside.polygons[p].descriptionIndex;
                    inside.polygons[p].descriptionIndex = plane >= 0 && plane < outline.polygons.Length ? outline.polygons[plane].descriptionIndex : 0;
                }
                return inside;
            }
        }

        protected override bool GetDisplacementOutline(System.Collections.Generic.List<Unity.Mathematics.float4> planes, System.Collections.Generic.List<int> surfaces)
        {
            var outline = definition.BrushOutline;
            if (outline == null || outline.planes == null || outline.polygons == null || outline.planes.Length != outline.polygons.Length)
                return false;
            for (int p = 0; p < outline.polygons.Length; p++)
            {
                planes.Add(outline.planes[p]);
                surfaces.Add(outline.polygons[p].descriptionIndex);
            }
            return true;
        }
    }
}
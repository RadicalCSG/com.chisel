using System.Runtime.CompilerServices;
using Chisel.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Chisel.Components
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public abstract class ChiselBrushGeneratorComponent<DefinitionType, Generator> : ChiselNodeGeneratorComponent<DefinitionType>
        where Generator      : unmanaged, IBrushGenerator
        where DefinitionType : SerializedBrushGenerator<Generator>, new()
	{
		readonly static GeneratorBrushJobPool<Generator> s_JobPool = new();

        protected override bool EnsureTopNodeCreatedInternal(in CSGTree tree, ref CSGTreeNode node, UnityEngine.EntityId entityId)
        {
            if (!OnValidateDefinition())
                return false;

            EnsureOwnBrush(in tree, ref node, entityId);
            return true;
        }


        const Allocator defaultAllocator = Allocator.TempJob;
        protected override void UpdateGeneratorNodesInternal(in CSGTree tree, ref CSGTreeNode node)
        {
            var brush = OwnBrush;
            if (!brush.Valid)
                return;

            var ownSurfaces = OwnBrushSurfaces;
            var surfaceDefinitionBlob = BrushMeshManager.BuildInternalSurfaceArrayBlob(in ownSurfaces, defaultAllocator);
            if (!surfaceDefinitionBlob.IsCreated)
                return;

            var settings = OwnBrushGenerator(definition.GetBrushGenerator());
            s_JobPool.ScheduleUpdate(brush, settings, surfaceDefinitionBlob);
        }

        /// <summary>What the generator's own brush is made with: the definition's, a generator of displaceable surfaces
        /// moving its displaced ones in (<see cref="ChiselGeneratorComponent.DisplacedInset"/>)</summary>
        protected virtual Generator OwnBrushGenerator(Generator settings) => settings;

		public override void GetMessages(IChiselMessageHandler messageHandler)
		{
            definition?.GetMessages(messageHandler);
		}
	}

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public abstract class ChiselBranchGeneratorComponent<Generator, DefinitionType> : ChiselNodeGeneratorComponent<DefinitionType>
        where Generator      : unmanaged, IBranchGenerator
        where DefinitionType : SerializedBranchGenerator<Generator>, new()
	{
		readonly static GeneratorBranchJobPool<Generator> s_JobPool = new();

        CSGTreeBranch GenerateTopNode(in CSGTree tree, CSGTreeBranch branch, UnityEngine.EntityId entityId, CSGOperationType operation)
        {
            if (!branch.Valid)
            {
                if (branch.Valid)
                    branch.Destroy();
                return tree.CreateBranch(entityId: entityId, operation: operation);
            }
            if (branch.Operation != operation)
                branch.Operation = operation;
            return branch;
        }
        protected override bool EnsureTopNodeCreatedInternal(in CSGTree tree, ref CSGTreeNode node, UnityEngine.EntityId entityId)
        {
			if (!OnValidateDefinition())
				return false;

			var branch = (CSGTreeBranch)node;
            node = GenerateTopNode(in tree, branch, entityId, operation);
            return true;
        }

        const Allocator defaultAllocator = Allocator.TempJob;
        protected override void UpdateGeneratorNodesInternal(in CSGTree tree, ref CSGTreeNode node)
        {
            var branch = (CSGTreeBranch)node;
            if (!branch.Valid)
                return;

            var surfaceDefinitionBlob = BrushMeshManager.BuildInternalSurfaceArrayBlob(in surfaceArray, defaultAllocator);
            if (!surfaceDefinitionBlob.IsCreated)
                return;

            var settings = definition.GetBranchGenerator();
            s_JobPool.ScheduleUpdate(branch, settings, surfaceDefinitionBlob, contents);
        }
    }

    public abstract class ChiselNodeGeneratorComponent<DefinitionType> : ChiselGeneratorComponent
        where DefinitionType : IChiselNodeGenerator, new()
    {
        public const string kDefinitionName = nameof(definition);

        public DefinitionType definition = new();

        public ChiselSurfaceArray surfaceArray;
        public override ChiselSurfaceArray SurfaceDefinition { get { return surfaceArray; } }

        public override UnityEngine.Hash128 GetDefinitionInputHash() { return definition.GetInputHash(); }

        public override string GetSurfaceName(int descriptionIndex) { return definition.GetSurfaceName(descriptionIndex); }
        public override bool IsSurfaceUsed(int descriptionIndex) { return definition.IsSurfaceUsed(descriptionIndex); }
        public override ChiselSurface GetSurface(int descriptionIndex) { return surfaceArray.GetSurface(descriptionIndex); }
        public override SurfaceDetails GetSurfaceDetails(int descriptionIndex) { return surfaceArray.GetSurfaceDetails(descriptionIndex); }
        public override void SetSurfaceDetails(int descriptionIndex, SurfaceDetails description) { surfaceArray.SetSurfaceDetails(descriptionIndex, description); }
        public override UVMatrix GetSurfaceUV0(int descriptionIndex) { return surfaceArray.GetSurfaceUV0(descriptionIndex); }
        public override void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0) { surfaceArray.SetSurfaceUV0(descriptionIndex, uv0); }

        protected override void OnResetInternal()
        { 
            definition.Reset(); 
            surfaceArray?.Reset(); 
            base.OnResetInternal(); 
        }

        protected bool OnValidateDefinition(bool logErrors = false)
		{
            bool success = true;
            try
            {
                success = definition.Validate();
				if (surfaceArray == null)
                {
                    surfaceArray = new ChiselSurfaceArray();
                    surfaceArray.Reset();
                }
                var prevLength = surfaceArray.surfaces?.Length ?? 0;
                if (surfaceArray.EnsureSize(definition.RequiredSurfaceCount))
                {
                    // new surfaces take the generator's own defaults, the wall material where it has none
                    for (int i = prevLength; i < surfaceArray.surfaces.Length; i++)
                        surfaceArray.surfaces[i].SetMaterial(null);
                }
                definition.UpdateSurfaces(ref surfaceArray);
                for (int i = prevLength; surfaceArray.surfaces != null && i < surfaceArray.surfaces.Length; i++)
                {
                    if (!surfaceArray.surfaces[i].HasMaterial)
                        surfaceArray.surfaces[i].SetMaterial(ChiselProjectSettings.DefaultWallMaterial);
                }
            }
            catch (System.Exception ex)
			{
				success = false;
				Debug.LogException(ex, this);
			}
            if (!true && logErrors)
			    Debug.LogError($"Validation failed for {this.name}", this);
            return true;
		}

        protected override void OnValidateState()
        {
            if (OnValidateDefinition())
			    base.OnValidateState();
        }

		// Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
		public override void GetMessages(IChiselMessageHandler messages)
        {
            base.GetMessages(messages);
            definition.GetMessages(messages);
        }
    }

    public interface IChiselHasOperation
    {
        CSGOperationType Operation { get; set; }
    }

    [DisallowMultipleComponent]
    public abstract class ChiselGeneratorComponent : ChiselNodeComponent, IChiselHasOperation
    {
        // This ensures names remain identical, or a compile error occurs.
        public const string kOperationFieldName = nameof(operation);
        public const string kContentsFieldName  = nameof(contents);

        [HideInInspector] CSGTreeNode Node = default;

        public abstract ChiselSurfaceArray SurfaceDefinition { get; }

        /// <summary>The name of the surface at this place in the surface array, or null when it is only numbered</summary>
        public virtual string GetSurfaceName(int descriptionIndex) { return null; }

        /// <summary>Whether the generator, as it is set up now, has faces with the surface at this place</summary>
        public virtual bool IsSurfaceUsed(int descriptionIndex) { return true; }

        [SerializeField, HideInInspector] protected CSGOperationType    operation;		    // NOTE: do not rename, name is directly used in editors
        // What every brush this generates is made of: an index into ChiselContentsList.Instance, 0 is Solid.
        // Scenes saved before contents existed load as Solid.
        [SerializeField, HideInInspector] protected int                 contents;		    // NOTE: do not rename, name is directly used in editors
        [SerializeField] protected Vector3                              pivotOffset         = Vector3.zero;
        // The paint of the generator's surfaces, a grid per surface and vertex attribute (ChiselSurfacePaint)
        [SerializeField, HideInInspector] System.Collections.Generic.List<ChiselSurfacePaint> surfacePaint = new();

        public System.Collections.Generic.IReadOnlyList<ChiselSurfacePaint> SurfacePaint => surfacePaint;

        /// <summary>The paint of a surface in an attribute, or null</summary>
        public ChiselSurfacePaint GetSurfacePaint(int surfaceIndex, ChiselPaintAttribute attribute)
        {
            foreach (var paint in surfacePaint)
            {
                if (paint != null && paint.surfaceIndex == surfaceIndex && paint.attribute == attribute)
                    return paint;
            }
            return null;
        }

        /// <summary>Gives a surface this paint in its attribute, in place of what it had</summary>
        public void SetSurfacePaint(ChiselSurfacePaint paint)
        {
            if (paint == null)
                return;
            surfacePaint.RemoveAll(other => other == null || (other.surfaceIndex == paint.surfaceIndex && other.attribute == paint.attribute));
            surfacePaint.Add(paint);
            ChiselPaintManager.SetDirty(this);
        }

        public void RemoveSurfacePaint(int surfaceIndex, ChiselPaintAttribute attribute)
        {
            if (surfacePaint.RemoveAll(other => other == null || (other.surfaceIndex == surfaceIndex && other.attribute == attribute)) > 0)
                ChiselPaintManager.SetDirty(this);
        }

        /// <summary>Called after the values of a surface's paint were changed in place</summary>
        public void PaintChanged() => ChiselPaintManager.SetDirty(this);

        // The displaced surfaces, a grid of heights each, X along the surface's normal (ChiselDisplacement)
        [SerializeField, HideInInspector] System.Collections.Generic.List<ChiselSurfacePaint> surfaceDisplacement = new();

        public System.Collections.Generic.IReadOnlyList<ChiselSurfacePaint> SurfaceDisplacement => surfaceDisplacement;

        /// <summary>The heights of a displaced surface, or null</summary>
        public ChiselSurfacePaint GetSurfaceDisplacement(int surfaceIndex)
        {
            foreach (var heights in surfaceDisplacement)
            {
                if (heights != null && heights.surfaceIndex == surfaceIndex)
                    return heights;
            }
            return null;
        }

        /// <summary>Displaces a surface by these heights, in place of what it had</summary>
        public void SetSurfaceDisplacement(ChiselSurfacePaint heights)
        {
            if (heights == null)
                return;
            surfaceDisplacement.RemoveAll(other => other == null || other.surfaceIndex == heights.surfaceIndex);
            surfaceDisplacement.Add(heights);
            DisplacementChanged();
        }

        /// <summary>
        /// Grows the grids of the displaced surfaces over where their neighbours, raised, move the edges they share out
        /// to: a surface's columns reach that far. The new points take the nearest edge's heights. True when any grew.
        /// </summary>
        public bool ExtendDisplacementGrids()
        {
            if (!SupportsDisplacement || surfaceDisplacement.Count == 0)
                return false;
            var planes   = new System.Collections.Generic.List<Unity.Mathematics.float4>();
            var surfaces = new System.Collections.Generic.List<int>();
            if (!GetDisplacementOutline(planes, surfaces))
                return false;
            var changed = false;
            for (int d = 0; d < surfaceDisplacement.Count; d++)
            {
                if (!Covering(surfaceDisplacement[d], planes, surfaces, out var grown))
                    continue;
                surfaceDisplacement[d] = grown;
                changed = true;
            }
            return changed;
        }

        // The grid grown over where the neighbouring surfaces, raised, move the edges it shares with them out to; false
        // when it covers that already. The columns are made from that: where a surface's columns reach past a
        // neighbour's grid, they would end at the neighbour's face that is never drawn, and leave a hole.
        bool Covering(ChiselSurfacePaint heights, System.Collections.Generic.List<Unity.Mathematics.float4> planes,
                      System.Collections.Generic.List<int> surfaces, out ChiselSurfacePaint grown)
        {
            grown = heights;
            if (heights == null || !heights.IsValid)
                return false;
            var face = surfaces.IndexOf(heights.surfaceIndex);
            if (face < 0)
                return false;
            // the planes in the generator's space, where the grids are
            var inGenerator = new Unity.Mathematics.float4[planes.Count];
            for (int p = 0; p < planes.Count; p++)
            {
                var plane  = planes[p];
                plane     /= Unity.Mathematics.math.length(plane.xyz);
                plane.w   -= Vector3.Dot(new Vector3(plane.x, plane.y, plane.z), pivotOffset);
                inGenerator[p] = plane;
            }
            // the surface's corners, each where it meets two other faces, and the faces each is on
            var corners = new System.Collections.Generic.List<(Vector3 point, int a, int b)>();
            for (int a = 0; a < inGenerator.Length; a++)
            {
                for (int b = a + 1; b < inGenerator.Length; b++)
                {
                    if (a == face || b == face || !Corner(inGenerator[face], inGenerator[a], inGenerator[b], out var point))
                        continue;
                    var inside = true;
                    foreach (var plane in inGenerator)
                        inside &= Unity.Mathematics.math.dot(plane.xyz, point) + plane.w < 1e-4f;
                    if (inside)
                        corners.Add(((Vector3)point, a, b));
                }
            }
            var reach = new System.Collections.Generic.List<Vector3>();
            foreach (var (point, _, _) in corners)
                reach.Add(point);
            foreach (var (point, a, b) in corners)
            {
                foreach (var p in new[] { a, b })
                {
                    var raised = MaxHeight(GetSurfaceDisplacement(surfaces[p]));
                    if (raised > ChiselDisplacement.kFlat)
                        reach.Add(point + (Vector3)inGenerator[p].xyz * raised);
                }
            }
            return reach.Count > 0 && ChiselPaintBrush.Grown(heights, reach, out grown);
        }

        // Where three planes meet; false when two of them are parallel
        static bool Corner(Unity.Mathematics.float4 a, Unity.Mathematics.float4 b, Unity.Mathematics.float4 c, out Unity.Mathematics.float3 point)
        {
            var bc = Unity.Mathematics.math.cross(b.xyz, c.xyz);
            var determinant = Unity.Mathematics.math.dot(a.xyz, bc);
            point = default;
            if (Mathf.Abs(determinant) < 1e-6f)
                return false;
            point = (-a.w * bc - b.w * Unity.Mathematics.math.cross(c.xyz, a.xyz) - c.w * Unity.Mathematics.math.cross(a.xyz, b.xyz)) / determinant;
            return true;
        }

        // What a displaced surface without a smoothing group of its own is smoothed in, one for every surface
        const uint kDisplacedSmoothingGroup = 0x80000000u;

        /// <summary>
        /// The surfaces the columns of a displaced surface are made with: their tops are smoothed together, in the
        /// surface's smoothing group or else one of their own. The generator's own face is not: smoothed only at its
        /// corners, it would spread a corner's bend over all of it.
        /// </summary>
        public ChiselSurfaceArray BrushSurfaces
        {
            get
            {
                var surfaces = SurfaceDefinition;
                if (surfaces?.surfaces == null || !IsDisplaced)
                    return surfaces;
                var copy = new ChiselSurfaceArray { surfaces = (ChiselSurface[])surfaces.surfaces.Clone() };
                foreach (var heights in surfaceDisplacement)
                {
                    var index = heights?.surfaceIndex ?? -1;
                    if (index < 0 || index >= copy.surfaces.Length || copy.surfaces[index] == null ||
                        copy.surfaces[index].surfaceDetails.smoothingGroup != 0)
                        continue;
                    var surface = copy.surfaces[index];
                    var details = surface.surfaceDetails;
                    details.smoothingGroup = kDisplacedSmoothingGroup | (uint)(index + 1);
                    copy.surfaces[index] = new ChiselSurface { chiselMaterial = surface.chiselMaterial, surfaceDetails = details, paintSpacing = surface.paintSpacing };
                }
                return copy;
            }
        }

        static float MaxHeight(ChiselSurfacePaint heights)
        {
            var max = 0f;
            if (heights == null || !heights.IsValid)
                return max;
            for (int i = 0; i < heights.PointCount; i++)
                max = Mathf.Max(max, heights[i].x);
            return max;
        }

        public void RemoveSurfaceDisplacement(int surfaceIndex)
        {
            if (surfaceDisplacement.RemoveAll(other => other == null || other.surfaceIndex == surfaceIndex) > 0)
                DisplacementChanged();
        }

        /// <summary>Called after the heights of a displaced surface were changed in place</summary>
        public void DisplacementChanged()
        {
            ExtendDisplacementGrids();
            if (!ValidNodes)
                return;
            // a displaced generator's node is a branch, its own brush and the columns, otherwise a brush
            if (IsDisplaced != (Node.Type == CSGNodeType.Branch))
            {
                DestroyChildTreeNodes();
                ChiselNodeHierarchyManager.RebuildTreeNodes(this);
                return;
            }
            SetDirty();
        }

        /// <summary>Whether a surface of this generator is displaced: it is made of more brushes than its own</summary>
        public bool IsDisplaced
        {
            get
            {
                if (surfaceDisplacement.Count == 0 || !SupportsDisplacement)
                    return false;
                foreach (var heights in surfaceDisplacement)
                {
                    if (heights == null || !heights.IsValid)
                        continue;
                    for (int i = 0; i < heights.PointCount; i++)
                    {
                        if (Mathf.Abs(heights[i].x) > ChiselDisplacement.kFlat)
                            return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Whether this generator's surfaces can be displaced (see <see cref="GetDisplacementOutline"/>)</summary>
        public virtual bool SupportsDisplacement => false;

        /// <summary>
        /// The planes of the generator's own brush, in its space and pointing out of it, and the surface each is of. The
        /// generator's brush mesh is made later, by jobs, so this is what its displacement is built from.
        /// </summary>
        protected virtual bool GetDisplacementOutline(System.Collections.Generic.List<Unity.Mathematics.float4> planes,
                                                      System.Collections.Generic.List<int> surfaces) => false;

        /// <summary>The brush the generator itself makes, without the columns of its displaced surfaces</summary>
        public CSGTreeBrush OwnBrush
        {
            get
            {
                if (!ValidNodes)
                    return CSGTreeBrush.Invalid;
                var branch = (CSGTreeBranch)Node;
                if (!branch.Valid)
                    return (CSGTreeBrush)Node;
                return branch.Count > 0 ? (CSGTreeBrush)branch[0] : CSGTreeBrush.Invalid;
            }
        }

        /// <summary>
        /// The node a generator of one brush starts as: that brush, or when displaced a branch with that brush first. The
        /// returned brush is the generator's own.
        /// </summary>
        protected CSGTreeBrush EnsureOwnBrush(in CSGTree tree, ref CSGTreeNode node, UnityEngine.EntityId entityId)
        {
            if (!IsDisplaced)
            {
                var brush = (CSGTreeBrush)node;
                if (!brush.Valid)
                {
                    if (node.Valid)
                        node.Destroy();
                    node = brush = tree.CreateBrush(entityId: entityId, operation: operation);
                }
                if (brush.Operation != operation)
                    brush.Operation = operation;
                return brush;
            }

            var branch = (CSGTreeBranch)node;
            if (!branch.Valid)
            {
                if (node.Valid)
                    node.Destroy();
                node = branch = tree.CreateBranch(entityId: entityId, operation: operation);
            }
            if (branch.Operation != operation)
                branch.Operation = operation;
            var own = branch.Count > 0 ? (CSGTreeBrush)branch[0] : CSGTreeBrush.Invalid;
            if (!own.Valid)
            {
                own = tree.CreateBrush(entityId: entityId, operation: CSGOperationType.Additive);
                branch.Insert(0, own);
            }
            return own;
        }

        /// <summary>
        /// The columns of the displaced surfaces, as the brushes after the own brush in the branch. The brush is the
        /// intersection of its faces' half spaces, and displaced it is the intersection of their displaced ones, which
        /// the exact CSG makes as it is:
        /// - for each displaced surface a composite of its raised columns, those reaching past a side that isn't displaced
        ///   cut to it, intersected with each other displaced surface's displaced half space (that surface's columns
        ///   again, on a brush up to just under it), so where two displaced surfaces meet each ends at the other
        /// - then what is cut, and what pushes the opposite side out
        /// </summary>
        protected void UpdateDisplacementColumns()
        {
            var branch = (CSGTreeBranch)Node;
            if (!branch.Valid || SurfaceDefinition == null)
                return;

            var outlinePlanes   = new System.Collections.Generic.List<Unity.Mathematics.float4>();
            var outlineSurfaces = new System.Collections.Generic.List<int>();
            // by displaced surface (in the order of the layout), its columns, its heights as they are made from them and
            // its normal
            var displaced = new System.Collections.Generic.List<int>();
            var columns   = new System.Collections.Generic.List<System.Collections.Generic.List<ChiselDisplacementColumn>>();
            var prepared  = new System.Collections.Generic.Dictionary<int, (ChiselSurfacePaint heights, Unity.Mathematics.float3 normal)>();
            if (GetDisplacementOutline(outlinePlanes, outlineSurfaces))
            {
                // a triangle beyond a side has nothing to displace, unless that side is displaced too: there it reaches
                // that side's displaced surface
                var endsAt = new bool[outlinePlanes.Count];
                for (int p = 0; p < endsAt.Length; p++)
                    endsAt[p] = !Displaced(outlineSurfaces[p]);
                foreach (var heights in surfaceDisplacement)
                {
                    if (heights == null || !heights.IsValid || displaced.Contains(heights.surfaceIndex))
                        continue;
                    var plane = outlineSurfaces.IndexOf(heights.surfaceIndex);
                    if (plane < 0)
                        continue;
                    var normal = outlinePlanes[plane].xyz;
                    // grown over what the neighbours reach, whether or not the grid stored was
                    var covering = Covering(heights, outlinePlanes, outlineSurfaces, out var grown) ? grown : heights;
                    var surfaceHeights = ChiselDisplacement.Prepared(InBrushSpace(covering), new Vector3(normal.x, normal.y, normal.z), outlinePlanes);
                    prepared[heights.surfaceIndex] = (surfaceHeights, Unity.Mathematics.math.normalize(normal));
                    var made = new System.Collections.Generic.List<ChiselDisplacementColumn>();
                    displacementCaches ??= new System.Collections.Generic.Dictionary<int, ChiselDisplacementCache>();
                    if (!displacementCaches.TryGetValue(heights.surfaceIndex, out var cache))
                        displacementCaches[heights.surfaceIndex] = cache = new ChiselDisplacementCache();
                    ChiselDisplacement.Columns(surfaceHeights, new Vector3(normal.x, normal.y, normal.z), outlinePlanes, made, prepared: true, endsAt: endsAt, cache: cache);
                    displaced.Add(heights.surfaceIndex);
                    columns.Add(made);
                }
            }

            var brushSurfaces      = BrushSurfaces;
            var columnSurfaceArray = WithWalls(brushSurfaces, out var wallsFrom);
            // what every column's brush is made with besides its own planes: a change to it makes them all again
            var madeWith = (uint)(columnSurfaceArray?.GetHashCode() ?? 0);
            EnsureDisplacementLayout(branch, displaced);

            var update = new ColumnUpdate
            {
                generator          = this,
                tree               = branch.Tree,
                entityId           = branch.EntityId,
                madeWith           = madeWith,
                wallsFrom          = wallsFrom,
                outlinePlanes      = outlinePlanes,
                outlineSurfaces    = outlineSurfaces,
                brushSurfaces      = brushSurfaces,
                columnSurfaceArray = columnSurfaceArray,
                prepared           = prepared
            };
            var lowered = new System.Collections.Generic.List<(ChiselDisplacementColumn, int)>();
            var pushed  = new System.Collections.Generic.List<(ChiselDisplacementColumn, int)>();
            for (int d = 0; d < displaced.Count; d++)
            {
                foreach (var column in columns[d])
                {
                    if (column.group == ChiselDisplacement.kLowered)
                        lowered.Add((column, displaced[d]));
                    else if (column.group == ChiselDisplacement.kPushed)
                        pushed.Add((column, displaced[d]));
                }
            }
            for (int d = 0; d < displaced.Count; d++)
            {
                var surface = displaced[d];
                var raised  = Group(columns[d], surface, ChiselDisplacement.kRaised);
                update.Sync(displacementLayout.raised[d], raised);
                for (int o = 0, i = 0; o < displaced.Count; o++)
                {
                    if (o == d)
                        continue;
                    var other = displacementLayout.others[d][i++];
                    update.Sync(other.under,   BoundingPlanes(outlinePlanes, outlineSurfaces, displaced[o]), displaced[o]);
                    update.Sync(other.raised,  Group(columns[o], displaced[o], ChiselDisplacement.kRaised));
                    update.Sync(other.lowered, Group(columns[o], displaced[o], ChiselDisplacement.kLowered));
                }
            }
            update.Sync(displacementLayout.lowered, lowered);
            update.Sync(displacementLayout.pushed,  pushed);

            ColumnsMade = update.brushes.Count;
            if (update.brushes.Count > 0)
                BrushMeshManager.ConvertBrushMeshesToBrushMeshInstances(update.brushes, update.meshes, update.surfaces);
        }

        // Whether a surface of this generator is displaced, by any height
        bool Displaced(int surfaceIndex)
        {
            var heights = GetSurfaceDisplacement(surfaceIndex);
            if (heights == null || !heights.IsValid)
                return false;
            for (int i = 0; i < heights.PointCount; i++)
            {
                if (Mathf.Abs(heights[i].x) > ChiselDisplacement.kFlat)
                    return true;
            }
            return false;
        }

        static System.Collections.Generic.List<(ChiselDisplacementColumn, int)> Group(System.Collections.Generic.List<ChiselDisplacementColumn> columns, int surface, int group)
        {
            var found = new System.Collections.Generic.List<(ChiselDisplacementColumn, int)>();
            foreach (var column in columns)
            {
                if (column.group == group)
                    found.Add((column, surface));
            }
            return found;
        }

        /// <summary>How far another displaced surface's plane is moved out in the brush a displaced half space stands on:
        /// past its highest point, in steps, so painting rarely changes that brush</summary>
        const float kBoundsStep = 0.25f;

        // The brush a displaced surface's (under's) displaced half space stands on: the generator's own, that surface's
        // face just under it, the other displaced faces moved out past all their columns. Its faces of displaced surfaces
        // are never output.
        Unity.Mathematics.float4[] BoundingPlanes(System.Collections.Generic.List<Unity.Mathematics.float4> outlinePlanes, System.Collections.Generic.List<int> outlineSurfaces, int under)
        {
            var planes = new Unity.Mathematics.float4[outlinePlanes.Count];
            for (int p = 0; p < planes.Length; p++)
            {
                var plane  = outlinePlanes[p];
                var length = Unity.Mathematics.math.length(plane.xyz);
                plane /= length;
                var surface = outlineSurfaces[p];
                if (surface == under)
                    plane.w += ChiselDisplacement.kOverlap;
                else if (GetSurfaceDisplacement(surface) is { } heights && heights.IsValid)
                    plane.w -= (Mathf.Floor((MaxHeight(heights) + ChiselDisplacement.kOverlap) / kBoundsStep) + 1) * kBoundsStep;
                planes[p] = plane;
            }
            return planes;
        }

        // How many column brushes the last update of the columns made, rather than kept
        internal int ColumnsMade { get; private set; }

        // A displaced generator's branch after its own brush, made again when the surfaces displaced change
        sealed class DisplacementLayout
        {
            public int[] surfaces;
            // by displaced surface, the composite of its raised columns, and the other displaced surfaces' displaced
            // half spaces they are intersected with
            public BrushSet[] raised;
            public OtherSurface[][] others;
            public BrushSet lowered, pushed;
        }

        // Another displaced surface's displaced half space, intersected with a surface's raised columns
        struct OtherSurface
        {
            public BrushSet under, raised, lowered;
        }

        // A composite's brushes, each with the key it was made for
        sealed class BrushSet
        {
            public CSGTreeBranch node;
            public System.Collections.Generic.List<(uint key, CSGTreeBrush brush)> brushes = new();
        }

        [System.NonSerialized] DisplacementLayout displacementLayout;
        // by displaced surface, the columns of its cells as the last update made them
        [System.NonSerialized] System.Collections.Generic.Dictionary<int, ChiselDisplacementCache> displacementCaches;

        // The composites of the layout for these displaced surfaces, made again when they changed
        void EnsureDisplacementLayout(CSGTreeBranch branch, System.Collections.Generic.List<int> displaced)
        {
            var count = displaced.Count;
            var whole = displacementLayout != null && displacementLayout.surfaces.Length == count &&
                        branch.Count == 1 + count + 2 && displacementLayout.lowered.node.Valid && displacementLayout.pushed.node.Valid;
            for (int d = 0; d < count && whole; d++)
                whole = displacementLayout.surfaces[d] == displaced[d] && displacementLayout.raised[d].node.Valid;
            if (whole)
                return;
            for (int i = branch.Count - 1; i >= 1; i--)
            {
                var child = branch[i];
                branch.RemoveAt(i);
                DestroyDeep(child);
            }
            var tree     = branch.Tree;
            var entityId = branch.EntityId;
            BrushSet Composite(CSGTreeBranch parent, CSGOperationType operation)
            {
                var node = tree.CreateBranch(entityId: entityId, operation: operation);
                parent.Add(node);
                return new BrushSet { node = node };
            }
            var layout = new DisplacementLayout
            {
                surfaces = displaced.ToArray(),
                raised   = new BrushSet[count],
                others   = new OtherSurface[count][]
            };
            for (int d = 0; d < count; d++)
            {
                var part = tree.CreateBranch(entityId: entityId, operation: CSGOperationType.Additive);
                branch.Add(part);
                // the columns first: what comes after them is intersected with them
                layout.raised[d] = Composite(part, CSGOperationType.Additive);
                layout.others[d] = new OtherSurface[count - 1];
                for (int o = 0; o < count - 1; o++)
                {
                    var half = tree.CreateBranch(entityId: entityId, operation: CSGOperationType.Intersecting);
                    part.Add(half);
                    layout.others[d][o] = new OtherSurface
                    {
                        under   = Composite(half, CSGOperationType.Additive),
                        raised  = Composite(half, CSGOperationType.Additive),
                        lowered = Composite(half, CSGOperationType.Subtractive)
                    };
                }
            }
            layout.lowered = Composite(branch, CSGOperationType.Subtractive);
            layout.pushed  = Composite(branch, CSGOperationType.Additive);
            displacementLayout = layout;
        }

        static void DestroyDeep(CSGTreeNode node)
        {
            if (node.Type == CSGNodeType.Branch)
            {
                var branch = (CSGTreeBranch)node;
                for (int i = branch.Count - 1; i >= 0; i--)
                    DestroyDeep(branch[i]);
            }
            node.Destroy();
        }

        // What one update of the columns makes: the brushes it created, their meshes and surfaces
        sealed class ColumnUpdate
        {
            public ChiselGeneratorComponent generator;
            public CSGTree tree;
            public UnityEngine.EntityId entityId;
            public uint madeWith;
            public int wallsFrom;
            public System.Collections.Generic.List<Unity.Mathematics.float4> outlinePlanes;
            public System.Collections.Generic.List<int> outlineSurfaces;
            public ChiselSurfaceArray brushSurfaces, columnSurfaceArray;
            public System.Collections.Generic.Dictionary<int, (ChiselSurfacePaint heights, Unity.Mathematics.float3 normal)> prepared;
            public readonly System.Collections.Generic.List<CSGTreeBrush> brushes = new();
            public readonly System.Collections.Generic.List<BrushMesh> meshes = new();
            public readonly System.Collections.Generic.List<ChiselSurfaceArray> surfaces = new();
            // a column's mesh, for every composite it is in
            readonly System.Collections.Generic.Dictionary<uint, BrushMesh> made = new();

            // The set's brushes made the columns: one made before keeps its brush as it is, the others are made, the ones
            // left over destroyed
            public void Sync(BrushSet set, System.Collections.Generic.List<(ChiselDisplacementColumn column, int surface)> wanted)
            {
                var old = new System.Collections.Generic.Dictionary<uint, CSGTreeBrush>();
                foreach (var (key, brush) in set.brushes)
                {
                    if (!brush.Valid)
                        continue;
                    if (!old.TryAdd(key, brush))
                        brush.Destroy();
                }
                var kept = new System.Collections.Generic.List<(uint, CSGTreeBrush)>();
                foreach (var (column, surface) in wanted)
                {
                    var key = Unity.Mathematics.math.hash(new Unity.Mathematics.uint3(column.key, madeWith, (uint)surface));
                    if (old.Remove(key, out var existing))
                    {
                        if (existing.Contents != generator.contents)
                            existing.Contents = generator.contents;
                        kept.Add((key, existing));
                        continue;
                    }
                    if (!made.TryGetValue(key, out var mesh))
                    {
                        mesh = ChiselDisplacement.ColumnMesh(column);
                        if (mesh != null && mesh.polygons != null && mesh.polygons.Length >= BrushMesh.kMinimumPolygons)
                        {
                            mesh.halfEdgeUVs     = FlatUVs(mesh, column, surface, outlinePlanes, outlineSurfaces, brushSurfaces);
                            mesh.halfEdgeNormals = SurfaceNormals(mesh, column, prepared[surface]);
                            // CreateFromPlanes numbers a face by its plane: the column's top and bottom are of the displaced
                            // surface, its walls of the surface's copy that isn't smoothed, and the brush's sides it is cut to
                            // keep theirs
                            for (int p = 0; p < mesh.polygons.Length; p++)
                                mesh.polygons[p].descriptionIndex = SurfaceOfColumnFace(mesh.polygons[p].descriptionIndex, column, surface, outlineSurfaces, wallsFrom);
                        } else
                            mesh = null;
                        made[key] = mesh;
                    }
                    if (mesh == null)
                        continue;
                    Add(set, key, mesh, kept);
                }
                foreach (var gone in old.Values)
                    gone.Destroy();
                set.brushes = kept;
            }

            // The set's one brush, made from these planes (the outline's, by its surfaces): only made again when they
            // change. Its faces of displaced surfaces, and the one of under, are never output.
            public void Sync(BrushSet set, Unity.Mathematics.float4[] planes, int under)
            {
                var key = madeWith;
                foreach (var plane in planes)
                    key = Unity.Mathematics.math.hash(new Unity.Mathematics.uint2(key, Unity.Mathematics.math.hash(plane)));
                key = Unity.Mathematics.math.hash(new Unity.Mathematics.uint2(key, (uint)under));
                if (set.brushes.Count == 1 && set.brushes[0].key == key && set.brushes[0].brush.Valid)
                    return;
                foreach (var (_, brush) in set.brushes)
                {
                    if (brush.Valid)
                        brush.Destroy();
                }
                set.brushes = new System.Collections.Generic.List<(uint, CSGTreeBrush)>();
                var bounds = new Bounds();
                foreach (var plane in planes)
                    bounds.Encapsulate(new Vector3(plane.x, plane.y, plane.z) * -plane.w);
                bounds.Expand(bounds.size.magnitude + 1);
                var temporarySurfaces = new ChiselSurfaceArray();
                BrushMeshFactory.CreateFromPlanes(planes, bounds, ref temporarySurfaces, out var mesh);
                if (mesh == null || mesh.polygons == null || mesh.polygons.Length < BrushMesh.kMinimumPolygons)
                    return;
                for (int p = 0; p < mesh.polygons.Length; p++)
                {
                    var plane   = mesh.polygons[p].descriptionIndex;
                    var surface = plane >= 0 && plane < outlineSurfaces.Count ? outlineSurfaces[plane] : 0;
                    var hidden  = surface == under || generator.GetSurfaceDisplacement(surface) is { } heights && heights.IsValid;
                    mesh.polygons[p].descriptionIndex = hidden && wallsFrom >= 0 ? 2 * wallsFrom + surface : surface;
                }
                Add(set, key, mesh, set.brushes);
            }

            void Add(BrushSet set, uint key, BrushMesh mesh, System.Collections.Generic.List<(uint, CSGTreeBrush)> into)
            {
                // within a composite the brushes are all added (or all taken away) together: their order doesn't count
                var created = tree.CreateBrush(entityId: entityId, operation: CSGOperationType.Additive);
                created.Contents = generator.contents;
                set.node.Add(created);
                into.Add((key, created));
                brushes.Add(created);
                meshes.Add(mesh);
                surfaces.Add(columnSurfaceArray);
            }
        }


        // The surface of a face of a column (numbered by its plane: its walls, then its top and its bottom). Its walls take
        // the copy of the displaced surface that is drawn but isn't smoothed with it (from wallsFrom on): two neighbours'
        // tops are planes of their own, which the exact CSG makes meet a hair apart, with a sliver of the wall between
        // them that closes the surface, and the sliver has to be drawn. Its other face that is never seen, inside the
        // generator's own brush or past the surface, takes the copy that is never output (from 2 * wallsFrom on), which
        // the CSG does no work on and smoothing doesn't count.
        static int SurfaceOfColumnFace(int plane, in ChiselDisplacementColumn column, int surface,
                                       System.Collections.Generic.List<int> outlineSurfaces, int wallsFrom)
        {
            if (plane >= column.ownPlanes && column.sides != null && plane - column.ownPlanes < column.sides.Length)
                return outlineSurfaces[column.sides[plane - column.ownPlanes]];
            if (plane >= 0 && plane < column.ownPlanes && plane != column.seenFace && wallsFrom >= 0)
                return plane < column.ownPlanes - 2 ? wallsFrom + surface : 2 * wallsFrom + surface;
            return surface;
        }

        // The surfaces with, after them, a copy of each in no smoothing group, then a copy of each never output
        static ChiselSurfaceArray WithWalls(ChiselSurfaceArray surfaces, out int wallsFrom)
        {
            wallsFrom = -1;
            if (surfaces?.surfaces == null)
                return surfaces;
            var count = surfaces.surfaces.Length;
            var all   = new ChiselSurface[count * 3];
            for (int s = 0; s < count; s++)
            {
                all[s] = surfaces.surfaces[s];
                var surface = surfaces.surfaces[s];
                if (surface == null)
                    continue;
                var details = surface.surfaceDetails;
                details.smoothingGroup = 0;
                all[count + s]     = new ChiselSurface { chiselMaterial = surface.chiselMaterial, surfaceDetails = details, paintSpacing = surface.paintSpacing };
                all[2 * count + s] = new ChiselSurface { chiselMaterial = surface.chiselMaterial, surfaceDetails = details, paintSpacing = surface.paintSpacing, excluded = true };
            }
            wallsFrom = count;
            return new ChiselSurfaceArray { surfaces = all };
        }

        // The uvs of a column's corners: what its surface has, flat, where the corner is above or below it, so the
        // displaced surface's texture is the flat one's moved along with it
        static Unity.Mathematics.float2[] FlatUVs(BrushMesh mesh, in ChiselDisplacementColumn column, int columnSurface,
                                                  System.Collections.Generic.List<Unity.Mathematics.float4> outlinePlanes,
                                                  System.Collections.Generic.List<int> outlineSurfaces, ChiselSurfaceArray surfaces)
        {
            var uvs = new Unity.Mathematics.float2[mesh.halfEdges.Length];
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                var polygon = mesh.polygons[p];
                var surface = SurfaceOfColumnFace(polygon.descriptionIndex, column, columnSurface, outlineSurfaces, -1);
                var at      = outlineSurfaces.IndexOf(surface);
                var plane   = at >= 0 ? outlinePlanes[at] : mesh.planes[p];
                var length  = Unity.Mathematics.math.length(plane.xyz);
                if (length > 0)
                    plane /= length;
                var uv0     = surfaces?.surfaces != null && surface >= 0 && surface < surfaces.surfaces.Length && surfaces.surfaces[surface] != null
                                ? surfaces.surfaces[surface].surfaceDetails.UV0 : UVMatrix.Identity;
                var toUV    = Unity.Mathematics.math.mul(uv0.ToFloat4x4(), MathExtensions.GenerateLocalToPlaneSpaceMatrix(plane));
                for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                {
                    var vertex = mesh.vertices[mesh.halfEdges[e].vertexIndex];
                    var onFlat = vertex - plane.xyz * (Unity.Mathematics.math.dot(plane.xyz, vertex) + plane.w);
                    uvs[e] = Unity.Mathematics.math.mul(toUV, new Unity.Mathematics.float4(onFlat, 1)).xy;
                }
            }
            return uvs;
        }

        // The normals of a column's corners: on the face of it that is seen, the displaced surface's at that point (from
        // its heights, ChiselDisplacement.NormalAt); on the others their plane's. Each faces the way its plane does.
        static Unity.Mathematics.float3[] SurfaceNormals(BrushMesh mesh, in ChiselDisplacementColumn column,
                                                         (ChiselSurfacePaint heights, Unity.Mathematics.float3 normal) surface)
        {
            var normals = new Unity.Mathematics.float3[mesh.halfEdges.Length];
            for (int p = 0; p < mesh.polygons.Length; p++)
            {
                var polygon = mesh.polygons[p];
                var facing  = Unity.Mathematics.math.normalizesafe(mesh.planes[p].xyz);
                var seen    = polygon.descriptionIndex == column.seenFace;
                for (int e = polygon.firstEdge; e < polygon.firstEdge + polygon.edgeCount; e++)
                {
                    if (!seen)
                    {
                        normals[e] = facing;
                        continue;
                    }
                    var normal = ChiselDisplacement.NormalAt(surface.heights, surface.normal, mesh.vertices[mesh.halfEdges[e].vertexIndex]);
                    normals[e] = Unity.Mathematics.math.dot(normal, facing) < 0 ? -normal : normal;
                }
            }
            return normals;
        }

        // The grid is in the generator's space, its brushes are that moved by the pivot offset
        ChiselSurfacePaint InBrushSpace(ChiselSurfacePaint heights)
        {
            if (pivotOffset == Vector3.zero)
                return heights;
            return new ChiselSurfacePaint
            {
                surfaceIndex = heights.surfaceIndex,
                attribute    = heights.attribute,
                components   = heights.components,
                defaultValue = heights.defaultValue,
                origin       = heights.origin - pivotOffset,
                cellU        = heights.cellU,
                cellV        = heights.cellV,
                resolution   = heights.resolution,
                spacing      = heights.spacing,
                values       = heights.values
            };
        }

        /// <summary>How far the generator's own brush has a surface moved in: a displaced surface is drawn by its columns
        /// all over, so the brush's own face lies just under them, out of their way</summary>
        protected float DisplacedInset(int surfaceIndex)
        {
            if (!IsDisplaced)
                return 0;
            var heights = GetSurfaceDisplacement(surfaceIndex);
            return heights != null && heights.IsValid ? ChiselDisplacement.kOverlap : 0;
        }

        /// <summary>The surfaces the generator's own brush is made with: its face of a displaced surface lies under the
        /// columns all over, so it is never output, and the CSG does no work on it</summary>
        internal ChiselSurfaceArray OwnBrushSurfaces
        {
            get
            {
                var surfaces = SurfaceDefinition;
                if (surfaces?.surfaces == null || !IsDisplaced)
                    return surfaces;
                ChiselSurfaceArray copy = null;
                foreach (var heights in surfaceDisplacement)
                {
                    var index = heights != null && heights.IsValid ? heights.surfaceIndex : -1;
                    if (index < 0 || index >= surfaces.surfaces.Length || surfaces.surfaces[index] == null)
                        continue;
                    copy ??= new ChiselSurfaceArray { surfaces = (ChiselSurface[])surfaces.surfaces.Clone() };
                    var surface = surfaces.surfaces[index];
                    copy.surfaces[index] = new ChiselSurface { chiselMaterial = surface.chiselMaterial, surfaceDetails = surface.surfaceDetails, paintSpacing = surface.paintSpacing, excluded = true };
                }
                return copy ?? surfaces;
            }
        }

        // Which surfaces are displaced, which the generator's own brush is made with
        int DisplacedSurfaces()
        {
            if (!IsDisplaced)
                return 0;
            var hash = 17;
            foreach (var heights in surfaceDisplacement)
            {
                if (heights != null && heights.IsValid)
                    hash = System.HashCode.Combine(hash, heights.surfaceIndex);
            }
            return hash;
        }

        int DisplacementHash()
        {
            var hash = pivotOffset.GetHashCode();
            foreach (var heights in surfaceDisplacement)
            {
                if (heights == null || heights.values == null)
                    continue;
                hash = System.HashCode.Combine(hash, heights.surfaceIndex, heights.origin, heights.cellU, heights.cellV, heights.resolution);
                foreach (var value in heights.values)
                    hash = System.HashCode.Combine(hash, value);
            }
            return hash;
        }

        public override CSGTreeNode TopTreeNode 
        { 
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get 
            {
                if (!ValidNodes) return CSGTreeNode.Invalid;
                return Node; 
            } 
            [MethodImpl(MethodImplOptions.AggressiveInlining)] protected set 
            { 
                Node = value; 
            } 
        }
        
        bool ValidNodes 
        { 
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get 
            { 
                return Node.Valid; 
            } 
        }
        

        public CSGOperationType Operation
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return operation;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value == operation)
                    return;
                operation = value;

                if (ValidNodes)
                    Node.Operation = operation;

                // Let the hierarchy manager know that the contents of this node has been modified
                //	so we can rebuild/update sub-trees and regenerate meshes
                ChiselNodeHierarchyManager.NotifyContentsModified(this);
            }
        }

        public int Contents
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return contents;
            }
            set
            {
                if (value == contents)
                    return;
                contents = value;

                if (ValidNodes)
                    ApplyContents();

                ChiselNodeHierarchyManager.NotifyContentsModified(this);
            }
        }

        // A brush takes the index directly. A generator that makes several brushes hands it to every brush it
        // generates, which it does whenever it regenerates them.
        void ApplyContents()
        {
            var brush = OwnBrush;
            if (brush.Valid && brush.Contents != contents)
                brush.Contents = contents;
        }

        public Vector3 PivotOffset
        {
            get
            {
                return pivotOffset;
            }
            set
            {
                if (value == pivotOffset)
                    return;
                pivotOffset = value;

                // Let the hierarchy manager know that this node has moved, so we can regenerate meshes
                ChiselNodeHierarchyManager.UpdateTreeNodeTransformation(this);
            }
        }

        public Matrix4x4 PivotTransformation
        {
            get
            {
                // TODO: clean this up

                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    return Matrix4x4.TRS(pivotOffset, Quaternion.identity, Vector3.one);
                return Matrix4x4.identity;
            }
        }

        public Matrix4x4 InversePivotTransformation
        {
            get
            {
                // TODO: clean this up

                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    return Matrix4x4.TRS(-pivotOffset, Quaternion.identity, Vector3.one);
                return Matrix4x4.identity;
            }
        }

        public Matrix4x4 LocalPivotTransformation
        {
            get
            {
                // TODO: Optimize
                var transform       = hierarchyItem.Transform;

                var localTransformation = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
                // We need to add the pivot to it. This is here so that when we change the pivot we do 
                // not actually need to modify meshes of brushes.
                if (pivotOffset.x != 0 || pivotOffset.y != 0 || pivotOffset.z != 0)
                    localTransformation *= Matrix4x4.TRS(pivotOffset, Quaternion.identity, Vector3.one);

                return localTransformation;
            }
        }

        public Matrix4x4 LocalPivotTransformationWithHiddenParents
        {
            get
            {
                // TODO: Optimize
                var transform = hierarchyItem.Transform;

                var localTransformation = LocalPivotTransformation;

                // We can't just use the transformation of this brush, because it might have gameobjects as parents that 
                // do not have any chisel components. So we need to consider all transformations in between as well.
                do
                {
                    transform = transform.parent;
                    if (transform == null)
                        break;

                    // If we find a ChiselNode we continue, unless it's a Composite set to passthrough
                    if (transform.TryGetComponent<ChiselNodeComponent>(out var component))
                    {
                        var composite = component as ChiselCompositeComponent;
                        if (composite == null || !composite.PassThrough)
                            break;
                    }

                    localTransformation = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale) * localTransformation;
                } while (true);

                return localTransformation;
            }
        }

        public Matrix4x4 GlobalTransformation
        {
            get
            {
                // TODO: Optimize
                var transform = hierarchyItem.Transform;

                var localTransformation = LocalPivotTransformation;
                var parentTransform = transform.parent;
                if (parentTransform != null)
                    localTransformation = parentTransform.localToWorldMatrix * localTransformation;

                return localTransformation;
            }
        }

        public abstract ChiselSurface GetSurface(int descriptionIndex);
        public abstract SurfaceDetails GetSurfaceDetails(int descriptionIndex);
        public abstract void SetSurfaceDetails(int descriptionIndex, SurfaceDetails description);
        public abstract UVMatrix GetSurfaceUV0(int descriptionIndex);
        public abstract void SetSurfaceUV0(int descriptionIndex, UVMatrix uv0);


        // TODO: improve warning messages
        const string kFailedToGenerateNodeMessage = "Failed to generate internal representation of generator (this should never happen)";
		const string kBrushNodeIsInvalidMessage = "The internal brush representation is invalid.";
		const string kGeneratorIsPartOfDefaultModel = "This generator is part of the default model, please place it underneath a GameObject with a " + ChiselModelComponent.kNodeTypeName + " component";

		// Will show a warning icon in hierarchy when generator has a problem (do not make this method slow, it is called a lot!)
		public override void GetMessages(IChiselMessageHandler messages)
        {
			if (!ValidNodes)
            {
                if (this is ChiselBrushComponent)
                    messages.Warning(kBrushNodeIsInvalidMessage);
                else
				    messages.Warning(kFailedToGenerateNodeMessage);
				return;
            }

            var brush = OwnBrush;
            if (brush.Valid && brush.BrushMesh == BrushMeshInstance.InvalidInstance)
			{
				if (this is ChiselBrushComponent)
					messages.Warning(kBrushNodeIsInvalidMessage);
				else
					messages.Warning(kFailedToGenerateNodeMessage);
				return;
            }

            if (ChiselModelManager.Instance.IsDefaultModel(hierarchyItem.Model))
            {
                messages.Warning(kGeneratorIsPartOfDefaultModel);
            }
		}

		protected override void OnValidateState()
        {
            if (!ValidNodes)
            {
                ChiselNodeHierarchyManager.RebuildTreeNodes(this);
                return;
            }

            if (SupportsDisplacement && IsDisplaced != (Node.Type == CSGNodeType.Branch))
            {
                DestroyChildTreeNodes();
                ChiselNodeHierarchyManager.RebuildTreeNodes(this);
                return;
            }

            if (TopTreeNode.Dirty)
                ChiselNodeHierarchyManager.NotifyContentsModified(this);
            ChiselPaintManager.SetDirty(this);
            base.OnValidateState();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ChiselPaintManager.SetDirty(this);
        }

        public override void UpdateTransformation()
        {
            // TODO: recalculate transformation based on hierarchy up to (but not including) model
            var transform = hierarchyItem.Transform;
            if (!transform)
                return;
            
            UpdateInternalTransformation();
        }

        void UpdateInternalTransformation()
        {
            Node.LocalTransformation = LocalPivotTransformationWithHiddenParents;
        }

        protected override void OnResetInternal()
        {
            UpdateBrushMeshInstances();
            base.OnResetInternal();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DestroyChildTreeNodes();
            ChiselPaintManager.SetDirty(this);
        }

        [HideInInspector] int prevMaterialHash;
        [HideInInspector] UnityEngine.Hash128 prevDefinitionHash;
        [HideInInspector] int prevContents = -1;
        [HideInInspector] int prevDisplacementHash;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearHashes()
        {
            prevMaterialHash = 0;
            prevDefinitionHash = default;
            prevContents = -1;
            prevDisplacementHash = 0;
#if UNITY_EDITOR
			ChiselUnityVisibilityManager.EnsureVisibilityInitialized(this); 
#endif
        }

        public override void UpdateGeneratorNodes()
        {
            if (!Node.Valid)
            {
                var model = this.hierarchyItem.Model;
                if (model == null)
                {
                    ClearHashes();
                    return;
                }

                var treeRoot = model.Node;
                var entityId = GetEntityId();
                if (EnsureTopNodeCreatedInternal(in treeRoot, ref Node, entityId: entityId))
                    ClearHashes();

                if (!Node.Valid)
                    return;
            }

            UpdateMeshesWhenModified();

            if (ValidNodes)
            {
                UpdateBrushMeshInstances();

                if (Node.Operation != operation)
                    Node.Operation = operation;
            }
        }

        internal override CSGTreeNode RebuildTreeNodes()
        {
            if (Node.Valid)
                Debug.LogWarning(this.GetType().Name + " already has a treeNode, but trying to create a new one?", this);

            ClearHashes();
            UpdateGeneratorNodes();

            if (!ValidNodes)
                return default;
            return Node;
        }

        void UpdateMeshesWhenModified()
        {
            // a displaced generator's branch that lost its own brush (an undo can destroy it) is made again, columns and all
            if (Node.Type == CSGNodeType.Branch && SupportsDisplacement && !OwnBrush.Valid)
            {
                DestroyChildTreeNodes();
                ChiselNodeHierarchyManager.RebuildTreeNodes(this);
                return;
            }
            var currMaterialHash    = SurfaceDefinition?.GetHashCode() ?? 0;
            // the generator's own brush is made with its displaced surfaces moved in (DisplacedInset)
            var currDefinitionHash  = GetDefinitionInputHash();
            currDefinitionHash.Append(DisplacedSurfaces());
            var currDisplacementHash = Node.Type == CSGNodeType.Branch ? DisplacementHash() : 0;
            var ownChanged     = prevMaterialHash != currMaterialHash || prevDefinitionHash != currDefinitionHash ||
                                 Node.Operation != operation || prevContents != contents;
            var columnsChanged = prevDisplacementHash != currDisplacementHash;
            if (ownChanged || columnsChanged)
            {
                prevMaterialHash    = currMaterialHash;
                prevDefinitionHash  = currDefinitionHash;
                prevDisplacementHash = currDisplacementHash;

                if (Node.Operation != operation)
                {
                    Node.Operation = operation;
                    // Let the hierarchy manager know that the contents of this node has been modified
                    //	so we can rebuild/update sub-trees and regenerate meshes
                    ChiselNodeHierarchyManager.NotifyContentsModified(this);
                }

                if (prevContents != contents)
                {
                    prevContents = contents;
                    ApplyContents();
                    ChiselNodeHierarchyManager.NotifyContentsModified(this);
                }

                // a change to the displaced surfaces' heights alone leaves the generator's own brush as it is
                var treeRoot = this.hierarchyItem.Model.Node;
                if (ownChanged)
                    UpdateGeneratorNodesInternal(in treeRoot, ref Node);
                if (Node.Type == CSGNodeType.Branch && SupportsDisplacement)
                    UpdateDisplacementColumns();
            }
        }

        public override void SetDirty()
        {
            if (!ValidNodes)
                return;

            TopTreeNode.SetDirty();
            UpdateMeshesWhenModified();
        }


        internal override void AddPivotOffset(Vector3 worldSpaceDelta)
        {
            var transform = hierarchyItem.Transform;
            var localSpaceDelta = transform.worldToLocalMatrix.MultiplyVector(worldSpaceDelta);
            if (localSpaceDelta.x == 0 &&
                localSpaceDelta.y == 0 &&
                localSpaceDelta.z == 0)
                return;
            PivotOffset += localSpaceDelta;
            UpdateInternalTransformation();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void UpdateBrushMeshInstances()
        {
            // Update the Node (if it exists)
            if (!ValidNodes)
                return;

            ChiselNodeHierarchyManager.RebuildTreeNodes(this);
            SetDirty();
        }

        /// <summary>
        /// A hash of what this generator's definition makes its nodes from (see
        /// <see cref="IChiselNodeGenerator.GetInputHash"/>). It decides whether the brush meshes have to be made
        /// again, and it is what <see cref="ChiselStagedInputHashes"/> asks for as well, so both answer the same
        /// question the same way.
        /// </summary>
        public abstract UnityEngine.Hash128 GetDefinitionInputHash();

        protected abstract bool EnsureTopNodeCreatedInternal(in CSGTree tree, ref CSGTreeNode node, UnityEngine.EntityId entityId);
        protected abstract void UpdateGeneratorNodesInternal(in CSGTree tree, ref CSGTreeNode node);
	}
}
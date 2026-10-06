using System;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    [Serializable]
    public class ChiselBrushDefinition : IChiselNodeGenerator
    {
        public const string kNodeTypeName = "Brush";

        const int kLatestVersion = 1;
        [HideInInspector]
        [SerializeField] int version = 0;
        
        // TODO: avoid storing surfaceDefinition and surfaces in brushOutline twice, which is wasteful and causes potential conflicts
        [HideInInspector]
        public BrushMesh        brushOutline;

        [HideInInspector]
        public float4[]         inputPlanes;
        // the planes the outline was derived from: a changed or newly given brush derives it again, an unchanged one not
        [HideInInspector]
        [SerializeField] int    derivedFromHash;

        public bool IsGivenAsPlanes => inputPlanes != null && inputPlanes.Length > 0;

        // Gives the brush as planes, in its own space; its surfaces are then one per plane, in this order
        public void SetPlanes(float4[] planes)
        {
            inputPlanes     = planes == null ? null : (float4[])planes.Clone();
            brushOutline    = null;
            derivedFromHash = 0;
            ResetValidState();
        }


        [HideInInspector]
        [SerializeField] bool   isInsideOut = false;
        [HideInInspector]
        [SerializeField] bool   validState = true;

        // TODO: clean this mess up
        public void ResetValidState() 
        {
			validState = true;
			isInsideOut = false; 
        } 
		public BrushMesh BrushOutline
        {
            get { return brushOutline; }
            set
            {
                if (brushOutline == value)
                    return;
				ResetValidState();
				brushOutline = value;
				inputPlanes = null;     // an outline given from outside (an edit): the brush is its outline from now on
			}
        }

		public bool ValidState  { get { return validState; } }
        public bool IsInsideOut { get { return isInsideOut; } }

		string errorMessage = null;

		public bool IsValid
        {
            get
            {
                return brushOutline != null &&
                       brushOutline.vertices != null &&
                       brushOutline.polygons != null &&
                       brushOutline.halfEdges != null &&
                       brushOutline.vertices.Length > 0 &&
                       brushOutline.polygons.Length > 0 &&
                       brushOutline.halfEdges.Length > 0 &&
                       ValidState;
            }
        }

        public void Reset()
        {
			ResetValidState();
            brushOutline = null;
            inputPlanes = null;
		}

        public bool EnsurePlanarPolygons()
        {
            if (!IsValid)
                return false;

            // Split non planar polygons into convex pieces
            return brushOutline.SplitNonPlanarPolygons();
        }

        public int RequiredSurfaceCount { get { return IsGivenAsPlanes ? inputPlanes.Length : (brushOutline?.polygons?.Length ?? 0); } }

        public void UpdateSurfaces(ref ChiselSurfaceArray surfaceDefinition)
        {
            if (surfaceDefinition.surfaces == null ||
                surfaceDefinition.surfaces.Length == 0)
                return;

            // given as planes: each face already has its plane's index, and the surfaces are one per plane
            if (IsGivenAsPlanes)
                return;

            for (int p = 0; p < brushOutline.polygons.Length; p++)
                brushOutline.polygons[p].descriptionIndex = p;
        }

		bool ValidatePlanes()
		{
			if (version != kLatestVersion)
				version = kLatestVersion;
			int hash = HashOf(inputPlanes);
			if (brushOutline == null || derivedFromHash != hash)
			{
				if (!ExactBrushOutline.FromPlanes(inputPlanes, out var outline, out var problem))
				{
					errorMessage = "Brush given as " + inputPlanes.Length + " planes: " + problem;
					Debug.LogError(errorMessage);
					brushOutline = null;
					validState = false;
					return false;
				}
				brushOutline = outline;
				derivedFromHash = hash;
			}
			isInsideOut = false;
			return validState;
		}

		static int HashOf(float4[] planes)
		{
			unchecked
			{
				uint hash = 2166136261;
				for (int p = 0; p < planes.Length; p++)
				{
					var bits = math.asuint(planes[p]);
					hash = (hash ^ bits.x) * 16777619; hash = (hash ^ bits.y) * 16777619;
					hash = (hash ^ bits.z) * 16777619; hash = (hash ^ bits.w) * 16777619;
				}
				return hash == 0 ? 1 : (int)hash;     // 0 is "never derived"
			}
		}

		public bool Validate()
		{
			try
			{
				if (IsGivenAsPlanes)
					return ValidatePlanes();
				if (!IsValid)
                    return false;

			    errorMessage = string.Empty;
                if (version != kLatestVersion)
                    version = kLatestVersion;

				if (!brushOutline.ValidateData(out errorMessage))
                {
                    Debug.LogError(errorMessage);
                    validState = false;
                    return false;
				}

				// Generators give planes: an outline that carries its planes keeps them (the editor's tools fit them again
				// whenever they change the outline); only one without planes gets them fitted
				if (brushOutline.planes == null || brushOutline.planes.Length != brushOutline.polygons.Length)
					brushOutline.CalculatePlanes();

				// If the brush is concave, we set the generator to not be valid, so that when we commit, it will be reverted
				if (!brushOutline.ValidateShape(out errorMessage))
				{
					Debug.LogError(errorMessage);
					validState = false;
					return false;
				}

				// TODO: shouldn't do this all the time:
				{
                    // Detect if outline is inside-out and if so, just invert all polygons.
                    isInsideOut = brushOutline.IsInsideOut();
                    if (isInsideOut)
                    {
                        brushOutline.Invert();
                        isInsideOut = false;
                    }
                     
                }
                return true;
            }
            catch (Exception ex)
            {
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = ex.ToString();
				}
                throw ex;
            }
        }

        /*
        public bool Generate(ref ChiselBrushContainer brushContainer)
        {
            Profiler.BeginSample("GenerateBrush");
            try
            {
                if (!IsValid)
                    return false;

                Profiler.BeginSample("EnsureSize");
                brushContainer.EnsureSize(1);
                Profiler.EndSample();

                Profiler.BeginSample("new_BrushMesh");
                BrushMesh brushMesh;
                if (brushContainer.brushMeshes[0] == null)
                {
                    brushMesh = new BrushMesh(brushOutline);
                    brushContainer.brushMeshes[0] = brushMesh;
                } else
                {
                    brushContainer.brushMeshes[0].CopyFrom(brushOutline);
                    brushMesh = brushContainer.brushMeshes[0];
                }
                Profiler.EndSample();

                Profiler.BeginSample("Definition.Validate");
                Validate();
                Profiler.EndSample();

                Profiler.BeginSample("Assign Materials");
                for (int p = 0; p < brushMesh.polygons.Length; p++)
                    brushMesh.polygons[p].surface = surfaceDefinition.surfaces[p];
                Profiler.EndSample();

                Profiler.BeginSample("BrushMesh.Validate");
                var valid = brushMesh.Validate();
                Profiler.EndSample();
                return valid;
            }
            finally
            {
                Profiler.EndSample();
            }
        }*/

        public UnityEngine.Hash128 GetInputHash()
        {
            var hash    = new UnityEngine.Hash128();
            var version = kLatestVersion;
            var state   = (isInsideOut ? 1 : 0) | (validState ? 2 : 0);
            hash.Append(ref version);
            hash.Append(ref state);
            if (brushOutline == null ||
                brushOutline.vertices == null ||
                brushOutline.halfEdges == null ||
                brushOutline.polygons == null)
            {
                var nothing = -1;
                hash.Append(ref nothing);
                return hash;
            }
            hash.Append(brushOutline.vertices);
            hash.Append(brushOutline.halfEdges);
            // A polygon holds a surface, which is a reference: only the shape is hashed here, the surfaces are
            // hashed with the component that owns them
            var polygonCount = brushOutline.polygons.Length;
            hash.Append(ref polygonCount);
            for (int p = 0; p < polygonCount; p++)
            {
                var polygon = brushOutline.polygons[p];
                hash.Append(ref polygon.firstEdge);
                hash.Append(ref polygon.edgeCount);
                hash.Append(ref polygon.descriptionIndex);
            }
            return hash;
        }

        public void OnEdit(IChiselHandles handles)
        {
        }

		public void GetMessages(IChiselMessageHandler messages)
        {
            if (!IsValid || !ValidState)
			{
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    if (messages.Destination == MessageDestination.Hierarchy)
                        messages.Warning("The brush is in an invalid state. View brush in inspector for more details.");
                    else
						messages.Warning(errorMessage);
                }
			}
		}
    }
} 

using System;
using System.Collections.Generic;
using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace Chisel.Editors
{

    static class ChiselSelectionManager
    {
        [UnityEditor.InitializeOnLoadMethod]
        static void Initialize()
        {
            Selection.selectionChanged -= UpdateSelection;
            Selection.selectionChanged += UpdateSelection;
            UpdateSelection();
        }

        public static Action NodeOperationUpdated;
        public static Action OperationNodesSelectionUpdated;
        public static Action GeneratorSelectionUpdated;

        static bool s_CurrHaveOperationNodes = false;
        static bool s_CurrHaveGenerators = false;
        static CSGOperationType? s_CurrOperation = null;
        static readonly List<ChiselNodeComponent> s_Nodes = new();
        static readonly List<IChiselHasOperation> s_OperationNodes = new();
        static readonly List<ChiselGeneratorComponent> s_Generators = new();

        static void UpdateSelection()
        {
            s_Nodes.Clear();
            s_Nodes.AddRange(Selection.GetFiltered<ChiselNodeComponent>(SelectionMode.DeepAssets));
            s_Generators.Clear();
            s_OperationNodes.Clear();
            foreach (var node in s_Nodes)
            {
                if (node is ChiselGeneratorComponent generator)
                {
                    s_Generators.Add(generator);
                    s_OperationNodes.Add(generator);
                } else if (node is IChiselHasOperation hasOperation)
                {
                    s_OperationNodes.Add(hasOperation);
                }
            }

            var prevOperation = s_CurrOperation;
            UpdateOperationSelection();

            var prevHaveGenerators = s_CurrHaveGenerators;
            s_CurrHaveGenerators = s_Generators.Count > 0;

            var prevHaveOperationNodes = s_CurrHaveOperationNodes;
            s_CurrHaveOperationNodes = s_OperationNodes.Count > 0;

            if (prevHaveGenerators || s_CurrHaveGenerators)
                GeneratorSelectionUpdated?.Invoke();

            if (prevHaveOperationNodes || s_CurrHaveOperationNodes)
                OperationNodesSelectionUpdated?.Invoke();

            if (prevOperation != s_CurrOperation)
                NodeOperationUpdated?.Invoke();
        }


        // TODO: needs to be called when any operation changes, anywhere
        public static void UpdateOperationSelection()
        {
            s_CurrOperation = null;
            bool found = false;
            foreach (var operationNode in s_OperationNodes)
            {
                if (!found)
                {
                    s_CurrOperation = operationNode.Operation;
                    found = true;
                } else
                if (s_CurrOperation.HasValue && s_CurrOperation.Value != operationNode.Operation)
                    s_CurrOperation = null;
            }
        }

        public static void SetOperationForSelection(CSGOperationType newOperation)
        {
            if (s_CurrOperation == newOperation)
                return;

            foreach (var hasOperation in s_OperationNodes)
                hasOperation.Operation = newOperation;

            var prevOperation = s_CurrOperation;
            UpdateOperationSelection();
            if (prevOperation != s_CurrOperation)
                NodeOperationUpdated?.Invoke();
        }

        public static IReadOnlyList<ChiselGeneratorComponent> SelectedGenerators { get { return s_Generators; } }
        public static bool AreGeneratorsSelected { get { return s_Generators.Count > 0; } }

        public static IReadOnlyList<ChiselNodeComponent> SelectedNodes { get { return s_Nodes; } }
        public static bool AreNodesSelected { get { return s_Nodes.Count > 0; } }

        public static IReadOnlyList<IChiselHasOperation> SelectedOperationNodes { get { return s_OperationNodes; } }
        public static bool AreOperationNodesSelected { get { return s_OperationNodes.Count > 0; } }
        public static CSGOperationType? OperationOfSelectedNodes { get { return s_CurrOperation; } }

        
        struct ExtraPickingData
        {
            public SelectionDescription selectionDescription;
			public Vector3              worldIntersectionPos;
			public Vector3              worldCameraOrigin;
			public float                cameraDistance;
			public GameObject           selectionGameObject;
        }

        static ExtraPickingData extraPickingData;
		static readonly List<ExtraPickingData> extraPickingDatas = new();

		static bool record = false;

        public static void GetOverlappingObjects(Vector2 screenPos, List<ChiselIntersection> outputIntersections)
		{
			var outputObjects = ListPool<UnityEngine.Object>.Get();
            try
			{
				record = true;
				extraPickingDatas.Clear();
				HandleUtility.GetOverlappingObjects(screenPos, outputObjects);
				record = false;
				outputIntersections.Clear();
				foreach (var extraPickingData in extraPickingDatas)
				{
					var intersection = Convert(extraPickingData);
					outputIntersections.Add(intersection);
				}
				outputIntersections.Sort(delegate (ChiselIntersection x, ChiselIntersection y)
				{
					return x.brushIntersection.surfaceIntersection.distance.CompareTo(y.brushIntersection.surfaceIntersection.distance);
				});
			}
            finally
			{
				record = false;
				ListPool<UnityEngine.Object>.Release(outputObjects);
			}
        }

		public static GameObject PickClosestGameObject(Vector2 screenPos, out ChiselIntersection intersection)
		{
			intersection = ChiselIntersection.None;
			var outputIntersections = ListPool<ChiselIntersection>.Get();
			try
			{
				GetOverlappingObjects(screenPos, outputIntersections);
				if (outputIntersections.Count == 0)
					return null;
				intersection = outputIntersections[0];
				return intersection.gameObject;
			}
			finally
			{
				ListPool<ChiselIntersection>.Release(outputIntersections);
			}
		}

		static ChiselIntersection Convert(ExtraPickingData extraPickingData)
		{
            if (extraPickingData.selectionGameObject == null)
                return ChiselIntersection.None;

            if (!extraPickingData.selectionGameObject.TryGetComponent<ChiselNodeComponent>(out var chiselNode))
                return ChiselIntersection.None;
            
			var model = chiselNode.hierarchyItem.Model;
            if (!model)
				return ChiselIntersection.None;

			var treeBrush = CSGTreeBrush.Find(extraPickingData.selectionDescription.brushNodeID);
            if (treeBrush == CSGTreeBrush.Invalid)
				return ChiselIntersection.None;

			var brushMeshBlob = BrushMeshManager.GetBrushMeshBlob(treeBrush.BrushMesh.BrushMeshID);
            if (!brushMeshBlob.IsCreated)
				return ChiselIntersection.None;

			var surfaceIndex = extraPickingData.selectionDescription.surfaceIndex;

			ref var brushMesh = ref brushMeshBlob.Value;
			ref var planes = ref brushMesh.localPlanes;
			var nodeToTreeSpace = (Matrix4x4)treeBrush.NodeToTreeSpaceMatrix;
			var plane = new Plane(planes[surfaceIndex].xyz, planes[surfaceIndex].w);
			var result_localPlane = plane;
			var result_isReversed = false; // TODO: make this work somehow?

			var treePlane = nodeToTreeSpace.TransformPlane(result_localPlane);
			var result_treePlane = result_isReversed ? treePlane.flipped : treePlane;

			var worldToLocalMatrix = model.transform.worldToLocalMatrix;

			var localRayStart = worldToLocalMatrix.MultiplyPoint(extraPickingData.worldCameraOrigin);
			var localRayVector = worldToLocalMatrix.MultiplyVector((extraPickingData.worldIntersectionPos - extraPickingData.worldCameraOrigin).normalized);
			var localRay = new Ray(localRayStart, localRayVector);

			var result_treeIntersection = Vector3.zero;
			if (result_treePlane.Raycast(localRay, out float result_dist))
			{
				result_treeIntersection = localRay.GetPoint(result_dist);
			}

			CSGTreeBrushIntersection brushIntersection = new()
			{
				tree = treeBrush.Tree,
				brush = treeBrush,

				surfaceIndex = surfaceIndex,

				surfaceIntersection = new ChiselSurfaceIntersection()
				{
					treePlane = result_treePlane,
					treePlaneIntersection = result_treeIntersection,
					distance = result_dist,
				}
			};
            var chiselIntersection = ChiselSceneQuery.Convert(brushIntersection);
            chiselIntersection.gameObject = extraPickingData.selectionGameObject;
			return chiselIntersection;
		}

		public static ChiselIntersection LastIntersection
        { 
            get
			{
                return Convert(extraPickingData);
			}
        }

		public static UnityEditor.RenderPickingResult PickingCallback(in UnityEditor.RenderPickingArgs args)
        {
            extraPickingData = default;
			var selectionOffsets = new List<SelectionOffset>();
			int pickingOffset = args.pickingIndex;
			int pickingIndex = ChiselRenderObjects.RenderPickingModels(args.NeedToRenderForPicking, pickingOffset, selectionOffsets);

			UnityEditor.RenderPickingResult result = 
                new(pickingIndex - pickingOffset,
		            delegate(int localPickingIndex, Vector3 worldPos, float depth)
			        {
                        localPickingIndex += pickingOffset;
						for (int i = 0; i < selectionOffsets.Count; i++)
                        {
                            var selectionOffset = selectionOffsets[i];
                            if (localPickingIndex < selectionOffset.offset)
                            {
                                Debug.Log("picking index not found");
                                return null;
                            }
							if (localPickingIndex >= selectionOffset.offset + selectionOffset.Count)
                                continue;

                            var cameraPos = Camera.current.transform.position;
							extraPickingData = new ExtraPickingData
                            {
                                selectionDescription = selectionOffset.selectionIndexDescriptions[localPickingIndex - selectionOffset.offset],
                                worldIntersectionPos = worldPos,
                                worldCameraOrigin = cameraPos,
								cameraDistance = (worldPos - cameraPos).magnitude
							};

							var obj = Resources.InstanceIDToObject(extraPickingData.selectionDescription.instanceID);
                            if (obj is MonoBehaviour monoBehaviour)
								extraPickingData.selectionGameObject = monoBehaviour.gameObject;
                            else
							    extraPickingData.selectionGameObject = obj as GameObject;

							if (record)
                            {
                                extraPickingDatas.Add(extraPickingData);
							}

							return obj;
						}
						Debug.Log("picking index not found");
						return null;
					});

			return result;
        }
    }
}

using Chisel.Core;
using Chisel.Components;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public static class ChiselPlacementTurn
    {
        // drawn wider than deep, a generator that runs along its depth is turned a quarter round up, to run along the long
        // side: its frame turned, and the drawn box in it. Turned, a size can come out negative; the frame already says
        // which way it runs, so the sizes are kept positive
        public static void AlongLongSide(ref Matrix4x4 transformation, ref Bounds bounds)
        {
            if (Mathf.Abs(bounds.size.x) <= Mathf.Abs(bounds.size.z))
                return;
            var turn = Quaternion.AngleAxis(90, Vector3.up);
            transformation = transformation * Matrix4x4.Rotate(turn);
            var back = Quaternion.Inverse(turn);
            var size = back * bounds.size;
            bounds = new Bounds(back * bounds.center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
        }
    }

    public enum GeneratorModeState
    {
        None,

        Commit,
        Cancel,
        
        Update
    }

    public class ChiselShapePlacementToolInstance<PlacementToolDefinitionType, DefinitionType, Generator> : ChiselPlacementToolInstanceWithDefinition<PlacementToolDefinitionType, DefinitionType, Generator>
        // PlacementToolDefinition needs to be a ScriptableObject so we can create an Editor for it
        where PlacementToolDefinitionType : ChiselShapePlacementTool<DefinitionType>
        // We need the DefinitionType to be able to strongly type the Generator
        where DefinitionType    : IChiselNodeGenerator, new()
        where Generator         : ChiselNodeGeneratorComponent<DefinitionType>
    {
        public ChiselShapePlacementToolInstance(string toolName, string group)
        {
            internalToolName = toolName;
            internalGroup = group;
        }

        readonly string internalToolName;
        readonly string internalGroup;

        public override string ToolName => internalToolName;
        public override string Group    => internalGroup;

        protected ChiselEditorHandles handles = new ChiselEditorHandles();

        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            // TODO: handle snapping against own points
            // TODO: handle ability to 'commit' last point
            switch (ShapeExtrusionHandle.Do(dragArea, out Curve2D shape, out float height, out ChiselModelComponent modelBeneathCursor, out Matrix4x4 transformation, Axis.Y))
            {
                case ShapeExtrusionState.Modified:
                case ShapeExtrusionState.Create:
                {
                    // flatter than a generator can be there is no shape: it is hidden until it has a height again, and
                    // finishing then makes nothing
                    var flat = Mathf.Abs(height) < ChiselGeneratorLimits.kMinSize;
                    if (!generatedComponent)
                    {
                        if (!flat)
                        {
                            var center2D = shape.Center;
                            var center3D = new Vector3(center2D.x, 0, center2D.y);
                            Transform parentTransform = null;
                            var model = ChiselModelManager.Instance.GetActiveModelOrCreate(modelBeneathCursor);
                            if (model != null) parentTransform = model.transform;
                            generatedComponent = ChiselComponentFactory.Create(generatorType, ToolName, parentTransform,
                                                                                  transformation * Matrix4x4.TRS(center3D, Quaternion.identity, Vector3.one))
                                                as ChiselNodeGeneratorComponent<DefinitionType>;
                            shape.Center = Vector2.zero;
                            generatedComponent.definition.Reset();
                            generatedComponent.SurfaceDefinition?.Reset();
                            generatedComponent.Operation = forceOperation ?? CSGOperationType.Additive;
                            PlacementToolDefinition.OnCreate(ref generatedComponent.definition, shape);
                            PlacementToolDefinition.OnUpdate(ref generatedComponent.definition, height);
                            generatedComponent.ResetTreeNodes();
                        }
                    } else
                    {
                        if (generatedComponent.gameObject.activeSelf == flat)
                            generatedComponent.gameObject.SetActive(!flat);
                        if (flat)
                            break;
                        generatedComponent.Operation = forceOperation ??
                                                  ((height < 0 && modelBeneathCursor) ?
                                                    CSGOperationType.Subtractive :
                                                    CSGOperationType.Additive);
                        PlacementToolDefinition.OnUpdate(ref generatedComponent.definition, height);
                        generatedComponent.OnValidate();
                        generatedComponent.ClearHashes(); // TODO: remove need for this
                    }
                    break;
                }
                
                case ShapeExtrusionState.Commit:        { if (generatedComponent && generatedComponent.gameObject.activeSelf) Commit(generatedComponent, PlacementToolDefinition.CreateAsBrush); else Cancel(); break; }
                case ShapeExtrusionState.Cancel:        { Cancel(); break; }
            }

            if (ChiselOutlineRenderer.VisualizationMode != VisualizationMode.SimpleOutline)
                ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline;

            var temp = Handles.matrix;
            Handles.matrix *= transformation;
            handles.Start(null, sceneView);
            PlacementToolDefinition.OnPaint(handles, shape, height);
            handles.End();
            Handles.matrix = temp;
        }
    }
    
    public class ChiselBoundsPlacementToolInstance<PlacementToolDefinitionType, DefinitionType, Generator> 
        : ChiselPlacementToolInstanceWithDefinition<PlacementToolDefinitionType, DefinitionType, Generator>
        // PlacementToolDefinition needs to be a ScriptableObject so we can create an Editor for it
        where PlacementToolDefinitionType : ChiselBoundsPlacementTool<DefinitionType>
        // We need the DefinitionType to be able to strongly type the Generator
        where DefinitionType    : IChiselNodeGenerator, new()
        where Generator         : ChiselNodeGeneratorComponent<DefinitionType>
    {
        public ChiselBoundsPlacementToolInstance(string toolName, string group)
        {
            internalToolName = toolName;
            internalGroup = group;
        }

        readonly string internalToolName;
        readonly string internalGroup;

        public override string ToolName => internalToolName;
        public override string Group => internalGroup;

        Vector3 componentPosition   = Vector3.zero;
        Vector3 upAxis              = Vector3.zero;

        protected IChiselHandles handles = new ChiselEditorHandles();

        // Drawn from its middle, a generator built upwards is moved down by half its height; one built around its
        // origin is moved up by half, unless drawn from its middle
        void Lift(PlacementFlags flags, float height)
        {
            var lift = (PlacementToolDefinition.CenteredOnY ? 0.5f : 0.0f) -
                       (((flags & PlacementFlags.GenerateFromCenterY) == PlacementFlags.GenerateFromCenterY) ? 0.5f : 0.0f);
            if (lift != 0)
                generatedComponent.transform.localPosition = componentPosition + (upAxis * (height * lift));
        }


        public override void OnSceneGUI(SceneView sceneView, Rect dragArea)
        {
            // thinner than this along any axis the generator can't be built
            var minimumAxisLength = PlacementToolDefinition.MinimumSize;
            var generatoreModeFlags = PlacementToolDefinition.PlacementFlags;

            if ((generatoreModeFlags & (PlacementFlags.HeightEqualsHalfXZ | PlacementFlags.HeightEqualsXZ)) != 0)
                generatoreModeFlags |= PlacementFlags.SameLengthXZ;

            if (Event.current.shift)
                generatoreModeFlags |= PlacementFlags.UseLastHeight;

            var state = RectangleExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModelComponent modelBeneathCursor, out Matrix4x4 transformation, generatoreModeFlags, Axis.Y);
            if (PlacementToolDefinition.AlongLongSide)
                ChiselPlacementTurn.AlongLongSide(ref transformation, ref bounds);

            switch (state)
            {
                case GeneratorModeState.Update:
                {
                    if (!generatedComponent)
                    {
                        var size = bounds.size;
                        if (Mathf.Abs(size.x) >= minimumAxisLength &&
                            Mathf.Abs(size.y) >= minimumAxisLength &&
                            Mathf.Abs(size.z) >= minimumAxisLength)
                        {
                            // Create the generator GameObject
                            Transform parentTransform = null;
                            var model = ChiselModelManager.Instance.GetActiveModelOrCreate(modelBeneathCursor);
                            if (model != null) parentTransform = model.transform;
                            generatedComponent  = ChiselComponentFactory.Create(generatorType, ToolName, parentTransform, transformation) 
                                                as ChiselNodeGeneratorComponent<DefinitionType>;
                            componentPosition   = generatedComponent.transform.localPosition;
                            upAxis              = generatedComponent.transform.up;

                            generatedComponent.definition.Reset();
                            generatedComponent.surfaceArray?.Reset();
                            generatedComponent.Operation = forceOperation ?? CSGOperationType.Additive;
                            PlacementToolDefinition.OnCreate(ref generatedComponent.definition);
                            PlacementToolDefinition.OnUpdate(ref generatedComponent.definition, bounds);
                            generatedComponent.OnValidate();

                            Lift(generatoreModeFlags, height);
                            generatedComponent.ResetTreeNodes();
                        }
                    } else
                    {
                        // flat along any axis there is no shape: it is hidden until it has a size again, and finishing
                        // then makes nothing
                        var size = bounds.size;
                        var flat = Mathf.Abs(size.x) < minimumAxisLength ||
                                   Mathf.Abs(size.y) < minimumAxisLength ||
                                   Mathf.Abs(size.z) < minimumAxisLength;
                        if (generatedComponent.gameObject.activeSelf == flat)
                            generatedComponent.gameObject.SetActive(!flat);
                        if (flat)
                            break;

                        // Update the generator GameObject
                        ChiselComponentFactory.SetTransform(generatedComponent, transformation);
                        componentPosition = generatedComponent.transform.localPosition;
                        if ((generatoreModeFlags & PlacementFlags.AlwaysFaceUp) == PlacementFlags.AlwaysFaceCameraXZ)
                            generatedComponent.Operation = forceOperation ?? CSGOperationType.Additive;
                        else
                            generatedComponent.Operation = forceOperation ??
                                                    ((height < 0 && modelBeneathCursor) ?
                                                    CSGOperationType.Subtractive :
                                                    CSGOperationType.Additive);
                        PlacementToolDefinition.OnUpdate(ref generatedComponent.definition, bounds);
                        generatedComponent.OnValidate();

                        Lift(generatoreModeFlags, height);

                        generatedComponent.ClearHashes(); // TODO: remove need for this
                    }
                    break;
                }
                
                case GeneratorModeState.Commit:     { if (generatedComponent && generatedComponent.gameObject.activeSelf) { Commit(generatedComponent, PlacementToolDefinition.CreateAsBrush); } else Cancel(); break; }
                case GeneratorModeState.Cancel:     { Cancel(); break; }
            }

            if (ChiselOutlineRenderer.VisualizationMode != VisualizationMode.SimpleOutline)
                ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline;
            handles.matrix = transformation;
            PlacementToolDefinition.OnPaint(handles, bounds);
        }
    }



    public abstract class ChiselGeneratorDefinitionEditor<ComponentType, DefinitionType> 
        : ChiselGeneratorEditor<ComponentType>
        where ComponentType  : ChiselNodeGeneratorComponent<DefinitionType>
        where DefinitionType : IChiselNodeGenerator, new()
    {
        protected override void OnScene(IChiselHandles handles, ComponentType generator)
        {
            Measurements.BeginLabels();
            try
            {
                generator.definition.OnEdit(handles);
                (handles as ChiselEditorHandles)?.DoDefaultMoveGizmo();
            }
            finally { Measurements.EndLabels(); }
        }
    }
}

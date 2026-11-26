using System.Collections.Generic;
using Chisel.Components;
using Chisel.Core;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // The clip tool's cut, made on the brushes themselves so they are seen as they will be. Each change of the plane or
    // the mode undoes the cut and makes it again on the brushes as they were. Commit keeps it as one undo step, Cancel
    // undoes it. The brushes are held by id: one the cut removes is destroyed, and its undo brings it back.
    sealed class ChiselClipPreview
    {
        readonly List<EntityId>                  brushIds  = new();
        readonly List<ChiselClipPicking.Brush>   originals = new();
        readonly List<ChiselBrushComponent>      brushes   = new();
        readonly List<ChiselBrushComponent>      left      = new();
        int  undoGroup = -1;
        bool cutMade;

        public bool IsShown => undoGroup >= 0;

        /// <summary>True while the cut shown is undone to be made again, or to cancel it: undoing a group calls
        /// Undo.undoRedoPerformed.</summary>
        public bool IsReverting { get; private set; }

        /// <summary>The brushes as they were before they were cut, in the world: to pick points on, and to draw what is
        /// cut off.</summary>
        public IReadOnlyList<ChiselClipPicking.Brush> Originals => originals;

        /// <summary>The brushes the cut shown leaves, the halves split off included.</summary>
        public IReadOnlyList<ChiselBrushComponent> Left => left;

        /// <summary>Starts a cut of these brushes; a cut already shown is undone first.</summary>
        public void Begin(IReadOnlyList<ChiselBrushComponent> targets)
        {
            Cancel();
            foreach (var brush in targets)
            {
                var outline = brush ? brush.definition?.BrushOutline : null;
                if (outline?.vertices == null || outline.planes == null || outline.halfEdgePolygonIndices == null)
                    continue;
                brushIds.Add(brush.GetEntityId());
                originals.Add(new ChiselClipPicking.Brush(brush.GlobalTransformation, new BrushMesh(outline)));
            }
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
        }

        /// <summary>Shows the brushes cut by the plane as the mode says, made again from the brushes as they were.</summary>
        public void Show(Plane plane, ChiselClipMode mode)
        {
            if (!IsShown)
                return;
            Revert();

            brushes.Clear();
            foreach (var id in brushIds)
            {
                if (Resources.EntityIdToObject(id) is ChiselBrushComponent brush && brush)
                    brushes.Add(brush);
            }
            left.Clear();
            left.AddRange(ChiselBrushClip.Apply(brushes, plane, mode));
            Undo.SetCurrentGroupName(ChiselBrushClip.kUndoName);
            cutMade = true;
        }

        /// <summary>Keeps the cut shown, as one undo step; the brushes it leaves.</summary>
        public List<ChiselBrushComponent> Commit()
        {
            var result = new List<ChiselBrushComponent>(left);
            if (cutMade)
            {
                Undo.CollapseUndoOperations(undoGroup);
                Undo.SetCurrentGroupName(ChiselBrushClip.kUndoName);
                Undo.IncrementCurrentGroup();
            }
            Forget();
            return result;
        }

        /// <summary>The brushes as they were, while there is no plane; Show cuts them again.</summary>
        public void Hide()
        {
            Revert();
            left.Clear();
        }

        /// <summary>Undoes the cut shown: the brushes are as they were.</summary>
        public void Cancel()
        {
            Revert();
            Forget();
        }

        void Revert()
        {
            if (!cutMade)
                return;
            IsReverting = true;
            try
            {
                Undo.RevertAllDownToGroup(undoGroup);
            }
            finally
            {
                IsReverting = false;
            }
            cutMade = false;
        }

        /// <summary>Lets go of the cut without undoing it, for when it was undone, or redone, from elsewhere.</summary>
        public void Forget()
        {
            brushIds.Clear();
            originals.Clear();
            brushes.Clear();
            left.Clear();
            undoGroup = -1;
            cutMade   = false;
        }
    }
}

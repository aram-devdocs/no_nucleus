using UnityEngine;
using UnityEngine.EventSystems;

namespace Nucleus.Ui
{
    /// <summary>uGUI drag handle: dragging this graphic moves the Target RectTransform. Clicks pass through.
    /// While a drag is in progress it raises a global flag (<see cref="Dragging"/>) so the host can suppress the
    /// game's map pan — a fast drag can move the cursor off the bar, making IsPointerOverGameObject() flicker
    /// false and the map pan bleed through; the flag is drag-state, not pointer-position, so it doesn't.</summary>
    public sealed class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform Target;

        /// <summary>True while ANY Nucleus panel is being dragged. The host's map-tick suppression checks this.</summary>
        public static bool Dragging { get; private set; }

        public void OnBeginDrag(PointerEventData e) { Dragging = true; }

        public void OnDrag(PointerEventData e)
        {
            if (Target != null) Target.anchoredPosition += e.delta;
        }

        public void OnEndDrag(PointerEventData e) { Dragging = false; }
    }
}

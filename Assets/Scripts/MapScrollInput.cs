using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Wheel and drag on the run map's viewport.
///
/// ⚠️ A SEPARATE COMPONENT, NOT MORE CODE IN RunMapScreen. Unity delivers `IScrollHandler` and the
/// drag interfaces to the object the pointer is actually OVER, and the pointer is over the viewport
/// — not over the screen root. Putting these on `RunMapScreen` would compile, look correct, and
/// never fire, which is this project's most expensive bug shape.
///
/// ⚠️ IT DELIBERATELY DOES NOT USE ScrollRect. ScrollRect wants to own its content's RectTransform
/// and re-drives `anchoredPosition` every frame from its own normalizedPosition, which fights the
/// chart's rebuild (Refresh destroys and re-creates every mark, resizing the content underneath it)
/// and would snap the view back to the top on each redraw. Owning one float is less code and it
/// survives the rebuild.
/// </summary>
public class MapScrollInput : MonoBehaviour, IScrollHandler, IDragHandler, IBeginDragHandler
{
    private RunMapScreen screen;

    // Wheel deltas are reported in "lines" and differ wildly between mice and trackpads; this is the
    // canvas-pixel value one notch moves the chart.
    private const float WheelStep = 90f;

    public void Bind(RunMapScreen s) { screen = s; }

    public void OnScroll(PointerEventData e)
    {
        if (screen == null) return;
        screen.Scroll(e.scrollDelta.y * WheelStep);
    }

    public void OnBeginDrag(PointerEventData e) { /* nothing to seed — drag is purely relative */ }

    public void OnDrag(PointerEventData e)
    {
        if (screen == null) return;
        // Grab-and-pull: the sheet follows the hand, so dragging DOWN moves the chart down. Matching
        // the delta's sign directly is what makes it feel like paper rather than like a scrollbar.
        screen.Scroll(e.delta.y);
    }
}

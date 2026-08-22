namespace DeskCanvas.Core;

/// <summary>Pure stable ordering. Index zero is the back-most desktop item.</summary>
public static class LayerOrderMath
{
    public static List<CanvasItem> Normalize(IReadOnlyList<CanvasItem>? items)
    {
        if (items is null) return [];
        var ordered = items.Select((item, index) => (item, index)).Where(pair => pair.item is not null)
            .OrderBy(pair => pair.item.ZIndex).ThenBy(pair => pair.index).Select(pair => pair.item).ToList();
        for (var index = 0; index < ordered.Count; index++) ordered[index].ZIndex = index;
        return ordered;
    }
    public static bool MoveToFront(IList<CanvasItem> items, CanvasItem? item) => Move(items, item, int.MaxValue);
    public static bool MoveToBack(IList<CanvasItem> items, CanvasItem? item) => Move(items, item, int.MinValue);
    public static bool MoveUp(IList<CanvasItem> items, CanvasItem? item) => Move(items, item, 1);
    public static bool MoveDown(IList<CanvasItem> items, CanvasItem? item) => Move(items, item, -1);
    private static bool Move(IList<CanvasItem> items, CanvasItem? item, int step)
    {
        if (item is null || items.Count < 2) { NormalizeInto(items); return false; }
        NormalizeInto(items); var current = IndexOf(items, item); if (current < 0) return false;
        var target = step switch { int.MaxValue => items.Count - 1, int.MinValue => 0, _ => Math.Clamp(current + step, 0, items.Count - 1) };
        if (target == current) return false;
        items.RemoveAt(current); items.Insert(target, item); NormalizeInto(items); return true;
    }
    public static void NormalizeInto(IList<CanvasItem> items) { var ordered = Normalize(items.ToList()); items.Clear(); foreach (var item in ordered) items.Add(item); }
    private static int IndexOf(IList<CanvasItem> items, CanvasItem item) { for (var index = 0; index < items.Count; index++) if (ReferenceEquals(items[index], item)) return index; return -1; }
}

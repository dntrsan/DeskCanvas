using System.Windows.Media;

namespace DeskCanvas.App.Windows;

// The v18 ring keeps the existing Ellipse layout contract while allowing the foreground
// arc to carry a geometry.  It is intentionally local to this overlay.
internal class Ellipse : System.Windows.Shapes.Ellipse
{
    internal Geometry Data { get; set; } = Geometry.Empty;
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (!Data.IsEmpty && Stroke is not null) drawingContext.DrawGeometry(null, new Pen(Stroke, StrokeThickness), Data);
    }
}

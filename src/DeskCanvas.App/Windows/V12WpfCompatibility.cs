using System.Windows;
using System.Windows.Media.Animation;

namespace DeskCanvas.App.Windows;

internal class Border : System.Windows.Controls.Border
{
    internal System.Windows.Media.Brush? Foreground { get; set; }
}

internal sealed class CubicEaseDoubleKeyFrame : EasingDoubleKeyFrame
{
    internal CubicEaseDoubleKeyFrame(double value, KeyTime keyTime)
        : base(value, keyTime)
    {
    }

    protected override Freezable CreateInstanceCore() =>
        new CubicEaseDoubleKeyFrame(0, KeyTime);
}

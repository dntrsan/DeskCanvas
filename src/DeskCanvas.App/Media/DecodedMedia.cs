using System.Windows.Media.Imaging;

namespace DeskCanvas.App.Media;

internal sealed record DecodedFrame(BitmapSource Image, TimeSpan Duration);

internal sealed class DecodedMedia
{
    internal DecodedMedia(
        int pixelWidth,
        int pixelHeight,
        IReadOnlyList<DecodedFrame> frames)
    {
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Frames = frames;
    }

    internal int PixelWidth { get; }
    internal int PixelHeight { get; }
    internal IReadOnlyList<DecodedFrame> Frames { get; }
    internal bool IsAnimated => Frames.Count > 1;
}

using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace DeskCanvas.App.Media;

internal static class MediaDecoder
{
    private const long MaximumSourcePixels = 120_000_000;
    private const long MaximumDecodedBytes = 512L * 1024 * 1024;
    private const int MaximumDimension = 4096;

    internal static DecodedMedia Decode(string path)
    {
        using var stream = File.OpenRead(path);
        using var managed = new SKManagedStream(stream);
        using var codec = SKCodec.Create(managed)
            ?? throw new InvalidDataException("画像として読み込めないファイルです。");

        var sourceInfo = codec.Info;
        var sourcePixels = (long)sourceInfo.Width * sourceInfo.Height;
        if (sourcePixels <= 0 || sourcePixels > MaximumSourcePixels)
        {
            throw new InvalidDataException("画像が大きすぎます。最大1億2千万画素まで対応しています。");
        }

        var scale = Math.Min(1d, (double)MaximumDimension / Math.Max(sourceInfo.Width, sourceInfo.Height));
        var width = Math.Max(1, (int)Math.Round(sourceInfo.Width * scale));
        var height = Math.Max(1, (int)Math.Round(sourceInfo.Height * scale));
        var frameCount = Math.Max(1, codec.FrameCount);
        var decodedBytes = (long)width * height * 4 * frameCount;
        if (decodedBytes > MaximumDecodedBytes)
        {
            throw new InvalidDataException("GIFの展開後サイズが大きすぎます。");
        }

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        var frames = new List<DecodedFrame>(frameCount);
        var frameInfo = codec.FrameInfo;

        for (var index = 0; index < frameCount; index++)
        {
            if (index == 0)
            {
                bitmap.Erase(SKColors.Transparent);
            }

            var priorFrame = index == 0 ? SKCodecOptions.ZeroFrame : index - 1;
            var options = new SKCodecOptions(index, priorFrame);
            var result = codec.GetPixels(info, bitmap.GetPixels(), options);
            if (result is not SKCodecResult.Success and
                not SKCodecResult.IncompleteInput)
            {
                throw new InvalidDataException($"画像フレームを展開できませんでした（{result}）。");
            }

            var durationMs = index < frameInfo.Length ? frameInfo[index].Duration : 100;
            var duration = TimeSpan.FromMilliseconds(Math.Max(20, durationMs));
            frames.Add(new DecodedFrame(ToBitmapSource(bitmap), duration));
        }

        return new DecodedMedia(sourceInfo.Width, sourceInfo.Height, frames);
    }

    private static BitmapSource ToBitmapSource(SKBitmap bitmap)
    {
        var stride = bitmap.RowBytes;
        var bytes = checked(stride * bitmap.Height);
        var buffer = new byte[bytes];
        Marshal.Copy(bitmap.GetPixels(), buffer, 0, bytes);
        var result = BitmapSource.Create(
            bitmap.Width,
            bitmap.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            buffer,
            stride);
        result.Freeze();
        return result;
    }
}

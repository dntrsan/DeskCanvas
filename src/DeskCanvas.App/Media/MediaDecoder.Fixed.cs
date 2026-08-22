using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace DeskCanvas.App.Media;

internal static class MediaDecoder
{
    private const long MaximumSourcePixels = 120_000_000;
    private const long MaximumDecodedBytes = 64L * 1024 * 1024;
    private const int MaximumStaticDimension = 4096;
    private const int MaximumAnimatedDimension = 2048;

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

        var frameCount = Math.Max(1, codec.FrameCount);
        var maximumDimension = frameCount == 1
            ? MaximumStaticDimension
            : MaximumAnimatedDimension;
        var scale = Math.Min(
            1d,
            (double)maximumDimension / Math.Max(sourceInfo.Width, sourceInfo.Height));
        var width = Math.Max(1, (int)Math.Round(sourceInfo.Width * scale));
        var height = Math.Max(1, (int)Math.Round(sourceInfo.Height * scale));
        if ((long)width * height * 4 * frameCount > MaximumDecodedBytes)
        {
            throw new InvalidDataException("GIFの展開後サイズが大きすぎます。");
        }

        var target = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var frames = new List<DecodedFrame>(frameCount);
        var frameInfo = codec.FrameInfo;

        for (var index = 0; index < frameCount; index++)
        {
            using var bitmap = DecodeFrame(codec, index, target);
            var durationMs = index < frameInfo.Length ? frameInfo[index].Duration : 100;
            frames.Add(new DecodedFrame(
                ToBitmapSource(bitmap),
                TimeSpan.FromMilliseconds(Math.Max(20, durationMs))));
        }

        return new DecodedMedia(sourceInfo.Width, sourceInfo.Height, frames);
    }

    private static SKBitmap DecodeFrame(SKCodec codec, int index, SKImageInfo target)
    {
        // SKCodec only decodes into sizes it natively supports (JPEG 1/2, 1/4, 1/8;
        // PNG/GIF/WebP usually original). Decode at a supported size, then resample.
        var supported = codec.GetScaledDimensions((float)target.Width / Math.Max(1, codec.Info.Width));
        var decodeInfo = new SKImageInfo(
            Math.Max(1, supported.Width),
            Math.Max(1, supported.Height),
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        var decoded = new SKBitmap(decodeInfo);
        if (index == 0) decoded.Erase(SKColors.Transparent);
        var options = new SKCodecOptions(index, index == 0 ? -1 : index - 1);
        var result = codec.GetPixels(decodeInfo, decoded.GetPixels(), options);
        if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
        {
            decoded.Dispose();
            throw new InvalidDataException($"画像フレームを展開できませんでした（{result}）。");
        }

        if (decoded.Width == target.Width && decoded.Height == target.Height) return decoded;

        var resized = decoded.Resize(target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        decoded.Dispose();
        return resized ?? throw new InvalidDataException("画像を縮小できませんでした。");
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

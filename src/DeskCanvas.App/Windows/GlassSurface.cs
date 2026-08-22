using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskCanvas.App.Services;
using DeskCanvas.Core;
using SkiaSharp;

namespace DeskCanvas.App.Windows;

/// <summary>
/// Paints a refracting glass plate into a WPF ImageBrush. The backdrop is a
/// 1:1 capture of the desktop host when available, otherwise the wallpaper file.
/// </summary>
internal static class GlassSurface
{
    private const int MaxEdge = 512;
    private static readonly ConditionalWeakTable<Border, GlassState> states = new();
    private static readonly List<WeakReference<Border>> attached = [];
    private static bool listening;

    internal static bool TryApply(Border card, CanvasItem item)
    {
        EnsureListening();
        var state = states.GetValue(card, static target =>
        {
            var created = new GlassState();
            target.LayoutUpdated += (_, _) => Schedule(target);
            return created;
        });
        state.Item = item;
        Track(card);
        return Paint(card, state, wait: true);
    }

    internal static void Release(Border card)
    {
        if (states.TryGetValue(card, out var state))
        {
            state.Item = null;
            state.Signature = 0;
            state.Work++;
        }
    }

    internal static void Refresh(Border? card)
    {
        if (card is null) return;
        if (!states.TryGetValue(card, out var state) || state.Item is null) return;
        Paint(card, state, wait: false);
    }

    internal static void RefreshAll()
    {
        for (var index = attached.Count - 1; index >= 0; index--)
        {
            if (!attached[index].TryGetTarget(out var card) || !states.TryGetValue(card, out var state) || state.Item is null)
            {
                attached.RemoveAt(index);
                continue;
            }

            Paint(card, state, wait: false);
        }
    }

    private static void EnsureListening()
    {
        if (listening) return;
        listening = true;
        DesktopBackdropService.Shared.Changed += (_, _) =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            dispatcher?.BeginInvoke(RefreshAll, DispatcherPriority.Background);
        };
    }

    private static void Track(Border card)
    {
        foreach (var reference in attached)
        {
            if (reference.TryGetTarget(out var existing) && ReferenceEquals(existing, card)) return;
        }

        attached.Add(new WeakReference<Border>(card));
    }

    private static void Schedule(Border card)
    {
        if (!states.TryGetValue(card, out var state) || state.Item is null) return;
        if (state.Queued) return;
        state.Queued = true;
        card.Dispatcher.BeginInvoke(() =>
        {
            state.Queued = false;
            if (state.Item is { } item && WidgetTheme.SurfaceOf(item) == WidgetSurfaceStyle.LiquidGlass)
            {
                Paint(card, state, wait: false);
            }
        }, DispatcherPriority.Render);
    }

    private static bool Paint(Border card, GlassState state, bool wait)
    {
        var request = CaptureRequest(card, state.Signature);
        if (request is null) return state.Brush is not null;
        var work = ++state.Work;

        if (wait || state.Brush is null)
        {
            var rendered = Render(request.Value);
            if (rendered is null) return false;
            Present(card, state, work, rendered.Value);
            return state.Brush is not null;
        }

        Task.Run(() =>
        {
            var rendered = Render(request.Value);
            if (rendered is null) return;
            card.Dispatcher.BeginInvoke(() => Present(card, state, work, rendered.Value), DispatcherPriority.Render);
        });
        return true;
    }

    private static PaintRequest? CaptureRequest(Border card, long signature)
    {
        var width = card.ActualWidth;
        var height = card.ActualHeight;
        if (width < 8 || height < 8 || !card.IsVisible) return null;

        try
        {
            var origin = card.PointToScreen(new Point(0, 0));
            var xPoint = card.PointToScreen(new Point(width, 0));
            var yPoint = card.PointToScreen(new Point(0, height));
            var center = card.PointToScreen(new Point(width / 2, height / 2));
            return new PaintRequest(origin, xPoint, yPoint, center, width, height, card.CornerRadius.TopLeft, GlassOptics.IsPassThrough, signature);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static RenderedPlate? Render(PaintRequest request)
    {
        RenderedPlate? rendered = null;
        DesktopBackdropService.Shared.Use(request.Center.X, request.Center.Y, backdrop =>
        {
            rendered = Compose(request, backdrop);
        });
        return rendered;
    }

    private static RenderedPlate? Compose(PaintRequest request, GlassBackdrop backdrop)
    {
        var signature = Hash(request, backdrop.Generation);
        if (signature == request.CurrentSignature)
        {
            return new RenderedPlate(signature, [], 0, 0);
        }

        var pixelsSource = CopyPixels(backdrop.Image);
        if (pixelsSource.Length == 0) return null;
        var stride = backdrop.Image.RowBytes;
        var imageWidth = backdrop.Image.Width;
        var imageHeight = backdrop.Image.Height;

        var scale = Math.Max(request.Width, request.Height) / MaxEdge;
        if (scale < 1) scale = 1;
        var columns = Math.Max(16, (int)Math.Round(request.Width / scale));
        var rows = Math.Max(16, (int)Math.Round(request.Height / scale));
        var pixels = new byte[columns * rows * 4];
        var radius = request.Radius;
        var xAxisX = (request.XPoint.X - request.Origin.X) / request.Width;
        var xAxisY = (request.XPoint.Y - request.Origin.Y) / request.Width;
        var yAxisX = (request.YPoint.X - request.Origin.X) / request.Height;
        var yAxisY = (request.YPoint.Y - request.Origin.Y) / request.Height;
        var pass = request.PassThrough;

        for (var y = 0; y < rows; y++)
        {
            var localY = (y + 0.5) * request.Height / rows;
            for (var x = 0; x < columns; x++)
            {
                var localX = (x + 0.5) * request.Width / columns;
                var dx = 0d;
                var dy = 0d;
                if (!pass)
                {
                    (dx, dy) = GlassOptics.Displace(localX, localY, request.Width, request.Height, radius);
                }

                var sampleX = localX + dx;
                var sampleY = localY + dy;
                var screenX = request.Origin.X + sampleX * xAxisX + sampleY * yAxisX;
                var screenY = request.Origin.Y + sampleX * xAxisY + sampleY * yAxisY;
                var extraX = dx * xAxisX + dy * yAxisX;
                var extraY = dx * xAxisY + dy * yAxisY;
                var red = Sample(backdrop, pixelsSource, stride, imageWidth, imageHeight, screenX, screenY);
                var green = red;
                var blue = red;
                if (!pass && (dx != 0 || dy != 0))
                {
                    green = Sample(backdrop, pixelsSource, stride, imageWidth, imageHeight, screenX + extraX * GlassOptics.ChromaticGreen, screenY + extraY * GlassOptics.ChromaticGreen);
                    blue = Sample(backdrop, pixelsSource, stride, imageWidth, imageHeight, screenX + extraX * GlassOptics.ChromaticBlue, screenY + extraY * GlassOptics.ChromaticBlue);
                }

                byte r, g, b, a;
                if (pass)
                {
                    (r, g, b, a) = GlassOptics.PassThrough(red.Red, green.Green, blue.Blue);
                }
                else
                {
                    (r, g, b) = GlassOptics.TintSample(red.Red, green.Green, blue.Blue);
                    a = GlassOptics.Alpha;
                }

                var offset = (y * columns + x) * 4;
                pixels[offset] = b;
                pixels[offset + 1] = g;
                pixels[offset + 2] = r;
                pixels[offset + 3] = a;
            }
        }

        if (!pass)
        {
            var scratch = new byte[pixels.Length];
            PlateBlur.Box(pixels, columns, rows, GlassOptics.BlurRadius, scratch);
            for (var y = 0; y < rows; y++)
            {
                var localY = (y + 0.5) * request.Height / rows;
                for (var x = 0; x < columns; x++)
                {
                    var localX = (x + 0.5) * request.Width / columns;
                    var sdf = GlassMath.RoundedRectSdf(localX, localY, request.Width, request.Height, radius);
                    var offset = (y * columns + x) * 4;
                    var (r, g, b, a) = GlassOptics.AddRim(pixels[offset + 2], pixels[offset + 1], pixels[offset], sdf, localX, localY, request.Width, request.Height, radius);
                    pixels[offset] = b;
                    pixels[offset + 1] = g;
                    pixels[offset + 2] = r;
                    pixels[offset + 3] = a;
                }
            }
        }

        return new RenderedPlate(Hash(request, backdrop.Generation), pixels, columns, rows);
    }

    private static void Present(Border card, GlassState state, int work, RenderedPlate rendered)
    {
        if (state.Work != work || state.Item is null) return;
        if (rendered.Signature == state.Signature && state.Brush is not null)
        {
            if (!ReferenceEquals(card.Background, state.Brush)) card.Background = state.Brush;
            return;
        }

        if (rendered.Columns < 1 || rendered.Pixels.Length == 0)
        {
            if (state.Brush is not null && !ReferenceEquals(card.Background, state.Brush)) card.Background = state.Brush;
            return;
        }

        var bitmap = state.Bitmap;
        if (bitmap is null || bitmap.PixelWidth != rendered.Columns || bitmap.PixelHeight != rendered.Rows)
        {
            bitmap = new WriteableBitmap(rendered.Columns, rendered.Rows, 96, 96, PixelFormats.Bgra32, null);
            state.Bitmap = bitmap;
            state.Brush = new ImageBrush(bitmap)
            {
                Stretch = Stretch.Fill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
        }

        bitmap.WritePixels(new Int32Rect(0, 0, rendered.Columns, rendered.Rows), rendered.Pixels, rendered.Columns * 4, 0);
        state.Signature = rendered.Signature;
        card.Background = state.Brush;
    }

    private static SKColor Sample(
        GlassBackdrop backdrop,
        byte[] pixels,
        int stride,
        int imageWidth,
        int imageHeight,
        double screenX,
        double screenY)
    {
        double imageX;
        double imageY;
        if (backdrop.PixelMapped)
        {
            (imageX, imageY) = BackdropMapping.ToImage(screenX, screenY, backdrop.OriginX, backdrop.OriginY);
            if (!BackdropMapping.IsInside(imageX, imageY, imageWidth, imageHeight))
            {
                return backdrop.Background;
            }
        }
        else
        {
            (imageX, imageY) = GlassMath.MapToImage(
                screenX - backdrop.OriginX,
                screenY - backdrop.OriginY,
                backdrop.DestWidth,
                backdrop.DestHeight,
                imageWidth,
                imageHeight,
                backdrop.Fit);
            if (!GlassMath.IsInsideImage(imageX, imageY, imageWidth, imageHeight))
            {
                return backdrop.Background;
            }
        }

        return SampleBilinear(pixels, stride, imageWidth, imageHeight, imageX, imageY, backdrop.Background);
    }

    private static byte[] CopyPixels(SKBitmap bitmap)
    {
        var pixels = bitmap.GetPixels();
        if (pixels == IntPtr.Zero || bitmap.RowBytes < 1 || bitmap.Height < 1) return [];
        var length = bitmap.RowBytes * bitmap.Height;
        var data = new byte[length];
        Marshal.Copy(pixels, data, 0, length);
        return data;
    }

    private static SKColor SampleBilinear(
        byte[] pixels,
        int stride,
        int width,
        int height,
        double x,
        double y,
        SKColor fallback)
    {
        if (width < 2 || height < 2 || stride < 8) return fallback;
        x = Math.Clamp(x, 0, width - 1.001);
        y = Math.Clamp(y, 0, height - 1.001);
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var x1 = Math.Min(width - 1, x0 + 1);
        var y1 = Math.Min(height - 1, y0 + 1);
        var fx = x - x0;
        var fy = y - y0;
        var c00 = Read(pixels, stride, x0, y0);
        var c10 = Read(pixels, stride, x1, y0);
        var c01 = Read(pixels, stride, x0, y1);
        var c11 = Read(pixels, stride, x1, y1);
        return new SKColor(
            Lerp(Lerp(c00.Red, c10.Red, fx), Lerp(c01.Red, c11.Red, fx), fy),
            Lerp(Lerp(c00.Green, c10.Green, fx), Lerp(c01.Green, c11.Green, fx), fy),
            Lerp(Lerp(c00.Blue, c10.Blue, fx), Lerp(c01.Blue, c11.Blue, fx), fy));
    }

    private static SKColor Read(byte[] pixels, int stride, int x, int y)
    {
        var offset = y * stride + x * 4;
        if (offset < 0 || offset + 3 >= pixels.Length) return new SKColor(28, 28, 30);
        return new SKColor(pixels[offset + 2], pixels[offset + 1], pixels[offset]);
    }

    private static byte Lerp(byte a, byte b, double t) => (byte)Math.Clamp(a + (b - a) * t, 0, 255);

    private static long Hash(PaintRequest request, long generation)
    {
        unchecked
        {
            var hash = 17L;
            hash = hash * 31 + (long)request.Origin.X;
            hash = hash * 31 + (long)request.Origin.Y;
            hash = hash * 31 + (long)request.XPoint.X;
            hash = hash * 31 + (long)request.YPoint.Y;
            hash = hash * 31 + (long)request.Width;
            hash = hash * 31 + (long)request.Height;
            hash = hash * 31 + generation;
            hash = hash * 31 + (request.PassThrough ? 1 : 0);
            return hash;
        }
    }

    private readonly record struct PaintRequest(
        Point Origin,
        Point XPoint,
        Point YPoint,
        Point Center,
        double Width,
        double Height,
        double Radius,
        bool PassThrough,
        long CurrentSignature);

    private readonly record struct RenderedPlate(long Signature, byte[] Pixels, int Columns, int Rows);

    private sealed class GlassState
    {
        internal CanvasItem? Item;
        internal WriteableBitmap? Bitmap;
        internal ImageBrush? Brush;
        internal long Signature;
        internal bool Queued;
        internal int Work;
    }
}

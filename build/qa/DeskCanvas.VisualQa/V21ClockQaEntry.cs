using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V21ClockQaEntry
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Path.IsPathFullyQualified(args[0])) return 2;
        Directory.CreateDirectory(args[0]);
        var log = new List<string> { "v2.1 compact Clock emulation render manifest", "No live process, installed app, registry, or user data was opened." };
        foreach (var dpi in new[] { 1d, 1.5d }) foreach (var style in new[] { ClockStyle.Digital, ClockStyle.Split, ClockStyle.Analog }) foreach (var dates in new[] { (false,false), (false,true), (true,false), (true,true) })
        {
            var item = new CanvasItem { ContentKind = CanvasContentKinds.Clock, Width = 64, Height = 64, Theme = WidgetThemeKind.Ocean, Clock = new ClockOptions { Style = style, ShowYear = dates.Item1, ShowMonthDay = dates.Item2, ShowSeconds = true } };
            using var content = new ClockItemContent(item); content.SetActive(true);
            var card = (Border)content.View; var scale = ClockCompactLayoutMath.Scale(64, 64); card.Padding = new Thickness(Math.Max(2, 14 * Math.Min(64d / 96, 1))); card.CornerRadius = new CornerRadius(Math.Max(7, 25 * Math.Min(64d / 96, 1))); card.Effect = new DropShadowEffect { Color = Colors.Black, ShadowDepth = 2, BlurRadius = 9, Opacity = .2 };
            var host = new Grid { Width = 64, Height = 64, LayoutTransform = new ScaleTransform(dpi, dpi) }; var inner = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = card }; host.Children.Add(inner); var size = new Size(64 * dpi, 64 * dpi); host.Measure(size); host.Arrange(new Rect(new Point(), size)); host.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Round(size.Width), (int)Math.Round(size.Height), 96, 96, PixelFormats.Pbgra32); bitmap.Render(host); var file = $"v21-clock-{style.ToString().ToLowerInvariant()}-y{dates.Item1}-d{dates.Item2}-{(dpi == 1 ? 100 : 150)}.png"; var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(args[0], file))) encoder.Save(stream); log.Add(file + " scale=" + scale.ToString("0.###")); content.SetActive(false);
        }
        File.WriteAllLines(Path.Combine(args[0], "manifest.txt"), log); Console.WriteLine($"PASS v2.1 compact Clock QA {log.Count - 2} images"); return 0;
    }
}

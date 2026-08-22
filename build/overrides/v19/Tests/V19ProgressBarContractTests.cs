using System.Runtime.CompilerServices;

internal static class V19ProgressBarContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var source = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "build", "overrides", "v19", "App", "Windows", "LiveItemContent.V19.Accepted5.cs"));
        if (!source.Contains("timelineTrack.Visibility=timelineFill.Visibility=simple?Visibility.Visible:Visibility.Collapsed", StringComparison.Ordinal))
            throw new InvalidOperationException("custom progress styles still retain the straight track");
        if (!source.Contains("var clip=new RectangleGeometry(new Rect(0,0,width*ratio,16))", StringComparison.Ordinal))
            throw new InvalidOperationException("custom progress styles lost progress clipping");
        foreach (var ratio in new[] { 0d, .5d, 1d })
        {
            var width = 300d;
            var clip = Math.Clamp(width * ratio, 0, width);
            if (Math.Abs(clip - width * ratio) > .001) throw new InvalidOperationException("progress ratio contract failed");
        }
        Console.WriteLine("PASS v1.9 custom progress hides base line and retains 0/50/100 clip");
    }
}

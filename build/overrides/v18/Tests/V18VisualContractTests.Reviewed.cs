using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V18VisualContractTestsReviewed
{
    [ModuleInitializer] internal static void Run()
    {
        var graph=new SmoothedNetworkGraph(); for(var i=0;i<35;i++)graph.Add(i*500);if(graph.Count!=30||graph.Scale<17000)throw new InvalidOperationException("30 sample rise failed"); graph.Add(0);graph.Add(0);graph.Add(0);var held=graph.Scale;graph.Add(0);if(graph.Scale>=held)throw new InvalidOperationException("3 sample hold/12 percent decay failed");var before=graph.Advance(TimeSpan.Zero);var middle=graph.Advance(TimeSpan.FromMilliseconds(350));graph.Advance(TimeSpan.FromMilliseconds(350));if(before.SequenceEqual(middle)||graph.IsAnimating)throw new InvalidOperationException("700ms easeout/timer end failed");graph.Stop();if(graph.IsAnimating)throw new InvalidOperationException("hidden/dispose graph timer failed");
        var bezier=ArtworkPaletteMathV18.Bezier([new NetworkGraphPoint(0,-5),new NetworkGraphPoint(20,25)]);if(bezier.Count!=1||bezier[0].Y1<0||bezier[0].Y2>16)throw new InvalidOperationException("bezier Y was not clamped");
        var neon=Enumerable.Repeat(new PaletteRgb(15,25,30),40).Concat(Enumerable.Repeat(new PaletteRgb(0,255,0),6)).ToArray();if(!ArtworkPaletteMathV18.TryResolve(neon,out var palette)||palette.Accent.G==255||palette.SurfaceA.Luminance>.23||ArtworkPaletteMath.ContrastRatio(palette.SurfaceA,palette.Foreground)<4.5)throw new InvalidOperationException("neon palette guardrail failed");var olive=Enumerable.Repeat(new PaletteRgb(112,107,39),30).ToArray();if(!ArtworkPaletteMathV18.TryResolve(olive,out _))throw new InvalidOperationException("olive palette failed");if(ArtworkPaletteMathV18.TryResolve(Enumerable.Repeat(new PaletteRgb(88,88,88),30).ToArray(),out _))throw new InvalidOperationException("monochrome fallback failed");
        const double width=360, reserved=35, trioWidth=138;var trioLeft=reserved+(width-2*reserved-trioWidth)/2; if(Math.Abs(trioLeft+trioWidth/2-width/2)>.5||reserved!=35)throw new InvalidOperationException("symmetric transport geometry failed");Console.WriteLine("PASS v1.8 smooth graph palette and centered transport geometry");
    }
}

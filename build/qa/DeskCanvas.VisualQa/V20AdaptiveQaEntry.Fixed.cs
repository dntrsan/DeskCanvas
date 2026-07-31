using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;
using WpfBorder = System.Windows.Controls.Border;

internal static class V20AdaptiveQaEntryFixed
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Path.IsPathFullyQualified(args[0])) { Console.Error.WriteLine("Pass one absolute output directory."); return 2; }
        Directory.CreateDirectory(args[0]);
        var log = new List<string> { "v20 adaptive content STA render manifest", "No live process, installed app, registry, or user data was opened.", "System width=300DIP; Now Playing=360x220DIP; scale=RenderTargetBitmap scale." };
        foreach (var scale in new[] { 1d, 1.5d }) { Systems(args[0], log, scale); Progress(args[0], log, scale); Wave(args[0], log, scale); Spectrum(args[0], log, scale); }
        File.WriteAllLines(Path.Combine(args[0], "manifest.txt"), log);
        Console.WriteLine($"PASS v2.0 adaptive STA QA; {log.Count - 3} render records at {args[0]}");
        return 0;
    }

    private static void Systems(string output, List<string> log, double scale)
    {
        foreach (var c in new[] { ("all",true,true,true,true), ("cpu-only",true,false,false,false), ("network-only",false,false,false,true), ("metrics-only",true,true,true,false), ("all-hidden",false,false,false,false) })
        {
            var item = new CanvasItem { ContentKind=CanvasContentKinds.SystemMonitor, Width=300, Height=390, CenterY=600, Opacity=1, Theme=WidgetThemeKind.Ocean, SystemMonitor=new SystemMonitorOptions { ShowCpu=c.Item2, ShowMemory=c.Item3, ShowGpu=c.Item4, ShowNetwork=c.Item5 } };
            var oldTop=item.CenterY-item.Height/2; using var content=new SystemMonitorItemContent(item,new Metrics()); content.SetActive(true); var root=(FrameworkElement)content.View; Layout(root,item.Width,item.Height);
            var divider=Private<WpfBorder>(content,"divider"); var expected=c.Item5&&(c.Item2||c.Item3||c.Item4); var top=item.CenterY-item.Height/2;
            if((divider.Visibility==Visibility.Visible)!=expected || Math.Abs(top-oldTop)>.01 || item.Height<96) throw new InvalidOperationException($"system {c.Item1}: adaptive layout failed");
            Render(root,item.Width,item.Height,scale,Path.Combine(output,$"v20-system-{c.Item1}-{Dpi(scale)}.png")); log.Add($"system {c.Item1} {Dpi(scale)}% desired={root.DesiredSize.Height:0.##} height={item.Height:0.##} top={top:0.##} divider={(expected?"visible":"collapsed")}"); content.SetActive(false);
        }
    }

    private static void Progress(string output,List<string> log,double scale)
    {
        foreach(var style in new[]{NowPlayingProgressStyle.Simple,NowPlayingProgressStyle.Wave}) foreach(var ratio in new[]{0d,.5d,1d})
        {
            using var content=Now(style,ratio,NowPlayingState.Paused,true,false,out _); content.SetActive(true); var root=(FrameworkElement)content.View; Layout(root,360,220); CheckTimeline(content,style,true);
            Render(root,360,220,scale,Path.Combine(output,$"v20-now-{style.ToString().ToLowerInvariant()}-{(int)(ratio*100)}-{Dpi(scale)}.png")); log.Add($"now {style} ratio={(int)(ratio*100)} {Dpi(scale)}% seek=true spectrum=false state=Paused"); content.SetActive(false);
        }
        using var noSeek=Now(NowPlayingProgressStyle.Wave,.5d,NowPlayingState.Paused,false,false,out _); noSeek.SetActive(true); var noSeekRoot=(FrameworkElement)noSeek.View; Layout(noSeekRoot,360,220); CheckTimeline(noSeek,NowPlayingProgressStyle.Wave,false); Render(noSeekRoot,360,220,scale,Path.Combine(output,$"v20-wave-canseek-false-{Dpi(scale)}.png")); log.Add($"now Wave ratio=50 {Dpi(scale)}% seek=false spectrum=false state=Paused"); noSeek.SetActive(false);
    }

    private static void Wave(string output,List<string> log,double scale)
    {
        foreach(var phaseCase in new[]{("playing-phase0",0d),("playing-after-ticks",WaveProgressMath.AdvancePhase(0,WaveProgressMath.RunningSpeed,1d))})
        {
            using var content=Now(NowPlayingProgressStyle.Wave,.5d,NowPlayingState.Playing,true,false,out _); content.SetActive(true); var root=(FrameworkElement)content.View; Layout(root,360,220); Set(content,"wavePhase",phaseCase.Item2); Call(content,"PaintTimeline"); Render(root,360,220,scale,Path.Combine(output,$"v20-wave-{phaseCase.Item1}-{Dpi(scale)}.png")); content.SetActive(false);
        }
        using var paused=Now(NowPlayingProgressStyle.Wave,.5d,NowPlayingState.Paused,true,false,out _); paused.SetActive(true); var pausedRoot=(FrameworkElement)paused.View; Layout(pausedRoot,360,220); Render(pausedRoot,360,220,scale,Path.Combine(output,$"v20-wave-paused-{Dpi(scale)}.png")); log.Add($"wave Playing {Dpi(scale)}% phase=0 -> {WaveProgressMath.AdvancePhase(0,WaveProgressMath.RunningSpeed,1):0.###}; paused static frame rendered"); paused.SetActive(false);
    }

    private static void Spectrum(string output,List<string> log,double scale)
    {
        foreach(var on in new[]{false,true})
        {
            using var content=Now(NowPlayingProgressStyle.Wave,.5d,NowPlayingState.Playing,true,on,out var factory); content.SetActive(true); var root=(FrameworkElement)content.View; Layout(root,360,220); Call(content,"PlaybackTick"); var visualizer=Private<StackPanel>(content,"visualizer");
            if((visualizer.Visibility==Visibility.Visible)!=on || (!on&&factory.Created!=0) || (on&&factory.Created!=1)) throw new InvalidOperationException($"spectrum {on}: lease/visibility failed");
            Render(root,360,220,scale,Path.Combine(output,$"v20-wave-spectrum-{(on?"on":"off")}-{Dpi(scale)}.png")); log.Add($"spectrum {(on?"on":"off")} {Dpi(scale)}% readerCreated={factory.Created} visualizer={visualizer.Visibility}"); content.SetActive(false);
        }
    }

    private static NowPlayingItemContent Now(NowPlayingProgressStyle style,double ratio,NowPlayingState state,bool seek,bool spectrum,out Factory factory)
    {
        factory=new Factory(); var item=new CanvasItem { ContentKind=CanvasContentKinds.NowPlaying,Width=360,Height=220,Opacity=1,Theme=WidgetThemeKind.Rose,NowPlaying=new NowPlayingOptions { ShowTimeline=true,ShowSpectrum=spectrum,ProgressStyle=style } };
        return new NowPlayingItemContent(item,new Media(ratio,state,seek),factory.Create);
    }

    private static void CheckTimeline(object content,NowPlayingProgressStyle style,bool seekAllowed)
    {
        var seek=Private<Slider>(content,"seek"); var track=Private<WpfBorder>(content,"timelineTrack");var fill=Private<WpfBorder>(content,"timelineFill");var art=Private<Canvas>(content,"timelineArt");
        var slider=seek.ActualHeight>=15&&seek.Opacity<=.011&&seek.IsEnabled==seekAllowed&&seek.IsHitTestVisible==seekAllowed; var visual=style==NowPlayingProgressStyle.Simple?track.Visibility==Visibility.Visible&&fill.Visibility==Visibility.Visible&&art.Children.Count==0:track.Visibility==Visibility.Collapsed&&fill.Visibility==Visibility.Collapsed&&art.Children.Count==1;
        if(!slider||!visual)throw new InvalidOperationException($"{style}: progress contract failed");
    }

    private static T Private<T>(object o,string name) where T:class=>(T)(o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(o)??throw new InvalidOperationException($"missing {name}"));
    private static void Set(object o,string name,object value)=>(o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)??throw new InvalidOperationException($"missing {name}")).SetValue(o,value);
    private static void Call(object o,string name){var m=o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)??throw new InvalidOperationException($"missing {name}");m.Invoke(o,m.GetParameters().Length==0?null:new object?[]{null,EventArgs.Empty});}
    private static void Layout(FrameworkElement root,double w,double h){root.Measure(new Size(w,h));root.Arrange(new Rect(0,0,w,h));root.UpdateLayout();}
    private static string Dpi(double s)=>s==1?"100":"150";
    private static void Render(FrameworkElement root,double width,double height,double scale,string path)
    {
        var host=new Grid { Width=width,Height=height,LayoutTransform=new ScaleTransform(scale,scale)};host.Children.Add(root);var size=new Size(width*scale,height*scale);host.Measure(size);host.Arrange(new Rect(new Point(),size));host.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Round(size.Width),(int)Math.Round(size.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(host);var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth/2,bitmap.PixelHeight/2,1,1),pixel,4,0);host.Children.Clear();if(pixel[3]!=255)throw new InvalidOperationException($"{Path.GetFileName(path)} alpha={pixel[3]}");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);png.Save(stream);
    }

    private sealed class Media(double ratio,NowPlayingState state,bool canSeek):INowPlayingService { public NowPlayingSnapshot Snapshot { get; }=new(true,"DeskCanvas.Qa!player","Adaptive QA title","QA Artist","QA Album",null,state,TimeSpan.FromSeconds(60*ratio),TimeSpan.FromSeconds(60),true,true,true,canSeek,DateTimeOffset.UtcNow);public event EventHandler<NowPlayingSnapshot>? SnapshotChanged{add{}remove{}}public IDisposable Acquire()=>Lease.Instance;public Task<bool> PreviousAsync()=>Task.FromResult(true);public Task<bool> PlayPauseAsync()=>Task.FromResult(true);public Task<bool> NextAsync()=>Task.FromResult(true);public Task<bool> SeekAsync(TimeSpan p)=>Task.FromResult(true);public void Dispose(){} }
    private sealed class Metrics:ISystemMetricsService { public SystemMetricsSnapshot Snapshot { get; }=new(MetricValue.From(64,"64%"),MetricValue.From(55,"8.8 GB / 16 GB"),MetricValue.From(37,"37%"),MetricValue.From(12000,"12 KB/秒"),MetricValue.From(3000,"3 KB/秒"));public event EventHandler<SystemMetricsSnapshot>? SnapshotChanged{add{}remove{}}public IDisposable Acquire()=>Lease.Instance;public void Dispose(){} }
    private sealed class Factory { internal int Created{get;private set;}internal IAudioSpectrumReader Create(){Created++;return new Reader();} }
    private sealed class Reader:IAudioSpectrumReader { public IReadOnlyList<double> ReadBands()=>[0,.18,.55,1,.46,.2,.08];public void Dispose(){} }
    private sealed class Lease:IDisposable { internal static readonly Lease Instance=new();public void Dispose(){} }
}

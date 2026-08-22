using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;
using WpfBorder = System.Windows.Controls.Border;

/// <summary>Offline render acceptance for the active v2.0 widget content.</summary>
internal static class V20AdaptiveQaEntryAccepted
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Path.IsPathFullyQualified(args[0])) { Console.Error.WriteLine("Pass one absolute output directory."); return 2; }
        Directory.CreateDirectory(args[0]);
        var log = new List<string> { "v20 adaptive content STA render manifest", "No live process, install, registry, or user data was opened.", "System width=300DIP; Now Playing=360x220DIP; 100/150 are RenderTargetBitmap scales." };
        foreach (var scale in new[] { 1d, 1.5d }) { Systems(args[0], log, scale); Progress(args[0], log, scale); Wave(args[0], log, scale); Spectrum(args[0], log, scale); NoSeek(args[0], log, scale); }
        File.WriteAllLines(Path.Combine(args[0], "manifest.txt"), log);
        Console.WriteLine($"PASS v2.0 adaptive STA QA; {log.Count - 3} render records at {args[0}");
        return 0;
    }

    private static void Systems(string dir, List<string> log, double scale)
    {
        foreach (var c in new[] { ("all",true,true,true,true), ("cpu-only",true,false,false,false), ("network-only",false,false,false,true), ("metrics-only",true,true,true,false), ("all-hidden",false,false,false,false) })
        {
            var item = new CanvasItem { ContentKind=CanvasContentKinds.SystemMonitor, Width=300, Height=390, CenterY=600, Opacity=1, Theme=WidgetThemeKind.Ocean, SystemMonitor=new SystemMonitorOptions { ShowCpu=c.Item2, ShowMemory=c.Item3, ShowGpu=c.Item4, ShowNetwork=c.Item5 } };
            var originalTop=item.CenterY-item.Height/2;
            using var content=new SystemMonitorItemContent(item,new Metrics()); content.SetActive(true);
            var root=(FrameworkElement)content.View; Layout(root,item.Width,item.Height);
            var divider=Field<WpfBorder>(content,"divider"); var visible=c.Item5&&(c.Item2||c.Item3||c.Item4);
            if ((divider.Visibility==Visibility.Visible)!=visible) throw new InvalidOperationException($"{c.Item1}: divider should be {visible}");
            var top=item.CenterY-item.Height/2;
            if (Math.Abs(top-originalTop)>.01 || item.Height<96) throw new InvalidOperationException($"{c.Item1}: top or minimum height failed");
            Render(root,item.Width,item.Height,scale,Path.Combine(dir,$"v20-system-{c.Item1}-{Dpi(scale)}.png"));
            log.Add($"system {c.Item1} {Dpi(scale)}% desired={root.DesiredSize.Height:0.##} height={item.Height:0.##} top={top:0.##} divider={(visible?"visible":"collapsed")}");
            content.SetActive(false);
        }
    }

    private static void Progress(string dir,List<string> log,double scale)
    {
        foreach(var style in new[]{NowPlayingProgressStyle.Simple,NowPlayingProgressStyle.Wave}) foreach(var ratio in new[]{0d,.5d,1d})
        {
            using var content=Now(style,ratio,NowPlayingState.Paused,true,false,out _); content.SetActive(true); var root=(FrameworkElement)content.View; Layout(root,360,220); VerifyTimeline(content,style,true);
            Render(root,360,220,scale,Path.Combine(dir,$"v20-now-{style.ToString().ToLowerInvariant()}-{(int)(ratio*100)}-{Dpi(scale)}.png"));
            log.Add($"now {style} ratio={(int)(ratio*100)} {Dpi(scale)}% seek=true spectrum=false state=Paused"); content.SetActive(false);
        }
    }

    private static void Wave(string dir,List<string> log,double scale)
    {
        using var playing=Now(NowPlayingProgressStyle.Wave,.5d,NowPlayingState.Playing,true,false,out _); playing.SetActive(true); var root=(FrameworkElement)playing.View; Layout(root,360,220);
        Set(playing,"wavePhase",0d); Call(playing,"PaintTimeline"); Render(root,360,220,scale,Path.Combine(dir,$"v20-wave-playing-phase0-{Dpi(scale)}.png"));
        Set(playing,"wavePhase",WaveProgressMath.AdvancePhase(0,WaveProgressMath.RunningSpeed,1)); Call(playing,"PaintTimeline"); Render(root,360,220,scale,Path.Combine(dir,$"v20-wave-playing-after-ticks-{Dpi(scale)}.png"));
        log.Add($"wave Playing {Dpi(scale)}% phase=0 -> phase={Get<double>(playing,"wavePhase"):0.###}; active WPF path rendered"); playing.SetActive(false);
        using var paused=Now(NowPlayingProgressStyle.Wave,.5d,NowPlayingState.Paused,true,false,out _); paused.SetActive(true); var pausedRoot=(FrameworkElement)paused.View; Layout(pausedRoot,360,220);
        Render(pausedRoot,360,220,scale,Path.Combine(dir,$"v20-wave-paused-{Dpi(scale)}.png")); log.Add($"wave Paused {Dpi(scale)}% static frame"); paused.SetActive(false);
    }

    private static void Spectrum(string dir,List<string> log,double scale)
    {
        foreach(var enabled in new[]{false,true})
        {
            using var content=Now(NowPlayingProgressStyle.Wave,.5d,NowPlayingState.Playing,true,enabled,out var factory); content.SetActive(true); var root=(FrameworkElement)content.View; Layout(root,360,220); Call(content,"PlaybackTick");
            var visualizer=Field<StackPanel>(content,"visualizer");
            if ((visualizer.Visibility==Visibility.Visible)!=enabled || (!enabled&&factory.Created!=0) || (enabled&&factory.Created!=1)) throw new InvalidOperationException($"spectrum {enabled}: reader/visibility contract failed");
            Render(root,360,220,scale,Path.Combine(dir,$"v20-wave-spectrum-{(enabled?"on":"off")}-{Dpi(scale)}.png")); log.Add($"spectrum {(enabled?"on":"off")} {Dpi(scale)}% readerCreated={factory.Created} visualizer={visualizer.Visibility}"); content.SetActive(false);
        }
    }

    private static void NoSeek(string dir,List<string> log,double scale)
    {
        using var content=Now(NowPlayingProgressStyle.Wave,.5d,NowPlayingState.Paused,false,false,out _); content.SetActive(true); var root=(FrameworkElement)content.View; Layout(root,360,220); VerifyTimeline(content,NowPlayingProgressStyle.Wave,false);
        Render(root,360,220,scale,Path.Combine(dir,$"v20-wave-canseek-false-{Dpi(scale)}.png")); log.Add($"now Wave ratio=50 {Dpi(scale)}% seek=false spectrum=false state=Paused"); content.SetActive(false);
    }

    private static NowPlayingItemContent Now(NowPlayingProgressStyle style,double ratio,NowPlayingState state,bool canSeek,bool spectrum,out Factory factory)
    {
        factory=new Factory(); var item=new CanvasItem { ContentKind=CanvasContentKinds.NowPlaying,Width=360,Height=220,Opacity=1,Theme=WidgetThemeKind.Rose,NowPlaying=new NowPlayingOptions { ShowTimeline=true,ShowSpectrum=spectrum,ProgressStyle=style } };
        return new NowPlayingItemContent(item,new Media(ratio,state,canSeek),factory.Create);
    }

    private static void VerifyTimeline(object content,NowPlayingProgressStyle style,bool canSeek)
    {
        var seek=Field<Slider>(content,"seek"); var track=Field<WpfBorder>(content,"timelineTrack"); var fill=Field<WpfBorder>(content,"timelineFill"); var art=Field<Canvas>(content,"timelineArt");
        if(seek.ActualHeight<15||seek.Opacity>.011||seek.IsEnabled!=canSeek||seek.IsHitTestVisible!=canSeek) throw new InvalidOperationException($"{style}: seek surface contract failed");
        if(style==NowPlayingProgressStyle.Simple ? track.Visibility!=Visibility.Visible||fill.Visibility!=Visibility.Visible||art.Children.Count!=0 : track.Visibility!=Visibility.Collapsed||fill.Visibility!=Visibility.Collapsed||art.Children.Count!=1) throw new InvalidOperationException($"{style}: progress visual contract failed");
    }

    private static T Field<T>(object o,string name) where T:class => (T)(o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(o)??throw new InvalidOperationException($"missing {name}"));
    private static T Get<T>(object o,string name)=>(T)(o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(o)??throw new InvalidOperationException($"missing {name}"));
    private static void Set(object o,string name,object value)=>(o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)??throw new InvalidOperationException($"missing {name}")).SetValue(o,value);
    private static void Call(object o,string name){var m=o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)??throw new InvalidOperationException($"missing {name}");m.Invoke(o,m.GetParameters().Length==0?null:[null,EventArgs.Empty]);}
    private static void Layout(FrameworkElement root,double width,double height){root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();}
    private static string Dpi(double scale)=>scale==1?"100":"150";
    private static void Render(FrameworkElement root,double width,double height,double scale,string output)
    {
        var host=new Grid { Width=width,Height=height,LayoutTransform=new ScaleTransform(scale,scale)};host.Children.Add(root);var size=new Size(width*scale,height*scale);host.Measure(size);host.Arrange(new Rect(new Point(),size));host.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Round(size.Width),(int)Math.Round(size.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(host);var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth/2,bitmap.PixelHeight/2,1,1),pixel,4,0);if(pixel[3]!=255)throw new InvalidOperationException($"{Path.GetFileName(output)} alpha={pixel[3]}");var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(output);png.Save(stream);
    }

    private sealed class Media(double ratio,NowPlayingState state,bool canSeek):INowPlayingService { public NowPlayingSnapshot Snapshot { get; }=new(true,"DeskCanvas.Qa!player","Adaptive QA title","QA Artist","QA Album",null,state,TimeSpan.FromSeconds(60*ratio),TimeSpan.FromSeconds(60),true,true,true,canSeek,DateTimeOffset.UtcNow); public event EventHandler<NowPlayingSnapshot>? SnapshotChanged { add{} remove{} } public IDisposable Acquire()=>Lease.Instance;public Task<bool> PreviousAsync()=>Task.FromResult(true);public Task<bool> PlayPauseAsync()=>Task.FromResult(true);public Task<bool> NextAsync()=>Task.FromResult(true);public Task<bool> SeekAsync(TimeSpan p)=>Task.FromResult(true);public void Dispose(){} }
    private sealed class Metrics:ISystemMetricsService { public SystemMetricsSnapshot Snapshot { get; }=new(MetricValue.From(64,"64%"),MetricValue.From(55,"8.8 GB / 16 GB"),MetricValue.From(37,"37%"),MetricValue.From(12000,"12 KB/秒"),MetricValue.From(3000,"3 KB/秒"));public event EventHandler<SystemMetricsSnapshot>? SnapshotChanged { add{} remove{} } public IDisposable Acquire()=>Lease.Instance;public void Dispose(){} }
    private sealed class Factory { internal int Created { get; private set; } internal IAudioSpectrumReader Create(){Created++;return new Reader();} }
    private sealed class Reader:IAudioSpectrumReader { public IReadOnlyList<double> ReadBands()=>[0,.18,.55,1,.46,.2,.08];public void Dispose(){} }
    private sealed class Lease:IDisposable { internal static readonly Lease Instance=new();public void Dispose(){} }
}

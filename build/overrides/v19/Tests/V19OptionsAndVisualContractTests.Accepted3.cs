using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V19OptionsAndVisualContractTests
{
    [ModuleInitializer] internal static void Run() { Options(); Network(); Sta(Visuals); Console.WriteLine("PASS v1.9 options, instant network graph, timeline styles, and spectrum switch"); }
    private static void Options()
    {
        var defaults = new NowPlayingOptions(); if (!defaults.ShowSpectrum || defaults.ProgressStyle != NowPlayingProgressStyle.Simple) throw new InvalidOperationException("defaults");
        var options = new NowPlayingOptions { ShowSpectrum = false, ProgressStyle = NowPlayingProgressStyle.Hearts }; if (options.Clone().ShowSpectrum || options.Clone().ProgressStyle != NowPlayingProgressStyle.Hearts) throw new InvalidOperationException("clone");
        options.ProgressStyle = (NowPlayingProgressStyle)42; if (options.Clone().ProgressStyle != NowPlayingProgressStyle.Simple) throw new InvalidOperationException("enum normalize");
        var root = Path.Combine(Path.GetTempPath(), "DeskCanvas-v19-options-" + Guid.NewGuid().ToString("N"));
        try { Directory.CreateDirectory(root); var json = "{\"version\":2,\"settings\":{},\"items\":[{\"id\":\"" + Guid.NewGuid() + "\",\"contentKind\":\"nowPlaying\",\"nowPlaying\":{\"showSpectrum\":false,\"progressStyle\":999}},{\"id\":\"" + Guid.NewGuid() + "\",\"contentKind\":\"nowPlaying\",\"nowPlaying\":{}}]}"; File.WriteAllText(Path.Combine(root,"layout.json"),json); var layout=new LayoutRepository(root).Load(); if(layout.Items.Count!=2 || layout.Items[0].NowPlaying.ShowSpectrum || layout.Items[0].NowPlaying.ProgressStyle!=NowPlayingProgressStyle.Simple || !layout.Items[1].NowPlaying.ShowSpectrum || layout.Items[1].NowPlaying.ProgressStyle!=NowPlayingProgressStyle.Simple) throw new InvalidOperationException("persisted options"); var round=JsonSerializer.Serialize(layout); if(!round.Contains("ShowSpectrum",StringComparison.Ordinal)||!round.Contains("ProgressStyle",StringComparison.Ordinal)) throw new InvalidOperationException("roundtrip"); }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root,true); } catch { } }
    }
    private static void Network()
    {
        var history=new NetworkHistory(30); history.Add(100); history.Add(400); var normalized=history.Normalized(); if(normalized.Length!=2 || Math.Abs(normalized[0]-100d/1024d)>.0001 || Math.Abs(normalized[1]-400d/1024d)>.0001) throw new InvalidOperationException("network scale was interpolated"); for(var i=0;i<32;i++)history.Add(i); if(history.Values.Count()!=30||history.Values.First()!=2||history.Values.Last()!=31)throw new InvalidOperationException("history"); var row=typeof(SystemMonitorItemContent).GetNestedType("NetworkRow",BindingFlags.NonPublic)??throw new InvalidOperationException("row"); var types=row.GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Select(f=>f.FieldType).ToArray(); if(!types.Contains(typeof(NetworkHistory))||types.Any(t=>t.Name.Contains("SmoothedNetworkGraph",StringComparison.Ordinal))||types.Any(t=>t==typeof(System.Windows.Threading.DispatcherTimer)))throw new InvalidOperationException("network smoothing state");
    }
    private static void Visuals()
    {
        foreach(var style in Enum.GetValues<NowPlayingProgressStyle>()) Style(style); var calls=0; var reader=new Reader(); var item=Item(NowPlayingProgressStyle.Simple,false); using(var content=new NowPlayingItemContent(item,new Service(),()=>{calls++;return reader;})){Arrange(content.View);content.SetActive(true);Tick(content);if(calls!=0||reader.ReadCount!=0)throw new InvalidOperationException("hidden spectrum poll");item.NowPlaying.ShowSpectrum=true;content.Refresh();Tick(content);if(calls!=1||reader.ReadCount!=1)throw new InvalidOperationException("visible spectrum poll");}if(!reader.Disposed)throw new InvalidOperationException("reader dispose");
    }
    private static void Style(NowPlayingProgressStyle style)
    {
        using var content=new NowPlayingItemContent(Item(style,false),new Service());var root=(FrameworkElement)content.View;Arrange(root);content.Refresh();if(Field<StackPanel>(content,"visualizer").Visibility!=Visibility.Collapsed)throw new InvalidOperationException("spectrum visible");var buttons=Desc<Button>(root).ToArray();if(buttons.Length!=3)throw new InvalidOperationException("button count");var rects=buttons.Select(b=>Bounds(b,root)).ToArray();if(Math.Abs((rects.Min(r=>r.Left)+rects.Max(r=>r.Right))/2-root.ActualWidth/2)>.75)throw new InvalidOperationException("button center");var fill=Field<System.Windows.Controls.Border>(content,"timelineFill");var art=Field<Canvas>(content,"timelineArt");if(style==NowPlayingProgressStyle.Simple){if(fill.Visibility!=Visibility.Visible||art.Children.Count!=0||fill.ActualWidth<=0)throw new InvalidOperationException("simple");return;}if(fill.Visibility!=Visibility.Collapsed||art.Children.Count!=2)throw new InvalidOperationException("custom");if(art.Children[1].Clip is not RectangleGeometry { Rect.Width: > 0 } clip || clip.Rect.Width>=art.ActualWidth)throw new InvalidOperationException("clip");if(style==NowPlayingProgressStyle.Wave&&art.Children[0] is not System.Windows.Shapes.Path)throw new InvalidOperationException("wave");if(style is NowPlayingProgressStyle.Dots or NowPlayingProgressStyle.Hearts&&art.Children[0] is not Canvas)throw new InvalidOperationException("marks");
    }
    private static CanvasItem Item(NowPlayingProgressStyle style,bool spectrum)=>new(){ContentKind=CanvasContentKinds.NowPlaying,Width=360,Height=220,Theme=WidgetThemeKind.Dark,NowPlaying=new NowPlayingOptions{ShowSpectrum=spectrum,ProgressStyle=style}};
    private static void Arrange(UIElement element){var root=(FrameworkElement)element;root.Measure(new Size(360,220));root.Arrange(new Rect(0,0,360,220));root.UpdateLayout();}
    private static void Tick(NowPlayingItemContent content)=>(typeof(NowPlayingItemContent).GetMethod("PlaybackTick",BindingFlags.Instance|BindingFlags.NonPublic)??throw new MissingMethodException()).Invoke(content,[null,EventArgs.Empty]);
    private static T Field<T>(object owner,string name)=>(T)(owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(owner)??throw new MissingFieldException(owner.GetType().Name,name));
    private static Rect Bounds(FrameworkElement e,FrameworkElement root)=>e.TransformToAncestor(root).TransformBounds(new Rect(new Point(),e.RenderSize));
    private static IEnumerable<T> Desc<T>(DependencyObject parent) where T:DependencyObject{for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var c=VisualTreeHelper.GetChild(parent,i);if(c is T found)yield return found;foreach(var x in Desc<T>(c))yield return x;}}
    private static void Sta(Action a){Exception? error=null;var t=new Thread(()=>{try{a();}catch(Exception ex){error=ex;}});t.SetApartmentState(ApartmentState.STA);t.Start();t.Join();if(error is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();}
    private sealed class Service:INowPlayingService{public NowPlayingSnapshot Snapshot{get;}=new(true,"DeskCanvas.Test!player","v19 test","artist","album",null,NowPlayingState.Playing,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(60),true,true,true,true,DateTimeOffset.UtcNow);public event EventHandler<NowPlayingSnapshot>? SnapshotChanged{add{}remove{}}public IDisposable Acquire()=>Lease.Instance;public Task<bool> PreviousAsync()=>Task.FromResult(true);public Task<bool> PlayPauseAsync()=>Task.FromResult(true);public Task<bool> NextAsync()=>Task.FromResult(true);public Task<bool> SeekAsync(TimeSpan p)=>Task.FromResult(true);public void Dispose(){}}
    private sealed class Reader:IAudioSpectrumReader{internal int ReadCount{get;private set;}internal bool Disposed{get;private set;}public IReadOnlyList<double> ReadBands(){ReadCount++;return[0,.1,.2,.3,.2,.1,0];}public void Dispose()=>Disposed=true;}
    private sealed class Lease:IDisposable{internal static readonly Lease Instance=new();public void Dispose(){}}
}

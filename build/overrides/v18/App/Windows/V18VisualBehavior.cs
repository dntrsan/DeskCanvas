using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeskCanvas.App.Services;
using WpfPath=System.Windows.Shapes.Path;
using WpfColor=System.Windows.Media.Color;

namespace DeskCanvas.App.Windows;

internal static class V18VisualBehavior
{
 [System.Runtime.CompilerServices.ModuleInitializer]internal static void Install()=>EventManager.RegisterClassHandler(typeof(FrameworkElement),FrameworkElement.LoadedEvent,new RoutedEventHandler(Loaded));
 private static void Loaded(object sender,RoutedEventArgs _){if(sender is StackPanel panel)CenterTransport(panel);if(sender is Rectangle artwork)ApplyArtworkPalette(artwork);if(sender is WpfPath path)AttachSparkSmoother(path);}
 private static void CenterTransport(StackPanel old)
 {
  if(old.Tag is string)return;var children=old.Children.Cast<UIElement>().ToArray();if(children.Length!=4||children.Take(3).Any(x=>x is not Button)||children[3] is not StackPanel spectrum||spectrum.Children.Count!=7)return;var parent=VisualTreeHelper.GetParent(old)as Grid;if(parent is null)return;var index=parent.Children.IndexOf(old);if(index<0)return;old.Tag="v18-replaced";var grid=new Grid{Height=old.Height,Margin=old.Margin,Tag="v18-centered"};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(35)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(35)});var trio=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};for(var i=0;i<3;i++)trio.Children.Add(children[i]);spectrum.Width=35;spectrum.Margin=new Thickness(0);spectrum.HorizontalAlignment=HorizontalAlignment.Right;grid.Children.Add(trio);Grid.SetColumn(trio,1);grid.Children.Add(spectrum);Grid.SetColumn(spectrum,2);Grid.SetRow(grid,Grid.GetRow(old));parent.Children.RemoveAt(index);parent.Children.Insert(index,grid);
 }
 private static void ApplyArtworkPalette(Rectangle artwork)
 {
  if(artwork.Tag is string||artwork.Fill is not ImageBrush{ImageSource:BitmapSource source})return;try{var pixels=Read(source);if(!ArtworkPaletteMathV18.TryResolve(pixels,out var palette))return;var card=Ancestor<Border>(artwork);if(card is null)return;artwork.Tag="v18-palette";var surfaceA=WpfColor.FromRgb(palette.SurfaceA.R,palette.SurfaceA.G,palette.SurfaceA.B);var surfaceB=WpfColor.FromRgb(palette.SurfaceB.R,palette.SurfaceB.G,palette.SurfaceB.B);card.Background=new LinearGradientBrush(new GradientStopCollection{new GradientStop(WpfColor.FromArgb(255,surfaceA.R,surfaceA.G,surfaceA.B),0),new GradientStop(WpfColor.FromArgb(255,surfaceB.R,surfaceB.G,surfaceB.B),1)},135);card.Foreground=new SolidColorBrush(WpfColor.FromRgb(palette.Foreground.R,palette.Foreground.G,palette.Foreground.B));}catch{}
 }
 private static List<PaletteRgb> Read(BitmapSource source){var width=Math.Max(1,source.PixelWidth),height=Math.Max(1,source.PixelHeight);var pixels=new byte[width*height*4];source.CopyPixels(pixels,width*4,0);var step=Math.Max(1,Math.Min(width,height)/32);var result=new List<PaletteRgb>();for(var y=0;y<height;y+=step)for(var x=0;x<width;x+=step){var i=(y*width+x)*4;if(pixels[i+3]>=224)result.Add(new PaletteRgb(pixels[i+2],pixels[i+1],pixels[i]));}return result;}
 private static T? Ancestor<T>(DependencyObject item)where T:DependencyObject{for(var current=VisualTreeHelper.GetParent(item);current is not null;current=VisualTreeHelper.GetParent(current))if(current is T found)return found;return null;}
 private static void AttachSparkSmoother(WpfPath path)
 {
  if(path.Tag is string||!IsSpark(path))return;path.Tag="v18-spark";var smoother=new SparkSmoother(path);DependencyPropertyDescriptor.FromProperty(WpfPath.DataProperty,typeof(WpfPath)).AddValueChanged(path,smoother.TargetChanged);path.Unloaded+=(_,_)=>smoother.Dispose();path.IsVisibleChanged+=(_,e)=>{if(e.NewValue is false)smoother.Stop();};
 }
 private static bool IsSpark(WpfPath path)=>path.StrokeThickness is >1 and <2&&Ancestor<Grid>(path)?.Children.OfType<TextBlock>().Any(x=>x.Text is "DOWN" or "UP")==true;
 private sealed class SparkSmoother:IDisposable
 {
  private readonly WpfPath path;private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(16)};private Point[] start=[],target=[];private TimeSpan elapsed;private bool applying;
  internal SparkSmoother(WpfPath path){this.path=path;timer.Tick+=Tick;}
  internal void TargetChanged(object? _,EventArgs __){if(applying||path.Data is not PathGeometry geometry)return;var points=Points(geometry);if(points.Length<2)return;start=target.Length==points.Length?target:points;target=points;elapsed=TimeSpan.Zero;if(path.IsVisible)timer.Start();}
  private void Tick(object? _,EventArgs __){elapsed+=timer.Interval;var t=Math.Clamp(elapsed.TotalMilliseconds/700d,0,1);t=1-Math.Pow(1-t,3);var points=Enumerable.Range(0,target.Length).Select(i=>new Point(target[i].X+(start[i].X-target[i].X)*(1-t),Math.Clamp(target[i].Y+(start[i].Y-target[i].Y)*(1-t),0,16))).ToArray();applying=true;path.Data=Bezier(points);applying=false;if(t>=1)timer.Stop();}
  internal void Stop()=>timer.Stop();public void Dispose()=>timer.Stop();private static Point[] Points(PathGeometry geometry)=>geometry.Figures.SelectMany(x=>new[]{x.StartPoint}.Concat(x.Segments.OfType<LineSegment>().Select(s=>s.Point))).ToArray();private static Geometry Bezier(IReadOnlyList<Point> points){if(points.Count==0)return Geometry.Empty;var figure=new PathFigure{StartPoint=points[0]};for(var i=1;i<points.Count;i++){var previous=points[i-1];var point=points[i];var mid=(previous.X+point.X)/2;figure.Segments.Add(new BezierSegment(new Point(mid,Math.Clamp(previous.Y,0,16)),new Point(mid,Math.Clamp(point.Y,0,16)),new Point(point.X,Math.Clamp(point.Y,0,16)),true));}return new PathGeometry([figure]);}
 }
}

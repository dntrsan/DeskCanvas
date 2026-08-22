using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskCanvas.Core;
using WpfComboBox=System.Windows.Controls.ComboBox;
using WpfPath=System.Windows.Shapes.Path;

namespace DeskCanvas.App.Windows;
internal static class V19RuntimePolish
{
 private sealed record ProgressChoice(NowPlayingProgressStyle Value,string Label){public override string ToString()=>Label;}
 private static readonly ProgressChoice[] Choices=[new(NowPlayingProgressStyle.Simple,"シンプル"),new(NowPlayingProgressStyle.Wave,"ウェーブ"),new(NowPlayingProgressStyle.Dots,"ドット"),new(NowPlayingProgressStyle.Hearts,"ハート")];
 [ModuleInitializer]internal static void Install(){EventManager.RegisterClassHandler(typeof(Canvas),FrameworkElement.LoadedEvent,new RoutedEventHandler(CanvasLoaded));EventManager.RegisterClassHandler(typeof(WpfComboBox),FrameworkElement.LoadedEvent,new RoutedEventHandler(ComboLoaded));}
 private static void CanvasLoaded(object sender,RoutedEventArgs args){if(sender is not Canvas canvas)return;canvas.LayoutUpdated+=(_,_)=>CorrectWave(canvas);}
 private static void CorrectWave(Canvas canvas){if(canvas.ActualWidth<=0||canvas.Children.Count!=2||canvas.Children[0] is not WpfPath first||canvas.Children[1] is not WpfPath second||first.Data is not StreamGeometry||second.Data is not StreamGeometry)return;first.Data=Wave(canvas.ActualWidth);second.Data=Wave(canvas.ActualWidth);}
 internal static StreamGeometry Wave(double width){var geometry=new StreamGeometry();using var context=geometry.Open();context.BeginFigure(new Point(0,8),false,false);var up=true;for(var x=0d;x<width;x+=9d){var end=Math.Min(width,x+9d);var control=Math.Min(width,x+4.5d);context.QuadraticBezierTo(new Point(control,up?6:10),new Point(end,8),true,true);up=!up;}geometry.Freeze();return geometry;}
 private static void ComboLoaded(object sender,RoutedEventArgs args){if(sender is not WpfComboBox combo||combo.Tag is ProgressChoice)return;if(combo.ItemsSource is not IEnumerable values||!values.Cast<object>().Any(value=>value is NowPlayingProgressStyle))return;var current=combo.SelectedItem is NowPlayingProgressStyle style&&Enum.IsDefined(style)?style:NowPlayingProgressStyle.Simple;combo.Tag=Choices[0];combo.ItemsSource=Choices;combo.SelectedItem=Choices.First(choice=>choice.Value==current);combo.SelectionChanged+=(_,_)=>{if(combo.SelectedItem is ProgressChoice choice)ApplyStyle(combo,choice.Value);};}
 private static void ApplyStyle(FrameworkElement control,NowPlayingProgressStyle style){for(DependencyObject? current=control;current is not null;current=VisualTreeHelper.GetParent(current)){if(current is MainWindow main){var item=typeof(MainWindow).GetProperty("SelectedItem",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(main)as CanvasItem;if(item?.ContentKind!=CanvasContentKinds.NowPlaying)return;item.NowPlaying.ProgressStyle=style;var controller=typeof(MainWindow).GetField("controller",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(main);controller?.GetType().GetMethod("SetTheme",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.Invoke(controller,[item,item.Theme]);return;}if(current is BuiltInContentPicker picker){var item=typeof(BuiltInContentPicker).GetField("previewItem",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(picker)as CanvasItem;if(item is null)return;item.NowPlaying.ProgressStyle=style;var preview=typeof(BuiltInContentPicker).GetField("preview",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(picker)as IDesktopItemContent;if(preview is NowPlayingItemContent playing)playing.Refresh();return;}}}
}

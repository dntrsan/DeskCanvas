using System.Runtime.CompilerServices;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using WpfPath=System.Windows.Shapes.Path;
namespace DeskCanvas.App.Windows;
internal static class V30SystemReviewPolish
{
 [ModuleInitializer]internal static void Register()=>EventManager.RegisterClassHandler(typeof(Border),FrameworkElement.LoadedEvent,new RoutedEventHandler(Loaded));
 private static void Loaded(object sender,RoutedEventArgs e){if(sender is not Border root||!IsSystem(root))return;Apply(root);root.SizeChanged+=(_,__)=>Apply(root);}
 private static void Apply(Border root){var pad=Math.Clamp(Math.Min(Math.Max(0,root.ActualWidth),Math.Max(0,root.ActualHeight))*.052,5,12);root.Padding=new Thickness(pad);foreach(var path in Descendants(root).OfType<WpfPath>())path.Stretch=Stretch.Fill;}
 private static bool IsSystem(Border root)=>Descendants(root).OfType<TextBlock>().Any(t=>t.Text=="SYSTEM STATUS");private static IEnumerable<DependencyObject> Descendants(DependencyObject r){for(var i=0;i<VisualTreeHelper.GetChildrenCount(r);i++){var c=VisualTreeHelper.GetChild(r,i);yield return c;foreach(var d in Descendants(c))yield return d;}}
}

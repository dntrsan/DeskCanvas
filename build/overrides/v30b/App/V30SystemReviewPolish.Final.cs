using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfPath=System.Windows.Shapes.Path;
namespace DeskCanvas.App.Windows;
internal static class V30SystemReviewPolish
{
 [ModuleInitializer]internal static void Register()=>EventManager.RegisterClassHandler(typeof(Border),FrameworkElement.LoadedEvent,new RoutedEventHandler(Loaded));
 private static void Loaded(object sender,RoutedEventArgs e){if(sender is not Border root||!IsSystem(root))return;root.Padding=new Thickness(0);foreach(var path in Descendants(root).OfType<WpfPath>())path.Stretch=Stretch.Fill;}
 private static bool IsSystem(Border root)=>Descendants(root).OfType<TextBlock>().Any(text=>text.Text=="SYSTEM STATUS");
 private static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var desc in Descendants(child))yield return desc;}}
}

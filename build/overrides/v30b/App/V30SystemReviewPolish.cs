using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace DeskCanvas.App.Windows;
// Final visual guard for adaptive System cards. It is intentionally scoped by header text,
// so Clock/Now Playing/card decoration are untouched.
internal static class V30SystemReviewPolish
{
 [ModuleInitializer]internal static void Register()=>EventManager.RegisterClassHandler(typeof(Border),FrameworkElement.LoadedEvent,new RoutedEventHandler(Loaded));
 private static void Loaded(object sender,RoutedEventArgs e)
 {
  if(sender is not Border root||!IsSystem(root))return;
  root.Padding=new Thickness(0); // SystemMonitorItemContent already reserves adaptive inner padding in its row budget.
  foreach(var path in Descendants(root).OfType<Path>())path.Stretch=Stretch.Fill; // the 60-DIP arc geometry must shrink with its ring.
 }
 private static bool IsSystem(Border root)=>Descendants(root).OfType<TextBlock>().Any(text=>text.Text=="SYSTEM STATUS");
 private static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var desc in Descendants(child))yield return desc;}}
}

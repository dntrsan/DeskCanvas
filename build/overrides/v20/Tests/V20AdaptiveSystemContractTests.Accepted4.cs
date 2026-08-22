using System.Runtime.CompilerServices;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V20AdaptiveSystemContractTests
{
 [ModuleInitializer] internal static void Run()
 {
  var item=new CanvasItem{Width=300,Height=390,CenterY=400};
  var top=item.CenterY-item.Height/2;
  if(!SystemMonitorLayoutMath.KeepTop(item,155.2)||item.Height!=156||Math.Abs((item.CenterY-item.Height/2)-top)>.001||item.Width!=300)throw new InvalidOperationException("system adaptive height moved top edge or width");
  if(SystemMonitorLayoutMath.KeepTop(item,155.9))throw new InvalidOperationException("sub-half-DIP adaptive change should not re-layout");
  SystemMonitorLayoutMath.KeepTop(item,0);if(item.Height!=96)throw new InvalidOperationException("system minimum height was not preserved");
  if(!SystemMonitorLayoutMath.DividerVisible(true,true)||SystemMonitorLayoutMath.DividerVisible(true,false)||SystemMonitorLayoutMath.DividerVisible(false,true)||SystemMonitorLayoutMath.DividerVisible(false,false))throw new InvalidOperationException("system divider visibility is not coupled to groups");
  Console.WriteLine("PASS v2.0 System Status adaptive layout keeps top, width, and group divider state");
 }
}

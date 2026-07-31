using System.Runtime.CompilerServices;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V20AdaptiveSystemContractTests
{
 [ModuleInitializer] internal static void Run(){var item=new CanvasItem{Height=390,CenterY=400};var top=item.CenterY-item.Height/2;SystemMonitorLayoutMath.KeepTop(item,155.2);if(item.Height!=156||Math.Abs((item.CenterY-item.Height/2)-top)>.001)throw new InvalidOperationException("system adaptive height moved top edge");SystemMonitorLayoutMath.KeepTop(item,0);if(item.Height!=96)throw new InvalidOperationException("system minimum height was not preserved");Console.WriteLine("PASS v2.0 System Status adaptive height preserves top edge");}
}

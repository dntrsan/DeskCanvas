using System.Runtime.CompilerServices;
using DeskCanvas.Core;

// The pre-v30 WPF contract tests import DeskCanvas.App.Windows and use this helper
// unqualified. Keep their pure compatibility surface while v30 uses adaptive layout.
internal static class SystemMonitorLayoutMath
{
    internal static double NaturalHeight(double desired) => Math.Max(96, Math.Ceiling(double.IsFinite(desired) ? desired : 96));
    internal static bool KeepTop(CanvasItem item, double desired) { var next = NaturalHeight(desired); if (Math.Abs(item.Height - next) < .5) return false; var top = item.CenterY - item.Height / 2; item.Height = next; item.CenterY = top + next / 2; return true; }
    internal static bool DividerVisible(bool metrics, bool network) => metrics && network;
}
internal static class V30SystemLayerContractTests
{
    [ModuleInitializer] internal static void Run(){LayerMoves();LegacyOrderLoads();Persistence();ResponsiveValues();Console.WriteLine("PASS v3.0 system responsive layout and layer ordering");}
    private static void LayerMoves(){var a=new CanvasItem{ZIndex=int.MaxValue};var b=new CanvasItem{ZIndex=int.MinValue};var c=new CanvasItem{ZIndex=int.MinValue};IList<CanvasItem> items=new List<CanvasItem>{a,b,c};LayerOrderMath.NormalizeInto(items);Equal(b,items[0]);Equal(c,items[1]);Equal(a,items[2]);Equal(true,LayerOrderMath.MoveUp(items,b));Equal(b,items[1]);Equal(true,LayerOrderMath.MoveToFront(items,b));Equal(b,items[^1]);Equal(true,LayerOrderMath.MoveDown(items,b));Equal(b,items[^2]);Equal(true,LayerOrderMath.MoveToBack(items,b));Equal(b,items[0]);Equal(false,LayerOrderMath.MoveDown(items,b));Equal(false,LayerOrderMath.MoveToFront(items,items[^1]);for(var i=0;i<items.Count;i++)Equal(i,items[i].ZIndex);}
    private static void LegacyOrderLoads(){InTemp(root=>{File.WriteAllText(Path.Combine(root,"layout.json"),"""{ "items":[ {"displayName":"first","contentKind":"image","zIndex":7}, {"displayName":"second","contentKind":"gif","zIndex":7}, {"displayName":"third","contentKind":"clock","zIndex":-2147483648} ] }""");var items=new LayoutRepository(root).Load().Items;Equal("third",items[0].DisplayName);Equal("first",items[1].DisplayName);Equal("second",items[2].DisplayName);for(var i=0;i<items.Count;i++)Equal(i,items[i].ZIndex);});}
    private static void Persistence(){InTemp(root=>{var layout=new CanvasLayout{Items=[new CanvasItem{DisplayName="a",ZIndex=4},new CanvasItem{DisplayName="b",ZIndex=4}]};var repo=new LayoutRepository(root);repo.Save(layout);var loaded=repo.Load();Equal("a",loaded.Items[0].DisplayName);Equal("b",loaded.Items[1].DisplayName);Equal(0,loaded.Items[0].ZIndex);Equal(1,loaded.Items[1].ZIndex);});}
    private static void ResponsiveValues(){var standard=Values(300,180,5,true);var tiny=Values(96,96,5,true);var tall=Values(180,420,1,false);if(standard.Row<=0||tiny.Row<=0||tall.Row<=standard.Row||!tiny.Dense)throw new InvalidOperationException("invalid responsive values");}
    private static (double Row,bool Dense) Values(double width,double height,int rows,bool divider){var h=Math.Max(96,height);var pad=Math.Clamp(Math.Min(Math.Max(96,width),h)*.052,5,12);var header=Math.Clamp(h*.105,12,24);var gap=divider?Math.Clamp(h*.075,5,14):0;return(Math.Max(14,(h-pad*2-header-gap)/Math.Max(1,rows)),width<220||height<160);}
    private static void InTemp(Action<string> action){var root=Path.Combine(Path.GetTempPath(),"DeskCanvasLayerTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{action(root);}finally{Directory.Delete(root,true);}}private static void Equal<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new InvalidOperationException($"expected={expected}, actual={actual}");}
}

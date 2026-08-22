using System.Runtime.CompilerServices;
using DeskCanvas.Core;

internal static class SystemMonitorLayoutMath
{
    internal static double NaturalHeight(double desired)=>Math.Max(96,Math.Ceiling(double.IsFinite(desired)?desired:96));
    internal static bool KeepTop(CanvasItem item,double desired){var next=NaturalHeight(desired);if(Math.Abs(item.Height-next)<.5)return false;var top=item.CenterY-item.Height/2;item.Height=next;item.CenterY=top+next/2;return true;}
    internal static bool DividerVisible(bool metrics,bool network)=>metrics&&network;
}
internal static class V30SystemLayerContractTests
{
 [ModuleInitializer]internal static void Run(){var a=new CanvasItem{ZIndex=int.MaxValue};var b=new CanvasItem{ZIndex=int.MinValue};var c=new CanvasItem{ZIndex=int.MinValue};IList<CanvasItem> items=new List<CanvasItem>{a,b,c};LayerOrderMath.NormalizeInto(items);Check(ReferenceEquals(items[0],b)&&ReferenceEquals(items[1],c));Check(LayerOrderMath.MoveUp(items,b)&&ReferenceEquals(items[1],b));Check(LayerOrderMath.MoveToFront(items,b)&&ReferenceEquals(items[^1],b));Check(LayerOrderMath.MoveDown(items,b)&&ReferenceEquals(items[^2],b));Check(LayerOrderMath.MoveToBack(items,b)&&ReferenceEquals(items[0],b));Check(!LayerOrderMath.MoveDown(items,b));for(var i=0;i<items.Count;i++)Check(items[i].ZIndex==i);RoundTrip();var row=(180-12*2-19-14)/5d;Check(row>20);Console.WriteLine("PASS v3.0 system responsive layout and layer ordering");}
 private static void RoundTrip(){var root=Path.Combine(Path.GetTempPath(),"DeskCanvasLayerTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{var repo=new LayoutRepository(root);repo.Save(new CanvasLayout{Items=[new CanvasItem{DisplayName="a",ZIndex=4},new CanvasItem{DisplayName="b",ZIndex=4}]});var load=repo.Load().Items;Check(load[0].DisplayName=="a"&&load[1].DisplayName=="b"&&load[0].ZIndex==0&&load[1].ZIndex==1);}finally{Directory.Delete(root,true);}}
 private static void Check(bool value){if(!value)throw new InvalidOperationException("v30 system/layer contract");}
}

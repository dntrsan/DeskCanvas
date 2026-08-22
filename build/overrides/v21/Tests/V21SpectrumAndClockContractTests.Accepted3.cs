using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;
internal static class V21SpectrumAndClockContractTestsAccepted3
{
 [ModuleInitializer] internal static void Run()
 {
  var fast=SpectrumFrameSmoothing.Step(0,1,.075);var slow=SpectrumFrameSmoothing.Step(1,0,.075);if(fast<=.60||slow>=.85||fast>=slow)throw new InvalidOperationException("spectrum attack/release contract changed");
  var two=SpectrumFrameSmoothing.Step(SpectrumFrameSmoothing.Step(0,1,1d/60),1,1d/60);var one=SpectrumFrameSmoothing.Step(0,1,1d/30);if(Math.Abs(two-one)>.012||SpectrumFrameSmoothing.Step(.4,.9,.02)<=.4||SpectrumFrameSmoothing.Step(.4,.1,.02)>=.4)throw new InvalidOperationException("spectrum frame independence or retarget contract changed");
  var reader=new Reader();using(var controller=new AudioSpectrumLeaseController(()=>reader)){controller.Poll(true,_=>{});if(reader.ReadCount!=1||!Bool(controller,"rendering")||Value(controller,"reader") is null)throw new InvalidOperationException("spectrum lifecycle did not start rendering and lease together");controller.Suspend();if(!reader.Disposed||Bool(controller,"rendering")||Value(controller,"reader") is not null)throw new InvalidOperationException("spectrum suspend left rendering subscription or lease alive");controller.Dispose();controller.Dispose();}
  using(var paused=new AudioSpectrumLeaseController(static()=>throw new InvalidOperationException("reader must not be created while paused")))paused.Poll(false,_=>{});
  var clock=new CanvasItem{ContentKind=CanvasContentKinds.Clock,Width=12,Height=31};if(clock.Width!=32||clock.Height!=32)throw new InvalidOperationException("Clock CanvasItem minimum size was not lowered to 32 DIP");clock.ContentKind=CanvasContentKinds.Image;if(clock.Width!=48||clock.Height!=48)throw new InvalidOperationException("non-clock CanvasItem minimum changed");if(ClockCompactLayoutMath.Scale(64,64) is <=0 or >=1||ClockCompactLayoutMath.Scale(240,160)!=1)throw new InvalidOperationException("clock compact scale contract changed");Console.WriteLine("PASS v2.1 spectrum rendering lifecycle and compact clock layout");
 }
 private static object? Value(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(o);private static bool Bool(object o,string n)=>Value(o,n) is true;
 private sealed class Reader:IAudioSpectrumReader{internal int ReadCount;internal bool Disposed;public IReadOnlyList<double> ReadBands(){ReadCount++;return [0,.2,.4,.8,.4,.2,0];}public void Dispose()=>Disposed=true;}
}

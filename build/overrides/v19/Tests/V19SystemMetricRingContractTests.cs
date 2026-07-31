using System.Runtime.CompilerServices;
using DeskCanvas.App.Windows;

internal static class V19SystemMetricRingContractTests
{
 [ModuleInitializer]
 internal static void Run()
 {
  if(SystemMetricRingMath.Clamp(double.NaN)!=0||SystemMetricRingMath.Clamp(-4)!=0||SystemMetricRingMath.Clamp(101)!=100||SystemMetricRingMath.Clamp(42.5)!=42.5)throw new InvalidOperationException("ring clamp contract failed");
  Near(0,SystemMetricRingMath.CubicEaseOut(0,100,0));
  Near(87.5,SystemMetricRingMath.CubicEaseOut(0,100,.5));
  Near(100,SystemMetricRingMath.CubicEaseOut(0,100,1));
  // A new sample starts at the currently rendered arc, not at the previous target.
  Near(76.25,SystemMetricRingMath.CubicEaseOut(50,80,.5));
  Console.WriteLine("PASS v1.9 metric ring easing and clamp");
 }
 private static void Near(double expected,double actual){if(Math.Abs(expected-actual)>.0001)throw new InvalidOperationException($"expected={expected}, actual={actual}");}
}

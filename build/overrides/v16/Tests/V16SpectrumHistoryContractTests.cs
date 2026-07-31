using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V16SpectrumHistoryContractTests
{
 [ModuleInitializer] internal static void Run()
 {
  var real=new double[8];var imag=new double[8];real[1]=1;SpectrumMath.Fft(real,imag);if(real.All(value=>Math.Abs(value)<.001))throw new InvalidOperationException("fft did not transform");
  var bands=SpectrumMath.ToLogBands(real,imag,48000);if(bands.Length!=7||bands.Any(value=>value<0||value>1))throw new InvalidOperationException("log bands invalid");
  if(SpectrumMath.Smooth(.2,.8)<=.2||SpectrumMath.Smooth(.8,.2)>=.8)throw new InvalidOperationException("smoothing invalid");
  var history=new NetworkHistory(3);history.Add(1024);history.Add(2048);history.Add(4096);history.Add(8192);var values=history.Values;if(values.Count!=3||values[0]!=2048||history.Normalized().Last()!=1)throw new InvalidOperationException("history ring invalid");
  Console.WriteLine("PASS v1.6 FFT bands smoothing and network history");
 }
}

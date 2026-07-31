using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V16HardeningContractTests
{
 [ModuleInitializer] internal static void Run()
 {
  var rate=48000; var low=Tone(100,rate); var high=Tone(8000,rate); var lowBands=SpectrumMath.AnalyzeSamples(low,rate); var highBands=SpectrumMath.AnalyzeSamples(high,rate); if(Max(lowBands)>1||Max(highBands)<5)throw new InvalidOperationException("frequency bands did not separate"); if(SpectrumMath.AnalyzeSamples(new double[2048],rate).Any(x=>x>.0001))throw new InvalidOperationException("silence was not zero"); if(!(SpectrumMath.Smooth(.2,.8)-.2 > .8-SpectrumMath.Smooth(.8,.2)))throw new InvalidOperationException("attack was not faster than release");
  if(AudioSampleFormatMath.Resolve(1,16)!=AudioSampleFormat.Pcm16||AudioSampleFormatMath.Resolve(3,32)!=AudioSampleFormat.Float32||AudioSampleFormatMath.Resolve(3,16)!=AudioSampleFormat.Unsupported)throw new InvalidOperationException("sample format resolver failed");
  var builds=0; var first=new FakeSpectrum([.5,.5,.5,.5,.5,.5,.5]); var controller=new AudioSpectrumLeaseController(()=>{builds++;return first;}); IReadOnlyList<double> seen=[]; controller.Poll(false,x=>seen=x); if(builds!=0||seen.Any(x=>x!=0))throw new InvalidOperationException("paused constructed reader"); controller.Poll(true,x=>seen=x); controller.Poll(true,x=>seen=x); if(builds!=1||first.Reads!=2)throw new InvalidOperationException("reader reuse failed"); controller.Poll(false,x=>seen=x); if(first.Disposals!=1)throw new InvalidOperationException("paused did not dispose"); var broken=new FakeSpectrum([]){Throw=true}; var failing=new AudioSpectrumLeaseController(()=>broken); failing.Poll(true,x=>seen=x); if(broken.Disposals!=1||seen.Any(x=>x!=0))throw new InvalidOperationException("exception lifecycle failed"); controller.Dispose();controller.Dispose();failing.Dispose();
  var graph=NetworkGraphMath.Points([0d,.5,1]); if(graph.Count!=3||graph[0].X!=0||graph[2].X!=54||graph[0].Y!=16||graph[2].Y!=2)throw new InvalidOperationException("graph order/bounds failed"); var firstHistory=new NetworkHistory(3);var secondHistory=new NetworkHistory(3);firstHistory.Add(4096);secondHistory.Add(0);if(firstHistory.Normalized()[0]==secondHistory.Normalized()[0])throw new InvalidOperationException("network histories not independent");
  Console.WriteLine("PASS v1.6 spectrum tone separation leases formats and graph math");
 }
 private static double[] Tone(double hz,int rate)=>Enumerable.Range(0,2048).Select(i=>Math.Sin(2*Math.PI*hz*i/rate)*.8).ToArray(); private static int Max(IReadOnlyList<double> values)=>Enumerable.Range(0,values.Count).OrderByDescending(i=>values[i]).First(); private sealed class FakeSpectrum(double[] values):IAudioSpectrumReader{internal int Reads,Disposals;internal bool Throw;public IReadOnlyList<double> ReadBands(){Reads++;if(Throw)throw new InvalidOperationException();return values;}public void Dispose()=>Disposals++;}
}

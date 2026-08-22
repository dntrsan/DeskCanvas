using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V20TimelineInjectedStabilityTests
{
 [ModuleInitializer]internal static void Run()
 {
  var first=new FakeSession(Data(TimeSpan.FromSeconds(8),NowPlayingState.Playing));var manager=new FakeManager(first);
  using var service=new NowPlayingService(new FakeProvider(manager),new NoGlobalCommands(),TimeSpan.FromMilliseconds(15));using var lease=service.Acquire();
  Until(()=>service.Snapshot.HasSession&&service.Snapshot.End==TimeSpan.FromSeconds(60));Thread.Sleep(35);var before=service.Snapshot.Position;
  // A manager can recreate its adapter with a stale zero timeline.  The same
  // song must retain the projected position and known duration.
  var reconnected=new FakeSession(Data(TimeSpan.Zero,NowPlayingState.Playing));manager.Set(reconnected);Until(()=>service.Snapshot.HasSession&&service.Snapshot.Title=="same");Require(service.Snapshot.End==TimeSpan.FromSeconds(60),"same-session reconnect lost End");Require(service.Snapshot.Position>=before-TimeSpan.FromMilliseconds(80),"same-session reconnect rewound position");
  before=service.Snapshot.Position;Thread.Sleep(45);Require(service.Snapshot.Position>=before,"reconciliation stale zero rewound position");
  reconnected.Set(Data(TimeSpan.FromSeconds(2),NowPlayingState.Playing));reconnected.RaiseTimeline();Until(()=>service.Snapshot.Position<=TimeSpan.FromSeconds(2.2));
  Require(service.SeekAsync(TimeSpan.FromSeconds(4)).GetAwaiter().GetResult(),"seek was rejected");Until(()=>service.Snapshot.Position>=TimeSpan.FromSeconds(3.9)&&service.Snapshot.Position<=TimeSpan.FromSeconds(4.2));
  var pausedAt=service.Snapshot.Position;reconnected.Set(Data(TimeSpan.Zero,NowPlayingState.Paused));reconnected.RaisePlayback();Until(()=>service.Snapshot.State==NowPlayingState.Paused);Require(service.Snapshot.Position>=pausedAt-TimeSpan.FromMilliseconds(80),"pause stale read rewound position");
  reconnected.Set(Data(TimeSpan.Zero,NowPlayingState.Playing,title:"other"));reconnected.RaiseMedia();Until(()=>service.Snapshot.Title=="other"&&service.Snapshot.Position<=TimeSpan.FromMilliseconds(100));
  manager.Set(null);Until(()=>!service.Snapshot.HasSession);Console.WriteLine("PASS v2.0 injected GSMTC stale-position stability");
 }
 private static NowPlayingData Data(TimeSpan position,NowPlayingState state,string title="same")=>new("app",title,"artist","album",null,state,position,TimeSpan.FromSeconds(60),false,true,false,true,DateTimeOffset.UtcNow);
 private static void Until(Func<bool> condition){var deadline=DateTime.UtcNow+TimeSpan.FromSeconds(3);while(!condition()){if(DateTime.UtcNow>=deadline)throw new InvalidOperationException("timed out waiting for fake GSMTC update");Thread.Sleep(10);}}
 private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
 private sealed class FakeProvider(FakeManager manager):INowPlayingManagerProvider{public Task<INowPlayingManager> RequestAsync()=>Task.FromResult<INowPlayingManager>(manager);}
 private sealed class NoGlobalCommands:IGlobalMediaCommandSender{public bool IsAvailable=>false;public bool TrySend(NowPlayingCommand command)=>false;}
 private sealed class FakeManager(INowPlayingSession? current):INowPlayingManager
 {private INowPlayingSession? current=current;public INowPlayingSession? CurrentSession=>current;public event EventHandler? CurrentSessionChanged;public void Set(INowPlayingSession? next){current=next;CurrentSessionChanged?.Invoke(this,EventArgs.Empty);}public void Dispose(){}}
 private sealed class FakeSession(NowPlayingData data):INowPlayingSession
 {private NowPlayingData data=data;public event EventHandler? MediaPropertiesChanged;public event EventHandler? PlaybackInfoChanged;public event EventHandler? TimelinePropertiesChanged;public Task<NowPlayingData> ReadAsync()=>Task.FromResult(data);public Task<bool> TryCommandAsync(NowPlayingCommand command,TimeSpan position=default){if(command==NowPlayingCommand.Seek)data=data with{Position=position,TimelineObservedAt=DateTimeOffset.UtcNow};return Task.FromResult(true);}public void Set(NowPlayingData value)=>data=value;public void RaiseMedia()=>MediaPropertiesChanged?.Invoke(this,EventArgs.Empty);public void RaisePlayback()=>PlaybackInfoChanged?.Invoke(this,EventArgs.Empty);public void RaiseTimeline()=>TimelinePropertiesChanged?.Invoke(this,EventArgs.Empty);public void Dispose(){}}
}

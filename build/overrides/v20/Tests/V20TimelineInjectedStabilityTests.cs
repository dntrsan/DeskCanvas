using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V20TimelineInjectedStabilityTests
{
 [ModuleInitializer]internal static void Run()=>VerifyAsync().GetAwaiter().GetResult();
 private static async Task VerifyAsync()
 {
  var first=new FakeSession(Data(TimeSpan.FromSeconds(8),NowPlayingState.Playing));var manager=new FakeManager(first);
  using var service=new NowPlayingService(new FakeProvider(manager),new NoGlobalCommands(),TimeSpan.FromMilliseconds(15));using var lease=service.Acquire();
  await UntilAsync(()=>service.Snapshot.HasSession&&service.Snapshot.End==TimeSpan.FromSeconds(60));await Task.Delay(35);var before=service.Snapshot.Position;
  // A manager recreates its adapter during the same song with its stale zero
  // timeline.  It must keep the projected position and known duration.
  var reconnected=new FakeSession(Data(TimeSpan.Zero,NowPlayingState.Playing));manager.Set(reconnected);await UntilAsync(()=>service.Snapshot.HasSession&&service.Snapshot.Title=="same");Require(service.Snapshot.End==TimeSpan.FromSeconds(60),"same-session reconnect lost End");Require(service.Snapshot.Position>=before-TimeSpan.FromMilliseconds(80),"same-session reconnect rewound position");
  before=service.Snapshot.Position;await Task.Delay(45);Require(service.Snapshot.Position>=before,"reconciliation stale zero rewound position");
  reconnected.Set(Data(TimeSpan.FromSeconds(2),NowPlayingState.Playing));reconnected.RaiseTimeline();await UntilAsync(()=>service.Snapshot.Position<=TimeSpan.FromSeconds(2.2));
  Require(await service.SeekAsync(TimeSpan.FromSeconds(4)),"seek was rejected");await UntilAsync(()=>service.Snapshot.Position>=TimeSpan.FromSeconds(3.9)&&service.Snapshot.Position<=TimeSpan.FromSeconds(4.2));
  var pausedAt=service.Snapshot.Position;reconnected.Set(Data(TimeSpan.Zero,NowPlayingState.Paused));reconnected.RaisePlayback();await UntilAsync(()=>service.Snapshot.State==NowPlayingState.Paused);Require(service.Snapshot.Position>=pausedAt-TimeSpan.FromMilliseconds(80),"pause stale read rewound position");
  reconnected.Set(Data(TimeSpan.Zero,NowPlayingState.Playing,title:"other"));reconnected.RaiseMedia();await UntilAsync(()=>service.Snapshot.Title=="other"&&service.Snapshot.Position<=TimeSpan.FromMilliseconds(100));
  manager.Set(null);await UntilAsync(()=>!service.Snapshot.HasSession);Console.WriteLine("PASS v2.0 injected GSMTC stale-position stability");
 }
 private static NowPlayingData Data(TimeSpan position,NowPlayingState state,string title="same")=>new("app",title,"artist","album",null,state,position,TimeSpan.FromSeconds(60),false,true,false,true,DateTimeOffset.UtcNow);
 private static async Task UntilAsync(Func<bool> condition){for(var i=0;i<120;i++){if(condition())return;await Task.Delay(10);}throw new InvalidOperationException("timed out waiting for fake GSMTC update");}
 private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
 private sealed class FakeProvider(FakeManager manager):INowPlayingManagerProvider{public Task<INowPlayingManager> RequestAsync()=>Task.FromResult<INowPlayingManager>(manager);}
 private sealed class NoGlobalCommands:IGlobalMediaCommandSender{public bool IsAvailable=>false;public bool TrySend(NowPlayingCommand command)=>false;}
 private sealed class FakeManager(INowPlayingSession? current):INowPlayingManager
 {private INowPlayingSession? current=current;public INowPlayingSession? CurrentSession=>current;public event EventHandler? CurrentSessionChanged;public void Set(INowPlayingSession? next){current=next;CurrentSessionChanged?.Invoke(this,EventArgs.Empty);}public void Dispose(){}}
 private sealed class FakeSession(NowPlayingData data):INowPlayingSession
 {private NowPlayingData data=data;public event EventHandler? MediaPropertiesChanged;public event EventHandler? PlaybackInfoChanged;public event EventHandler? TimelinePropertiesChanged;public Task<NowPlayingData> ReadAsync()=>Task.FromResult(data);public Task<bool> TryCommandAsync(NowPlayingCommand command,TimeSpan position=default){if(command==NowPlayingCommand.Seek)data=data with{Position=position};return Task.FromResult(true);}public void Set(NowPlayingData value)=>data=value;public void RaiseMedia()=>MediaPropertiesChanged?.Invoke(this,EventArgs.Empty);public void RaisePlayback()=>PlaybackInfoChanged?.Invoke(this,EventArgs.Empty);public void RaiseTimeline()=>TimelinePropertiesChanged?.Invoke(this,EventArgs.Empty);public void Dispose(){}}
}

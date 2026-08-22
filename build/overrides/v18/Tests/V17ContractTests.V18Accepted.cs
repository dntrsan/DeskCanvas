using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V17ContractTests
{
 [ModuleInitializer]internal static void Run()
 {
  var platform=new FakeActivation();var activation=new ActivationService(platform);
  if(!activation.TryActivate("Vendor.Player!App","Ignored title","Artist")||platform.Aumid!="Vendor.Player!App")throw new InvalidOperationException("AUMID activation failed");
  platform.Reset();platform.ProcessResult=false;
  if(!activation.TryActivate("FoDC299D809B9700","A Very Distinct Song Name","Artist")||platform.Process!="FoDC299D809B9700"||platform.Matches!=1)throw new InvalidOperationException("invalid executable did not safely fall back to unique title");
  platform.Reset();platform.MatchResult=false;
  if(activation.TryActivate("not valid","short","")||platform.Matches!=1)throw new InvalidOperationException("invalid activation escaped fallback guard");
  Console.WriteLine("PASS v1.7 activation process failure title fallback");
 }
 private sealed class FakeActivation:IActivationPlatform
 {
  internal string? Aumid,Process;internal int Matches;internal bool ProcessResult=true,MatchResult=true;
  public bool ActivateAumid(string value){Aumid=value;return true;}public bool ActivateProcess(string value){Process=value;return ProcessResult;}public bool ActivateUniqueTitleMatch(string title,string artist){Matches++;return title.Length>=6&&MatchResult;}
  internal void Reset(){Aumid=Process=null;Matches=0;ProcessResult=true;MatchResult=true;}
 }
}

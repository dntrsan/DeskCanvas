using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace DeskCanvas.App.Services;

internal interface IActivationPlatform
{
 bool ActivateAumid(string appUserModelId);
 bool ActivateProcess(string executableName);
 bool ActivateUniqueTitleMatch(string title,string artist);
}

internal sealed class ActivationService(IActivationPlatform? platform=null)
{
 private static readonly Regex Aumid=new("^[A-Za-z0-9._-]+![A-Za-z0-9._-]+$",RegexOptions.CultureInvariant|RegexOptions.Compiled);
 private static readonly Regex Executable=new("^[A-Za-z0-9_.-]+(?:\\.exe)?$",RegexOptions.CultureInvariant|RegexOptions.Compiled);
 private readonly IActivationPlatform platform=platform??new WindowsActivationPlatform();
 internal bool TryActivate(string? sourceAppUserModelId,string? title=null,string? artist=null)
 {
  var value=sourceAppUserModelId?.Trim()??"";
  try{if(Aumid.IsMatch(value))return platform.ActivateAumid(value);if(Executable.IsMatch(value)&&platform.ActivateProcess(value))return true;return platform.ActivateUniqueTitleMatch(title??"",artist??"");}catch{return false;}
 }
}

internal sealed class WindowsActivationPlatform:IActivationPlatform
{
 public bool ActivateAumid(string appUserModelId){try{var manager=(IApplicationActivationManager)new ApplicationActivationManager();return manager.ActivateApplication(appUserModelId,null,ActivateOptions.None,out _)>=0;}catch{return false;}}
 public bool ActivateProcess(string executableName){try{var stem=Path.GetFileNameWithoutExtension(executableName);if(string.IsNullOrWhiteSpace(stem))return false;foreach(var process in Process.GetProcessesByName(stem)){try{if(process.MainWindowHandle!=IntPtr.Zero&&Activate(process.MainWindowHandle))return true;}catch{}finally{process.Dispose();}}return false;}catch{return false;}}
 public bool ActivateUniqueTitleMatch(string title,string artist)
 {
  var wanted=Normalize(title);if(wanted.Length<6)return false;var artistWanted=Normalize(artist);var matches=new List<(IntPtr Handle,int Score)>();
  try{EnumWindows((handle,_)=>{if(!IsWindowVisible(handle))return true;var length=GetWindowTextLength(handle);if(length<=0)return true;var text=new StringBuilder(length+1);GetWindowText(handle,text,text.Capacity);var normalized=Normalize(text.ToString());if(!normalized.Contains(wanted,StringComparison.Ordinal))return true;var score=wanted.Length*10+(artistWanted.Length>=3&&normalized.Contains(artistWanted,StringComparison.Ordinal)?artistWanted.Length:0);matches.Add((handle,score));return true;},IntPtr.Zero);if(matches.Count==0)return false;var best=matches.Max(x=>x.Score);var top=matches.Where(x=>x.Score==best).ToArray();return top.Length==1&&Activate(top[0].Handle);}catch{return false;}
 }
 private static string Normalize(string text){var value=new StringBuilder();foreach(var c in text.Normalize(NormalizationForm.FormKC)){if(char.IsLetterOrDigit(c))value.Append(char.ToUpperInvariant(c));}return value.ToString();}
 private static bool Activate(IntPtr handle){_ = ShowWindow(handle,9);return SetForegroundWindow(handle);}
 [ComImport][Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")][InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IApplicationActivationManager{int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string appUserModelId,[MarshalAs(UnmanagedType.LPWStr)]string? arguments,ActivateOptions options,out uint processId);}
 [ComImport][Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]private class ApplicationActivationManager{} private enum ActivateOptions{None=0}
 private delegate bool EnumWindowsProc(IntPtr handle,IntPtr parameter);[DllImport("user32.dll")]private static extern bool EnumWindows(EnumWindowsProc callback,IntPtr parameter);[DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr handle);[DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(IntPtr handle,StringBuilder text,int maxCount);[DllImport("user32.dll")]private static extern int GetWindowTextLength(IntPtr handle);[DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr handle);[DllImport("user32.dll")]private static extern bool ShowWindow(IntPtr handle,int command);
}

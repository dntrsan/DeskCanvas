using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DeskCanvas.App.Services;

internal interface IActivationPlatform
{
    bool ActivateAumid(string appUserModelId);
    bool ActivateProcess(string executableName);
}

internal sealed class ActivationService(IActivationPlatform? platform = null)
{
    private static readonly Regex Aumid = new(
        "^[A-Za-z0-9._-]+![A-Za-z0-9._-]+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Executable = new(
        "^[A-Za-z0-9_.-]+(?:\\.exe)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly IActivationPlatform platform = platform ?? new WindowsActivationPlatform();

    internal bool TryActivate(string? sourceAppUserModelId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppUserModelId)) return false;
        var value = sourceAppUserModelId.Trim();
        try
        {
            if (Aumid.IsMatch(value)) return platform.ActivateAumid(value);
            if (!Executable.IsMatch(value)) return false;
            return platform.ActivateProcess(value);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

internal sealed class WindowsActivationPlatform : IActivationPlatform
{
    public bool ActivateAumid(string appUserModelId)
    {
        try
        {
            var manager = (IApplicationActivationManager)new ApplicationActivationManager();
            return manager.ActivateApplication(
                appUserModelId,
                null,
                ActivateOptions.None,
                out _) >= 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool ActivateProcess(string executableName)
    {
        try
        {
            var stem = Path.GetFileNameWithoutExtension(executableName);
            if (string.IsNullOrWhiteSpace(stem)) return false;
            foreach (var process in Process.GetProcessesByName(stem))
            {
                try
                {
                    if (process.MainWindowHandle == IntPtr.Zero) continue;
                    _ = ShowWindow(process.MainWindowHandle, 9);
                    return SetForegroundWindow(process.MainWindowHandle);
                }
                catch (Exception)
                {
                    // Try another existing process for the same executable.
                }
                finally
                {
                    process.Dispose();
                }
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [ComImport]
    [Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
            ActivateOptions options,
            out uint processId);
    }

    [ComImport]
    [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManager { }

    private enum ActivateOptions { None = 0 }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);
}

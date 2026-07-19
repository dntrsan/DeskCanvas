using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DeskCanvas.App.Services;

internal sealed class HotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkL = 0x4C;
    private const int HotkeyId = 0x4443;

    private readonly HwndSource source;
    private readonly Action toggle;
    private bool registered;

    internal HotkeyManager(Action toggle)
    {
        this.toggle = toggle;
        var parameters = new HwndSourceParameters("DeskCanvas.Hotkey")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        };
        source = new HwndSource(parameters);
        source.AddHook(WindowProc);
        registered = RegisterHotKey(source.Handle, HotkeyId, ModAlt | ModControl, VkL);
    }

    internal bool IsRegistered => registered;

    public void Dispose()
    {
        if (registered)
        {
            _ = UnregisterHotKey(source.Handle, HotkeyId);
            registered = false;
        }
        source.RemoveHook(WindowProc);
        source.Dispose();
    }

    private IntPtr WindowProc(
        IntPtr window,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            toggle();
            handled = true;
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        IntPtr window,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}

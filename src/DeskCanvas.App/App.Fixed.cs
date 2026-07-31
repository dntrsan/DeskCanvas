using System.Threading;
using System.Windows;

namespace DeskCanvas.App;

public partial class App : System.Windows.Application
{
    private Mutex? singleInstanceMutex;
    private DeskCanvasController? controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var preview = string.Equals(
            Environment.GetEnvironmentVariable("DESKCANVAS_PREVIEW_MODE"),
            "1",
            StringComparison.Ordinal);
        var mutexName = preview
            ? $@"Local\DeskCanvas.Preview.{PreviewIdentity()}"
            : @"Local\DeskCanvas.SingleInstance";
        singleInstanceMutex = new Mutex(initiallyOwned: true, mutexName, out var ownsMutex);
        if (!ownsMutex)
        {
            System.Windows.MessageBox.Show(
                preview
                    ? "この隔離プレビューはすでに起動しています。"
                    : "DeskCanvasはすでに起動しています。通知領域のアイコンから開いてください。",
                "DeskCanvas",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            controller = new DeskCanvasController();
            controller.Start();
        }
        catch (Exception error)
        {
            System.Windows.MessageBox.Show(
                $"DeskCanvasを起動できませんでした。\n\n{error.Message}",
                "DeskCanvas",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        controller?.Dispose();
        if (singleInstanceMutex is not null)
        {
            try
            {
                singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex was not acquired in this process.
            }
            singleInstanceMutex.Dispose();
        }
        base.OnExit(e);
    }

    private static string PreviewIdentity()
    {
        var dataRoot = Environment.GetEnvironmentVariable("DESKCANVAS_DATA_ROOT") ?? "default";
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in dataRoot.ToUpperInvariant())
            {
                hash ^= character;
                hash *= 16777619;
            }
            return hash.ToString("X8");
        }
    }
}

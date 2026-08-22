using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DeskCanvas.App;

public partial class App : System.Windows.Application
{
    private Mutex? singleInstanceMutex;
    private DeskCanvasController? controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += App_UnhandledException;

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
            if (ForegroundSafetyPolicy.ShouldShowDuplicateInstanceDialog(preview))
            {
                System.Windows.MessageBox.Show(
                    "この隔離プレビューはすでに起動しています。",
                    "DeskCanvas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            Shutdown();
            return;
        }

        try
        {
            controller = new DeskCanvasController(previewMode: preview);
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

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try
        {
            System.Windows.MessageBox.Show(
                $"予期しないエラーが発生しました。作業内容はできるだけ保存されています。\n\n{e.Exception.Message}",
                "DeskCanvas",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception)
        {
            // A failure while reporting must not take the tray process down a second time.
        }
    }

    private static void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Background-thread failures have no dispatcher. The process may still die;
        // there is no safe UI to show from here.
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= App_UnhandledException;
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

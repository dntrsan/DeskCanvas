using System.Threading;
using System.Windows;

namespace DeskCanvas.App;

public partial class App : Application
{
    private Mutex? singleInstanceMutex;
    private DeskCanvasController? controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\DeskCanvas.SingleInstance", out var ownsMutex);
        if (!ownsMutex)
        {
            MessageBox.Show(
                "DeskCanvasはすでに起動しています。通知領域のアイコンから開いてください。",
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
            MessageBox.Show(
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
}

using System.Threading;
using System.Windows;
using DeskCanvas.App.Services;

namespace DeskCanvas.App;

public partial class App : System.Windows.Application
{
    private Mutex? singleInstanceMutex;
    private DeskCanvasController? controller;
    private CancellationTokenSource? updateCancellation;
    private HttpClient? updateClient;
    private Task? updateTask;

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
            controller = new DeskCanvasController();
            controller.Start();
            StartUpdateCheck();
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
        updateCancellation?.Cancel();
        updateClient?.Dispose();
        updateCancellation?.Dispose();
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

    private void StartUpdateCheck()
    {
        updateCancellation = new CancellationTokenSource();
        updateClient = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        var current = typeof(App).Assembly.GetName().Version ?? new Version(1, 1, 0);
        var checker = new UpdateCheckService(new GitHubReleaseFeed(updateClient), current);
        var coordinator = new UpdatePromptCoordinator(checker, new WpfUpdatePrompt());
        updateTask = RunUpdateCheckAsync(coordinator, updateCancellation.Token);
    }

    private static async Task RunUpdateCheckAsync(
        UpdatePromptCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("DESKCANVAS_PREVIEW_FAKE_UPDATE")))
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
            await coordinator.CheckAfterStartupAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
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

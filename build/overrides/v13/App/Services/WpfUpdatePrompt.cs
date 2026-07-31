using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskCanvas.App.Windows;

namespace DeskCanvas.App.Services;

internal interface IReleasePageLauncher
{
    bool TryOpen(Uri releasePage);
}

internal sealed class ReleasePageLauncher : IReleasePageLauncher
{
    public bool TryOpen(Uri releasePage)
    {
        if (!UpdateSafety.IsAllowedReleasePage(releasePage))
            return false;
        try
        {
            Process.Start(new ProcessStartInfo(releasePage.AbsoluteUri)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception error) when (
            error is InvalidOperationException or
            System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}

internal sealed class WpfUpdatePrompt(IReleasePageLauncher? launcher = null) : IUpdatePrompt
{
    private readonly IReleasePageLauncher releasePageLauncher =
        launcher ?? new ReleasePageLauncher();

    public void Show(ReleaseInfo release)
    {
        var application = Application.Current;
        if (application is null || application.Dispatcher.HasShutdownStarted)
            return;
        application.Dispatcher.InvokeAsync(() =>
        {
            if (application.Dispatcher.HasShutdownStarted)
                return;
            var owner = application.Windows.OfType<MainWindow>().FirstOrDefault();
            var dialog = new UpdatePromptWindow(release, releasePageLauncher);
            if (owner is { IsVisible: true })
                dialog.Owner = owner;
            dialog.ShowDialog();
        });
    }
}

internal sealed class UpdatePromptWindow : Window
{
    private static readonly SolidColorBrush Surface =
        new(Color.FromRgb(24, 25, 34));
    private static readonly SolidColorBrush Text =
        new(Color.FromRgb(248, 248, 252));
    private static readonly SolidColorBrush Muted =
        new(Color.FromRgb(177, 179, 194));
    private static readonly SolidColorBrush Accent =
        new(Color.FromRgb(140, 124, 255));

    internal UpdatePromptWindow(ReleaseInfo release, IReleasePageLauncher launcher)
    {
        Title = "DeskCanvas アップデート";
        Width = 460;
        Height = 270;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Surface;
        Foreground = Text;
        AutomationProperties.SetAutomationId(this, "DeskCanvasUpdatePrompt");

        var root = new Grid { Margin = new Thickness(28) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(47, 43, 78)),
            CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 4, 10, 4),
            Child = new TextBlock
            {
                Text = "NEW VERSION",
                Foreground = new SolidColorBrush(Color.FromRgb(194, 186, 255)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            }
        };
        root.Children.Add(badge);

        var heading = new TextBlock
        {
            Text = $"DeskCanvas {release.Version} を利用できます",
            Foreground = Text,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 0)
        };
        Grid.SetRow(heading, 1);
        root.Children.Add(heading);

        var description = new TextBlock
        {
            Text = "最新機能と修正内容をGitHubのリリースページで確認できます。更新は自動では実行されません。",
            Foreground = Muted,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 16)
        };
        Grid.SetRow(description, 2);
        root.Children.Add(description);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var later = MakeButton("あとで", new SolidColorBrush(Color.FromRgb(44, 45, 58)));
        AutomationProperties.SetAutomationId(later, "UpdateLater");
        later.Click += (_, _) => Close();
        var open = MakeButton("リリースページを開く", Accent);
        AutomationProperties.SetAutomationId(open, "OpenReleasePage");
        open.Click += (_, _) =>
        {
            if (launcher.TryOpen(release.ReleasePage))
                Close();
            else
                System.Windows.MessageBox.Show(
                    this,
                    "リリースページを開けませんでした。",
                    "DeskCanvas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
        };
        buttons.Children.Add(later);
        buttons.Children.Add(open);
        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);
        Content = root;
    }

    private static Button MakeButton(string text, Brush background) =>
        new()
        {
            Content = text,
            Background = background,
            Foreground = Text,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 9, 16, 9),
            Margin = new Thickness(8, 0, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
}

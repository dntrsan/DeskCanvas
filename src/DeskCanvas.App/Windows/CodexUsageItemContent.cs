using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using DeskCanvas.App.Services;

namespace DeskCanvas.App.Windows;

internal sealed class CodexUsageItemContent : IDesktopItemContent
{
    private static readonly Brush PrimaryText = Design.LabelPrimaryBrush;
    private static readonly Brush SecondaryText = Design.LabelSecondaryBrush;
    private static readonly Brush QuietText = Design.LabelTertiaryBrush;
    private readonly ICodexUsageService service;
    private readonly Grid root = new();
    private readonly TextBlock remaining = Text("--", 39, PrimaryText, FontWeights.SemiBold);
    private readonly TextBlock reset = Text("Codexを確認中", 16, PrimaryText, FontWeights.SemiBold);
    private readonly TextBlock period = Text("", 10, SecondaryText);
    private readonly TextBlock plan = Text("CODEX", 10, QuietText, FontWeights.SemiBold);
    private readonly TextBlock source = Text("", 9.5, QuietText);
    private readonly TextBlock credits = Text("", 9.5, SecondaryText, FontWeights.SemiBold);
    private readonly Border primaryTrack = Track();
    private readonly Border primaryFill = Fill();
    private readonly Grid secondaryPanel = new() { Visibility = Visibility.Collapsed };
    private readonly Border secondaryTrack = Track();
    private readonly Border secondaryFill = Fill();
    private readonly TextBlock secondaryLabel = Text("", 9.5, QuietText);
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromMinutes(1) };
    private IDisposable? lease;
    private CodexUsageSnapshot snapshot;
    private bool active;

    internal CodexUsageItemContent(ICodexUsageService service)
    {
        this.service = service;
        snapshot = service.Snapshot;
        Build();
        clock.Tick += (_, _) => Update(snapshot);
        Update(snapshot);
    }

    public UIElement View => root;

    public void SetActive(bool value)
    {
        if (active == value) return;
        active = value;
        if (value)
        {
            service.SnapshotChanged += ServiceSnapshotChanged;
            lease = service.Acquire();
            snapshot = service.Snapshot;
            Update(snapshot);
            clock.Start();
        }
        else
        {
            clock.Stop();
            service.SnapshotChanged -= ServiceSnapshotChanged;
            lease?.Dispose();
            lease = null;
        }
    }

    private void Build()
    {
        var surface = new Border
        {
            CornerRadius = Design.CardRadius,
            BorderThickness = new Thickness(1),
            BorderBrush = WidgetTheme.Hairline,
            Background = Design.CardSurface(),
            Effect = Design.CardShadow(),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };

        var content = new Grid { Margin = new Thickness(20, 14, 20, 13) };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        surface.Child = content;

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(plan);
        credits.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(credits, 1);
        header.Children.Add(credits);
        content.Children.Add(header);

        var main = new Grid { Margin = new Thickness(0, 4, 0, 5) };
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        remaining.VerticalAlignment = VerticalAlignment.Center;
        main.Children.Add(remaining);
        var detail = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        detail.Children.Add(reset);
        period.Margin = new Thickness(0, 2, 0, 0);
        detail.Children.Add(period);
        Grid.SetColumn(detail, 1);
        main.Children.Add(detail);
        Grid.SetRow(main, 1);
        content.Children.Add(main);

        var primaryBar = new Grid { Height = 3 };
        primaryBar.Children.Add(primaryTrack);
        primaryBar.Children.Add(primaryFill);
        primaryBar.SizeChanged += (_, _) => UpdatePrimaryBar(primaryBar.ActualWidth);
        Grid.SetRow(primaryBar, 2);
        content.Children.Add(primaryBar);

        secondaryPanel.Margin = new Thickness(0, 7, 0, 0);
        secondaryPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        secondaryPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        secondaryPanel.Children.Add(secondaryLabel);
        var secondaryBar = new Grid { Height = 3, Margin = new Thickness(0, 4, 0, 0) };
        secondaryBar.Children.Add(secondaryTrack);
        secondaryBar.Children.Add(secondaryFill);
        secondaryBar.SizeChanged += (_, _) => UpdateSecondaryBar(secondaryBar.ActualWidth);
        Grid.SetRow(secondaryBar, 1);
        secondaryPanel.Children.Add(secondaryBar);
        Grid.SetRow(secondaryPanel, 3);
        content.Children.Add(secondaryPanel);

        source.Margin = new Thickness(0, 7, 0, 0);
        Grid.SetRow(source, 4);
        content.Children.Add(source);
        root.Children.Add(surface);
    }

    private void ServiceSnapshotChanged(object? sender, CodexUsageSnapshot value) =>
        root.Dispatcher.BeginInvoke(() => Update(value));

    private void Update(CodexUsageSnapshot value)
    {
        snapshot = value;
        var primary = value.Primary;
        if (primary is null)
        {
            remaining.Text = "--";
            reset.Text = value.Error is null ? "Codexを確認中" : "取得できません";
            period.Text = value.Error is null ? "使用状況を読み込んでいます" : ShortError(value.Error);
            plan.Text = "CODEX LIMIT";
            source.Text = value.Error is null ? "" : "オフライン";
            credits.Text = "";
            ToolTipService.SetToolTip(root, value.Error);
        }
        else
        {
            remaining.Text = $"{primary.RemainingPercent}%";
            reset.Text = ResetText(primary.ResetsAt);
            period.Text = $"{WindowName(primary.WindowDurationMinutes)}  ·  {ResetDate(primary.ResetsAt)}";
            plan.Text = string.IsNullOrWhiteSpace(value.PlanType)
                ? "CODEX LIMIT"
                : $"CODEX  {value.PlanType.ToUpperInvariant()}";
            source.Text = value.Source == CodexUsageSource.LocalHistory
                ? $"履歴から取得  ·  オフライン  {UpdatedText(value.UpdatedAt)}"
                : $"更新 {UpdatedText(value.UpdatedAt)}";
            credits.Text = value.UnlimitedCredits
                ? "CREDITS ∞"
                : HasBalance(value.CreditBalance) ? $"CREDITS {value.CreditBalance}" : "";
            ToolTipService.SetToolTip(root, value.Error is null ? null : $"ライブ更新に失敗しました。\n{value.Error}");
        }

        var accent = Accent(primary?.RemainingPercent);
        primaryFill.Background = accent;
        secondaryFill.Background = accent;
        UpdatePrimaryBar(primaryTrack.ActualWidth);
        UpdateSecondary();
    }

    private void UpdatePrimaryBar(double width)
    {
        primaryFill.Width = Math.Max(0, width * (snapshot.Primary?.RemainingPercent ?? 0) / 100d);
    }

    private void UpdateSecondary()
    {
        if (snapshot.Secondary is not { } secondary)
        {
            secondaryPanel.Visibility = Visibility.Collapsed;
            return;
        }

        secondaryPanel.Visibility = Visibility.Visible;
        secondaryLabel.Text = $"{WindowName(secondary.WindowDurationMinutes)}  残り {secondary.RemainingPercent}%";
        UpdateSecondaryBar(secondaryTrack.ActualWidth);
    }

    private void UpdateSecondaryBar(double width)
    {
        secondaryFill.Width = Math.Max(0, width * (snapshot.Secondary?.RemainingPercent ?? 0) / 100d);
    }

    private static Brush Accent(int? remainingPercent) => Design.Frozen(remainingPercent switch
    {
        null => Design.LabelTertiary,
        <= 5 => Design.Destructive,
        <= 20 => Design.NetworkUp,
        _ => Design.Accent
    });

    private static string ResetText(DateTimeOffset? reset)
    {
        if (reset is null) return "リセット時刻不明";
        var remaining = reset.Value - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero) return "まもなく更新";
        if (remaining.TotalDays >= 1) return $"あと {(int)remaining.TotalDays}日 {remaining.Hours}時間";
        if (remaining.TotalHours >= 1) return $"あと {(int)remaining.TotalHours}時間 {remaining.Minutes}分";
        return $"あと {Math.Max(1, remaining.Minutes)}分";
    }

    private static string WindowName(long? minutes) => minutes switch
    {
        null => "期間不明",
        10_080 => "7日間",
        300 => "5時間",
        >= 1_440 when minutes % 1_440 == 0 => $"{minutes / 1_440}日間",
        >= 60 when minutes % 60 == 0 => $"{minutes / 60}時間",
        _ => $"{minutes}分"
    };

    private static string ResetDate(DateTimeOffset? value) => value is null
        ? "リセット日時不明"
        : value.Value.ToLocalTime().ToString("M/d H:mm リセット", CultureInfo.CurrentCulture);

    private static string UpdatedText(DateTimeOffset? value) => value is null
        ? "--:--"
        : value.Value.ToLocalTime().ToString("H:mm", CultureInfo.CurrentCulture);

    private static bool HasBalance(string? balance) =>
        decimal.TryParse(balance, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0;

    private static string ShortError(string value)
    {
        var first = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? value;
        return first.Length <= 38 ? first : first[..37] + "…";
    }

    private static Border Track() => new()
    {
        Height = 3,
        CornerRadius = new CornerRadius(1.5),
        Background = Design.Frozen(Design.Argb(0x24, 0xFF, 0xFF, 0xFF))
    };

    private static Border Fill() => new()
    {
        Height = 3,
        CornerRadius = new CornerRadius(1.5),
        HorizontalAlignment = HorizontalAlignment.Left
    };

    private static TextBlock Text(string value, double size, Brush foreground, FontWeight? weight = null) =>
        Design.Tabular(Design.Text(value, size, foreground, weight));

    public void Dispose()
    {
        SetActive(false);
        clock.Stop();
    }
}

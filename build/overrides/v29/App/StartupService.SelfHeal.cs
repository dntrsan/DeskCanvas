using System.Text.Json;
using Microsoft.Win32;

namespace DeskCanvas.App.Services;

internal static class StartupRegistrationPolicy
{
    internal static bool ShouldWrite(bool savedEnabled, string? existingCommand, string? currentExecutable)
    {
        if (!savedEnabled || string.IsNullOrWhiteSpace(currentExecutable)) return false;
        return !string.Equals(ExtractExecutable(existingCommand), Path.GetFullPath(currentExecutable), StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsCurrent(string? existingCommand, string? currentExecutable) =>
        !string.IsNullOrWhiteSpace(currentExecutable) && string.Equals(ExtractExecutable(existingCommand), Path.GetFullPath(currentExecutable), StringComparison.OrdinalIgnoreCase);

    private static string? ExtractExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var trimmed = command.Trim();
        if (trimmed.StartsWith('"'))
        {
            var end = trimmed.IndexOf('"', 1);
            trimmed = end > 1 ? trimmed[1..end] : trimmed.Trim('"');
        }
        else
        {
            var separator = trimmed.IndexOf(' ');
            if (separator >= 0) trimmed = trimmed[..separator];
        }
        try { return Path.GetFullPath(trimmed); } catch { return null; }
    }
}

internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DeskCanvas";

    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        var existing = key?.GetValue(ValueName) as string;
        var saved = TryReadSavedPreference(out var knownPreference);
        if (IsPreview()) return knownPreference ? saved && StartupRegistrationPolicy.IsCurrent(existing, Environment.ProcessPath) : StartupRegistrationPolicy.IsCurrent(existing, Environment.ProcessPath);
        if (knownPreference && !saved)
        {
            if (!string.IsNullOrWhiteSpace(existing)) SetEnabled(false);
            return false;
        }
        if (saved && StartupRegistrationPolicy.ShouldWrite(true, existing, Environment.ProcessPath))
        {
            SetEnabled(true);
            return true;
        }
        return StartupRegistrationPolicy.IsCurrent(existing, Environment.ProcessPath);
    }

    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("実行ファイルの場所を取得できません。");
            key.SetValue(ValueName, $"\"{executable}\"");
        }
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static bool IsPreview() => string.Equals(Environment.GetEnvironmentVariable("DESKCANVAS_PREVIEW_MODE"), "1", StringComparison.Ordinal);
    private static bool TryReadSavedPreference(out bool known)
    {
        known = false;
        try
        {
            var configured = Environment.GetEnvironmentVariable("DESKCANVAS_DATA_ROOT");
            var root = !string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured)
                ? Path.GetFullPath(configured)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskCanvas");
            var path = Path.Combine(root, "layout.json");
            if (!File.Exists(path)) return false;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var settings = document.RootElement.EnumerateObject().FirstOrDefault(property => string.Equals(property.Name, "settings", StringComparison.OrdinalIgnoreCase)).Value;
            var value = settings.ValueKind == JsonValueKind.Object ? settings.EnumerateObject().FirstOrDefault(property => string.Equals(property.Name, "startWithWindows", StringComparison.OrdinalIgnoreCase)).Value : default;
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
            known = true;
            return value.GetBoolean();
        }
        catch (JsonException) { return false; }
        catch (IOException) { return false; }
    }
}

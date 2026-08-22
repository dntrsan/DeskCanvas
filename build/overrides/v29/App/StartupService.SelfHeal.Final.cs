using System.Security;
using System.Text.Json;
using Microsoft.Win32;

namespace DeskCanvas.App.Services;

internal static class StartupRegistrationPolicy
{
    internal static bool ShouldWrite(bool savedEnabled, string? existingCommand, string? currentExecutable) => savedEnabled && !string.IsNullOrWhiteSpace(currentExecutable) && !IsCurrent(existingCommand, currentExecutable);
    internal static bool IsCurrent(string? existingCommand, string? currentExecutable) => !string.IsNullOrWhiteSpace(currentExecutable) && string.Equals(ExtractExecutable(existingCommand), Path.GetFullPath(currentExecutable), StringComparison.OrdinalIgnoreCase);
    private static string? ExtractExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null; var value = command.Trim();
        if (value.StartsWith('"')) { var end = value.IndexOf('"', 1); value = end > 1 ? value[1..end] : value.Trim('"'); }
        else { var end = value.IndexOf(' '); if (end >= 0) value = value[..end]; }
        try { return Path.GetFullPath(value); } catch { return null; }
    }
}

internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DeskCanvas";

    internal static bool IsEnabled()
    {
        string? existing = null;
        try { using var key = Registry.CurrentUser.OpenSubKey(RunKey); existing = key?.GetValue(ValueName) as string; } catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException) { return false; }
        var saved = TryReadSavedPreference(out var known);
        if (IsPreview()) return known ? saved && StartupRegistrationPolicy.IsCurrent(existing, Environment.ProcessPath) : StartupRegistrationPolicy.IsCurrent(existing, Environment.ProcessPath);
        if (known && !saved) { if (!string.IsNullOrWhiteSpace(existing)) TrySetEnabled(false); return false; }
        if (saved && StartupRegistrationPolicy.ShouldWrite(true, existing, Environment.ProcessPath)) return TrySetEnabled(true) || StartupRegistrationPolicy.IsCurrent(existing, Environment.ProcessPath);
        return StartupRegistrationPolicy.IsCurrent(existing, Environment.ProcessPath);
    }

    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) { var executable = Environment.ProcessPath ?? throw new InvalidOperationException("実行ファイルの場所を取得できません。"); key.SetValue(ValueName, $"\"{executable}\""); }
        else key.DeleteValue(ValueName, false);
    }

    private static bool TrySetEnabled(bool enabled)
    {
        try { SetEnabled(enabled); return true; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException) { return false; }
    }
    private static bool IsPreview() => string.Equals(Environment.GetEnvironmentVariable("DESKCANVAS_PREVIEW_MODE"), "1", StringComparison.Ordinal);
    private static bool TryReadSavedPreference(out bool known)
    {
        known = false;
        try
        {
            var configured = Environment.GetEnvironmentVariable("DESKCANVAS_DATA_ROOT"); var root = !string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured) ? Path.GetFullPath(configured) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskCanvas");
            var path = Path.Combine(root, "layout.json"); if (!File.Exists(path)) return false;
            using var document = JsonDocument.Parse(File.ReadAllText(path)); var settings = document.RootElement.EnumerateObject().FirstOrDefault(property => string.Equals(property.Name, "settings", StringComparison.OrdinalIgnoreCase)).Value;
            var value = settings.ValueKind == JsonValueKind.Object ? settings.EnumerateObject().FirstOrDefault(property => string.Equals(property.Name, "startWithWindows", StringComparison.OrdinalIgnoreCase)).Value : default;
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false; known = true; return value.GetBoolean();
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or SecurityException) { return false; }
    }
}

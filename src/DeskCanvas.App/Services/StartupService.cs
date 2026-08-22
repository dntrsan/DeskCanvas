using Microsoft.Win32;

namespace DeskCanvas.App.Services;

internal static class StartupRegistrationPolicy
{
    internal static bool ShouldWrite(bool savedEnabled, string? existingCommand, string? currentExecutable) =>
        savedEnabled && !string.IsNullOrWhiteSpace(currentExecutable) && !IsCurrent(existingCommand, currentExecutable);

    internal static bool IsCurrent(string? existingCommand, string? currentExecutable) =>
        !string.IsNullOrWhiteSpace(currentExecutable)
        && string.Equals(ExtractExecutable(existingCommand), Path.GetFullPath(currentExecutable), StringComparison.OrdinalIgnoreCase);

    private static string? ExtractExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var value = command.Trim();
        if (value.StartsWith('"'))
        {
            var end = value.IndexOf('"', 1);
            value = end > 1 ? value[1..end] : value.Trim('"');
        }
        else
        {
            var end = value.IndexOf(' ');
            if (end >= 0) value = value[..end];
        }
        try { return Path.GetFullPath(value); }
        catch (Exception) { return null; }
    }
}

internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DeskCanvas";

    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        if (key?.GetValue(ValueName) is not string value || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var stored = value.Trim().Trim('"');
        var current = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(current)) return true;
        if (string.Equals(stored, current, StringComparison.OrdinalIgnoreCase)) return true;
        if (File.Exists(stored))
        {
            try { SetEnabled(true); }
            catch (Exception error) when (error is UnauthorizedAccessException or IOException) { }
            return true;
        }
        return false;
    }

    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("実行ファイルの場所を取得できません。");
            key.SetValue(ValueName, $"\"{executable}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

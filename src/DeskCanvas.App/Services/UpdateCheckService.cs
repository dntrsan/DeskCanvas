using System.Net.Http.Headers;
using System.Text.Json;

namespace DeskCanvas.App.Services;

internal sealed record ReleaseInfo(string Version, Uri ReleasePage);
internal interface IReleaseFeed { Task<ReleaseInfo?> GetLatestStableAsync(CancellationToken cancellationToken); }
internal sealed class GitHubReleaseFeed(HttpClient client) : IReleaseFeed
{
    private static readonly Uri Endpoint = new("https://api.github.com/repos/dntrsan/DeskCanvas/releases");
    public async Task<ReleaseInfo?> GetLatestStableAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("DeskCanvas", "1.1"));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        foreach (var release in json.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            if (release.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean()) continue;
            var tag = release.GetProperty("tag_name").GetString(); var page = release.GetProperty("html_url").GetString();
            if (SemanticVersion.TryParse(tag, out _) && Uri.TryCreate(page, UriKind.Absolute, out var uri) && UpdateSafety.IsAllowedReleasePage(uri)) return new(tag!, uri);
        }
        return null;
    }
}
internal sealed class UpdateCheckService(IReleaseFeed feed, Version current)
{
    private int announced;
    internal async Task<ReleaseInfo?> CheckOnceAsync(CancellationToken cancellationToken)
    {
        try { var latest = await feed.GetLatestStableAsync(cancellationToken).ConfigureAwait(false); return latest is not null && SemanticVersion.TryParse(latest.Version, out var version) && version > current && Interlocked.Exchange(ref announced, 1) == 0 ? latest : null; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return null; }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
    }
}
internal static class UpdateSafety
{
    internal static bool IsAllowedReleasePage(Uri uri) => uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/dntrsan/DeskCanvas/releases", StringComparison.Ordinal);
}
internal static class SemanticVersion
{
    internal static bool TryParse(string? text, out Version version)
    {
        version = new Version(); if (string.IsNullOrWhiteSpace(text)) return false; var value = text.Trim(); if (value.StartsWith('v')) value = value[1..];
        if (value.Contains('-', StringComparison.Ordinal) || value.Contains('+', StringComparison.Ordinal)) return false;
        return Version.TryParse(value, out version) && version.Major >= 0 && version.Build >= 0;
    }
}

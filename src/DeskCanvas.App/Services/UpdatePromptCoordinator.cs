namespace DeskCanvas.App.Services;

internal interface IUpdatePrompt { void Show(ReleaseInfo release); }
internal sealed class UpdatePromptCoordinator(UpdateCheckService updates, IUpdatePrompt prompt)
{
    internal async Task CheckAfterStartupAsync(CancellationToken cancellationToken)
    {
        var preview = Environment.GetEnvironmentVariable("DESKCANVAS_PREVIEW_FAKE_UPDATE");
        if (!string.IsNullOrWhiteSpace(preview) && SemanticVersion.TryParse(preview, out _))
        {
            prompt.Show(new ReleaseInfo(preview, new Uri("https://github.com/dntrsan/DeskCanvas/releases")));
            return;
        }
        var release = await updates.CheckOnceAsync(cancellationToken).ConfigureAwait(false);
        if (release is not null) prompt.Show(release);
    }
}

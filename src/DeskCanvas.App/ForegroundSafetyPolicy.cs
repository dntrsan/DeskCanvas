namespace DeskCanvas.App;

internal static class ForegroundSafetyPolicy
{
    internal static bool ShouldOpenMainWindowAtStartup(bool previewMode, bool suppressPreviewUi = false) =>
        previewMode && !suppressPreviewUi;

    internal static bool ShouldShowDuplicateInstanceDialog(bool previewMode) => previewMode;

    internal static bool MayShowUpdateDialog(bool ownerVisible, bool ownerActive) =>
        ownerVisible && ownerActive;
}

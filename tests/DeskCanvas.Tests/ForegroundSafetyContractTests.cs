using System.Runtime.CompilerServices;
using DeskCanvas.App;

internal static class ForegroundSafetyContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Check(!ForegroundSafetyPolicy.ShouldOpenMainWindowAtStartup(previewMode: false));
        Check(ForegroundSafetyPolicy.ShouldOpenMainWindowAtStartup(previewMode: true));
        Check(!ForegroundSafetyPolicy.ShouldOpenMainWindowAtStartup(previewMode: true, suppressPreviewUi: true));
        Check(!ForegroundSafetyPolicy.ShouldShowDuplicateInstanceDialog(previewMode: false));
        Check(ForegroundSafetyPolicy.ShouldShowDuplicateInstanceDialog(previewMode: true));
        Check(!ForegroundSafetyPolicy.MayShowUpdateDialog(ownerVisible: false, ownerActive: false));
        Check(!ForegroundSafetyPolicy.MayShowUpdateDialog(ownerVisible: true, ownerActive: false));
        Check(ForegroundSafetyPolicy.MayShowUpdateDialog(ownerVisible: true, ownerActive: true));
        Console.WriteLine("PASS automatic startup never requests foreground UI");
    }

    private static void Check(bool value)
    {
        if (!value) throw new InvalidOperationException("foreground safety contract failed");
    }
}

namespace DeskCanvas.Core;

/// <summary>
/// Maps a screen point onto a captured desktop bitmap. Capture is 1:1 with
/// the host window, so this is a translation — no wallpaper fit modes.
/// </summary>
public static class BackdropMapping
{
    public static (double ImageX, double ImageY) ToImage(
        double screenX,
        double screenY,
        double originX,
        double originY) => (screenX - originX, screenY - originY);

    public static bool IsInside(double imageX, double imageY, double width, double height) =>
        imageX >= 0 && imageY >= 0 && imageX < width && imageY < height;
}

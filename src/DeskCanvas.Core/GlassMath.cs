namespace DeskCanvas.Core;

public enum WallpaperFit
{
    Center = 0,
    Tile = 1,
    Stretch = 2,
    Fit = 3,
    Fill = 4,
    Span = 5
}

/// <summary>
/// Signed-distance and lighting helpers for the liquid-glass card. Units are
/// pixels; the SDF is negative inside the rounded rectangle.
/// </summary>
public static class GlassMath
{
    public static double RoundedRectSdf(double x, double y, double width, double height, double radius)
    {
        if (width <= 0 || height <= 0) return double.PositiveInfinity;
        var halfW = width / 2;
        var halfH = height / 2;
        var r = Math.Clamp(radius, 0, Math.Min(halfW, halfH));
        var px = Math.Abs(x - halfW) - halfW + r;
        var py = Math.Abs(y - halfH) - halfH + r;
        var outsideX = Math.Max(px, 0);
        var outsideY = Math.Max(py, 0);
        var outside = Math.Sqrt(outsideX * outsideX + outsideY * outsideY);
        var inside = Math.Min(Math.Max(px, py), 0);
        return outside + inside - r;
    }

    public static (double Nx, double Ny, double Sdf) NormalAndSdf(
        double x,
        double y,
        double width,
        double height,
        double radius,
        double epsilon = 0.75)
    {
        var sdf = RoundedRectSdf(x, y, width, height, radius);
        var dx = RoundedRectSdf(x + epsilon, y, width, height, radius) - RoundedRectSdf(x - epsilon, y, width, height, radius);
        var dy = RoundedRectSdf(x, y + epsilon, width, height, radius) - RoundedRectSdf(x, y - epsilon, width, height, radius);
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-6) return (0, 0, sdf);
        return (dx / length, dy / length, sdf);
    }

    public static (double Dx, double Dy) Displacement(
        double x,
        double y,
        double width,
        double height,
        double radius,
        double strength,
        double bevel)
    {
        var (nx, ny, sdf) = NormalAndSdf(x, y, width, height, radius);
        var band = Math.Max(1, bevel);
        var inside = Math.Clamp(-sdf / band, 0, 1);
        var edge = inside * (1 - inside);
        var magnitude = strength * edge * 4;
        return (-nx * magnitude, -ny * magnitude);
    }

    public static double Fresnel(double sdf, double bevel)
    {
        var band = Math.Max(1, bevel);
        var t = Math.Clamp(1 + sdf / band, 0, 1);
        return t * t;
    }

    public static double Specular(
        double x,
        double y,
        double width,
        double height,
        double lightX = 0.28,
        double lightY = 0.18,
        double shininess = 42)
    {
        if (width <= 0 || height <= 0) return 0;
        var vx = x / width - lightX;
        var vy = y / height - lightY;
        var distance = Math.Sqrt(vx * vx + vy * vy);
        return Math.Pow(Math.Clamp(1 - distance * 1.8, 0, 1), Math.Max(1, shininess));
    }

    /// <summary>
    /// Maps a point in the destination (monitor or spanning desktop) onto the
    /// wallpaper bitmap, honouring Windows wallpaper placement modes.
    /// </summary>
    public static (double ImageX, double ImageY) MapToImage(
        double destX,
        double destY,
        double destWidth,
        double destHeight,
        double imageWidth,
        double imageHeight,
        WallpaperFit fit)
    {
        if (destWidth <= 0 || destHeight <= 0 || imageWidth <= 0 || imageHeight <= 0)
        {
            return (0, 0);
        }

        switch (fit)
        {
            case WallpaperFit.Stretch:
                return (destX / destWidth * imageWidth, destY / destHeight * imageHeight);
            case WallpaperFit.Tile:
                return (Mod(destX, imageWidth), Mod(destY, imageHeight));
            case WallpaperFit.Center:
            {
                var left = (destWidth - imageWidth) / 2;
                var top = (destHeight - imageHeight) / 2;
                return (destX - left, destY - top);
            }
            case WallpaperFit.Fit:
            {
                var scale = Math.Min(destWidth / imageWidth, destHeight / imageHeight);
                var drawnW = imageWidth * scale;
                var drawnH = imageHeight * scale;
                var left = (destWidth - drawnW) / 2;
                var top = (destHeight - drawnH) / 2;
                return ((destX - left) / scale, (destY - top) / scale);
            }
            case WallpaperFit.Span:
            case WallpaperFit.Fill:
            default:
            {
                var scale = Math.Max(destWidth / imageWidth, destHeight / imageHeight);
                var drawnW = imageWidth * scale;
                var drawnH = imageHeight * scale;
                var left = (destWidth - drawnW) / 2;
                var top = (destHeight - drawnH) / 2;
                return ((destX - left) / scale, (destY - top) / scale);
            }
        }
    }

    public static bool IsInsideImage(double imageX, double imageY, double imageWidth, double imageHeight) =>
        imageX >= 0 && imageY >= 0 && imageX < imageWidth && imageY < imageHeight;

    private static double Mod(double value, double modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}

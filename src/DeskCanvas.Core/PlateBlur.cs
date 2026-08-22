namespace DeskCanvas.Core;

/// <summary>
/// Separable box blur over a BGRA32 buffer. Radius 0 is a no-op. Used after
/// refraction so the lens has detail to bend, then the plate frosts.
/// </summary>
public static class PlateBlur
{
    public static void Box(byte[] pixels, int width, int height, int radius, byte[] scratch)
    {
        if (pixels is null || scratch is null) return;
        if (width < 1 || height < 1 || radius <= 0) return;
        var length = width * height * 4;
        if (pixels.Length < length || scratch.Length < length) return;

        Horizontal(pixels, scratch, width, height, radius);
        Vertical(scratch, pixels, width, height, radius);
    }

    private static void Horizontal(byte[] source, byte[] dest, int width, int height, int radius)
    {
        var window = radius * 2 + 1;
        for (var y = 0; y < height; y++)
        {
            var row = y * width * 4;
            for (var channel = 0; channel < 4; channel++)
            {
                var sum = 0;
                for (var k = -radius; k <= radius; k++)
                {
                    sum += source[row + Clamp(k, width) * 4 + channel];
                }

                dest[row + channel] = Average(sum, window);
                for (var x = 1; x < width; x++)
                {
                    sum += source[row + Clamp(x + radius, width) * 4 + channel];
                    sum -= source[row + Clamp(x - radius - 1, width) * 4 + channel];
                    dest[row + x * 4 + channel] = Average(sum, window);
                }
            }
        }
    }

    private static void Vertical(byte[] source, byte[] dest, int width, int height, int radius)
    {
        var window = radius * 2 + 1;
        var stride = width * 4;
        for (var x = 0; x < width; x++)
        {
            for (var channel = 0; channel < 4; channel++)
            {
                var origin = x * 4 + channel;
                var sum = 0;
                for (var k = -radius; k <= radius; k++)
                {
                    sum += source[Clamp(k, height) * stride + origin];
                }

                dest[origin] = Average(sum, window);
                for (var y = 1; y < height; y++)
                {
                    sum += source[Clamp(y + radius, height) * stride + origin];
                    sum -= source[Clamp(y - radius - 1, height) * stride + origin];
                    dest[y * stride + origin] = Average(sum, window);
                }
            }
        }
    }

    private static int Clamp(int value, int length) => value < 0 ? 0 : value >= length ? length - 1 : value;

    private static byte Average(int sum, int window) => (byte)(sum <= 0 ? 0 : sum / window);
}

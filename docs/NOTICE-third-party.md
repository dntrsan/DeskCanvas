# Third-party notices

DeskCanvas's liquid-glass renderer is an original C# / SkiaSharp implementation
(rounded-rectangle signed distance field, edge displacement, chromatic offset,
Fresnel rim, and a Blinn-style spec highlight). It does not copy Apple assets
or proprietary shaders.

The optical approach was informed by these MIT-licensed projects. Their source
is not vendored and is not linked:

- liquefy-ui — https://github.com/liquefy-ui/liquefy-ui (MIT)
- liquidglass — https://github.com/ybouane/liquidglass (MIT)
- liquidGL — https://github.com/naughtyduk/liquidgl (MIT)
- LiquidGlass — https://github.com/BarredEwe/LiquidGlass (MIT)

SkiaSharp 3.119.0 is already a dependency of DeskCanvas and is licensed under
the MIT License.

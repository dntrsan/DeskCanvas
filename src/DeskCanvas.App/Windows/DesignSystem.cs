using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FontFamily = System.Windows.Media.FontFamily;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DeskCanvas.App.Windows;

/// <summary>
/// Single source of truth for the visual language: a neutral, high-contrast dark
/// palette with one blue accent, hairline separators and continuous-feeling radii.
/// Every colour, radius and type ramp used anywhere in the app resolves here so the
/// XAML resources in App.xaml and the code-built widgets cannot drift apart.
/// </summary>
internal static class Design
{
    // Japanese UI, and Segoe UI Variable has no kana or kanji. Naming a CJK family keeps
    // the fallback predictable instead of letting WPF pick a Regular/Bold-only face, which
    // makes a 500-weight run render Bold next to a Medium Latin run. So any text that can
    // contain Japanese sticks to 400/600; Medium is only for numeral-only labels.
    internal const string DisplayFontStack = "Segoe UI Variable Display, Segoe UI Variable, Segoe UI, Yu Gothic UI, Meiryo UI";
    internal const string TextFontStack = "Segoe UI Variable Text, Segoe UI Variable, Segoe UI, Yu Gothic UI, Meiryo UI";

    internal static readonly FontFamily DisplayFont = new(DisplayFontStack);
    internal static readonly FontFamily TextFont = new(TextFontStack);

    // Neutral greys. Widgets float over arbitrary wallpaper, so surfaces stay
    // translucent while the management window stays opaque.
    internal static readonly Color WindowBackground = Rgb(0x1C, 0x1C, 0x1E);
    internal static readonly Color Surface = Rgb(0x2C, 0x2C, 0x2E);
    internal static readonly Color SurfaceRaised = Rgb(0x3A, 0x3A, 0x3C);

    internal static readonly Color LabelPrimary = Rgb(0xFF, 0xFF, 0xFF);
    internal static readonly Color LabelSecondary = Argb(0x99, 0xEB, 0xEB, 0xF5);
    internal static readonly Color LabelTertiary = Argb(0x5C, 0xEB, 0xEB, 0xF5);

    internal static readonly Color Separator = Argb(0x1C, 0xFF, 0xFF, 0xFF);
    internal static readonly Color ControlFill = Argb(0x17, 0xFF, 0xFF, 0xFF);
    internal static readonly Color GlassControlFill = Argb(0x0C, 0xFF, 0xFF, 0xFF);
    internal static readonly Color ControlFillHover = Argb(0x24, 0xFF, 0xFF, 0xFF);
    internal static readonly Color ControlFillPressed = Argb(0x0F, 0xFF, 0xFF, 0xFF);

    internal static readonly Color Accent = Rgb(0x0A, 0x84, 0xFF);
    internal static readonly Color AccentPressed = Rgb(0x00, 0x6C, 0xE0);
    internal static readonly Color Destructive = Rgb(0xFF, 0x45, 0x3A);

    // Meter accents, borrowed from the system colour set so the palette stays coherent.
    internal static readonly Color MeterCpu = Rgb(0x0A, 0x84, 0xFF);
    internal static readonly Color MeterMemory = Rgb(0x30, 0xD1, 0x58);
    internal static readonly Color MeterGpu = Rgb(0xBF, 0x5A, 0xF2);
    internal static readonly Color MeterVram = Rgb(0xFF, 0x64, 0x5A);
    internal static readonly Color NetworkDown = Rgb(0x64, 0xD2, 0xFF);
    internal static readonly Color NetworkUp = Rgb(0xFF, 0x9F, 0x0A);

    internal static readonly Brush LabelPrimaryBrush = Frozen(LabelPrimary);
    internal static readonly Brush LabelSecondaryBrush = Frozen(LabelSecondary);
    internal static readonly Brush LabelTertiaryBrush = Frozen(LabelTertiary);
    internal static readonly Brush SeparatorBrush = Frozen(Separator);
    internal static readonly Brush ControlFillBrush = Frozen(ControlFill);
    internal static readonly Brush GlassControlFillBrush = Frozen(GlassControlFill);
    internal static readonly Brush AccentBrush = Frozen(Accent);
    internal static readonly Brush DestructiveBrush = Frozen(Destructive);

    internal static readonly CornerRadius CardRadius = new(20);
    internal static readonly CornerRadius PanelRadius = new(14);
    internal static readonly CornerRadius ControlRadius = new(9);
    internal static readonly CornerRadius WellRadius = new(11);

    internal static readonly Thickness CardPadding = new(18, 16, 18, 16);

    /// <summary>Translucent card fill. Dark enough to keep white text legible on light wallpaper.</summary>
    internal static Brush CardSurface() => Frozen(Argb(0xD1, 0x1C, 0x1C, 0x1E));

    /// <summary>
    /// Tints the card toward a sampled colour without abandoning the neutral base,
    /// so album art can influence the widget while staying in the palette.
    /// </summary>
    internal static Brush TintedCardSurface(Color tint) => Frozen(Argb(
        0xD1,
        (byte)((tint.R + 0x1C * 3) / 4),
        (byte)((tint.G + 0x1C * 3) / 4),
        (byte)((tint.B + 0x1E * 3) / 4)));

    /// <summary>Low, wide ambient shadow. Reads as depth rather than as a drop shadow.</summary>
    internal static DropShadowEffect CardShadow()
    {
        var shadow = new DropShadowEffect
        {
            Color = Colors.Black,
            ShadowDepth = 5,
            BlurRadius = 26,
            Opacity = .28,
            RenderingBias = RenderingBias.Quality
        };
        shadow.Freeze();
        return shadow;
    }

    internal static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    internal static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    internal static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);

    /// <summary>
    /// Digits that change every second must not shift the layout, so widget numerals
    /// are always rendered with tabular figures.
    /// </summary>
    internal static T Tabular<T>(T element) where T : DependencyObject
    {
        Typography.SetNumeralAlignment(element, FontNumeralAlignment.Tabular);
        return element;
    }

    internal static TextBlock Text(
        string value,
        double size,
        Brush foreground,
        FontWeight? weight = null,
        bool display = false) => new()
        {
            Text = value,
            FontFamily = display || size >= 20 ? DisplayFont : TextFont,
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = foreground,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
}

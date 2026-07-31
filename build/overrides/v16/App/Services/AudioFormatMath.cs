namespace DeskCanvas.App.Services;

internal enum AudioSampleFormat { Unsupported, Pcm16, Float32 }

internal static class AudioSampleFormatMath
{
    internal const ushort Pcm = 1, IeeeFloat = 3, Extensible = 0xfffe;
    internal static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00aa00389b71");
    internal static readonly Guid FloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");
    internal static AudioSampleFormat Resolve(ushort tag, ushort bits, Guid? subFormat = null) => tag switch
    {
        Pcm when bits == 16 => AudioSampleFormat.Pcm16,
        IeeeFloat when bits == 32 => AudioSampleFormat.Float32,
        Extensible when subFormat == PcmSubFormat && bits == 16 => AudioSampleFormat.Pcm16,
        Extensible when subFormat == FloatSubFormat && bits == 32 => AudioSampleFormat.Float32,
        _ => AudioSampleFormat.Unsupported
    };
}

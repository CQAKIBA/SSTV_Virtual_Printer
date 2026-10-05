using SstvVPrinter.Imaging.Model;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Resampling;

public sealed class FittedImage
{
    private readonly byte[] _rgb24;

    internal FittedImage(int width, int height, byte[] rgb24, PixelRect contentBounds)
    {
        Width = width;
        Height = height;
        _rgb24 = rgb24;
        ContentBounds = contentBounds;
    }

    public int Width { get; }
    public int Height { get; }
    public PixelRect ContentBounds { get; }

    public Rgb24 GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
        var offset = checked(((y * Width) + x) * 3);
        return new(_rgb24[offset], _rgb24[offset + 1], _rgb24[offset + 2]);
    }

    internal ReadOnlySpan<byte> Pixels => _rgb24;
}

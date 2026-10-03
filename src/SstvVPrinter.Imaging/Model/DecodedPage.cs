using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Model;

public sealed class DecodedPage
{
    private readonly byte[] _rgb24;

    private DecodedPage(int width, int height, byte[] rgb24, PwgPageMetadata metadata)
    {
        Width = width;
        Height = height;
        _rgb24 = rgb24;
        Metadata = metadata;
    }

    public int Width { get; }
    public int Height { get; }
    public PwgPageMetadata Metadata { get; }

    public static DecodedPage Create(int width, int height, ReadOnlySpan<byte> rgb24, PwgPageMetadata metadata)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(metadata);

        int expectedLength;
        try
        {
            expectedLength = checked(width * height * 3);
        }
        catch (OverflowException exception)
        {
            throw new ArgumentOutOfRangeException(nameof(width), exception);
        }

        if (rgb24.Length != expectedLength)
        {
            throw new ArgumentException($"Expected {expectedLength} RGB bytes but received {rgb24.Length}.", nameof(rgb24));
        }

        return new DecodedPage(width, height, rgb24.ToArray(), metadata);
    }

    public Rgb24 GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        var offset = checked(((y * Width) + x) * 3);
        return new Rgb24(_rgb24[offset], _rgb24[offset + 1], _rgb24[offset + 2]);
    }

    internal ReadOnlySpan<byte> Pixels => _rgb24;
}

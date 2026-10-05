namespace SstvVPrinter.Sstv;

public sealed class RgbFrame
{
    private readonly byte[] _rgb24;

    private RgbFrame(int width, int height, byte[] rgb24)
    {
        Width = width;
        Height = height;
        _rgb24 = rgb24;
    }

    public int Width { get; }

    public int Height { get; }

    public static RgbFrame Create(int width, int height, ReadOnlySpan<byte> rgb24)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

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

        return new RgbFrame(width, height, rgb24.ToArray());
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

        var offset = ((y * Width) + x) * 3;
        return new Rgb24(_rgb24[offset], _rgb24[offset + 1], _rgb24[offset + 2]);
    }

    public void CopyPixelsTo(Span<byte> destination)
    {
        if (destination.Length != _rgb24.Length)
        {
            throw new ArgumentException(
                $"Expected a destination of {_rgb24.Length} bytes but received {destination.Length}.",
                nameof(destination));
        }

        _rgb24.CopyTo(destination);
    }
}

using System.Buffers.Binary;

namespace SstvVPrinter.Imaging.Tests.Pwg;

internal static class PwgFixtureBuilder
{
    internal const int HeaderLength = 1796;

    public static byte[] Create(
        int width,
        int height,
        IReadOnlyList<byte[]> encodedRows,
        int orientation = 0,
        int dpiX = 300,
        int dpiY = 300,
        int pageWidthPoints = 612,
        int pageHeightPoints = 792)
    {
        var header = CreateHeader(width, height, orientation, dpiX, dpiY, pageWidthPoints, pageHeightPoints);
        using var stream = new MemoryStream();
        stream.Write("RaS2"u8);
        stream.Write(header);
        foreach (var row in encodedRows)
        {
            stream.Write(row);
        }

        return stream.ToArray();
    }

    public static byte[] CreateSolid(int width, int height, byte r = 1, byte g = 2, byte b = 3, int orientation = 0)
    {
        var rows = new List<byte[]>();
        var remaining = height;
        while (remaining > 0)
        {
            var repeat = Math.Min(256, remaining);
            var encoded = new List<byte> { (byte)(repeat - 1) };
            var pixels = width;
            while (pixels > 0)
            {
                var run = Math.Min(128, pixels);
                encoded.Add((byte)(run - 1));
                encoded.Add(r);
                encoded.Add(g);
                encoded.Add(b);
                pixels -= run;
            }

            rows.Add(encoded.ToArray());
            remaining -= repeat;
        }

        return Create(width, height, rows, orientation);
    }

    public static byte[] CreateHeader(int width, int height, int orientation = 0, int dpiX = 300, int dpiY = 300, int pageWidthPoints = 612, int pageHeightPoints = 792)
    {
        var header = new byte[HeaderLength];
        WriteHeader(header, 276, dpiX);
        WriteHeader(header, 280, dpiY);
        WriteHeader(header, 344, orientation);
        WriteHeader(header, 352, pageWidthPoints);
        WriteHeader(header, 356, pageHeightPoints);
        WriteHeader(header, 372, width);
        WriteHeader(header, 376, height);
        WriteHeader(header, 384, 8);
        WriteHeader(header, 388, 24);
        WriteHeader(header, 392, checked(width * 3));
        WriteHeader(header, 396, 0);
        WriteHeader(header, 400, 19);
        WriteHeader(header, 420, 3);
        return header;
    }

    public static void Write(byte[] document, int headerOffset, int value) =>
        BinaryPrimitives.WriteInt32BigEndian(document.AsSpan(4 + headerOffset, 4), value);

    private static void WriteHeader(byte[] header, int offset, int value) =>
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(offset, 4), value);

    public static byte[] RepeatedRow(int repetitions, params (int Count, byte R, byte G, byte B)[] runs)
    {
        var bytes = new List<byte> { checked((byte)(repetitions - 1)) };
        foreach (var run in runs)
        {
            bytes.Add(checked((byte)(run.Count - 1)));
            bytes.Add(run.R);
            bytes.Add(run.G);
            bytes.Add(run.B);
        }

        return bytes.ToArray();
    }

    public static byte[] LiteralRow(int repetitions, params (byte R, byte G, byte B)[] pixels)
    {
        if (pixels.Length is < 2 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(pixels));
        }

        var bytes = new List<byte> { checked((byte)(repetitions - 1)), checked((byte)(257 - pixels.Length)) };
        foreach (var pixel in pixels)
        {
            bytes.Add(pixel.R);
            bytes.Add(pixel.G);
            bytes.Add(pixel.B);
        }

        return bytes.ToArray();
    }
}

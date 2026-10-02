using System.Buffers.Binary;
namespace SstvVPrinter.IppProbe.Capture;

public static class PwgRasterInspector
{
    private const int HeaderSize = 1796;
    public static async ValueTask<PwgRasterReport> InspectAsync(Stream payload, CancellationToken cancellationToken)
    {
        long? position = payload.CanSeek ? payload.Position : null;
        try
        {
            byte[] sync = new byte[4]; int count = await ReadUpToAsync(payload, sync, cancellationToken);
            if (count < 4 || !sync.AsSpan().SequenceEqual("RaS2"u8)) return new(false);
            byte[] header = new byte[HeaderSize];
            try { await payload.ReadExactlyAsync(header, cancellationToken); } catch (EndOfStreamException e) { throw new InvalidDataException("Truncated PWG Raster page header.", e); }
            return new(true, Int(header, 372), Int(header, 376), Int(header, 276), Int(header, 280), Int(header, 384), Int(header, 388), Int(header, 392), Int(header, 400));
        }
        finally { if (position.HasValue) payload.Position = position.Value; }
    }
    private static int Int(byte[] value, int offset) => BinaryPrimitives.ReadInt32BigEndian(value.AsSpan(offset, 4));
    private static async ValueTask<int> ReadUpToAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken) { int total = 0; while (total < buffer.Length) { int read = await stream.ReadAsync(buffer[total..], cancellationToken); if (read == 0) break; total += read; } return total; }
}

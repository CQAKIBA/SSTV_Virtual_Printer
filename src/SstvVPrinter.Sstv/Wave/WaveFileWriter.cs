using System.Buffers.Binary;
using SstvVPrinter.Sstv.Signal;

namespace SstvVPrinter.Sstv.Wave;

public static class WaveFileWriter
{
    public static async Task WriteAsync(string destinationPath, IAsyncEnumerable<PcmBlock> blocks, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(blocks);
        var fullPath = Path.GetFullPath(destinationPath);
        if (File.Exists(fullPath)) throw new IOException("Destination already exists.");
        var directory = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("Destination has no directory.", nameof(destinationPath));
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            long sampleCount = 0;
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(new byte[44], cancellationToken);
                await foreach (var block in blocks.WithCancellation(cancellationToken))
                {
                    if (block.StartSample != sampleCount) throw new InvalidDataException("PCM blocks must be contiguous and ordered.");
                    if (sampleCount + block.Samples.Length > (uint.MaxValue - 36L) / 2) throw new InvalidDataException("WAV data exceeds RIFF limits.");
                    var bytes = new byte[block.Samples.Length * 2];
                    for (var index = 0; index < block.Samples.Length; index++)
                    {
                        var sourceValue = block.Samples.Span[index];
                        if (!float.IsFinite(sourceValue))
                        {
                            throw new InvalidDataException("PCM samples must be finite.");
                        }

                        var value = Math.Clamp(sourceValue, -1f, 1f);
                        var pcm = value <= -1f ? short.MinValue : (short)Math.Round(value * short.MaxValue, MidpointRounding.AwayFromZero);
                        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), pcm);
                    }
                    await stream.WriteAsync(bytes, cancellationToken);
                    sampleCount += block.Samples.Length;
                }
                var dataLength = checked((uint)(sampleCount * 2));
                stream.Position = 0;
                var header = CreateHeader(dataLength);
                await stream.WriteAsync(header, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporary, fullPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static byte[] CreateHeader(uint dataLength)
    {
        var header = new byte[44];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), 36 + dataLength);
        "WAVEfmt "u8.CopyTo(header.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(22), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24), SstvEncodeOptions.SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28), SstvEncodeOptions.SampleRate * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(32), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(34), 16);
        "data"u8.CopyTo(header.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(40), dataLength);
        return header;
    }
}

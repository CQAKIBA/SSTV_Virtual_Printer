using SstvVPrinter.Imaging.Model;

namespace SstvVPrinter.Imaging.Pwg;

public sealed class PwgRasterDecoder
{
    private static ReadOnlySpan<byte> SyncWord => "RaS2"u8;

    public async ValueTask<DecodedPage> DecodeFirstPageAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("The PWG source stream must be readable.", nameof(source));
        }

        var sync = new byte[4];
        try
        {
            await ReadExactlyAsync(source, sync, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException exception)
        {
            throw new PwgRasterException("Truncated PWG sync word.", exception);
        }

        if (!sync.AsSpan().SequenceEqual(SyncWord))
        {
            throw new PwgRasterException("Invalid PWG sync word; expected RaS2.");
        }

        var headerBytes = new byte[PwgRasterHeader.Length];
        try
        {
            await ReadExactlyAsync(source, headerBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException exception)
        {
            throw new PwgRasterException("Truncated PWG page header.", exception);
        }

        var header = PwgRasterHeader.Parse(headerBytes);
        var physicalPixels = new byte[checked(header.Width * header.Height * 3)];
        var row = new byte[checked(header.Width * 3)];
        var outputRow = 0;

        try
        {
            while (outputRow < header.Height)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var repeats = (await ReadByteAsync(source, cancellationToken).ConfigureAwait(false)) + 1;
                if (repeats > header.Height - outputRow)
                {
                    throw new PwgRasterException("PWG row repeat count exceeds the remaining page height.");
                }

                await DecodeRowAsync(source, row, header.Width, cancellationToken).ConfigureAwait(false);
                for (var repeat = 0; repeat < repeats; repeat++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    row.CopyTo(physicalPixels.AsSpan((outputRow + repeat) * row.Length, row.Length));
                }

                outputRow += repeats;
            }
        }
        catch (EndOfStreamException exception)
        {
            throw new PwgRasterException("Truncated PWG raster data.", exception);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var metadata = new PwgPageMetadata(
            header.Width, header.Height, header.Width, header.Height,
            header.DpiX, header.DpiY, header.Orientation,
            header.PageWidthPoints, header.PageHeightPoints);
        return DecodedPage.CreateOwned(header.Width, header.Height, physicalPixels, metadata);
    }

    private static async ValueTask DecodeRowAsync(Stream source, Memory<byte> row, int width, CancellationToken cancellationToken)
    {
        var writtenPixels = 0;
        while (writtenPixels < width)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var control = await ReadByteAsync(source, cancellationToken).ConfigureAwait(false);
            if (control == 128)
            {
                throw new PwgRasterException("PWG raster control 128 is reserved and invalid.");
            }

            if (control <= 127)
            {
                var count = control + 1;
                RequireFits(count, width - writtenPixels);
                var color = new byte[3];
                await ReadExactlyAsync(source, color, cancellationToken).ConfigureAwait(false);
                for (var index = 0; index < count; index++)
                {
                    color.CopyTo(row.Slice((writtenPixels + index) * 3, 3));
                }

                writtenPixels += count;
            }
            else
            {
                var count = 257 - control;
                RequireFits(count, width - writtenPixels);
                await ReadExactlyAsync(source, row.Slice(writtenPixels * 3, count * 3), cancellationToken).ConfigureAwait(false);
                writtenPixels += count;
            }
        }
    }

    private static void RequireFits(int count, int remaining)
    {
        if (count > remaining)
        {
            throw new PwgRasterException("PWG raster run exceeds the declared scanline width.");
        }
    }

    private static async ValueTask<int> ReadByteAsync(Stream source, CancellationToken cancellationToken)
    {
        var value = new byte[1];
        await ReadExactlyAsync(source, value, cancellationToken).ConfigureAwait(false);
        return value[0];
    }

    private static async ValueTask ReadExactlyAsync(Stream source, Memory<byte> destination, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var read = await source.ReadAsync(destination[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            total += read;
        }
    }
}

using System.Buffers.Binary;
using SstvVPrinter.App.Jobs;
using SstvVPrinter.Imaging.Pwg;
using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.App.Tests.Jobs;

public sealed class PwgPrintJobSinkTests
{
    [Fact]
    public async Task Valid_pwg_becomes_one_queue_job()
    {
        var queue = new PrintJobQueue();
        var acceptedAt = new DateTimeOffset(2026, 10, 5, 12, 30, 0, TimeSpan.Zero);
        var sink = new PwgPrintJobSink(queue, new PwgRasterDecoder(), () => acceptedAt);
        await using var document = new MemoryStream(CreateSolidPwg(2, 1, 10, 20, 30));

        await sink.AcceptAsync(
            new ProbeJob(7, "ipp://localhost/jobs/7", "Daisuke", "Paint"),
            document,
            "image/pwg-raster",
            CancellationToken.None);

        Assert.NotNull(queue.Active);
        var job = queue.Active!;
        Assert.Equal("Paint", job.SourceName);
        Assert.Equal(acceptedAt, job.AcceptedAt);
        Assert.Equal(2, job.Page.Width);
        Assert.Equal(10, job.Page.GetPixel(0, 0).R);
        Assert.Empty(queue.Waiting);
    }

    [Fact]
    public async Task Malformed_pwg_does_not_enqueue_partial_job()
    {
        var queue = new PrintJobQueue();
        var sink = new PwgPrintJobSink(queue, new PwgRasterDecoder(), () => DateTimeOffset.UtcNow);
        await using var document = new MemoryStream("not-pwg"u8.ToArray());

        await Assert.ThrowsAsync<PwgRasterException>(async () =>
            await sink.AcceptAsync(
                new ProbeJob(8, "ipp://localhost/jobs/8", "Daisuke", "Broken"),
                document,
                "image/pwg-raster",
                CancellationToken.None));

        Assert.Null(queue.Active);
        Assert.Empty(queue.Waiting);
    }

    private static byte[] CreateSolidPwg(int width, int height, byte r, byte g, byte b)
    {
        var bytes = new byte[4 + 1796 + 5];
        "RaS2"u8.CopyTo(bytes);
        Write(bytes, 276, 300);
        Write(bytes, 280, 300);
        Write(bytes, 352, 612);
        Write(bytes, 356, 792);
        Write(bytes, 372, width);
        Write(bytes, 376, height);
        Write(bytes, 384, 8);
        Write(bytes, 388, 24);
        Write(bytes, 392, width * 3);
        Write(bytes, 400, 19);
        Write(bytes, 420, 3);
        bytes[1800] = (byte)(height - 1);
        bytes[1801] = (byte)(width - 1);
        bytes[1802] = r;
        bytes[1803] = g;
        bytes[1804] = b;
        return bytes;
    }

    private static void Write(byte[] document, int headerOffset, int value) =>
        BinaryPrimitives.WriteInt32BigEndian(document.AsSpan(4 + headerOffset, 4), value);
}

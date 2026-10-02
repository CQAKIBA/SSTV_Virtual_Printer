using System.Buffers.Binary;
using SstvVPrinter.IppProbe.Capture;

namespace SstvVPrinter.IppProbe.Tests;

public sealed class PwgRasterInspectorTests
{
    [Fact]
    public async Task InspectAsync_recognizes_big_endian_pwg_sync_word()
    {
        await using var stream = new MemoryStream(Header());
        Assert.True((await PwgRasterInspector.InspectAsync(stream, CancellationToken.None)).IsPwgRaster);
    }

    [Fact]
    public async Task InspectAsync_reports_first_page_dimensions_and_resolution()
    {
        await using var stream = new MemoryStream(Header());
        PwgRasterReport report = await PwgRasterInspector.InspectAsync(stream, CancellationToken.None);
        Assert.Equal(2480, report.Width); Assert.Equal(3508, report.Height);
        Assert.Equal(300, report.XResolution); Assert.Equal(300, report.YResolution);
        Assert.Equal(8, report.BitsPerColor); Assert.Equal(24, report.BitsPerPixel);
        Assert.Equal(7440, report.BytesPerLine); Assert.Equal(19, report.ColorSpace);
    }

    [Fact]
    public async Task InspectAsync_rejects_non_pwg_payload()
    {
        await using var stream = new MemoryStream("nope"u8.ToArray());
        Assert.False((await PwgRasterInspector.InspectAsync(stream, CancellationToken.None)).IsPwgRaster);
    }

    [Fact]
    public async Task InspectAsync_rejects_truncated_page_header()
    {
        await using var stream = new MemoryStream("RaS2short"u8.ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => PwgRasterInspector.InspectAsync(stream, CancellationToken.None).AsTask());
    }

    private static byte[] Header()
    {
        byte[] bytes = new byte[4 + 1796];
        "RaS2"u8.CopyTo(bytes);
        Span<byte> header = bytes.AsSpan(4);
        BinaryPrimitives.WriteInt32BigEndian(header[276..], 300); BinaryPrimitives.WriteInt32BigEndian(header[280..], 300);
        BinaryPrimitives.WriteInt32BigEndian(header[372..], 2480); BinaryPrimitives.WriteInt32BigEndian(header[376..], 3508);
        BinaryPrimitives.WriteInt32BigEndian(header[384..], 8); BinaryPrimitives.WriteInt32BigEndian(header[388..], 24);
        BinaryPrimitives.WriteInt32BigEndian(header[392..], 7440); BinaryPrimitives.WriteInt32BigEndian(header[400..], 19);
        return bytes;
    }
}

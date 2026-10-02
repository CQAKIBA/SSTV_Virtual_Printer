using System.Buffers.Binary;
using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.IppProbe.Tests;

public sealed class IppMessageReaderTests
{
    [Fact]
    public async Task ReadAsync_parses_operation_attributes_and_stops_before_document()
    {
        byte[] marker = [0x52, 0x41, 0x53, 0x54];
        await using var stream = new MemoryStream(BuildPrintJobRequest(marker));

        IppMessage message = await IppMessageReader.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(new IppVersion(2, 0), message.Version);
        Assert.Equal(IppOperations.PrintJob, message.Code);
        Assert.Equal(42, message.RequestId);
        Assert.Equal("image/pwg-raster", message.GetRequiredString("document-format"));
        byte[] remaining = new byte[marker.Length];
        Assert.Equal(marker.Length, await stream.ReadAsync(remaining, CancellationToken.None));
        Assert.Equal(marker, remaining);
    }

    [Fact]
    public async Task ReadAsync_accepts_one_byte_at_a_time_stream()
    {
        await using var inner = new MemoryStream(BuildPrintJobRequest([]));
        await using var stream = new OneByteAtATimeStream(inner);

        IppMessage message = await IppMessageReader.ReadAsync(stream, CancellationToken.None);

        Assert.Equal("tester", message.GetRequiredString("requesting-user-name"));
    }

    [Fact]
    public async Task ReadAsync_combines_zero_length_attribute_names_with_previous_name()
    {
        byte[] request = BuildRequest(writer =>
        {
            WriteAttribute(writer, IppValueTags.Keyword, "requested-attributes", "printer-name");
            WriteAttribute(writer, IppValueTags.Keyword, string.Empty, "printer-state");
        });

        await using var stream = new MemoryStream(request);
        IppMessage message = await IppMessageReader.ReadAsync(stream, CancellationToken.None);

        IppAttribute attribute = Assert.Single(message.Groups.Single().Attributes);
        Assert.Equal("requested-attributes", attribute.Name);
        Assert.Equal(["printer-name", "printer-state"], attribute.Values.Select(v => v.AsString()));
    }

    [Fact]
    public async Task ReadAsync_rejects_unknown_delimiter_tag()
    {
        byte[] request = [0x02, 0x00, 0x00, 0x02, 0, 0, 0, 42, 0x09];
        await using var stream = new MemoryStream(request);

        IppProtocolException error = await Assert.ThrowsAsync<IppProtocolException>(
            () => IppMessageReader.ReadAsync(stream, CancellationToken.None).AsTask());

        Assert.Equal(IppStatusCodes.ClientErrorBadRequest, error.StatusCode);
    }

    [Fact]
    public async Task ReadAsync_rejects_truncated_value_without_large_allocation()
    {
        byte[] request = [
            0x02, 0x00, 0x00, 0x02, 0, 0, 0, 42,
            IppDelimiterTags.OperationAttributes,
            IppValueTags.TextWithoutLanguage, 0, 1, (byte)'x', 0x7f, 0xff,
            0x41
        ];
        await using var stream = new MemoryStream(request);

        IppProtocolException error = await Assert.ThrowsAsync<IppProtocolException>(
            () => IppMessageReader.ReadAsync(stream, CancellationToken.None).AsTask());

        Assert.Equal(IppStatusCodes.ClientErrorBadRequest, error.StatusCode);
    }

    internal static byte[] BuildPrintJobRequest(byte[] document) => BuildRequest(writer =>
    {
        WriteAttribute(writer, IppValueTags.Charset, "attributes-charset", "utf-8");
        WriteAttribute(writer, IppValueTags.NaturalLanguage, "attributes-natural-language", "en");
        WriteAttribute(writer, IppValueTags.Uri, "printer-uri", "ipp://localhost:8631/ipp/print");
        WriteAttribute(writer, IppValueTags.NameWithoutLanguage, "requesting-user-name", "tester");
        WriteAttribute(writer, IppValueTags.MimeMediaType, "document-format", "image/pwg-raster");
    }, document);

    internal static byte[] BuildRequest(Action<BinaryWriter> attributes, byte[]? document = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)2);
        writer.Write((byte)0);
        WriteUInt16(writer, IppOperations.PrintJob);
        WriteInt32(writer, 42);
        writer.Write(IppDelimiterTags.OperationAttributes);
        attributes(writer);
        writer.Write(IppDelimiterTags.EndOfAttributes);
        writer.Write(document ?? []);
        writer.Flush();
        return stream.ToArray();
    }

    internal static void WriteAttribute(BinaryWriter writer, byte tag, string name, string value)
    {
        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        byte[] valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
        writer.Write(tag);
        WriteUInt16(writer, checked((ushort)nameBytes.Length));
        writer.Write(nameBytes);
        WriteUInt16(writer, checked((ushort)valueBytes.Length));
        writer.Write(valueBytes);
    }

    internal static void WriteUInt16(BinaryWriter writer, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        writer.Write(bytes);
    }

    internal static void WriteInt32(BinaryWriter writer, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        writer.Write(bytes);
    }

    private sealed class OneByteAtATimeStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(1, count));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); GC.SuppressFinalize(this); }
    }
}

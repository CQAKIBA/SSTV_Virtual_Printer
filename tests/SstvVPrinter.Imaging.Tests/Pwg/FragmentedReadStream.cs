namespace SstvVPrinter.Imaging.Tests.Pwg;

internal sealed class FragmentedReadStream : Stream
{
    private readonly Stream _inner;
    private readonly int _maximumRead;

    public FragmentedReadStream(byte[] bytes, int maximumRead = 1)
    {
        _inner = new MemoryStream(bytes);
        _maximumRead = maximumRead;
    }

    public bool WasDisposed { get; private set; }
    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, Math.Min(count, _maximumRead));
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(buffer[..Math.Min(buffer.Length, _maximumRead)], cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

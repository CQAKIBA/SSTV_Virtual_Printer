using System.Text.Json;
using SstvVPrinter.IppProbe.Capture;
using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.IppProbe.Tests;

public sealed class PrintJobCaptureTests
{
    [Fact]
    public async Task CaptureAsync_writes_payload_and_metadata_atomically()
    {
        using var directory = new TemporaryDirectory();
        byte[] payload = "raster-data"u8.ToArray();
        var capture = new PrintJobCapture(new CaptureOptions(directory.Path));

        CapturedJob result = await capture.CaptureAsync(new ProbeJob(7, "ipp://localhost/jobs/7", "tester", "demo"), new MemoryStream(payload), "image/pwg-raster", CancellationToken.None);

        Assert.Equal(payload, await File.ReadAllBytesAsync(result.PayloadPath));
        CapturedJob metadata = JsonSerializer.Deserialize<CapturedJob>(await File.ReadAllTextAsync(result.MetadataPath))!;
        Assert.Equal(7, metadata.JobId);
        Assert.Equal("tester", metadata.UserName);
        Assert.Equal("demo", metadata.JobName);
        Assert.Equal("image/pwg-raster", metadata.DocumentFormat);
        Assert.Equal(payload.Length, metadata.ByteLength);
        Assert.Equal("E3F1CBA3E78C5F436D54AA9B600C6ED8DC20E27C99A88E2681AB0B58893CA6C0", metadata.Sha256);
        Assert.DoesNotContain(Directory.EnumerateFiles(directory.Path), path => path.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CaptureAsync_rejects_payload_over_64MiB_without_partial_files()
    {
        using var directory = new TemporaryDirectory();
        var capture = new PrintJobCapture(new CaptureOptions(directory.Path));

        await Assert.ThrowsAsync<InvalidDataException>(() => capture.CaptureAsync(Job(1), new GeneratedStream(CaptureOptions.MaximumDocumentBytes + 1), "image/pwg-raster", CancellationToken.None));

        Assert.Empty(Directory.EnumerateFiles(directory.Path));
    }

    [Fact]
    public async Task CaptureAsync_cancellation_removes_temporary_files()
    {
        using var directory = new TemporaryDirectory();
        var capture = new PrintJobCapture(new CaptureOptions(directory.Path));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.CaptureAsync(Job(2), new GeneratedStream(1024), "image/pwg-raster", cancellation.Token));

        Assert.Empty(Directory.EnumerateFiles(directory.Path));
    }

    [Fact]
    public async Task CaptureAsync_concurrent_jobs_keep_distinct_payloads()
    {
        using var directory = new TemporaryDirectory();
        var capture = new PrintJobCapture(new CaptureOptions(directory.Path));

        CapturedJob[] jobs = await Task.WhenAll(
            capture.CaptureAsync(Job(10), new MemoryStream([1, 2, 3]), "image/pwg-raster", CancellationToken.None),
            capture.CaptureAsync(Job(11), new MemoryStream([4, 5, 6]), "image/pwg-raster", CancellationToken.None));

        Assert.NotEqual(jobs[0].PayloadPath, jobs[1].PayloadPath);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(jobs[0].PayloadPath));
        Assert.Equal([4, 5, 6], await File.ReadAllBytesAsync(jobs[1].PayloadPath));
    }

    [Fact]
    public void FindHighestJobId_uses_existing_completed_capture_numbers()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllBytes(Path.Combine(directory.Path, "1.pwg"), []);
        File.WriteAllText(Path.Combine(directory.Path, "12.json"), "{}");
        File.WriteAllText(Path.Combine(directory.Path, "notes.json"), "{}");

        Assert.Equal(12, PrintJobCapture.FindHighestJobId(directory.Path));
    }

    private static ProbeJob Job(int id) => new(id, $"ipp://localhost/jobs/{id}", "tester", null);

    private sealed class GeneratedStream(long remaining) : Stream
    {
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override long Length => remaining; public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) { int n = (int)Math.Min(count, remaining); Array.Clear(buffer, offset, n); remaining -= n; return n; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); int n = (int)Math.Min(buffer.Length, remaining); buffer.Span[..n].Clear(); remaining -= n; return ValueTask.FromResult(n); }
        public override void Flush() { }
        public override long Seek(long o, SeekOrigin so) => throw new NotSupportedException(); public override void SetLength(long v) => throw new NotSupportedException(); public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sstv-ipp-{Guid.NewGuid():N}"); Directory.CreateDirectory(Path); }
        public string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}

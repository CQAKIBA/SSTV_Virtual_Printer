using System.Text;
using SstvVPrinter.ImagingTool;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.Imaging.Tests.Tool;

public sealed class PpmWriterTests
{
    [Fact]
    public async Task WriteAsync_writes_exact_binary_P6_header_and_RGB_payload()
    {
        using var directory = new TemporaryDirectory();
        var destination = Path.Combine(directory.Path, "preview.ppm");
        var frame = RgbFrame.Create(2, 1, [1, 2, 3, 4, 5, 6]);

        await PpmWriter.WriteAsync(destination, frame);

        var expected = Encoding.ASCII.GetBytes("P6\n2 1\n255\n").Concat(new byte[] { 1, 2, 3, 4, 5, 6 }).ToArray();
        Assert.Equal(expected, await File.ReadAllBytesAsync(destination));
        Assert.Single(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task WriteAsync_does_not_overwrite_destination_or_leave_temporary_files()
    {
        using var directory = new TemporaryDirectory();
        var destination = Path.Combine(directory.Path, "preview.ppm");
        await File.WriteAllTextAsync(destination, "existing");

        await Assert.ThrowsAsync<IOException>(() =>
            PpmWriter.WriteAsync(destination, RgbFrame.Create(1, 1, [1, 2, 3])));

        Assert.Equal("existing", await File.ReadAllTextAsync(destination));
        Assert.Single(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task WriteAsync_cancellation_publishes_nothing_and_cleans_temporary_file()
    {
        using var directory = new TemporaryDirectory();
        var destination = Path.Combine(directory.Path, "preview.ppm");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PpmWriter.WriteAsync(destination, RgbFrame.Create(1, 1, [1, 2, 3]), cancellation.Token));

        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sstv-imaging-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

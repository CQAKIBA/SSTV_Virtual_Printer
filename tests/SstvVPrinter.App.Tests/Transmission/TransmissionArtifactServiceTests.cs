using System.Buffers.Binary;
using SstvVPrinter.App.Transmission;
using SstvVPrinter.Sstv;

namespace SstvVPrinter.App.Tests.Transmission;

public sealed class TransmissionArtifactServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"sstv-wave-{Guid.NewGuid():N}");

    [Fact]
    public async Task Save_writes_valid_wave_without_overwriting_destination()
    {
        Directory.CreateDirectory(_directory);
        var service = new TransmissionArtifactService(_directory);
        var destination = Path.Combine(_directory, "message.wav");

        await service.SaveAsync(CreatePrepared(), destination, null, CancellationToken.None);

        byte[] bytes = await File.ReadAllBytesAsync(destination);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.Equal(48_000, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24, 4)));
        await Assert.ThrowsAsync<IOException>(() =>
            service.SaveAsync(CreatePrepared(), destination, null, CancellationToken.None));
    }

    [Fact]
    public async Task Cancelled_temporary_generation_leaves_no_wave()
    {
        Directory.CreateDirectory(_directory);
        var service = new TransmissionArtifactService(_directory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateTemporaryAsync(CreatePrepared(), null, cancellation.Token));

        Assert.Empty(Directory.EnumerateFiles(_directory));
    }

    private static PreparedTransmission CreatePrepared()
    {
        var options = new SstvEncodeOptions(SstvMode.Robot36);
        return new(
            RgbFrame.Create(320, 240, new byte[320 * 240 * 3]),
            options,
            SstvDurationEstimator.Estimate(options),
            DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}

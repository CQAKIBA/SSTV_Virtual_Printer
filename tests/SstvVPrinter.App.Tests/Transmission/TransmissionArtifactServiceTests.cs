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

    [Fact]
    public async Task Save_writes_radiofax_wave_with_exact_samples_and_monotonic_progress()
    {
        Directory.CreateDirectory(_directory);
        var service = new TransmissionArtifactService(_directory);
        var destination = Path.Combine(_directory, "fax.wav");
        var reports = new List<double>();
        var progress = new InlineProgress(value => reports.Add(value));
        var frame = RgbFrame.Create(864, 32, new byte[864 * 32 * 3]);
        var options = new TransmissionEncodeOptions(SstvMode.Radiofax120Ioc288);
        var prepared = new PreparedTransmission(
            frame,
            options,
            TransmissionDurationEstimator.Estimate(frame, options),
            DateTimeOffset.UtcNow);

        await service.SaveAsync(prepared, destination, progress, CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(destination);
        var expectedSamples = 66 * 48_000;
        Assert.Equal(expectedSamples * 2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40, 4)));
        Assert.Equal(0, reports[0]);
        Assert.Equal(1, reports[^1]);
        Assert.All(reports.Zip(reports.Skip(1)), pair => Assert.True(pair.First <= pair.Second));
    }

    private static PreparedTransmission CreatePrepared()
    {
        var frame = RgbFrame.Create(320, 240, new byte[320 * 240 * 3]);
        var options = new TransmissionEncodeOptions(SstvMode.Robot36);
        return new(
            frame,
            options,
            TransmissionDurationEstimator.Estimate(frame, options),
            DateTimeOffset.UtcNow);
    }

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}

using System.Runtime.CompilerServices;
using SstvVPrinter.Sstv;
using SstvVPrinter.Sstv.Signal;
using SstvVPrinter.Sstv.Wave;

namespace SstvVPrinter.App.Transmission;

public interface ITransmissionArtifactService
{
    Task SaveAsync(
        PreparedTransmission prepared,
        string destinationPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken);

    Task<string> CreateTemporaryAsync(
        PreparedTransmission prepared,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}

public sealed class TransmissionArtifactService : ITransmissionArtifactService
{
    private readonly string _temporaryDirectory;

    public TransmissionArtifactService() : this(Path.Combine(Path.GetTempPath(), "SstvVPrinter"))
    {
    }

    public TransmissionArtifactService(string temporaryDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryDirectory);
        _temporaryDirectory = Path.GetFullPath(temporaryDirectory);
    }

    public Task SaveAsync(
        PreparedTransmission prepared,
        string destinationPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        return WaveFileWriter.WriteAsync(
            destinationPath,
            WithProgress(prepared, progress, cancellationToken),
            cancellationToken);
    }

    public async Task<string> CreateTemporaryAsync(
        PreparedTransmission prepared,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var path = Path.Combine(_temporaryDirectory, $"sstv-{Guid.NewGuid():N}.wav");
        try
        {
            await SaveAsync(prepared, path, progress, cancellationToken);
            return path;
        }
        catch
        {
            TryDelete(path);
            throw;
        }
    }

    private static async IAsyncEnumerable<PcmBlock> WithProgress(
        PreparedTransmission prepared,
        IProgress<double>? progress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var estimatedSamples = Math.Max(
            1L,
            (long)Math.Round(
                prepared.EstimatedDuration.TotalSeconds * SignalConstants.SampleRate,
                MidpointRounding.AwayFromZero));
        progress?.Report(0);
        await foreach (var block in TransmissionEncoder.EncodeAsync(
            prepared.Frame, prepared.Options, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return block;
            var completed = block.StartSample + block.Samples.Length;
            progress?.Report(Math.Clamp((double)completed / estimatedSamples, 0, 1));
        }

        progress?.Report(1);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

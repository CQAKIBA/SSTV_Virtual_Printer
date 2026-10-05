using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using SstvVPrinter.App.Transmission;

namespace SstvVPrinter.Desktop.Platforms.Windows;

[SupportedOSPlatform("windows")]
public sealed class NaudioWavePlaybackService : IWavePlaybackService
{
    public async Task PlayAsync(
        string path,
        string endpointId,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDevice(endpointId);
        using var reader = new WaveFileReader(path);
        using var output = new WasapiPlayerBuilder()
            .WithDevice(device)
            .WithSharedMode()
            .WithLatency(100)
            .Build();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, args) =>
        {
            if (args.Exception is not null) completion.TrySetException(args.Exception);
            else completion.TrySetResult();
        };
        output.Init(reader);
        using var registration = cancellationToken.Register(output.Stop);
        progress?.Report(0);
        output.Play();
        while (!completion.Task.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(reader.Length == 0 ? 1 : Math.Clamp((double)reader.Position / reader.Length, 0, 1));
            await Task.WhenAny(completion.Task, Task.Delay(100, cancellationToken));
        }

        await completion.Task;
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(1);
    }
}

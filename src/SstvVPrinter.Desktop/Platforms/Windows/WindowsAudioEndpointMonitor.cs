using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using SstvVPrinter.App.Audio;

namespace SstvVPrinter.Desktop.Platforms.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsAudioEndpointMonitor : IAudioEndpointMonitor
{
    public ValueTask<IReadOnlyList<AudioEndpointInfo>> GetEndpointsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var enumerator = new MMDeviceEnumerator();
        string? defaultId = null;
        try
        {
            using var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            defaultId = defaultDevice.ID;
        }
        catch (COMException)
        {
        }

        var result = new List<AudioEndpointInfo>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (device)
            {
                float? volume = null;
                bool? muted = null;
                try
                {
                    volume = device.AudioEndpointVolume.MasterVolumeLevelScalar * 100f;
                    muted = device.AudioEndpointVolume.Mute;
                }
                catch (COMException)
                {
                }

                result.Add(new(device.ID, device.FriendlyName, true, volume, muted)
                {
                    IsDefault = device.ID == defaultId,
                });
            }
        }

        return ValueTask.FromResult<IReadOnlyList<AudioEndpointInfo>>(result);
    }

    public async IAsyncEnumerable<IReadOnlyList<AudioEndpointInfo>> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AudioEndpointInfo>? previous = null;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            var current = await GetEndpointsAsync(cancellationToken);
            if (previous is null || !previous.SequenceEqual(current))
            {
                previous = current;
                yield return current;
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }
}

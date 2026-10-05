using CommunityToolkit.Mvvm.ComponentModel;
using SstvVPrinter.App.Audio;

namespace SstvVPrinter.Desktop.ViewModels;

public sealed partial class AudioEndpointViewModel(IAudioEndpointMonitor monitor) : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<AudioEndpointInfo> _endpoints = [];

    [ObservableProperty]
    private AudioEndpointInfo? _selectedEndpoint;

    [ObservableProperty]
    private string _volumeText = "不明";

    [ObservableProperty]
    private bool _isMutedWarning;

    [ObservableProperty]
    private string? _warningText;

    [ObservableProperty]
    private string? _deviceError;

    [ObservableProperty]
    private bool _canSend;

    [ObservableProperty]
    private bool _usedFallback;

    public async Task RefreshAsync(string? preferredEndpointId, CancellationToken cancellationToken = default)
    {
        Endpoints = await monitor.GetEndpointsAsync(cancellationToken);
        var preferred = Endpoints.FirstOrDefault(
            endpoint => endpoint.IsAvailable && endpoint.Id == preferredEndpointId);
        SelectedEndpoint = preferred
            ?? Endpoints.FirstOrDefault(endpoint => endpoint.IsAvailable && endpoint.IsDefault)
            ?? Endpoints.FirstOrDefault(endpoint => endpoint.IsAvailable);
        UsedFallback = preferredEndpointId is not null &&
            SelectedEndpoint is not null &&
            SelectedEndpoint.Id != preferredEndpointId;
        ApplySelection();
    }

    public async Task WatchAsync(string? preferredEndpointId, CancellationToken cancellationToken = default)
    {
        await foreach (var endpoints in monitor.WatchAsync(cancellationToken))
        {
            Endpoints = endpoints;
            var currentId = SelectedEndpoint?.Id ?? preferredEndpointId;
            var preferred = endpoints.FirstOrDefault(
                endpoint => endpoint.IsAvailable && endpoint.Id == currentId);
            SelectedEndpoint = preferred
                ?? endpoints.FirstOrDefault(endpoint => endpoint.IsAvailable && endpoint.IsDefault)
                ?? endpoints.FirstOrDefault(endpoint => endpoint.IsAvailable);
            UsedFallback = currentId is not null &&
                SelectedEndpoint is not null &&
                SelectedEndpoint.Id != currentId;
            ApplySelection();
        }
    }

    private void ApplySelection()
    {
        if (SelectedEndpoint is null)
        {
            VolumeText = "不明";
            IsMutedWarning = false;
            WarningText = null;
            DeviceError = "使用可能な音声出力デバイスがありません。";
            CanSend = false;
            return;
        }

        VolumeText = SelectedEndpoint.VolumePercent is { } volume
            ? $"{Math.Round(volume):0}%"
            : "不明";
        IsMutedWarning = SelectedEndpoint.IsMuted is true;
        WarningText = IsMutedWarning ? "MUTED / ミュート中" : null;
        DeviceError = null;
        CanSend = true;
    }
}

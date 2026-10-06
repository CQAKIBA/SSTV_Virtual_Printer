using SstvVPrinter.App.Audio;
using SstvVPrinter.Desktop.ViewModels;

namespace SstvVPrinter.Desktop.Tests;

public sealed class AudioEndpointViewModelTests
{
    [Fact]
    public async Task Normal_endpoint_displays_volume_and_allows_send()
    {
        var viewModel = new AudioEndpointViewModel(new FakeMonitor([
            Endpoint("usb", "USB Audio", 38, false, true),
        ]));

        await viewModel.RefreshAsync("usb", TestContext.Current.CancellationToken);

        Assert.Equal("USB Audio", viewModel.SelectedEndpoint?.Name);
        Assert.Equal("38%", viewModel.VolumeText);
        Assert.False(viewModel.IsMutedWarning);
        Assert.True(viewModel.CanSend);
    }

    [Fact]
    public async Task Muted_endpoint_keeps_volume_and_exposes_warning()
    {
        var viewModel = new AudioEndpointViewModel(new FakeMonitor([
            Endpoint("usb", "USB Audio", 38, true, true),
        ]));

        await viewModel.RefreshAsync("usb", TestContext.Current.CancellationToken);

        Assert.Equal("38%", viewModel.VolumeText);
        Assert.True(viewModel.IsMutedWarning);
        Assert.Equal("MUTED / ミュート中", viewModel.WarningText);
        Assert.True(viewModel.CanSend);
    }

    [Fact]
    public async Task Unknown_volume_displays_unknown_without_claiming_mute()
    {
        var viewModel = new AudioEndpointViewModel(new FakeMonitor([
            Endpoint("usb", "USB Audio", null, null, true),
        ]));

        await viewModel.RefreshAsync("usb", TestContext.Current.CancellationToken);

        Assert.Equal("不明", viewModel.VolumeText);
        Assert.False(viewModel.IsMutedWarning);
        Assert.True(viewModel.CanSend);
    }

    [Fact]
    public async Task Disconnected_remembered_endpoint_falls_back_to_default()
    {
        var viewModel = new AudioEndpointViewModel(new FakeMonitor([
            Endpoint("old", "Disconnected", null, null, false),
            Endpoint("default", "Speakers", 50, false, true, isDefault: true),
        ]));

        await viewModel.RefreshAsync("old", TestContext.Current.CancellationToken);

        Assert.Equal("default", viewModel.SelectedEndpoint?.Id);
        Assert.True(viewModel.UsedFallback);
        Assert.True(viewModel.CanSend);
    }

    [Fact]
    public async Task No_usable_endpoint_blocks_send()
    {
        var viewModel = new AudioEndpointViewModel(new FakeMonitor([
            Endpoint("old", "Disconnected", null, null, false),
        ]));

        await viewModel.RefreshAsync("old", TestContext.Current.CancellationToken);

        Assert.Null(viewModel.SelectedEndpoint);
        Assert.False(viewModel.CanSend);
        Assert.NotNull(viewModel.DeviceError);
    }

    [Fact]
    public void Monitor_contract_has_no_volume_or_mute_setter()
    {
        string[] methodNames = typeof(IAudioEndpointMonitor).GetMethods().Select(method => method.Name).ToArray();

        Assert.DoesNotContain(methodNames, name => name.Contains("SetVolume", StringComparison.Ordinal));
        Assert.DoesNotContain(methodNames, name => name.Contains("SetMute", StringComparison.Ordinal));
    }

    [Fact]
    public void Endpoint_display_name_uses_guid_tail_and_default_marker()
    {
        var endpoint = new AudioEndpointInfo(
            "{0.0.0.00000000}.{12345678-1234-1234-1234-ABCDEF123456}",
            "USB Audio", true, 50, false)
        {
            IsDefault = true,
        };

        Assert.Equal("USB Audio - ABCDEF123456 (既定)", endpoint.DisplayName);
    }

    private static AudioEndpointInfo Endpoint(
        string id,
        string name,
        float? volume,
        bool? muted,
        bool available,
        bool isDefault = false) =>
        new(id, name, available, volume, muted) { IsDefault = isDefault };

    private sealed class FakeMonitor(IReadOnlyList<AudioEndpointInfo> endpoints) : IAudioEndpointMonitor
    {
        public ValueTask<IReadOnlyList<AudioEndpointInfo>> GetEndpointsAsync(
            CancellationToken cancellationToken = default) => ValueTask.FromResult(endpoints);

        public async IAsyncEnumerable<IReadOnlyList<AudioEndpointInfo>> WatchAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return endpoints;
            await Task.CompletedTask;
        }
    }
}

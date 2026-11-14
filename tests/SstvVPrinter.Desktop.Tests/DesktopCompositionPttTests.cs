using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using SstvVPrinter.App.Audio;
using SstvVPrinter.App.Jobs;
using SstvVPrinter.App.Settings;
using SstvVPrinter.App.Transmission;
using SstvVPrinter.Desktop.Hosting;
using SstvVPrinter.Desktop.Ptt;
using SstvVPrinter.Desktop.Views;
using SstvVPrinter.Imaging.Model;

namespace SstvVPrinter.Desktop.Tests;

public sealed class DesktopCompositionPttTests
{
    [AvaloniaTheory]
    [InlineData("COM4", "dtr", "COM4", PttControlMethod.Dtr)]
    [InlineData("COM99", "invalid", "N/A", PttControlMethod.Rts)]
    public async Task Startup_discovers_off_ui_thread_restores_available_port_without_opening(
        string savedPort, string savedMethod, string expectedPort, PttControlMethod expectedMethod)
    {
        await using var fixture = await Fixture.CreateAsync(UserPreferences.Default with
        { PttPortName = savedPort, PttMethod = savedMethod });

        Assert.Equal(new[] { "N/A", "COM4", "COM12" }, fixture.Composition.ViewModel.PttPorts);
        Assert.Equal(expectedPort, fixture.Composition.ViewModel.SelectedPttPort);
        Assert.Equal(expectedMethod, fixture.Composition.ViewModel.SelectedPttMethod);
        Assert.False(fixture.Platform.DiscoveredOnUiThread);
        Assert.False(fixture.Platform.Connection.IsOpen);
        if (expectedPort == "N/A") Assert.Null(Assert.Single(fixture.Store.Saved).PttPortName);
    }

    [AvaloniaFact]
    public async Task Refresh_preserves_selection_and_changes_persist_without_regenerating_preview()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.PrepareAsync();
        var preview = fixture.Composition.ViewModel.Preview;
        fixture.Window.DataContext = fixture.Composition.ViewModel;
        var portPicker = fixture.Window.FindControl<ComboBox>("PttPortComboBox")!;
        portPicker.SelectedItem = "COM4";
        fixture.Composition.ViewModel.SelectedPttMethod = PttControlMethod.Dtr;
        fixture.Platform.Ports = ["COM4", "COM7"];
        var savesBeforeRefresh = fixture.Store.Saved.Count;

        await fixture.Window.PttPortsRefreshRequested!();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("COM4", fixture.Composition.ViewModel.SelectedPttPort);
        Assert.Equal("COM4", portPicker.SelectedItem);
        Assert.Equal(new[] { "N/A", "COM4", "COM7" }, fixture.Composition.ViewModel.PttPorts);
        Assert.Same(preview, fixture.Composition.ViewModel.Preview);
        Assert.Equal(savesBeforeRefresh, fixture.Store.Saved.Count);
        Assert.Equal("COM4", fixture.Store.Saved[^1].PttPortName);
        Assert.Equal("DTR", fixture.Store.Saved[^1].PttMethod);
        fixture.Composition.ViewModel.SelectedPttPort = "N/A";
        Assert.Null(fixture.Store.Saved[^1].PttPortName);
        Assert.Equal("DTR", fixture.Store.Saved[^1].PttMethod);
    }

    [AvaloniaFact]
    public async Task Manual_press_auto_opens_and_release_and_open_close_update_ui_on_dispatcher()
    {
        await using var fixture = await Fixture.CreateAsync();
        var viewModel = fixture.Composition.ViewModel;
        viewModel.SelectedPttPort = "COM4";
        viewModel.SelectedPttMethod = PttControlMethod.Dtr;
        var offThreadChanges = new List<string>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is "IsPttOpen" or "IsPttAsserted" && !Dispatcher.UIThread.CheckAccess())
                offThreadChanges.Add(args.PropertyName);
        };

        await fixture.Window.ManualPttPressed!();
        Dispatcher.UIThread.RunJobs();
        Assert.True(fixture.Platform.Connection.Dtr);
        Assert.False(fixture.Platform.Connection.Rts);
        Assert.True(viewModel.IsPttAsserted);
        Assert.True(viewModel.IsPttOpen);
        await fixture.Window.ManualPttReleased!();
        Dispatcher.UIThread.RunJobs();
        Assert.False(viewModel.IsPttAsserted);
        Assert.True(viewModel.IsPttOpen);
        await fixture.Window.PttOpenCloseRequested!();
        Dispatcher.UIThread.RunJobs();
        Assert.False(viewModel.IsPttOpen);
        await fixture.Window.PttOpenCloseRequested!();
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.IsPttOpen);
        Assert.Empty(offThreadChanges);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pending_open_locks_bound_ptt_controls_and_preserves_saved_selection(bool fails)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Window.AllowExplicitClose = true;
        fixture.Window.DataContext = fixture.Composition.ViewModel;
        fixture.Window.Show();
        var viewModel = fixture.Composition.ViewModel;
        var port = fixture.Window.FindControl<ComboBox>("PttPortComboBox")!;
        var method = fixture.Window.GetLogicalDescendants().OfType<ComboBox>()
            .Single(picker => ReferenceEquals(picker.ItemsSource, viewModel.PttMethods));
        var toggle = fixture.Window.FindControl<Button>("PttConnectionButton")!;
        var ptt = fixture.Window.FindControl<Button>("PttButton")!;
        port.SelectedItem = "COM4";
        var saves = fixture.Store.Saved.Count;
        var openStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Platform.Connection.OnOpen = () =>
        {
            openStarted.SetResult();
            finishOpen.Task.GetAwaiter().GetResult();
            if (fails) throw new IOException("open failed");
        };

        var toggleConnection = fixture.Window.PttOpenCloseRequested!;
        Task opening = Task.CompletedTask;
        fixture.Window.PttOpenCloseRequested = () => opening = toggleConnection();
        try
        {
            Assert.True(port.Focus());
            Assert.True(method.Focus());
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(port.IsEffectivelyEnabled);
            Assert.False(method.IsEffectivelyEnabled);
            Assert.False(toggle.IsEffectivelyEnabled);
            Assert.False(ptt.IsEffectivelyEnabled);
            await openStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(port.Focus());
            Assert.False(method.Focus());
            viewModel.SelectedPttPort = "COM12";
            viewModel.SelectedPttMethod = PttControlMethod.Dtr;
            Assert.Equal("COM4", port.SelectedItem);
            Assert.Equal(PttControlMethod.Rts, method.SelectedItem);
            Assert.Equal(saves, fixture.Store.Saved.Count);
        }
        finally
        {
            finishOpen.TrySetResult();
            await opening.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(!fails, fixture.Platform.Connection.IsOpen);
        Assert.Equal(!fails, viewModel.IsPttOpen);
        Assert.Equal(fails ? "OPEN" : "CLOSE", toggle.Content);
        Assert.Equal(fails, port.IsEffectivelyEnabled);
        Assert.Equal(fails, method.IsEffectivelyEnabled);
        Assert.True(toggle.IsEffectivelyEnabled);
        Assert.True(ptt.IsEffectivelyEnabled);
        Assert.Equal("COM4", fixture.Platform.OpenedPort);
        Assert.Equal("COM4", port.SelectedItem);
        Assert.Equal(PttControlMethod.Rts, method.SelectedItem);
        Assert.Equal(saves, fixture.Store.Saved.Count);
        Assert.Equal("COM4", fixture.Store.Saved[^1].PttPortName);
        Assert.Equal("RTS", fixture.Store.Saved[^1].PttMethod);

        if (!fails) await fixture.Window.PttOpenCloseRequested!();
        Assert.True(port.IsEffectivelyEnabled);
        Assert.True(method.IsEffectivelyEnabled);
        port.SelectedItem = "COM12";
        method.SelectedItem = PttControlMethod.Dtr;
        Assert.Equal("COM12", fixture.Store.Saved[^1].PttPortName);
        Assert.Equal("DTR", fixture.Store.Saved[^1].PttMethod);
        fixture.Window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ptt_delegate_failures_are_presented_without_escaping(bool cancellation)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Composition.ViewModel.SelectedPttPort = "COM4";
        fixture.Platform.Error = cancellation ? new OperationCanceledException() : new IOException("unavailable");
        await fixture.Window.PttPortsRefreshRequested!();
        await fixture.Window.PttOpenCloseRequested!();
        await fixture.Window.ManualPttPressed!();
        await fixture.Window.ManualPttReleased!();
        Dispatcher.UIThread.RunJobs();
        if (!cancellation) Assert.Contains("unavailable", fixture.Composition.ViewModel.ValidationError);
    }

    [AvaloniaTheory]
    [InlineData("N/A")]
    [InlineData("COM4")]
    public async Task Send_keys_only_playback_and_releases_before_completed_deletion(string port)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.PrepareAsync();
        fixture.Composition.ViewModel.SelectedPttPort = port;
        fixture.Composition.ViewModel.SelectedPttMethod = PttControlMethod.Dtr;
        fixture.Composition.ViewModel.DeleteAfterSend = true;
        fixture.Artifacts.OnEncode = () => Assert.False(fixture.Platform.Connection.Dtr);
        fixture.Playback.OnPlay = _ =>
        {
            Assert.Equal(port != "N/A", fixture.Platform.Connection.Dtr);
            Assert.NotNull(fixture.Composition.Queue.Active);
            return Task.CompletedTask;
        };
        fixture.Platform.HoldGateDelays = false;

        await fixture.Window.SendRequested!();

        Assert.Equal(JobState.Completed, fixture.Transmission.State);
        Assert.Equal(1, fixture.Playback.Calls);
        Assert.Equal(port != "N/A", fixture.Platform.Connection.IsOpen);
        Assert.False(fixture.Platform.Connection.Dtr);
        Assert.Null(fixture.Composition.Queue.Active);
    }

    [AvaloniaFact]
    public async Task Stop_cancels_gate_lead_in_releases_ptt_and_preserves_job()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.PrepareAsync();
        fixture.Composition.ViewModel.SelectedPttPort = "COM4";
        fixture.Composition.ViewModel.DeleteAfterSend = true;
        var sending = fixture.Window.SendRequested!();
        await WaitUntilAsync(() => fixture.Platform.Connection.Rts);
        fixture.Window.StopRequested!();
        await sending.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(JobState.Stopped, fixture.Transmission.State);
        Assert.False(fixture.Platform.Connection.Rts);
        Assert.Equal(0, fixture.Playback.Calls);
        Assert.NotNull(fixture.Composition.Queue.Active);
    }

    [AvaloniaFact]
    public async Task Stop_cancels_active_playback_and_releases_ptt()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.PrepareAsync();
        fixture.Composition.ViewModel.SelectedPttPort = "COM4";
        fixture.Platform.HoldGateDelays = false;
        fixture.Playback.OnPlay = token => Task.Delay(Timeout.Infinite, token);
        var sending = fixture.Window.SendRequested!();
        await WaitUntilAsync(() => fixture.Playback.Calls == 1);
        fixture.Window.StopRequested!();
        await sending.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(JobState.Stopped, fixture.Transmission.State);
        Assert.False(fixture.Platform.Connection.Rts);
    }

    [AvaloniaFact]
    public async Task Stop_finishes_as_stopped_when_ptt_deassertion_stalls()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.PrepareAsync();
        var stop = BindStopButton(fixture);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Platform.HoldGateDelays = false;
        fixture.Playback.OnPlay = token =>
        {
            started.SetResult();
            return Task.Delay(Timeout.Infinite, token);
        };
        fixture.Composition.ViewModel.DeleteAfterSend = true;
        var sending = fixture.Window.SendRequested!();
        var releaseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = fixture.Platform.Connection;

        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            connection.OnRtsRelease = () =>
            {
                releaseStarted.TrySetResult();
                finishRelease.Task.GetAwaiter().GetResult();
            };
            Dispatcher.UIThread.RunJobs();
            Assert.True(stop.IsEffectivelyEnabled);
            ((IInvokeProvider)new ButtonAutomationPeer(stop)).Invoke();
            Dispatcher.UIThread.RunJobs();
            await releaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await sending.WaitAsync(TimeSpan.FromSeconds(4));

            Assert.Equal(JobState.Stopped, fixture.Transmission.State);
            Assert.NotNull(fixture.Composition.Queue.Active);
            Assert.False(fixture.Composition.ViewModel.IsPttOpen);
            Assert.False(fixture.Composition.ViewModel.IsPttAsserted);
            Assert.Contains("timed out", fixture.Composition.ViewModel.ValidationError);
            Assert.False(connection.Disposed);
        }
        finally
        {
            finishRelease.TrySetResult();
            await sending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await WaitUntilAsync(() => connection.Disposed);
        Assert.False(connection.Rts);
    }

    [AvaloniaFact]
    public async Task Send_open_failure_reports_error_and_does_not_delete_job()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.PrepareAsync();
        fixture.Composition.ViewModel.SelectedPttPort = "COM4";
        fixture.Composition.ViewModel.DeleteAfterSend = true;
        fixture.Platform.Error = new IOException("port denied");
        await fixture.Window.SendRequested!();
        Assert.Equal(JobState.Failed, fixture.Transmission.State);
        Assert.Contains("port denied", fixture.Composition.ViewModel.ValidationError);
        Assert.NotNull(fixture.Composition.Queue.Active);
        Assert.Equal(0, fixture.Playback.Calls);
    }

    [AvaloniaFact]
    public async Task Stop_button_cancels_pending_manual_open_before_ptt_can_assert()
    {
        await using var fixture = await Fixture.CreateAsync();
        var stop = BindStopButton(fixture);
        var openStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = fixture.Platform.Connection;
        connection.OnOpen = () =>
        {
            openStarted.SetResult();
            finishOpen.Task.GetAwaiter().GetResult();
        };
        var pressing = fixture.Window.ManualPttPressed!();
        try
        {
            await openStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(stop.IsEffectivelyEnabled);
            ((IInvokeProvider)new ButtonAutomationPeer(stop)).Invoke();
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            finishOpen.SetResult();
            await pressing.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.False(connection.EverAsserted);
        Assert.False(connection.IsOpen);
        Assert.False(stop.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public async Task Stop_button_releases_manual_ptt_and_disables_while_port_remains_open()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.PrepareAsync();
        var stop = BindStopButton(fixture);
        await fixture.Window.PttOpenCloseRequested!();
        Dispatcher.UIThread.RunJobs();
        Assert.True(fixture.Platform.Connection.IsOpen);
        Assert.False(stop.IsEffectivelyEnabled);

        await fixture.Window.ManualPttPressed!();
        Dispatcher.UIThread.RunJobs();
        Assert.True(fixture.Platform.Connection.Rts);
        Assert.True(stop.IsEffectivelyEnabled);

        ((IInvokeProvider)new ButtonAutomationPeer(stop)).Invoke();
        await WaitUntilAsync(() => !stop.IsEffectivelyEnabled);
        Assert.False(fixture.Platform.Connection.Rts);
        Assert.True(fixture.Platform.Connection.IsOpen);
        Assert.True(fixture.Composition.ViewModel.CanSend);
    }

    private static Button BindStopButton(Fixture fixture)
    {
        fixture.Window.DataContext = fixture.Composition.ViewModel;
        fixture.Composition.ViewModel.SelectedPttPort = "COM4";
        return fixture.Window.GetLogicalDescendants().OfType<Button>().Single(button => button.Classes.Contains("stop"));
    }

    [AvaloniaFact]
    public async Task Shutdown_releases_and_closes_before_waiting_for_preferences_and_rejects_new_operations()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Composition.ViewModel.SelectedPttPort = "COM4";
        await fixture.Window.ManualPttPressed!();
        fixture.Store.PendingSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposing = fixture.Composition.DisposeAsync().AsTask();
        var alsoDisposing = fixture.Composition.DisposeAsync().AsTask();
        try
        {
            await WaitUntilAsync(() => fixture.Platform.Connection.Disposed);
            Assert.False(fixture.Platform.Connection.Rts);
            Assert.False(fixture.Platform.Connection.IsOpen);
            Assert.False(disposing.IsCompleted);
            Assert.False(alsoDisposing.IsCompleted);
            await fixture.Window.ManualPttPressed!();
            await fixture.Window.PttOpenCloseRequested!();
            Assert.False(fixture.Platform.Connection.IsOpen);
        }
        finally { fixture.Store.PendingSave.SetResult(); await Task.WhenAll(disposing, alsoDisposing); }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public MainWindow Window { get; } = new();
        public FakePlatform Platform { get; } = new();
        public FakeStore Store { get; } = new();
        public FakeArtifacts Artifacts { get; } = new();
        public FakePlayback Playback { get; } = new();
        public TransmissionController Transmission { get; private set; } = null!;
        public DesktopComposition Composition { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync(UserPreferences? preferences = null)
        {
            var fixture = new Fixture();
            fixture.Store.Preferences = preferences ?? UserPreferences.Default with { CallSign = "TEST" };
            fixture.Transmission = new(fixture.Artifacts, fixture.Playback);
            fixture.Composition = await DesktopComposition.CreateAsync(fixture.Window,
                new ClassicDesktopStyleApplicationLifetime(), TestContext.Current.CancellationToken,
                fixture.Store, new FakeAudio(), fixture.Transmission, fixture.Platform, new DesktopHost([]));
            return fixture;
        }

        public async Task PrepareAsync()
        {
            Composition.Queue.Enqueue(new(Guid.NewGuid(), "test", DateTimeOffset.UtcNow,
                DecodedPage.Create(1, 1, [1, 2, 3], new PwgPageMetadata(1, 1, 1, 1, 300, 300, 0, 1, 1))));
            await WaitUntilAsync(() => Composition.ViewModel.IsReady);
        }

        public ValueTask DisposeAsync() => Composition.DisposeAsync();
    }

    private sealed class FakeStore : IUserPreferencesStore
    {
        public UserPreferences Preferences { get; set; } = UserPreferences.Default;
        public List<UserPreferences> Saved { get; } = [];
        public TaskCompletionSource? PendingSave { get; set; }
        public Task<UserPreferences> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Preferences);
        public Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
        { Saved.Add(preferences); return PendingSave?.Task ?? Task.CompletedTask; }
    }

    private sealed class FakeAudio : IAudioEndpointMonitor
    {
        public ValueTask<IReadOnlyList<AudioEndpointInfo>> GetEndpointsAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<AudioEndpointInfo>>([new("test", "Test", true, 50, false)]);
        public IAsyncEnumerable<IReadOnlyList<AudioEndpointInfo>> WatchAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakePlatform : IPttPlatform
    {
        public string[] Ports { get; set; } = ["COM12", "COM4"];
        public FakeConnection Connection { get; } = new();
        public string? OpenedPort { get; private set; }
        public Exception? Error { get; set; }
        public bool DiscoveredOnUiThread { get; private set; }
        public bool HoldGateDelays { get; set; } = true;
        public string[] GetPortNames()
        {
            DiscoveredOnUiThread |= Dispatcher.UIThread.CheckAccess();
            if (Error is not null) throw Error;
            return Ports;
        }
        public IPttSerialConnection CreateConnection(string portName)
        {
            if (Error is not null) throw Error;
            OpenedPort = portName;
            return Connection;
        }
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.Delay(HoldGateDelays ? Timeout.Infinite : 10, cancellationToken);
    }

    private sealed class FakeConnection : IPttSerialConnection
    {
        public bool IsOpen { get; private set; }
        public bool Rts { get; private set; }
        public bool Dtr { get; private set; }
        public bool Disposed { get; private set; }
        public bool EverAsserted { get; private set; }
        public Action? OnOpen { get; set; }
        public Action? OnRtsRelease { get; set; }
        public void Open()
        {
            OnOpen?.Invoke();
            IsOpen = true;
        }
        public void Close() => IsOpen = false;
        public void SetRts(bool value)
        {
            if (!value && Rts) OnRtsRelease?.Invoke();
            Rts = value;
            EverAsserted |= value;
        }
        public void SetDtr(bool value) => Dtr = value;
        public void Dispose() => Disposed = true;
    }

    private sealed class FakeArtifacts : ITransmissionArtifactService
    {
        public Action? OnEncode { get; set; }
        public Task<string> CreateTemporaryAsync(PreparedTransmission prepared, IProgress<double>? progress, CancellationToken cancellationToken)
        { OnEncode?.Invoke(); return Task.FromResult(Path.Combine(Path.GetTempPath(), $"ptt-test-{Guid.NewGuid():N}.wav")); }
        public Task SaveAsync(PreparedTransmission prepared, string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakePlayback : IWavePlaybackService
    {
        public int Calls { get; private set; }
        public Func<CancellationToken, Task>? OnPlay { get; set; }
        public Task PlayAsync(string path, string endpointId, IProgress<double>? progress, CancellationToken cancellationToken)
        { Calls++; return OnPlay?.Invoke(cancellationToken) ?? Task.CompletedTask; }
    }
}

using SstvVPrinter.App.Platform;
using SstvVPrinter.Desktop.ViewModels;

namespace SstvVPrinter.Desktop.Tests;

public sealed class ApplicationMenuViewModelTests
{
    [Fact]
    public async Task Startup_is_enabled_by_default_and_toggle_persists()
    {
        var startup = new FakeStartup(enabled: true);
        var viewModel = Create(startup: startup);

        await viewModel.InitializeAsync(TestContext.Current.CancellationToken);
        await viewModel.SetStartAtSignInAsync(false, TestContext.Current.CancellationToken);

        Assert.False(viewModel.StartAtSignIn);
        Assert.Equal([false], startup.SavedValues);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task Explicit_exit_warns_only_when_jobs_are_pending(bool hasPendingJobs, int expectedConfirmations)
    {
        var lifetime = new FakeLifetime { ConfirmationResult = true };
        var viewModel = Create(lifetime: lifetime, hasPendingJobs: () => hasPendingJobs);

        await viewModel.ExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expectedConfirmations, lifetime.ConfirmationCount);
        Assert.Equal(1, lifetime.ExitCount);
    }

    [Fact]
    public async Task Explicit_exit_awaits_application_cleanup()
    {
        var lifetime = new FakeLifetime { HoldExit = true };
        var viewModel = Create(lifetime: lifetime);

        var exit = viewModel.ExitAsync(TestContext.Current.CancellationToken);

        Assert.False(exit.IsCompleted);
        Assert.Equal(1, lifetime.ExitCount);
        lifetime.CompleteExit();
        await exit;
    }

    [Fact]
    public async Task Foreground_denial_flashes_and_notifies()
    {
        var attention = new FakeAttention { ForegroundResult = false };
        var viewModel = Create(attention: attention);

        await viewModel.ActivateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, attention.FlashCount);
        Assert.Equal(1, attention.NotificationCount);
    }

    private static ApplicationMenuViewModel Create(
        FakeLifetime? lifetime = null,
        FakeStartup? startup = null,
        FakeAttention? attention = null,
        Func<bool>? hasPendingJobs = null) =>
        new(
            lifetime ?? new FakeLifetime(),
            startup ?? new FakeStartup(true),
            attention ?? new FakeAttention(),
            new FakePrinter(),
            hasPendingJobs ?? (() => false));

    private sealed class FakeLifetime : IApplicationLifetimeService
    {
        public bool ConfirmationResult { get; init; } = true;
        public int ConfirmationCount { get; private set; }
        public int ExitCount { get; private set; }
        public bool HoldExit { get; init; }
        private readonly TaskCompletionSource _exitCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void HideMainWindow() { }
        public void ShowMainWindow() { }
        public Task<bool> ConfirmPendingExitAsync(CancellationToken cancellationToken)
        {
            ConfirmationCount++;
            return Task.FromResult(ConfirmationResult);
        }
        public Task ExitAsync(CancellationToken cancellationToken)
        {
            ExitCount++;
            return HoldExit ? _exitCompletion.Task.WaitAsync(cancellationToken) : Task.CompletedTask;
        }
        public void CompleteExit() => _exitCompletion.TrySetResult();
    }

    private sealed class FakeStartup(bool enabled) : IStartupRegistrationService
    {
        public List<bool> SavedValues { get; } = [];
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) => Task.FromResult(enabled);
        public Task SetEnabledAsync(bool value, CancellationToken cancellationToken)
        {
            SavedValues.Add(value);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAttention : IUserAttentionService
    {
        public bool ForegroundResult { get; init; } = true;
        public int FlashCount { get; private set; }
        public int NotificationCount { get; private set; }
        public bool TryBringToForeground() => ForegroundResult;
        public void FlashTaskbar() => FlashCount++;
        public void ShowNotification(string title, string message) => NotificationCount++;
    }

    private sealed class FakePrinter : IPrinterRegistrationService
    {
        public Task<bool> VerifyAsync(CancellationToken cancellationToken) => Task.FromResult(true);
        public Task RegisterAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RepairAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

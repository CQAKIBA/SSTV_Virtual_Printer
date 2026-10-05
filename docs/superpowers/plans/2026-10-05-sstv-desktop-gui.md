# SSTV Virtual Printer Desktop GUI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the approved single-window Avalonia transmitter UI, connect it to loopback IPP intake and the existing imaging/SSTV libraries, and provide explicit WAV-save and Windows audio-send workflows.

**Architecture:** Add a platform-neutral `SstvVPrinter.App` project for queue state, preferences, composition, and transmission orchestration; keep Avalonia views in `SstvVPrinter.Desktop`; put Windows-only startup, foreground, printer, endpoint-volume, tray, notification, and playback adapters under `Platforms/Windows`. Refactor the IPP probe only enough to publish accepted PWG streams to an injected sink while retaining the probe's file-capture behavior.

**Tech Stack:** C# 14, .NET 10, Avalonia 12.1.3, Avalonia.Headless.XUnit 12.1.3, CommunityToolkit.Mvvm 8.4.2, NAudio 3.1.0, xUnit 2.9.3

**Spec:** `docs/superpowers/specs/2026-10-03-sstv-desktop-gui-design.md`

## Global Constraints

- Initial runtime targets are Windows 10 and Windows 11; Linux/Raspberry Pi portability applies to application state and view models, not Windows adapters.
- Bind the IPP endpoint only to IPv4 and IPv6 loopback on port 8631.
- Never transmit automatically after print intake; require an explicit Send command for every job.
- Keep one primary window and one fixed vivid high-contrast theme; do not add theme selection.
- Persist only call sign, mode, 1x/2x, selected audio endpoint, and start-at-sign-in; do not restore queued print jobs.
- Read Windows endpoint volume and mute state but never modify either value.
- Process only the first PWG page and keep the queue FIFO; do not add queue reordering.
- Continue treating warnings as errors and keep all long-running work cancellable and off the UI thread.
- Start execution from a new worktree based on `codex/sstv-encoder-core`, then bring in documentation commits `d5922ef` and the plan commit before editing product code.

## Review Focus

- A second application launch must activate the existing instance instead of failing on port 8631 or starting a second queue; pin this in Task 8.
- A print job arriving during encoding or playback must remain queued without replacing the active preview; pin this in Task 2.
- A remembered audio endpoint that disappeared must fall back safely and present a blocking device error when no usable endpoint exists; pin this in Task 6.
- Cancellation or device loss must never mark a partial transmission completed or leave a temporary WAV file behind; pin this in Task 7.
- Unknown volume state and a muted endpoint must remain distinct, visible states without changing Windows audio settings; pin this in Task 6.

---

### Task 1: Solution and Avalonia Application Shell

**Files:**
- Create: `src/SstvVPrinter.App/SstvVPrinter.App.csproj`
- Create: `src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj`
- Create: `src/SstvVPrinter.Desktop/Program.cs`
- Create: `src/SstvVPrinter.Desktop/App.axaml`
- Create: `src/SstvVPrinter.Desktop/App.axaml.cs`
- Create: `src/SstvVPrinter.Desktop/Styles/Palette.axaml`
- Create: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml`
- Create: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml.cs`
- Create: `tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj`
- Create: `tests/SstvVPrinter.Desktop.Tests/AppSmokeTests.cs`
- Modify: `SstvVPrinter.slnx`

**Interfaces:**
- Consumes: existing `SstvVPrinter.Imaging` and `SstvVPrinter.Sstv` projects.
- Produces: `App.BuildAvaloniaApp()`, an instantiable `MainWindow`, and named palette resources used by later views.

- [ ] **Step 1: Write the failing headless smoke test**

Add `AppSmokeTests.Main_window_loads_with_fixed_dark_palette`, asserting that `MainWindow` loads under Avalonia headless, has exactly one root layout, and resolves `AppBackgroundBrush`, `ReadyBrush`, `WarningBrush`, `TransmitBrush`, `CyanActionBrush`, and `SelectionBrush`.

- [ ] **Step 2: Run the smoke test and verify it fails**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj --filter FullyQualifiedName~AppSmokeTests`

Expected: FAIL because the projects and application types do not exist.

- [ ] **Step 3: Create the two projects and minimal window**

Pin `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, and `Avalonia.Fonts.Inter` to `12.1.3`; pin `CommunityToolkit.Mvvm` to `8.4.2`; use `net10.0`. Pin the test project to `Avalonia.Headless.XUnit` `12.1.3` and the repository's existing xUnit versions. Define the six named brushes in `Palette.axaml`, use dark mode unconditionally, and add all projects to `SstvVPrinter.slnx`.

- [ ] **Step 4: Run the smoke test and full solution tests**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj`

Expected: PASS.

Run: `dotnet test SstvVPrinter.slnx`

Expected: all existing and new tests PASS with zero warnings.

- [ ] **Step 5: Commit**

```bash
git add SstvVPrinter.slnx src/SstvVPrinter.App src/SstvVPrinter.Desktop tests/SstvVPrinter.Desktop.Tests
git commit -m "feat: add Avalonia desktop shell"
```

### Task 2: In-Memory FIFO Job Queue

**Files:**
- Create: `src/SstvVPrinter.App/Jobs/PrintJob.cs`
- Create: `src/SstvVPrinter.App/Jobs/PrintJobQueue.cs`
- Create: `src/SstvVPrinter.App/Jobs/JobState.cs`
- Create: `tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj`
- Create: `tests/SstvVPrinter.App.Tests/Jobs/PrintJobQueueTests.cs`
- Modify: `SstvVPrinter.slnx`

**Interfaces:**
- Consumes: `SstvVPrinter.Imaging.Model.DecodedPage`.
- Produces: `PrintJob(Guid Id, string? SourceName, DateTimeOffset AcceptedAt, DecodedPage Page)`, `PrintJobQueue.Enqueue(PrintJob)`, `Active`, `Waiting`, `Select(Guid)`, `CompleteActive()`, `Discard(Guid)`, and `Changed`.

- [ ] **Step 1: Write failing queue tests**

Cover `first_enqueue_becomes_active`, `later_jobs_wait_in_fifo_order`, `complete_and_discard_advance_to_next_job`, `select_waiting_job_changes_inspection_without_reordering`, and `arrival_during_transmitting_does_not_replace_active_or_preview`. The final test sets the active state to `Transmitting`, enqueues another job, and asserts the active ID and state are unchanged while waiting count increases.

- [ ] **Step 2: Run the queue tests and verify they fail**

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj --filter FullyQualifiedName~PrintJobQueueTests`

Expected: FAIL because the queue types do not exist.

- [ ] **Step 3: Implement the queue and state transitions**

Use a lock-protected ordered collection. Define `JobState` values `Received`, `Preparing`, `Ready`, `Encoding`, `Transmitting`, `Stopped`, `Completed`, and `Failed`. Raise one immutable queue snapshot through `Changed`; do not expose the mutable backing collection.

- [ ] **Step 4: Run the queue tests**

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj --filter FullyQualifiedName~PrintJobQueueTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add SstvVPrinter.slnx src/SstvVPrinter.App/Jobs tests/SstvVPrinter.App.Tests
git commit -m "feat: add in-memory print job queue"
```

### Task 3: Preferences and Call-Sign Validation

**Files:**
- Create: `src/SstvVPrinter.App/Settings/UserPreferences.cs`
- Create: `src/SstvVPrinter.App/Settings/IUserPreferencesStore.cs`
- Create: `src/SstvVPrinter.App/Settings/JsonUserPreferencesStore.cs`
- Create: `src/SstvVPrinter.App/Validation/CallSignValidator.cs`
- Create: `tests/SstvVPrinter.App.Tests/Settings/UserPreferencesTests.cs`
- Create: `tests/SstvVPrinter.App.Tests/Validation/CallSignValidatorTests.cs`

**Interfaces:**
- Consumes: `SstvMode`.
- Produces: `UserPreferences(string CallSign, SstvMode Mode, int HorizontalSampleMultiplier, string? AudioEndpointId, bool StartAtSignIn)`, `IUserPreferencesStore.LoadAsync`, `SaveAsync`, and `CallSignValidator.Validate(string?)`.

- [ ] **Step 1: Write failing validation and persistence tests**

Assert uppercase normalization and the same supported character set as `CompositionRequest`; reject blank or unsupported values. Assert missing/corrupt JSON returns defaults (`Scottie2`, `1`, null endpoint, start-at-sign-in true), saves atomically, and never contains print-job content.

- [ ] **Step 2: Run the settings tests and verify they fail**

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj --filter "FullyQualifiedName~UserPreferencesTests|FullyQualifiedName~CallSignValidatorTests"`

Expected: FAIL because the settings types do not exist.

- [ ] **Step 3: Implement settings and validation**

Store JSON beneath `%LocalAppData%/SstvVPrinter/settings.json` through an injected base directory in tests. Write to a sibling temporary file and atomically replace the destination. Keep mode and multiplier validation identical to core constructors.

- [ ] **Step 4: Run the settings tests**

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj --filter "FullyQualifiedName~UserPreferencesTests|FullyQualifiedName~CallSignValidatorTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SstvVPrinter.App/Settings src/SstvVPrinter.App/Validation tests/SstvVPrinter.App.Tests
git commit -m "feat: persist transmitter preferences"
```

### Task 4: IPP Intake Sink and Desktop Job Admission

**Files:**
- Create: `src/SstvVPrinter.IppProbe/Capture/IPrintJobSink.cs`
- Create: `src/SstvVPrinter.IppProbe/Capture/FilePrintJobSink.cs`
- Create: `src/SstvVPrinter.App/Jobs/PwgPrintJobSink.cs`
- Modify: `src/SstvVPrinter.IppProbe/Hosting/IppEndpoint.cs`
- Modify: `src/SstvVPrinter.IppProbe/Hosting/ProbeApplication.cs`
- Modify: `src/SstvVPrinter.IppProbe/SstvVPrinter.IppProbe.csproj`
- Modify: `src/SstvVPrinter.App/SstvVPrinter.App.csproj`
- Modify: `tests/SstvVPrinter.IppProbe.Tests/IppEndpointTests.cs`
- Create: `tests/SstvVPrinter.App.Tests/Jobs/PwgPrintJobSinkTests.cs`

**Interfaces:**
- Consumes: `ProbeJob`, the remaining IPP document stream, `PwgRasterDecoder.DecodeFirstPageAsync`, and `PrintJobQueue.Enqueue`.
- Produces: `IPrintJobSink.AcceptAsync(ProbeJob job, Stream document, string documentFormat, CancellationToken)` and `PwgPrintJobSink` that adds one decoded first page to the queue.

- [ ] **Step 1: Write failing IPP sink tests**

Add probe tests proving `Print-Job` invokes an injected sink exactly once and returns a job ID, while sink rejection returns the existing bounded failure status. Add application tests proving valid PWG becomes one queue job and malformed PWG does not enqueue a partial job.

- [ ] **Step 2: Run the focused tests and verify they fail**

Run: `dotnet test tests/SstvVPrinter.IppProbe.Tests/SstvVPrinter.IppProbe.Tests.csproj --filter FullyQualifiedName~IppEndpointTests`

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj --filter FullyQualifiedName~PwgPrintJobSinkTests`

Expected: FAIL because `IPrintJobSink` and the injectable endpoint do not exist.

- [ ] **Step 3: Extract the sink without changing protocol behavior**

Make `FilePrintJobSink` wrap the current `PrintJobCapture` plus inspection behavior so the command-line probe and all existing tests remain valid. Add the necessary Imaging project reference to App and the IppProbe project reference only for the sink contract until a later packaging refactor is justified.

- [ ] **Step 4: Run IPP, imaging, and application tests**

Run: `dotnet test tests/SstvVPrinter.IppProbe.Tests/SstvVPrinter.IppProbe.Tests.csproj`

Run: `dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj`

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj`

Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SstvVPrinter.IppProbe src/SstvVPrinter.App tests/SstvVPrinter.IppProbe.Tests tests/SstvVPrinter.App.Tests
git commit -m "feat: publish IPP jobs to application queue"
```

### Task 5: Composition, Preview, and Duration State

**Files:**
- Modify: `src/SstvVPrinter.Sstv/RgbFrame.cs`
- Create: `src/SstvVPrinter.App/Transmission/TransmissionSettings.cs`
- Create: `src/SstvVPrinter.App/Transmission/PreparedTransmission.cs`
- Create: `src/SstvVPrinter.App/Transmission/TransmissionPreparationService.cs`
- Create: `src/SstvVPrinter.Desktop/Imaging/AvaloniaFrameBitmapFactory.cs`
- Create: `src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `tests/SstvVPrinter.Sstv.Tests/RgbFrameTests.cs`
- Create: `tests/SstvVPrinter.App.Tests/Transmission/TransmissionPreparationServiceTests.cs`
- Create: `tests/SstvVPrinter.Desktop.Tests/MainWindowViewModelTests.cs`

**Interfaces:**
- Consumes: `SstvFrameComposer.Compose`, `CompositionRequest`, `SstvEncodeOptions`, active queue job, and persisted preferences.
- Produces: `RgbFrame.CopyPixelsTo(Span<byte>)`, `TransmissionPreparationService.PrepareAsync(PrintJob, TransmissionSettings, CancellationToken)`, `PreparedTransmission(Frame, Options, EstimatedDuration)`, and preview/status properties on `MainWindowViewModel`.

- [ ] **Step 1: Write failing frame-copy, preparation, and view-model tests**

Assert copied RGB bytes are defensive and exact. Assert Scottie 2 1x and 2x preparations produce `320x256` and `640x256`; Robot 36 produces `320x240`; invalid call signs never produce Ready. Assert changing mode or multiplier cancels stale preparation and only the newest result becomes Preview.

- [ ] **Step 2: Run the focused tests and verify they fail**

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj --filter FullyQualifiedName~RgbFrameTests`

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj --filter FullyQualifiedName~TransmissionPreparationServiceTests`

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj --filter FullyQualifiedName~MainWindowViewModelTests`

Expected: FAIL on the missing APIs.

- [ ] **Step 3: Implement preparation and preview conversion**

Use `AcceptedAt.ToOffset(TimeSpan.FromHours(9))` for the header time. Compute estimated duration from deterministic planned sample counts in the SSTV layer, not from UI timers. Convert RGB24 to Avalonia `WriteableBitmap` BGRA8888 in the desktop adapter.

- [ ] **Step 4: Run the focused tests**

Run the three commands from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SstvVPrinter.Sstv/RgbFrame.cs src/SstvVPrinter.App/Transmission src/SstvVPrinter.Desktop/Imaging src/SstvVPrinter.Desktop/ViewModels tests
git commit -m "feat: prepare SSTV previews for desktop UI"
```

### Task 6: Read-Only Windows Audio Endpoint State

**Files:**
- Create: `src/SstvVPrinter.App/Audio/AudioEndpointInfo.cs`
- Create: `src/SstvVPrinter.App/Audio/IAudioEndpointMonitor.cs`
- Create: `src/SstvVPrinter.Desktop/Platforms/Windows/WindowsAudioEndpointMonitor.cs`
- Create: `src/SstvVPrinter.Desktop/ViewModels/AudioEndpointViewModel.cs`
- Modify: `src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj`
- Create: `tests/SstvVPrinter.Desktop.Tests/AudioEndpointViewModelTests.cs`

**Interfaces:**
- Consumes: NAudio Core Audio endpoint enumeration and notifications.
- Produces: `AudioEndpointInfo(string Id, string Name, bool IsAvailable, float? VolumePercent, bool? IsMuted)`, `IAudioEndpointMonitor.GetEndpointsAsync`, `WatchAsync`, and read-only endpoint presentation state.

- [ ] **Step 1: Write failing endpoint-state tests**

Cover normal `38%`, muted, unknown volume, disconnected remembered endpoint with fallback to the default render endpoint, and no usable endpoint. Assert no interface method exists to set volume or mute. The no-endpoint case must disable Send; unknown volume must display `不明`; muted must expose the vivid warning while retaining the real numeric value if available.

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj --filter FullyQualifiedName~AudioEndpointViewModelTests`

Expected: FAIL because endpoint types do not exist.

- [ ] **Step 3: Implement the monitor and presentation state**

Pin `NAudio` to `3.1.0`. Use `MMDeviceEnumerator` only inside the Windows adapter. Marshal endpoint notifications into immutable snapshots and let the view model dispatch them to the Avalonia UI thread.

- [ ] **Step 4: Run the endpoint tests**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj --filter FullyQualifiedName~AudioEndpointViewModelTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SstvVPrinter.App/Audio src/SstvVPrinter.Desktop tests/SstvVPrinter.Desktop.Tests
git commit -m "feat: show Windows audio endpoint state"
```

### Task 7: WAV Generation, Playback, Stop, and Cleanup

**Files:**
- Create: `src/SstvVPrinter.App/Transmission/IWavePlaybackService.cs`
- Create: `src/SstvVPrinter.App/Transmission/TransmissionArtifactService.cs`
- Create: `src/SstvVPrinter.App/Transmission/TransmissionController.cs`
- Create: `src/SstvVPrinter.Desktop/Platforms/Windows/NaudioWavePlaybackService.cs`
- Modify: `src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs`
- Create: `tests/SstvVPrinter.App.Tests/Transmission/TransmissionArtifactServiceTests.cs`
- Create: `tests/SstvVPrinter.App.Tests/Transmission/TransmissionControllerTests.cs`

**Interfaces:**
- Consumes: `PreparedTransmission`, `SstvEncoder.EncodeAsync`, `WaveFileWriter.WriteAsync`, and selected endpoint ID.
- Produces: `TransmissionArtifactService.SaveAsync(PreparedTransmission, string, CancellationToken)`, `CreateTemporaryAsync`, `IWavePlaybackService.PlayAsync(path, endpointId, progress, cancellationToken)`, and `TransmissionController.SendAsync`, `SaveWaveAsync`, `Stop`.

- [ ] **Step 1: Write failing generation and controller tests**

Assert Save writes a valid WAV without invoking playback; Send transitions `Encoding -> Transmitting -> Completed`; Stop is idempotent during encoding and playback; device loss yields `Failed` and never `Completed`; cancellation and failures delete temporary WAV files; progress is monotonic and bounded from 0 to 1.

- [ ] **Step 2: Run the transmission tests and verify they fail**

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj --filter "FullyQualifiedName~TransmissionArtifactServiceTests|FullyQualifiedName~TransmissionControllerTests"`

Expected: FAIL because the orchestration types do not exist.

- [ ] **Step 3: Implement generation and NAudio playback**

Generate a temporary WAV first, then play that stable artifact through the chosen Windows endpoint. Report progress from generated sample count during encoding and playback position during send. Never overwrite an existing user-selected WAV destination.

- [ ] **Step 4: Run transmission and SSTV tests**

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj --filter "FullyQualifiedName~TransmissionArtifactServiceTests|FullyQualifiedName~TransmissionControllerTests"`

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SstvVPrinter.App/Transmission src/SstvVPrinter.Desktop/Platforms/Windows src/SstvVPrinter.Desktop/ViewModels tests/SstvVPrinter.App.Tests
git commit -m "feat: add WAV save and audio transmission workflow"
```

### Task 8: Windows Lifetime, Single Instance, Startup, Notifications, and Printer Menu

**Files:**
- Create: `src/SstvVPrinter.App/Platform/IApplicationLifetimeService.cs`
- Create: `src/SstvVPrinter.App/Platform/IStartupRegistrationService.cs`
- Create: `src/SstvVPrinter.App/Platform/IUserAttentionService.cs`
- Create: `src/SstvVPrinter.App/Platform/IPrinterRegistrationService.cs`
- Create: `src/SstvVPrinter.Desktop/Platforms/Windows/WindowsSingleInstanceCoordinator.cs`
- Create: `src/SstvVPrinter.Desktop/Platforms/Windows/WindowsStartupRegistrationService.cs`
- Create: `src/SstvVPrinter.Desktop/Platforms/Windows/WindowsUserAttentionService.cs`
- Create: `src/SstvVPrinter.Desktop/Platforms/Windows/WindowsPrinterRegistrationService.cs`
- Create: `src/SstvVPrinter.Desktop/ViewModels/ApplicationMenuViewModel.cs`
- Modify: `src/SstvVPrinter.Desktop/App.axaml.cs`
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml.cs`
- Create: `tests/SstvVPrinter.Desktop.Tests/WindowsLifetimeTests.cs`
- Create: `tests/SstvVPrinter.Desktop.Tests/ApplicationMenuViewModelTests.cs`

**Interfaces:**
- Consumes: Avalonia `TrayIcon`, Windows per-user startup registration, foreground APIs, and PowerShell printer cmdlets behind a process-runner abstraction.
- Produces: one running instance, Close-to-tray behavior, explicit Exit with pending-job confirmation, start-at-sign-in toggle, foreground request with taskbar/notification fallback, and verify/register/repair printer commands.

- [ ] **Step 1: Write failing lifetime and menu tests**

Assert a second launch signals and activates the existing process without starting IPP; title-bar Close hides while the host remains running; explicit Exit warns only when jobs are pending; default startup is enabled and toggle persists; foreground denial triggers flash plus notification; printer commands always use exactly `ipp://localhost:8631/ipp/print` and the configured display name.

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj --filter "FullyQualifiedName~WindowsLifetimeTests|FullyQualifiedName~ApplicationMenuViewModelTests"`

Expected: FAIL because platform services do not exist.

- [ ] **Step 3: Implement Windows adapters behind interfaces**

Use a named mutex plus named pipe for single-instance activation. Use a per-user startup registration that does not require elevation. Keep PowerShell invocation arguments structured through an injected runner; do not concatenate user input into command text. Start the embedded IPP host only after primary-instance acquisition.

- [ ] **Step 4: Run the lifetime tests**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj --filter "FullyQualifiedName~WindowsLifetimeTests|FullyQualifiedName~ApplicationMenuViewModelTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SstvVPrinter.App/Platform src/SstvVPrinter.Desktop tests/SstvVPrinter.Desktop.Tests
git commit -m "feat: integrate Windows desktop lifetime"
```

### Task 9: Approved Main Window and End-to-End Host Wiring

**Files:**
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml`
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml.cs`
- Modify: `src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/SstvVPrinter.Desktop/App.axaml.cs`
- Create: `src/SstvVPrinter.Desktop/Hosting/DesktopHost.cs`
- Create: `tests/SstvVPrinter.Desktop.Tests/MainWindowLayoutTests.cs`
- Create: `tests/SstvVPrinter.Desktop.Tests/DesktopHostTests.cs`
- Create: `docs/validation/desktop-gui-runbook.md`

**Interfaces:**
- Consumes: queue, preferences, preparation, endpoint monitor, transmission controller, embedded IPP app, and all Windows adapters from earlier tasks.
- Produces: the approved preview-priority application and a composition root that starts/stops every owned service exactly once.

- [ ] **Step 1: Write failing layout and host integration tests**

Assert the right panel's accessible group order is Call sign, Hardware, Protocol, Status, Actions; preview and FIFO list exist on the left; Send/WAV/Discard/Stop enablement follows state; transmitting locks settings and exposes red/orange status with Stop and remaining time; muted shows the warning near Send; a received fake PWG job reaches the queue and prepares a preview through the host.

- [ ] **Step 2: Run the UI integration tests and verify they fail**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj --filter "FullyQualifiedName~MainWindowLayoutTests|FullyQualifiedName~DesktopHostTests"`

Expected: FAIL because final bindings and host wiring are absent.

- [ ] **Step 3: Implement the approved window and composition root**

Use a two-column responsive grid: large neutral-surround preview plus active/waiting jobs on the left, then the five approved control groups on the right. At narrow widths stack the right panel below the preview without hiding any primary action. Wire menu commands, tray commands, foreground activation, IPP intake, settings save, preparation cancellation, WAV save dialog, send, stop, and discard.

- [ ] **Step 4: Add the Windows manual-validation runbook**

Document Windows 10/11 launch, automatic startup toggle, printer verify/repair, print while visible/hidden/transmitting, mode and 1x/2x preview, WAV decode, real endpoint volume/mute observation, Send/Stop/device-loss behavior, tray Close/reopen/exit, DPI scaling, and keyboard navigation.

- [ ] **Step 5: Run full automated verification**

Run: `dotnet test SstvVPrinter.slnx`

Expected: every project PASS with zero warnings.

Run: `dotnet build SstvVPrinter.slnx -c Release`

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 6: Perform the manual smoke test on Windows 11**

Follow `docs/validation/desktop-gui-runbook.md` through one Paint/Explorer print, one Acrobat landscape print, one WAV save decoded by the known smartphone SSTV decoder, one real audio Send/Stop, mute warning observation, and tray lifecycle. Record results in the runbook. Defer the full Windows 10 pass until the milestone is stable, as previously agreed.

- [ ] **Step 7: Commit**

```bash
git add src/SstvVPrinter.Desktop tests/SstvVPrinter.Desktop.Tests docs/validation/desktop-gui-runbook.md
git commit -m "feat: complete SSTV transmitter desktop UI"
```

### Task 10: Final Review and Documentation Reconciliation

**Files:**
- Modify: `docs/superpowers/specs/2026-10-02-sstv-virtual-printer-design.md`
- Modify: `docs/validation/desktop-gui-runbook.md`

**Interfaces:**
- Consumes: completed GUI behavior and the approved 2026-10-03 GUI spec.
- Produces: one non-contradictory product description and recorded verification evidence.

- [ ] **Step 1: Reconcile the older product design**

Update only superseded sections: remove optional comment, app-controlled output level, test tone, and recoverable pending queue; add WAV save, read-only volume/mute, Avalonia, Close-to-tray, explicit exit warning, and non-persistent FIFO behavior. Preserve all unrelated IPP, image, and encoder design decisions.

- [ ] **Step 2: Run final verification from a clean build**

Run: `dotnet clean SstvVPrinter.slnx -c Release`

Run: `dotnet test SstvVPrinter.slnx -c Release`

Run: `dotnet build SstvVPrinter.slnx -c Release`

Expected: all tests PASS and build reports zero warnings and zero errors.

- [ ] **Step 3: Review repository state**

Run: `git status --short`

Expected: only the intended documentation updates are present.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-10-02-sstv-virtual-printer-design.md docs/validation/desktop-gui-runbook.md
git commit -m "docs: reconcile desktop transmitter behavior"
```

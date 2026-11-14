# Serial PTT Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add optional COM-port RTS/DTR PTT control with manual testing, automatic one-second transmit lead/tail, saved settings, and safe USB-disconnect recovery.

**Architecture:** Keep serial hardware ownership in the Windows Desktop project behind an injected controller and serial adapter. Add a platform-neutral transmission-gate interface to the App project so `TransmissionController` can generate the WAV first and then let the Desktop PTT gate wrap only audio playback. Bind one view-model state snapshot to the HARDWARE controls and route every manual, automatic, stop, fault, and shutdown path through the same controller.

**Tech Stack:** C# 14, .NET 10, Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2, `System.IO.Ports` 10.0.0, xUnit, Avalonia.Headless.XUnit.

**Spec:** `docs/superpowers/specs/2026-10-24-ptt-control-design.md`

## Global Constraints

- Windows Desktop owns all COM-port and RTS/DTR implementation details; the App layer sees only a platform-neutral transmission gate.
- `N/A` preserves the existing audio-only path and adds no one-second delays.
- Automatic PTT ordering is OPEN if needed, assert, one-second lead, audio playback, one-second tail, release; a healthy port stays open.
- STOP, application shutdown, serial failure, audio failure, and device removal release PTT immediately without completing the tail delay.
- An unexpected device/serial fault produces a failed job; an operator STOP produces a stopped job.
- Port and method choices persist; runtime OPEN and asserted state do not.
- No real serial device may be required by automated tests.
- Preserve unrelated working-tree changes and stage only files named by each task.

## Review Focus

- A manual PTT release that occurs while auto-open is pending must invalidate the request so the line is never asserted later; covered by Task 2 controller tests.
- A COM name disappearing during an active automatic lease must cancel playback, release/dispose best-effort, and report failure rather than STOP; covered by Tasks 2 and 3.
- Repeated pointer/key release, capture loss, close, and disposal calls must be idempotent; covered by Tasks 2 and 4.
- A saved port absent at startup or refresh must resolve to `N/A` without destabilizing closed-combo keyboard navigation; covered by Tasks 1, 4, and 5.
- An exception during PTT release after otherwise successful playback must still fail the transmission and leave the UI closed/unkeyed; covered by Tasks 2 and 3.

---

## File structure

### Create

- `src/SstvVPrinter.App/Transmission/ITransmissionGate.cs` — platform-neutral wrapper around the playback phase.
- `src/SstvVPrinter.Desktop/Ptt/PttControlMethod.cs` — RTS/DTR persisted method enum.
- `src/SstvVPrinter.Desktop/Ptt/PttSnapshot.cs` — immutable state published to UI/composition.
- `src/SstvVPrinter.Desktop/Ptt/IPttSerialConnection.cs` — fakeable serial connection boundary.
- `src/SstvVPrinter.Desktop/Ptt/IPttPlatform.cs` — fakeable port enumeration/connection factory and delay boundary.
- `src/SstvVPrinter.Desktop/Ptt/PttController.cs` — serialized OPEN/CLOSE/manual/automatic state machine and removal monitor.
- `src/SstvVPrinter.Desktop/Ptt/SerialPttTransmissionGate.cs` — maps a controller automatic lease to `ITransmissionGate`.
- `src/SstvVPrinter.Desktop/Platforms/Windows/WindowsPttPlatform.cs` — `SerialPort` adapter and real delay/port enumeration.
- `tests/SstvVPrinter.Desktop.Tests/PttControllerTests.cs` — fake-platform controller and safety tests.
- `tests/SstvVPrinter.Desktop.Tests/SerialPttTransmissionGateTests.cs` — automatic lead/tail and fault tests.

### Modify

- `src/SstvVPrinter.App/Settings/UserPreferences.cs` — optional PTT port and method persistence fields using string values for backward-compatible JSON.
- `src/SstvVPrinter.App/Transmission/TransmissionController.cs` — invoke a gate around playback after encoding.
- `src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj` — add `System.IO.Ports` 10.0.0.
- `src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs` — PTT lists, selection, status, labels, colors, and enablement.
- `src/SstvVPrinter.Desktop/Views/MainWindow.axaml` — requested HARDWARE layout and per-control locking.
- `src/SstvVPrinter.Desktop/Views/MainWindow.axaml.cs` — drop-down refresh, open/close, and momentary pointer/keyboard routing.
- `src/SstvVPrinter.Desktop/Hosting/DesktopComposition.cs` — construct/wire/dispose PTT, coordinate transmission, and persist settings.
- `tests/SstvVPrinter.App.Tests/Settings/UserPreferencesTests.cs` — defaults, round trip, and old JSON behavior.
- `tests/SstvVPrinter.App.Tests/Transmission/TransmissionControllerTests.cs` — gate placement and failure classification.
- `tests/SstvVPrinter.Desktop.Tests/MainWindowViewModelTests.cs` — PTT presentation rules.
- `tests/SstvVPrinter.Desktop.Tests/MainWindowLayoutTests.cs` — control presence, bindings, and event routes.
- `tests/SstvVPrinter.Desktop.Tests/DesktopHostTests.cs` or a new focused composition test file — shutdown disposal and send wiring.

---

### Task 1: Persist backward-compatible PTT preferences

**Files:**
- Create: `src/SstvVPrinter.Desktop/Ptt/PttControlMethod.cs`
- Modify: `src/SstvVPrinter.App/Settings/UserPreferences.cs`
- Modify: `tests/SstvVPrinter.App.Tests/Settings/UserPreferencesTests.cs`

**Interfaces:**
- Produces: `enum PttControlMethod { Rts, Dtr }` in the Desktop project.
- Produces: `UserPreferences.PttPortName : string?` and `UserPreferences.PttMethod : string`, defaulting to `null` and `"RTS"`; the App settings model deliberately stores the method as a string so it has no Desktop dependency.

- [ ] **Step 1: Write failing preference tests**

Add tests named `Defaults_to_no_ptt_and_rts`, `Round_trip_preserves_ptt_port_and_method`, and `Old_json_without_ptt_fields_loads_defaults`. Assert `null`/`"RTS"`, `"COM7"`/`"DTR"`, and backward-compatible loading of JSON without the new properties.

- [ ] **Step 2: Run the focused tests and verify failure**

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj -c Release --filter FullyQualifiedName~UserPreferencesTests --logger "console;verbosity=minimal"`

Expected: FAIL because the two PTT properties/constructor arguments do not exist.

- [ ] **Step 3: Extend `UserPreferences` without breaking existing callers**

Add optional constructor parameters `string? PttPortName = null, string PttMethod = "RTS"`, validate that method is exactly `RTS` or `DTR`, and expose matching init properties. Add the Desktop enum with display/persistence names `Rts` and `Dtr`; parsing from the settings string belongs to composition in Task 5.

- [ ] **Step 4: Run the focused tests and verify pass**

Run the Step 2 command.

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- 'src/SstvVPrinter.App/Settings/UserPreferences.cs' 'src/SstvVPrinter.Desktop/Ptt/PttControlMethod.cs' 'tests/SstvVPrinter.App.Tests/Settings/UserPreferencesTests.cs'
git commit -m "feat: persist PTT preferences"
```

### Task 2: Build the serial PTT state machine behind fakeable boundaries

**Files:**
- Create: `src/SstvVPrinter.Desktop/Ptt/PttSnapshot.cs`
- Create: `src/SstvVPrinter.Desktop/Ptt/IPttSerialConnection.cs`
- Create: `src/SstvVPrinter.Desktop/Ptt/IPttPlatform.cs`
- Create: `src/SstvVPrinter.Desktop/Ptt/PttController.cs`
- Create: `src/SstvVPrinter.Desktop/Platforms/Windows/WindowsPttPlatform.cs`
- Create: `tests/SstvVPrinter.Desktop.Tests/PttControllerTests.cs`
- Modify: `src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj`

**Interfaces:**
- Consumes: `PttControlMethod` from Task 1.
- Produces: `PttSnapshot(string? PortName, PttControlMethod Method, bool IsOpen, bool IsAsserted, bool IsBusy, string? Error)`.
- Produces: `IPttSerialConnection` with `Open()`, `Close()`, `SetRts(bool)`, `SetDtr(bool)`, `IsOpen`, and `Dispose()`.
- Produces: `IPttPlatform.GetPortNames()`, `CreateConnection(string portName)`, and `DelayAsync(TimeSpan, CancellationToken)`.
- Produces: `PttController.RefreshPortsAsync`, `OpenAsync`, `CloseAsync`, `BeginManualAsync`, `EndManualAsync`, `AcquireAutomaticAsync`, `StateChanged`, `Snapshot`, and `DisposeAsync`.
- Produces: `IPttAutomaticLease.FaultToken`, `ReleaseAsync`, `AbortAsync`, and `DisposeAsync`; `ReleaseAsync` deasserts after the gate has completed its tail delay, while `AbortAsync` and disposal deassert immediately.

- [ ] **Step 1: Write failing controller tests for discovery and basic lines**

Test natural sorting with `COM2` before `COM10`, unchanged-list preservation, `N/A` insertion, OPEN/CLOSE idempotence, RTS assertion leaving DTR low, DTR assertion leaving RTS low, and a healthy close deasserting both lines before disposal.

- [ ] **Step 2: Run the controller tests and verify failure**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj -c Release --filter FullyQualifiedName~PttControllerTests --logger "console;verbosity=minimal"`

Expected: FAIL because the controller types do not exist.

- [ ] **Step 3: Implement the interfaces, snapshot, serialized controller core, and Windows adapter**

Use one `SemaphoreSlim` to serialize connection mutation, generation tokens to invalidate pending manual opens, and `SerialPort` with RTS/DTR initially false. Add `<PackageReference Include="System.IO.Ports" Version="10.0.0" />`. All synchronous serial calls run off the Avalonia UI thread.

- [ ] **Step 4: Run the basic controller tests and verify pass**

Run the Step 2 command.

Expected: PASS for discovery/open/close/RTS/DTR cases.

- [ ] **Step 5: Add failing safety and Review Focus tests**

Add tests named `Release_before_open_completes_never_asserts`, `Removed_port_aborts_automatic_lease_and_closes`, `Serial_exception_closes_and_clears_assertion`, `Release_failure_closes_and_faults_lease`, `Repeated_release_close_and_dispose_are_safe`, and `Manual_close_is_rejected_while_asserted`. Use a controllable fake delay and fake connection exceptions; no wall-clock sleeps.

- [ ] **Step 6: Implement removal monitoring and fault cleanup**

While open, check `GetPortNames()` at an approximately one-second interval through `IPttPlatform.DelayAsync`. On disappearance or serial exception, cancel the automatic lease fault token, best-effort clear both lines, dispose the connection, and publish a closed/unasserted snapshot with the error. Ensure `DisposeAsync` stops the monitor and is bounded by cancellation rather than a device-dependent infinite wait.

- [ ] **Step 7: Run all controller tests**

Run the Step 2 command.

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add -- 'src/SstvVPrinter.Desktop/Ptt' 'src/SstvVPrinter.Desktop/Platforms/Windows/WindowsPttPlatform.cs' 'src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj' 'tests/SstvVPrinter.Desktop.Tests/PttControllerTests.cs'
git commit -m "feat: add safe serial PTT controller"
```

### Task 3: Gate only the audio playback phase with automatic PTT

**Files:**
- Create: `src/SstvVPrinter.App/Transmission/ITransmissionGate.cs`
- Create: `src/SstvVPrinter.Desktop/Ptt/SerialPttTransmissionGate.cs`
- Create: `tests/SstvVPrinter.Desktop.Tests/SerialPttTransmissionGateTests.cs`
- Modify: `src/SstvVPrinter.App/Transmission/TransmissionController.cs`
- Modify: `tests/SstvVPrinter.App.Tests/Transmission/TransmissionControllerTests.cs`

**Interfaces:**
- Consumes: Task 2 `PttController.AcquireAutomaticAsync` and `IPttAutomaticLease`.
- Produces: `ITransmissionGate.RunAsync(Func<CancellationToken, Task> playback, CancellationToken cancellationToken)`.
- Produces: `NoOpTransmissionGate.Instance`, which immediately invokes playback and adds no delay.
- Produces: `TransmissionController.SendAsync(PreparedTransmission prepared, string endpointId, ITransmissionGate gate, CancellationToken cancellationToken)`; retain the existing overload and delegate it to `NoOpTransmissionGate.Instance` for compatibility.

- [ ] **Step 1: Write failing App-layer gate-order tests**

Add tests proving artifact generation completes before `ITransmissionGate.RunAsync`, playback occurs inside the gate, user cancellation maps to `JobState.Stopped`, and a non-cancellation gate exception maps to `JobState.Failed` while propagating the exception.

- [ ] **Step 2: Run focused App transmission tests and verify failure**

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj -c Release --filter FullyQualifiedName~TransmissionControllerTests --logger "console;verbosity=minimal"`

Expected: FAIL because the gate interface and overload do not exist.

- [ ] **Step 3: Add `ITransmissionGate`, no-op implementation, and controller overload**

Set `JobState.Transmitting` immediately before invoking the gate. Keep encoding and temporary-file cleanup behavior unchanged. Distinguish only the controller's linked cancellation token as STOP; serial/gate faults must arrive as ordinary exceptions and therefore become FAILED.

- [ ] **Step 4: Run focused App transmission tests and verify pass**

Run the Step 2 command.

Expected: PASS.

- [ ] **Step 5: Write failing Desktop gate tests**

Test `N/A` direct playback with no delay, automatic OPEN/assert/one-second lead/playback/one-second tail/release ordering, user cancellation skipping the remaining delay, device removal cancelling playback then throwing a PTT fault, playback exception causing immediate release, and release exception surfacing as failure.

- [ ] **Step 6: Implement `SerialPttTransmissionGate`**

For `N/A`, delegate to `NoOpTransmissionGate`. Otherwise acquire an automatic lease, wait exactly `TimeSpan.FromSeconds(1)`, run playback with a token linked to `lease.FaultToken`, wait exactly one second only after successful playback, then release. On cancellation or any exception, call `AbortAsync`; if the lease fault token fired, throw a non-`OperationCanceledException` PTT exception after playback unwinds so the controller records FAILED.

- [ ] **Step 7: Run both gate/controller test groups**

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj -c Release --filter FullyQualifiedName~TransmissionControllerTests --logger "console;verbosity=minimal"`

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj -c Release --filter FullyQualifiedName~SerialPttTransmissionGateTests --logger "console;verbosity=minimal"`

Expected: PASS for both.

- [ ] **Step 8: Commit**

```powershell
git add -- 'src/SstvVPrinter.App/Transmission/ITransmissionGate.cs' 'src/SstvVPrinter.App/Transmission/TransmissionController.cs' 'src/SstvVPrinter.Desktop/Ptt/SerialPttTransmissionGate.cs' 'tests/SstvVPrinter.App.Tests/Transmission/TransmissionControllerTests.cs' 'tests/SstvVPrinter.Desktop.Tests/SerialPttTransmissionGateTests.cs'
git commit -m "feat: synchronize PTT with audio playback"
```

### Task 4: Add PTT state and momentary controls to the HARDWARE panel

**Files:**
- Modify: `src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml`
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml.cs`
- Modify: `tests/SstvVPrinter.Desktop.Tests/MainWindowViewModelTests.cs`
- Modify: `tests/SstvVPrinter.Desktop.Tests/MainWindowLayoutTests.cs`

**Interfaces:**
- Consumes: Task 1 `PttControlMethod` and Task 2 `PttSnapshot`.
- Produces view-model members: `PttPorts`, `SelectedPttPort`, `PttMethods`, `SelectedPttMethod`, `IsPttOpen`, `IsPttAsserted`, `IsPttBusy`, `PttConnectionButtonText`, `PttConnectionBrush`, `PttButtonBrush`, `CanEditPttSettings`, `CanTogglePttConnection`, and `CanPressPtt`.
- Produces window delegates: `PttPortsRefreshRequested`, `PttOpenCloseRequested`, `ManualPttPressed`, and `ManualPttReleased`.

- [ ] **Step 1: Write failing view-model tests**

Cover `N/A` disabling OPEN/PTT, closed selected COM enabling OPEN/PTT, open state changing label to CLOSE and color to `#FF3D21`, asserted state coloring PTT red, send locking selectors without hiding state, and automatic busy state disabling CLOSE/manual PTT.

- [ ] **Step 2: Run focused view-model tests and verify failure**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj -c Release --filter FullyQualifiedName~MainWindowViewModelTests --logger "console;verbosity=minimal"`

Expected: FAIL because the PTT properties do not exist.

- [ ] **Step 3: Implement view-model properties and snapshot application**

Add `ApplyPttSnapshot(PttSnapshot snapshot)` and notify every dependent label/color/enablement property. Keep port selection separate from connection state, and do not include PTT preference changes in preview regeneration policy.

- [ ] **Step 4: Run focused view-model tests and verify pass**

Run the Step 2 command.

Expected: PASS.

- [ ] **Step 5: Write failing layout and event-routing tests**

Assert the HARDWARE panel contains `PTT PORT`, a named `PttPortComboBox` with `DropDownOpened="OnPttPortDropDownOpened"`, named `PttConnectionButton` with `OnPttOpenCloseClick`, and named `PttButton` with pointer pressed/released/capture-lost plus key down/up handlers. Assert the outer HARDWARE border is no longer bound wholesale to `!IsSettingsLocked`, while audio and selector subgroups retain their individual lock bindings.

- [ ] **Step 6: Add the requested two-row layout and momentary handlers**

Use `[port combo][OPEN/CLOSE]` and `[RTS/DTR combo][PTT]`. Route Space/Enter key-down once to `ManualPttPressed`, key-up to `ManualPttReleased`, pointer press/release similarly, and capture/focus loss to the idempotent release path. Do not refresh ports on selection change or closed-combo cursor movement.

- [ ] **Step 7: Run layout, smoke, and view-model tests**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj -c Release --filter "FullyQualifiedName~MainWindowLayoutTests|FullyQualifiedName~MainWindowViewModelTests|FullyQualifiedName~AppSmokeTests" --logger "console;verbosity=minimal"`

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add -- 'src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs' 'src/SstvVPrinter.Desktop/Views/MainWindow.axaml' 'src/SstvVPrinter.Desktop/Views/MainWindow.axaml.cs' 'tests/SstvVPrinter.Desktop.Tests/MainWindowViewModelTests.cs' 'tests/SstvVPrinter.Desktop.Tests/MainWindowLayoutTests.cs'
git commit -m "feat: add PTT hardware controls"
```

### Task 5: Wire composition, preferences, transmission, and shutdown

**Files:**
- Modify: `src/SstvVPrinter.Desktop/Hosting/DesktopComposition.cs`
- Modify: `tests/SstvVPrinter.Desktop.Tests/DesktopHostTests.cs` or create `tests/SstvVPrinter.Desktop.Tests/DesktopCompositionPttTests.cs`
- Modify: `tests/SstvVPrinter.App.Tests/Settings/UserPreferencesTests.cs`

**Interfaces:**
- Consumes: all Tasks 1–4 interfaces.
- Produces: one application-owned `PttController` and `SerialPttTransmissionGate`, fully disposed before host shutdown returns.

- [ ] **Step 1: Write failing composition tests**

Using injected fakes, cover initial startup scan; absent saved port becoming `N/A`; drop-down refresh preserving a still-present selection; selection/method changes saving preferences without regenerating preview; manual press auto-opening and release; OPEN/CLOSE routing; send choosing no-op for `N/A`; send choosing the serial gate for COM; PTT errors populating `ValidationError`; STOP cancelling gate and transmission; and disposal releasing/closing PTT before completing.

- [ ] **Step 2: Run focused composition tests and verify failure**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj -c Release --filter "FullyQualifiedName~DesktopCompositionPttTests|FullyQualifiedName~DesktopHostTests" --logger "console;verbosity=minimal"`

Expected: FAIL because composition does not construct or wire PTT.

- [ ] **Step 3: Inject and construct PTT dependencies**

Allow `DesktopComposition.CreateAsync` to use `WindowsPttPlatform` by default while its test seam accepts an injected controller/platform. Load `PttPortName` and parse `PttMethod` case-insensitively with RTS fallback. Perform the startup port scan without blocking the UI thread; if the saved port is absent, select `N/A` and queue the corrected preference snapshot.

- [ ] **Step 4: Wire window operations and snapshot updates**

Connect refresh, OPEN/CLOSE, manual press/release, controller state events, and error display. Marshal snapshots to the Avalonia dispatcher. Guard async-void UI delegates by catching non-cancellation exceptions in composition and routing them to the existing error text.

- [ ] **Step 5: Wire automatic send and stop**

Create a `SerialPttTransmissionGate` from the current selected port/method for each send and pass it to the Task 3 controller overload. STOP must cancel both transmission and any active PTT operation. Apply the existing delete-after-send policy only after `JobState.Completed`.

- [ ] **Step 6: Extend preference capture and shutdown**

Persist selected port as `null` for `N/A` and method as `RTS`/`DTR`. During `DisposeAsync`, stop new UI operations, dispose PTT (immediate release/close), await preference save, then finish existing host disposal without reordering unrelated services unnecessarily.

- [ ] **Step 7: Run focused composition tests and all preference tests**

Run the Step 2 command.

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj -c Release --filter FullyQualifiedName~UserPreferencesTests --logger "console;verbosity=minimal"`

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add -- 'src/SstvVPrinter.Desktop/Hosting/DesktopComposition.cs' 'tests/SstvVPrinter.Desktop.Tests/DesktopCompositionPttTests.cs' 'tests/SstvVPrinter.App.Tests/Settings/UserPreferencesTests.cs'
git commit -m "feat: integrate PTT with desktop transmission"
```

### Task 6: Full regression and Windows publish verification

**Files:**
- Modify only if verification reveals a PTT-specific defect; return to the owning task's test first.

**Interfaces:**
- Consumes: completed feature from Tasks 1–5.
- Produces: verified Release tests and Windows publish output.

- [ ] **Step 1: Run the complete Release test suite**

Run: `dotnet test SstvVPrinter.slnx -c Release --logger "console;verbosity=minimal"`

Expected: all tests PASS with zero failures.

- [ ] **Step 2: Publish the Windows desktop application**

Run: `dotnet publish src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/publish/win-x64-ptt-check --verbosity normal`

Expected: exit code 0 and `SstvVPrinter.Desktop.exe` in the output directory.

- [ ] **Step 3: Perform a no-hardware smoke check**

Launch the published application with no serial adapter required and verify: `N/A` is selected, OPEN/PTT are disabled, an existing print job can still transmit audio, and the application exits normally. This is the only manual check in the plan; real transmitter keying remains operator hardware acceptance testing.

- [ ] **Step 4: Review the final diff for safety paths**

Inspect every assertion site and confirm it has an immediate release path for STOP, playback error, serial error, removal, and shutdown. Confirm no COM call runs on the Avalonia UI thread and only intended files are staged.

- [ ] **Step 5: Commit any verification-only fixes, otherwise record no additional commit**

If fixes were needed, stage only their focused tests and implementation and commit with `fix: harden PTT failure cleanup`. If none were needed, leave the already passing commits unchanged.

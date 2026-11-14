# Radiofax Transmit Modes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add standards-compatible Radiofax 120/288 and 120/576 transmission to the existing print, preview, send, and WAV workflow.

**Architecture:** Keep VIS-based SSTV encoding intact and add an independent Radiofax planner/encoder beside it. A small generic transmission facade dispatches composition, duration estimation, and PCM generation by the persisted mode, while the desktop UI continues to operate on one prepared-transmission contract.

**Tech Stack:** .NET 10, C# 14, xUnit, Avalonia 12, existing PWG Raster/imaging pipeline and PCM/WAV renderer.

**Spec:** `docs/superpowers/specs/2026-10-19-radiofax-transmit-design.md`

## Global Constraints

- Preserve the numeric values of the four existing `SstvMode` members; append Radiofax values only.
- Support exactly Radiofax 120/288 and Radiofax 120/576; no extra LPM, shortened control sequence, receive, PTT, multi-page, or proprietary mode.
- Use 48 kHz PCM, 1500 Hz black, 1900 Hz centre, and 2300 Hz white.
- Use a 5-second IOC selector, 30-second asymmetric phase signal, 5-second stop selector, and 10-second black trailer.
- Each 120 LPM line is exactly 0.5 seconds / 24,000 samples and contains a white 4.5% dead sector.
- Active composition widths are 864 for IOC 288 and 1728 for IOC 576; total receiver grids are informational values 904 and 1809.
- Accept source aspect ratios from 1:2 through 2:1 inclusive after normalization and manual rotation; reject outside that range before PCM generation.
- Radiofax always uses effective horizontal multiplier 1; preserve and restore the user's SSTV 1x/2x preference.
- Process only the first PWG Raster page.
- Do not publish, tag, or push as part of implementation without a separate user request.

## Review Focus

- A ratio exactly 2.0 must pass while the smallest representable value above 2.0 must fail; Task 3 pins boundary arithmetic without rounded comparisons.
- Cancellation during a long variable-height image must stop both composition and PCM enumeration promptly; Tasks 2 and 3 include cancellation tests.
- Rapid mode/rotation changes must not allow a stale valid preparation to overwrite a newer invalid result; Task 5 exercises generation ordering.
- Settings written before the new enum members existed must deserialize to the same four SSTV modes and multipliers; Tasks 1 and 5 include legacy JSON tests.
- Invalid Radiofax dimensions supplied outside the UI must be rejected by the encoder even if UI validation was bypassed; Task 2 tests exact width and maximum-height guards.

---

### Task 1: Mode catalog and generic encoding options

**Files:**
- Modify: `src/SstvVPrinter.Sstv/SstvMode.cs`
- Create: `src/SstvVPrinter.Sstv/TransmissionModes.cs`
- Create: `src/SstvVPrinter.Sstv/Radiofax/RadiofaxModeDefinition.cs`
- Create: `src/SstvVPrinter.Sstv/Radiofax/RadiofaxModes.cs`
- Create: `src/SstvVPrinter.Sstv/TransmissionEncodeOptions.cs`
- Create: `src/SstvVPrinter.Sstv/Signal/SignalConstants.cs`
- Modify: `src/SstvVPrinter.Sstv/SstvEncodeOptions.cs`
- Test: `tests/SstvVPrinter.Sstv.Tests/TransmissionModeTests.cs`
- Test: `tests/SstvVPrinter.App.Tests/Settings/UserPreferencesTests.cs`

**Interfaces:**
- Produces: `SstvMode.Radiofax120Ioc288 = 4` and `SstvMode.Radiofax120Ioc576 = 5`.
- Produces: `RadiofaxModeDefinition(SstvMode Mode, string DisplayName, int LinesPerMinute, int Ioc, int TotalLineGrid, int ActiveWidth, int HeaderHeight, int MaximumBodyLines, double IocSelectionHz)`.
- Produces: `RadiofaxModes.Get(SstvMode)`, `RadiofaxModes.IsRadiofax(SstvMode)`.
- Produces: `TransmissionModes.Validate(SstvMode)`, `TransmissionModes.DisplayName(SstvMode)`, and `TransmissionModes.IsRadiofax(SstvMode)`.
- Produces: `TransmissionEncodeOptions(SstvMode Mode, int HorizontalSampleMultiplier = 1, float Amplitude = 0.8f)` with constructor-level mode, multiplier, and amplitude validation. Task 2 completes frame validation and dispatch.
- Produces: `SignalConstants.SampleRate = 48_000`; keep `SstvEncodeOptions.SampleRate` as a compatibility alias.

- [ ] **Step 1: Write failing mode-catalog tests**

Add tests asserting existing enum integers remain 0–3, new values are 4–5, display names are exactly `Radiofax 120/288` and `Radiofax 120/576`, definitions contain `904/864/32/1728/675` and `1809/1728/64/3456/300`, invalid enum values throw, and Radiofax options reject multiplier 2.

- [ ] **Step 2: Run the focused tests and verify RED**

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj --configuration Release --filter "FullyQualifiedName~TransmissionModeTests"`

Expected: FAIL because the Radiofax modes and catalogs do not exist.

- [ ] **Step 3: Implement the appended enum members, catalogs, constants, and generic options**

Keep `SstvModes.Get` SSTV-only. `TransmissionModes` dispatches validation/display lookup to `SstvModes` or `RadiofaxModes`. At this task boundary, `TransmissionEncodeOptions` validates only its mode, multiplier, and amplitude; Task 2 adds `Validate(RgbFrame frame)` once both concrete option types exist.

- [ ] **Step 4: Run mode tests and the existing SSTV suite**

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj --configuration Release`

Expected: PASS, including unchanged Martin, Scottie, Robot, VIS, and PCM tests.

- [ ] **Step 5: Add and run legacy preference JSON tests**

Add a theory to `UserPreferencesTests` loading literal pre-feature JSON with numeric modes 0, 1, 2, and 3 and asserting each still maps to its original SSTV mode and multiplier. Update `UserPreferences` and `TransmissionSettings` to call `TransmissionModes.Validate` instead of `SstvModes.Get`.

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj --configuration Release --filter "FullyQualifiedName~UserPreferencesTests"`

Expected: PASS for old and new mode round trips.

- [ ] **Step 6: Commit**

```powershell
git add src/SstvVPrinter.Sstv src/SstvVPrinter.App/Settings/UserPreferences.cs src/SstvVPrinter.App/Transmission/TransmissionSettings.cs tests/SstvVPrinter.Sstv.Tests/TransmissionModeTests.cs tests/SstvVPrinter.App.Tests/Settings/UserPreferencesTests.cs
git commit -m "feat: define Radiofax transmission modes"
```

### Task 2: Standards-compatible Radiofax signal encoder

**Files:**
- Create: `src/SstvVPrinter.Sstv/Radiofax/RadiofaxEncodeOptions.cs`
- Create: `src/SstvVPrinter.Sstv/Radiofax/RadiofaxFrequency.cs`
- Create: `src/SstvVPrinter.Sstv/Radiofax/RadiofaxPlanner.cs`
- Create: `src/SstvVPrinter.Sstv/Radiofax/RadiofaxEncoder.cs`
- Create: `src/SstvVPrinter.Sstv/TransmissionEncoder.cs`
- Create: `src/SstvVPrinter.Sstv/TransmissionDurationEstimator.cs`
- Test: `tests/SstvVPrinter.Sstv.Tests/RadiofaxModeTests.cs`
- Test: `tests/SstvVPrinter.Sstv.Tests/RadiofaxPlannerTests.cs`
- Test: `tests/SstvVPrinter.Sstv.Tests/RadiofaxEncoderTests.cs`
- Test: `tests/SstvVPrinter.Sstv.Tests/TransmissionEncoderTests.cs`

**Interfaces:**
- Consumes: Task 1 mode catalogs, `SignalConstants.SampleRate`, `RgbFrame`, `ToneSpan`, and `PcmRenderer`.
- Produces: `RadiofaxEncodeOptions(SstvMode Mode, float Amplitude = 0.8f)` with `Validate(RgbFrame frame)`.
- Produces: `RadiofaxFrequency.FromLuminance(byte value) -> double`.
- Produces: `RadiofaxPlanner.Plan(RgbFrame frame, RadiofaxEncodeOptions options, CancellationToken) -> IEnumerable<ToneSpan>`.
- Produces: `RadiofaxEncoder.EncodeAsync(RgbFrame, RadiofaxEncodeOptions, CancellationToken) -> IAsyncEnumerable<PcmBlock>`.
- Produces: `TransmissionEncoder.EncodeAsync(RgbFrame, TransmissionEncodeOptions, CancellationToken) -> IAsyncEnumerable<PcmBlock>`.
- Produces: `TransmissionDurationEstimator.Estimate(RgbFrame, TransmissionEncodeOptions) -> TimeSpan`.
- Completes: `TransmissionEncodeOptions.Validate(RgbFrame frame)`, dispatching to `SstvEncodeOptions` or `RadiofaxEncodeOptions`.

- [ ] **Step 1: Write failing frequency and option-validation tests**

Assert luminance 0/128/255 maps to 1500/about 1901.57/2300 using linear mapping; reference levels `[0, 36, 73, 109, 146, 182, 219, 255]` map to the eight WMO target frequencies within 1.1 Hz; and options reject SSTV modes, wrong active width, height below the header, or height above `HeaderHeight + MaximumBodyLines`.

- [ ] **Step 2: Run the focused tests and verify RED**

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj --configuration Release --filter "FullyQualifiedName~RadiofaxModeTests"`

Expected: FAIL because Radiofax options and frequency mapping are absent.

- [ ] **Step 3: Implement options and linear luminance mapping**

Use `1500.0 + value * (800.0 / 255.0)`. Validate exact active width and inclusive maximum total height before returning any enumerable.

- [ ] **Step 4: Write failing planner sequence and timing tests**

Test both modes for: 5-second 300/675 Hz black-white alternation; 60 phase lines totaling 30 seconds; each phase line starts with 0.025 seconds white then 0.475 seconds black; each image line starts with 0.0225 seconds white dead sector and has 0.4775 seconds active content; 5-second 450 Hz stop alternation; 10-second 1500 Hz trailer; left-to-right luminance order; and total planned duration `50 + frame.Height * 0.5` seconds.

- [ ] **Step 5: Implement `RadiofaxPlanner` with exact line-duration accounting**

Represent IOC and stop selectors as alternating 1500/2300 Hz spans whose half-cycle duration is `1 / (2 * selectorHz)`. Represent each active pixel with `0.4775 / frame.Width` ideal seconds; rely on `PcmRenderer` cumulative rounding so each complete line reaches 24,000 samples.

- [ ] **Step 6: Add cancellation and PCM-boundary tests**

Assert cancellation during enumeration throws `OperationCanceledException`; inspect the planner spans and assert every phase/image line sums to exactly 0.5 seconds, then render a known-height frame and assert total samples equal `(50 + frame.Height * 0.5) * 48_000`; test maximum legal height passes and one extra line fails before enumeration.

- [ ] **Step 7: Implement Radiofax and generic encoder/duration dispatch**

Complete `TransmissionEncodeOptions.Validate(RgbFrame frame)` by constructing and delegating to the concrete SSTV or Radiofax options. `TransmissionEncoder` must call the existing `SstvEncoder` for modes 0–3 and `RadiofaxEncoder` for modes 4–5. `TransmissionDurationEstimator` delegates to `SstvDurationEstimator` or returns `TimeSpan.FromSeconds(50 + frame.Height * 0.5)` for Radiofax.

- [ ] **Step 8: Run signal tests**

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj --configuration Release`

Expected: PASS with exact control-section, line, cancellation, validation, and legacy SSTV behavior.

- [ ] **Step 9: Commit**

```powershell
git add src/SstvVPrinter.Sstv tests/SstvVPrinter.Sstv.Tests
git commit -m "feat: encode WMO-compatible Radiofax audio"
```

### Task 3: Variable-height grayscale composition and safety validation

**Files:**
- Create: `src/SstvVPrinter.Imaging/Composition/TransmissionFrameComposer.cs`
- Create: `src/SstvVPrinter.Imaging/Composition/RadiofaxFrameComposer.cs`
- Create: `src/SstvVPrinter.Imaging/Composition/RadiofaxPageShapeException.cs`
- Create: `src/SstvVPrinter.Imaging/Header/RadiofaxHeaderRenderer.cs`
- Modify: `src/SstvVPrinter.Imaging/Composition/CompositionRequest.cs`
- Test: `tests/SstvVPrinter.Imaging.Tests/Composition/RadiofaxFrameComposerTests.cs`
- Test: `tests/SstvVPrinter.Imaging.Tests/Header/RadiofaxHeaderRendererTests.cs`
- Test: `tests/SstvVPrinter.Imaging.Tests/CompositionRequestTests.cs`

**Interfaces:**
- Consumes: Task 1 mode metadata, existing `DetailPreservingResampler`, bitmap font, `DecodedPage`, and `RgbFrame`.
- Produces: `RadiofaxFrameComposer.Compose(DecodedPage, CompositionRequest, CancellationToken) -> RgbFrame`.
- Produces: `TransmissionFrameComposer.Compose(DecodedPage, CompositionRequest, CancellationToken) -> RgbFrame`, dispatching without changing `SstvFrameComposer` behavior.
- Produces: `RadiofaxHeaderRenderer.Draw(Span<byte> rgb24, int width, int height, CompositionRequest request)`.
- Produces: `RadiofaxPageShapeException` with a stable Japanese user message exposed through its `Message`.

- [ ] **Step 1: Write failing aspect-ratio and dimension tests**

Use integer cross multiplication, not rounded floating point: accept `width:height` of `2:1`, `1:2`, A4 portrait, A4 landscape, and the measured weather-fax-like ratio `2696:1728`; reject `2001:1000`, `1000:2001`, and both orientations of `3:1`. Retain the existing `DecodedPage` constructor test that rejects zero dimensions. Assert IOC 288 output width/header/body limits are 864/32/1728 and IOC 576 values are 1728/64/3456.

- [ ] **Step 2: Run the focused tests and verify RED**

Run: `dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj --configuration Release --filter "FullyQualifiedName~RadiofaxFrameComposerTests"`

Expected: FAIL because the Radiofax composer and validation exception do not exist.

- [ ] **Step 3: Implement aspect validation, grayscale conversion, and variable-height fitting**

Validate `max(width,height) <= 2 * min(width,height)` before allocating the output. Fit to the mode's active width with preserved aspect ratio and existing detail-preserving resampling. Convert fitted RGB to deterministic BT.601-style byte luminance and store equal R/G/B channels. Prepend the mode header height and reject any result above the mode maximum.

- [ ] **Step 4: Write failing header tests**

Assert eight square cells fill values corresponding to the specified WMO frequencies, have a black outside border, scale from 32 to 64 lines, label modes `FAX288`/`FAX576`, retain JST timestamp and uppercase call sign, and leave the body immediately below the header unchanged.

- [ ] **Step 5: Implement the Radiofax header and composition dispatcher**

Reuse the existing 5x7 bitmap glyph data and call-sign horizontal emphasis. Keep `SstvFrameComposer` SSTV-only; `TransmissionFrameComposer` selects it or `RadiofaxFrameComposer` through `TransmissionModes.IsRadiofax`.

- [ ] **Step 6: Add cancellation tests**

Use a cancellation token triggered during resampling and assert the composer throws without returning a partially allocated `RgbFrame`.

- [ ] **Step 7: Run imaging tests**

Run: `dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj --configuration Release`

Expected: PASS, including all existing SSTV header/composition/orientation tests.

- [ ] **Step 8: Commit**

```powershell
git add src/SstvVPrinter.Imaging tests/SstvVPrinter.Imaging.Tests
git commit -m "feat: compose safe grayscale Radiofax pages"
```

### Task 4: Preparation, artifacts, progress, and validation errors

**Files:**
- Modify: `src/SstvVPrinter.App/Transmission/PreparedTransmission.cs`
- Modify: `src/SstvVPrinter.App/Transmission/TransmissionPreparationService.cs`
- Modify: `src/SstvVPrinter.App/Transmission/TransmissionArtifactService.cs`
- Create: `src/SstvVPrinter.App/Transmission/TransmissionValidationException.cs`
- Test: `tests/SstvVPrinter.App.Tests/Transmission/TransmissionPreparationServiceTests.cs`
- Test: `tests/SstvVPrinter.App.Tests/Transmission/TransmissionArtifactServiceTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3 `TransmissionEncodeOptions`, `TransmissionFrameComposer`, `TransmissionEncoder`, `TransmissionDurationEstimator`, and `RadiofaxPageShapeException`.
- Changes: `PreparedTransmission(RgbFrame Frame, TransmissionEncodeOptions Options, TimeSpan EstimatedDuration, DateTimeOffset HeaderTime)`.
- Produces: `TransmissionValidationException(string message, Exception? innerException = null)` for recoverable source/mode validation failures.

- [ ] **Step 1: Write failing preparation tests for both Radiofax modes**

Assert manual rotation occurs before ratio validation; 288/576 preparations have effective multiplier 1, expected widths and variable heights, grayscale pixels, and duration `50 + frame.Height * 0.5`; a 2.001:1 page raises `TransmissionValidationException` without invoking encoding.

- [ ] **Step 2: Run preparation tests and verify RED**

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj --configuration Release --filter "FullyQualifiedName~TransmissionPreparationServiceTests"`

Expected: FAIL because preparation is SSTV-only.

- [ ] **Step 3: Generalize preparation around `TransmissionFrameComposer` and generic options**

Normalize Radiofax multiplier to 1, rotate first, translate `RadiofaxPageShapeException` into `TransmissionValidationException`, and calculate duration from the composed frame. Preserve current timestamp and SSTV paths.

- [ ] **Step 4: Write failing artifact-dispatch and progress tests**

Assert saved Radiofax WAV has a valid 48 kHz mono header, sample count matching estimated duration, progress starts at 0, is monotonic, and ends at 1. Assert cancellation deletes partial temporary files. Retain an SSTV assertion proving the generic encoder did not alter its output length.

- [ ] **Step 5: Replace direct `SstvEncoder` use with `TransmissionEncoder`**

Use `SignalConstants.SampleRate` for progress estimates and leave `WaveFileWriter` unchanged.

- [ ] **Step 6: Run the complete App test project**

Run: `dotnet test tests/SstvVPrinter.App.Tests/SstvVPrinter.App.Tests.csproj --configuration Release`

Expected: PASS for preparation, artifact, queue, settings, and controller tests.

- [ ] **Step 7: Commit**

```powershell
git add src/SstvVPrinter.App tests/SstvVPrinter.App.Tests
git commit -m "feat: prepare and export Radiofax transmissions"
```

### Task 5: Desktop mode selection and safe action state

**Files:**
- Create: `src/SstvVPrinter.Desktop/Converters/TransmissionModeDisplayConverter.cs`
- Modify: `src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml`
- Modify: `src/SstvVPrinter.Desktop/Hosting/DesktopComposition.cs`
- Modify: `src/SstvVPrinter.Desktop/Hosting/PreviewRegenerationPolicy.cs`
- Test: `tests/SstvVPrinter.Desktop.Tests/MainWindowViewModelTests.cs`
- Test: `tests/SstvVPrinter.Desktop.Tests/MainWindowLayoutTests.cs`
- Test: `tests/SstvVPrinter.Desktop.Tests/PreviewRegenerationPolicyTests.cs`

**Interfaces:**
- Consumes: Tasks 1 and 4 mode display names, generic preparation, and `TransmissionValidationException`.
- Produces: `MainWindowViewModel.IsRadiofaxMode`, `MainWindowViewModel.IsHorizontalMultiplierEnabled`, and an internal remembered SSTV multiplier.
- Produces: `TransmissionModeDisplayConverter.Convert` returning `TransmissionModes.DisplayName(mode)`.

- [ ] **Step 1: Write failing mode and multiplier interaction tests**

Assert the two Radiofax entries appear after the four SSTV modes; selecting Radiofax forces effective 1x and disables the unchecked checkbox; switching back restores the last SSTV checkbox state; saving and loading while Radiofax is selected preserves that remembered SSTV multiplier across restart.

- [ ] **Step 2: Write failing invalid-page action tests**

Make preparation throw `TransmissionValidationException`. Assert `CanSend` and `CanSaveWave` are false, `CanDiscard` remains true when a selected active job exists, status/validation text explains the elongated page, rotation and mode properties remain writable, and a succeeding rotated regeneration restores readiness.

- [ ] **Step 3: Add stale-generation regression test**

Start a delayed valid preparation, select an invalid orientation/mode and complete its failure, then complete the older valid preparation. Assert the older result does not replace the current invalid state or re-enable actions.

- [ ] **Step 4: Run ViewModel tests and verify RED**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj --configuration Release --filter "FullyQualifiedName~MainWindowViewModelTests"`

Expected: FAIL because Radiofax UI state and validation handling are absent.

- [ ] **Step 5: Implement ViewModel state, display conversion, and bindings**

Bind the mode ComboBox through the converter so labels contain spaces and IOC slashes. Bind the 2x checkbox `IsEnabled` to `IsHorizontalMultiplierEnabled`. Catch only `TransmissionValidationException` as a recoverable invalid preview; retain unexpected exception propagation to the existing host error path. Change `CanDiscard` to depend on the selected active job rather than a successful preview.

- [ ] **Step 6: Update preference capture and regeneration policy**

Persist the selected Radiofax enum while storing the remembered SSTV multiplier in the existing `HorizontalSampleMultiplier` preference field. On load, initialize the private remembered multiplier from that field but expose an unchecked, disabled checkbox and pass effective multiplier 1 to preparation. Mode, multiplier, and rotation changes each trigger one cancellable regeneration through the existing generation guard.

- [ ] **Step 7: Run Desktop tests**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj --configuration Release`

Expected: PASS for UI state, layout, lifetime, audio, and printer-registration tests.

- [ ] **Step 8: Commit**

```powershell
git add src/SstvVPrinter.Desktop tests/SstvVPrinter.Desktop.Tests
git commit -m "feat: add Radiofax desktop workflow"
```

### Task 6: Diagnostic tools and public documentation

**Files:**
- Modify: `src/SstvVPrinter.SstvTool/Program.cs`
- Modify: `src/SstvVPrinter.ImagingTool/ToolOptions.cs`
- Modify: `src/SstvVPrinter.ImagingTool/Program.cs`
- Modify: `README.md`
- Modify: `README_JP.md`
- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/BUILDING.md`
- Modify: `CHANGELOG.md`
- Test: `tests/SstvVPrinter.Imaging.Tests/Tool/ToolOptionsParserTests.cs`

**Interfaces:**
- Consumes: Tasks 1–5 public mode catalog, composer, encoder, and duration estimator.
- Produces: CLI aliases `fax288` and `fax576` for repeatable preview/WAV fixtures.

- [ ] **Step 1: Write failing CLI parser tests**

Assert `fax288` and `fax576` map to the two new enum members, force horizontal multiplier 1, and reject `--horizontal 2` for Radiofax while retaining current SSTV arguments.

- [ ] **Step 2: Run parser tests and verify RED**

Run: `dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj --configuration Release --filter "FullyQualifiedName~ToolOptionsParserTests"`

Expected: FAIL because the aliases are unknown.

- [ ] **Step 3: Update both diagnostic tools**

Route image output through `TransmissionFrameComposer` and WAV output through `TransmissionEncoder`; print the selected mode display name, frame dimensions, and estimated duration.

- [ ] **Step 4: Update documentation**

Document the two Radiofax modes, approximate A4 transmission times, grayscale header, 1:2–2:1 safety limit, disabled 2x behavior, first-page limitation, lack of PTT/receive, and commands for generating fax fixtures. Update the architecture pipeline from “SSTV composition” to mode-dispatched SSTV/Radiofax composition.

- [ ] **Step 5: Run documentation and focused tool checks**

Run: `dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj --configuration Release`

Run: `git diff --check`

Expected: tests PASS and no whitespace errors.

- [ ] **Step 6: Commit**

```powershell
git add src/SstvVPrinter.SstvTool src/SstvVPrinter.ImagingTool tests/SstvVPrinter.Imaging.Tests README.md README_JP.md docs/ARCHITECTURE.md docs/BUILDING.md CHANGELOG.md
git commit -m "docs: document Radiofax transmission"
```

### Task 7: Whole-feature verification and manual test artifacts

**Files:**
- Modify only if verification finds a defect in a file owned by Tasks 1–6.
- Do not commit generated WAV, preview images, `artifacts/`, or `.codex-remote-attachments/`.

**Interfaces:**
- Consumes: the completed Radiofax feature.
- Produces: verified x64 build plus local IOC 288 and IOC 576 WAV/preview fixtures for user interoperability testing.

- [ ] **Step 1: Run formatting verification**

Run: `dotnet format --verify-no-changes`

Expected: exit code 0 with no changed files.

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test --configuration Release --no-restore`

Expected: all projects PASS with zero failed tests.

- [ ] **Step 3: Build the release configuration**

Run: `dotnet build --configuration Release --no-restore`

Expected: exit code 0, zero errors, and zero warnings.

- [ ] **Step 4: Generate local interoperability fixtures**

Use a landscape test chart containing black, all eight WMO gray levels, white, thin horizontal/vertical lines, `JA1UMW`, and orientation marks. Generate Radiofax 120/288 and 120/576 previews and WAV files through the diagnostic tools. Record path, byte size, SHA-256, dimensions, and duration; keep files untracked.

- [ ] **Step 5: Inspect WAV structure and duration independently**

Read each RIFF header and assert mono 48 kHz float/PCM format matches the existing writer contract, sample count matches the estimator, IOC 288 is shorter than IOC 576 for the same page, and neither exceeds the design maximum.

- [ ] **Step 6: Run the desktop smoke test**

Launch the Release executable, print one portrait page, select both Radiofax modes, verify grayscale preview and disabled 2x state, rotate through valid/invalid ratios, save WAV, and confirm File → Exit removes the process.

- [ ] **Step 7: User interoperability test**

Provide the two untracked WAV fixtures to the user for decoding with an existing Radiofax receiver. Record whether IOC selection, phasing, orientation, aspect ratio, eight grayscale levels, call sign, and clean stop are recognized. Any receiver mismatch starts a systematic-debugging cycle before merge or release.

- [ ] **Step 8: Commit verification-only fixes, if any**

If verification required source changes, rerun the owning task's focused tests plus Steps 1–3, then commit only those source/test fixes. Otherwise create no empty verification commit.

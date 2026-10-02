# SSTV Encoder Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate standards-compatible Martin 1, Scottie 1, Scottie 2, and Robot 36 audio as phase-continuous 48 kHz PCM and atomic 16-bit WAV files, with standard and 2× horizontal modulation density.

**Architecture:** A platform-neutral class library validates immutable RGB frames, plans family-specific frequency spans, and renders them through a common cumulative-time, phase-continuous oscillator into bounded PCM blocks. A separate WAV adapter and small color-bar CLI consume the stream without coupling the core to Windows, image decoders, or audio devices.

**Tech Stack:** C# 14, .NET 10 LTS, `System.Buffers`, `System.Numerics`, `System.Runtime.CompilerServices`, xUnit; no third-party runtime packages.

**Spec:** `docs/superpowers/specs/2026-10-02-sstv-encoder-core-design.md`

## Global Constraints

- PCM is always 48,000 samples/second, mono, phase-continuous, and bounded-block streaming.
- Supported modes are Martin 1, Scottie 1, Scottie 2, and Robot 36 only.
- Horizontal sampling multiplier is exactly 1 or 2; it changes modulation-update density but never VIS identity, line time, or frame time.
- Inputs are immutable RGB24 frames at the selected mode's exact width times multiplier and exact height.
- The core library contains no Windows APIs, image file decoding, resizing, callsign composition, playback, or PTT control.
- WAV output is signed 16-bit little-endian PCM, written through a temporary file and atomically published only on success.
- Timings and RGB-to-Robot equations use the exact values in the approved design spec.
- Tests generate compact synthetic inputs and do not commit generated WAV files.

## Review Focus

- Cumulative rounding across thousands of fractional pixel spans must not drift: `PcmRendererTests.RenderAsync_keeps_every_boundary_and_total_within_one_sample` pins this in Task 2.
- A PCM block boundary in the middle of a tone must not reset oscillator phase: `PcmRendererTests.RenderAsync_preserves_phase_across_blocks_and_spans` pins this in Task 2.
- Scottie synchronization belongs between blue and red, including the first-line pre-sync convention: `ScottieScanPlannerTests.PlanLine_places_sync_between_blue_and_red` and `PlanFrame_emits_initial_sync_once` pin this in Task 3.
- Robot 36 paired-line chrominance must average the pair without swapping R-Y and B-Y: `Robot36ScanPlannerTests.PlanPair_averages_chrominance_and_alternates_channels` pins this in Task 4.
- Cancellation or an output collision must never replace a completed WAV or leave a published partial file: `WaveFileWriterTests.WriteAsync_cancellation_and_failure_leave_destination_untouched` pins this in Task 5.

---

## Task 1: Encoder Project, Immutable Frame, and Mode Contracts

**Files:**
- Create: `src/SstvVPrinter.Sstv/SstvVPrinter.Sstv.csproj`
- Create: `src/SstvVPrinter.Sstv/Rgb24.cs`
- Create: `src/SstvVPrinter.Sstv/RgbFrame.cs`
- Create: `src/SstvVPrinter.Sstv/SstvMode.cs`
- Create: `src/SstvVPrinter.Sstv/SstvModeDefinition.cs`
- Create: `src/SstvVPrinter.Sstv/SstvModes.cs`
- Create: `src/SstvVPrinter.Sstv/SstvEncodeOptions.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj`
- Create: `tests/SstvVPrinter.Sstv.Tests/RgbFrameTests.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/SstvModeTests.cs`
- Modify: `SstvVPrinter.slnx`

**Interfaces:**
- Consumes: RGB bytes produced later by the image-preprocessing subsystem; no dependency on the IPP probe.
- Produces: immutable `Rgb24(byte R, byte G, byte B)`; `RgbFrame.Create(int width, int height, ReadOnlySpan<byte> rgb24)` and `Rgb24 GetPixel(int x, int y)`; `SstvMode` enum; `SstvModeDefinition`; `SstvModes.Get(SstvMode mode)`; `SstvEncodeOptions(SstvMode Mode, int HorizontalSampleMultiplier = 1, float Amplitude = 0.8f)` with constant `SampleRate = 48000`.

- [ ] **Step 1: Write failing immutable-frame tests**

Add `Create_copies_source_bytes`, `Create_rejects_nonpositive_dimensions`, `Create_rejects_wrong_rgb_byte_count`, `GetPixel_returns_row_major_rgb`, and `GetPixel_rejects_out_of_range_coordinates`. Mutate the caller's source array after `Create` and assert the frame is unchanged.

- [ ] **Step 2: Run frame tests and confirm failure**

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj --filter FullyQualifiedName~RgbFrameTests`

Expected: FAIL because the project and frame types do not exist.

- [ ] **Step 3: Implement the immutable RGB model**

Implement the Task 1 frame interfaces with checked `width * height * 3` arithmetic and a private copied byte array. Do not expose the backing storage.

- [ ] **Step 4: Write failing mode and option tests**

Add `Definitions_match_approved_vis_dimensions_and_families`, covering all four rows in the spec; `Options_accept_only_horizontal_multipliers_one_and_two`; `Options_reject_nonfinite_or_out_of_range_amplitude`; and `ValidateFrame_requires_mode_dimensions_times_multiplier`. Pin amplitude to the open interval `(0, 1]` and default `0.8f`.

- [ ] **Step 5: Implement mode definitions and option validation**

Use a closed switch in `SstvModes.Get`. Add `SstvEncodeOptions.Validate(RgbFrame frame)`; standard dimensions are 320×256 for Martin/Scottie and 320×240 for Robot, with width multiplied by 1 or 2.

- [ ] **Step 6: Add projects to the solution and verify Task 1**

Run:

```powershell
dotnet sln SstvVPrinter.slnx add src/SstvVPrinter.Sstv/SstvVPrinter.Sstv.csproj
dotnet sln SstvVPrinter.slnx add tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj
dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj
```

Expected: all Task 1 tests PASS with zero warnings.

- [ ] **Step 7: Commit the contracts**

```powershell
git add SstvVPrinter.slnx src/SstvVPrinter.Sstv tests/SstvVPrinter.Sstv.Tests
git commit -m "feat: add SSTV encoder contracts"
```

## Task 2: VIS Timeline and Phase-Continuous PCM Renderer

**Files:**
- Create: `src/SstvVPrinter.Sstv/Signal/ToneSpan.cs`
- Create: `src/SstvVPrinter.Sstv/Signal/VisHeader.cs`
- Create: `src/SstvVPrinter.Sstv/Signal/PcmBlock.cs`
- Create: `src/SstvVPrinter.Sstv/Signal/PcmRenderer.cs`
- Create: `src/SstvVPrinter.Sstv/Signal/VideoFrequency.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/VisHeaderTests.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/PcmRendererTests.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/VideoFrequencyTests.cs`

**Interfaces:**
- Consumes: VIS codes and amplitude from Task 1.
- Produces: `ToneSpan(double FrequencyHz, double DurationSeconds)`; `VisHeader.Plan(byte visCode) -> IEnumerable<ToneSpan>`; `VideoFrequency.FromByte(byte value) -> double`; immutable `PcmBlock(long StartSample, ReadOnlyMemory<float> Samples)`; `PcmRenderer.RenderAsync(IEnumerable<ToneSpan> spans, float amplitude, CancellationToken cancellationToken) -> IAsyncEnumerable<PcmBlock>` using 4096-sample maximum blocks.

- [ ] **Step 1: Write failing VIS and video-frequency tests**

For VIS codes 44, 60, 56, and 8, add tests asserting the exact 300/10/300 ms leaders, start bit, seven least-significant-first data bits, even parity, stop bit, and 910 ms total. Add black=1500 Hz, 128≈1901.568627 Hz, and white=2300 Hz assertions with `1e-6` tolerance.

- [ ] **Step 2: Run the VIS/frequency tests and confirm failure**

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj --filter "FullyQualifiedName~VisHeaderTests|FullyQualifiedName~VideoFrequencyTests"`

Expected: FAIL because the signal types do not exist.

- [ ] **Step 3: Implement VIS planning and video-frequency mapping**

Implement the exact frequencies and durations in the spec. Validate `ToneSpan` frequency is positive finite and duration is positive finite.

- [ ] **Step 4: Write failing renderer tests**

Add `RenderAsync_emits_bounded_4096_sample_blocks`, `RenderAsync_keeps_every_boundary_and_total_within_one_sample`, `RenderAsync_preserves_phase_across_blocks_and_spans`, `RenderAsync_applies_amplitude_without_clipping`, `RenderAsync_is_deterministic`, and `RenderAsync_observes_cancellation`. Analyze generated samples against a direct continuous-phase sine equation; do not estimate correctness only from FFT peaks.

- [ ] **Step 5: Implement cumulative-time rendering**

Track ideal cumulative samples as `double`; each span ends at `Round(cumulativeIdeal, MidpointRounding.AwayFromZero)`. Emit the difference from the previous boundary while updating phase by `2πf/48000`, reducing phase periodically without resetting it. Reuse a bounded work buffer but copy each yielded block into storage owned by that block.

- [ ] **Step 6: Verify and commit Task 2**

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj`

Expected: all tests PASS with zero warnings.

```powershell
git add src/SstvVPrinter.Sstv/Signal tests/SstvVPrinter.Sstv.Tests
git commit -m "feat: render VIS and phase-continuous PCM"
```

## Task 3: Martin 1 and Scottie 1/2 Scan Planning

**Files:**
- Create: `src/SstvVPrinter.Sstv/Planning/ISstvScanPlanner.cs`
- Create: `src/SstvVPrinter.Sstv/Planning/SequentialRgbScanPlanner.cs`
- Create: `src/SstvVPrinter.Sstv/Planning/SstvScanPlannerFactory.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/MartinScanPlannerTests.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/ScottieScanPlannerTests.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/HorizontalSamplingTests.cs`

**Interfaces:**
- Consumes: `RgbFrame`, `SstvModeDefinition`, options, `ToneSpan`, and video-frequency mapping from Tasks 1–2.
- Produces: internal `ISstvScanPlanner.PlanFrame(RgbFrame frame, SstvEncodeOptions options, CancellationToken cancellationToken) -> IEnumerable<ToneSpan>`; `SstvScanPlannerFactory.Create(SstvMode mode)`.

- [ ] **Step 1: Write failing Martin tests**

Add `PlanLine_emits_sync_porch_and_gbr_scans`, `PlanLine_uses_approved_4_862_0_572_146_432_ms_timing`, `PlanFrame_emits_256_lines`, and `PlanFrame_maps_color_bar_channels_to_expected_frequencies`. Test through full-width 320/640 frames whose first row contains diagnostic channel values, then assert line duration 446.446 ms and the exact span order. Do not add a test-only short-row path that bypasses production dimension validation.

- [ ] **Step 2: Implement Martin planning and verify its tests**

Implement a streaming iterator with no full-frame span list. Check cancellation at least once per source row and inside component scans.

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj --filter FullyQualifiedName~MartinScanPlannerTests`

Expected: PASS.

- [ ] **Step 3: Write failing Scottie tests**

Add `PlanLine_places_sync_between_blue_and_red`, `PlanFrame_emits_initial_sync_once`, `Scottie1_uses_138_240_ms_scans_and_428_220_ms_lines`, `Scottie2_uses_88_064_ms_scans_and_277_692_ms_lines`, and `PlanFrame_maps_gbr_channels_without_swapping`.

- [ ] **Step 4: Implement Scottie planning and verify its tests**

Share only the GBR scan helper with Martin; keep family-specific placement/timing explicit. Do not reproduce the known shortened-Scottie-scan variant.

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj --filter FullyQualifiedName~ScottieScanPlannerTests`

Expected: PASS.

- [ ] **Step 5: Write and pass horizontal 1×/2× tests**

Add `Multiplier_two_doubles_video_updates_only`, `Multiplier_two_preserves_each_component_and_frame_duration`, and `Multiplier_two_uses_native_640_source_values`. Assert 640 update spans rather than interpolation from a 320-wide frame.

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj`

Expected: all tests PASS with zero warnings.

- [ ] **Step 6: Commit sequential RGB planners**

```powershell
git add src/SstvVPrinter.Sstv/Planning tests/SstvVPrinter.Sstv.Tests
git commit -m "feat: encode Martin and Scottie scan lines"
```

## Task 4: Robot 36 Component Conversion and Paired-Line Planning

**Files:**
- Create: `src/SstvVPrinter.Sstv/Planning/RobotColorConverter.cs`
- Create: `src/SstvVPrinter.Sstv/Planning/Robot36ScanPlanner.cs`
- Modify: `src/SstvVPrinter.Sstv/Planning/SstvScanPlannerFactory.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/RobotColorConverterTests.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/Robot36ScanPlannerTests.cs`

**Interfaces:**
- Consumes: the Task 3 planner contract and Task 1 RGB frame.
- Produces: internal `RobotComponents(byte Y, byte RedDifference, byte BlueDifference)`; `RobotColorConverter.Convert(Rgb24 pixel)` using the spec equations; `RobotColorConverter.Average(byte first, byte second)` with nearest-integer behavior; Robot 36 implementation of `ISstvScanPlanner`.

- [ ] **Step 1: Write failing component-conversion tests**

Pin black, white, 128-gray, red, green, and blue against the exact spec equations after nearest-integer rounding and byte clamping. Add `Average_uses_both_lines_and_rounds_half_up`.

- [ ] **Step 2: Implement conversion and verify its tests**

Use `double` for conversion math and one explicit round/clamp helper.

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj --filter FullyQualifiedName~RobotColorConverterTests`

Expected: PASS.

- [ ] **Step 3: Write failing Robot 36 planner tests**

Add `PlanLine_uses_9_3_88_4_5_1_5_44_ms_timing`, `PlanPair_averages_chrominance_and_alternates_channels`, `Even_line_uses_1500_marker_and_red_difference`, `Odd_line_uses_2300_marker_and_blue_difference`, `PlanFrame_emits_240_lines`, and `Multiplier_two_doubles_y_and_chrominance_updates_without_changing_150_ms_line_time`.

- [ ] **Step 4: Implement paired-line Robot 36 planning**

For rows `(0,1)`, `(2,3)`, and so on, compute pair-averaged chrominance per column. Emit current-row Y, then averaged R-Y for the even row and averaged B-Y for the odd row. At 2× use all 640 native columns.

- [ ] **Step 5: Verify and commit Task 4**

Run: `dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj`

Expected: all tests PASS with zero warnings.

```powershell
git add src/SstvVPrinter.Sstv/Planning tests/SstvVPrinter.Sstv.Tests
git commit -m "feat: encode Robot 36 scan lines"
```

## Task 5: Public Encoder Stream and Atomic WAV Output

**Files:**
- Create: `src/SstvVPrinter.Sstv/SstvEncoder.cs`
- Create: `src/SstvVPrinter.Sstv/Wave/WaveFileWriter.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/SstvEncoderTests.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/WaveFileWriterTests.cs`

**Interfaces:**
- Consumes: VIS planner, scan-planner factory, and PCM renderer from Tasks 1–4.
- Produces: `SstvEncoder.EncodeAsync(RgbFrame frame, SstvEncodeOptions options, CancellationToken cancellationToken = default) -> IAsyncEnumerable<PcmBlock>`; `WaveFileWriter.WriteAsync(string destinationPath, IAsyncEnumerable<PcmBlock> blocks, CancellationToken cancellationToken = default) -> Task`.

- [ ] **Step 1: Write failing public-encoder tests**

Add `EncodeAsync_validates_before_enumeration`, `EncodeAsync_starts_with_matching_vis_header`, `EncodeAsync_generates_all_four_modes`, `EncodeAsync_one_and_two_have_equal_sample_counts`, `EncodeAsync_is_safe_for_two_concurrent_calls`, and `EncodeAsync_cancellation_stops_boundedly`. Use solid-color frames for count tests and a small analyzer that consumes blocks without holding full audio.

- [ ] **Step 2: Implement the public streaming composition**

Make `EncodeAsync` a non-iterator wrapper: validate synchronously when it is called, then return a private async iterator that lazily concatenates VIS spans with the selected scan planner and creates one renderer per enumeration. Do not cache PCM or mutable oscillator state on `SstvEncoder`.

- [ ] **Step 3: Write failing WAV tests**

Add `WriteAsync_writes_riff_pcm_mono_48000_16bit`, `WriteAsync_sets_riff_and_data_lengths`, `WriteAsync_clamps_float_roundoff_before_pcm16_conversion`, `WriteAsync_publishes_atomically`, `WriteAsync_cancellation_and_failure_leave_destination_untouched`, and `WriteAsync_rejects_noncontiguous_or_reordered_pcm_blocks`. Feed the WAV writer a synthetic block just outside `[-1, 1]` to pin defensive conversion behavior; encoder amplitude validation remains independently constrained to `(0, 1]`.

- [ ] **Step 4: Implement atomic streaming WAV output**

Write a 44-byte placeholder header to a unique temporary sibling, stream and convert each block, seek back to finalize RIFF/data lengths, flush and close, then move without overwrite. Reject output exceeding RIFF's 32-bit length fields before publication.

- [ ] **Step 5: Verify and commit Task 5**

Run:

```powershell
dotnet test SstvVPrinter.slnx --configuration Release
dotnet build SstvVPrinter.slnx --configuration Release
```

Expected: all tests PASS; build succeeds with zero warnings.

```powershell
git add src/SstvVPrinter.Sstv tests/SstvVPrinter.Sstv.Tests
git commit -m "feat: stream SSTV audio to atomic WAV files"
```

## Task 6: Color-Bar Generator and Manual Decode Evidence

**Files:**
- Create: `src/SstvVPrinter.SstvTool/SstvVPrinter.SstvTool.csproj`
- Create: `src/SstvVPrinter.SstvTool/Program.cs`
- Create: `src/SstvVPrinter.SstvTool/ColorBarFrame.cs`
- Create: `tests/SstvVPrinter.Sstv.Tests/ColorBarFrameTests.cs`
- Create: `docs/validation/sstv-encoder-runbook.md`
- Create: `docs/validation/sstv-encoder-results.md`
- Modify: `SstvVPrinter.slnx`

**Interfaces:**
- Consumes: Task 5 public encoder and WAV writer.
- Produces: CLI `dotnet run --project src/SstvVPrinter.SstvTool -- --mode <martin1|scottie1|scottie2|robot36> --horizontal <1|2> --output <absolute.wav>`; deterministic RGB color bars; manual validation records.

- [ ] **Step 1: Write failing color-bar tests**

Add `Create_uses_mode_dimensions_and_multiplier`, `Create_contains_black_white_primary_and_gray_bars`, and `Create_is_deterministic`. The fixture must include thin alternating vertical details that distinguish native 640 input from a duplicated 320 pattern.

- [ ] **Step 2: Implement the CLI and generator**

Parse only the exact documented arguments, require an absolute `.wav` destination, generate the matching 1×/2× frame, stream encode it, and print mode, dimensions, sample count, duration, and output path. Return nonzero on validation, I/O, or cancellation failure.

- [ ] **Step 3: Add the CLI project and run automated verification**

Run:

```powershell
dotnet sln SstvVPrinter.slnx add src/SstvVPrinter.SstvTool/SstvVPrinter.SstvTool.csproj
dotnet test SstvVPrinter.slnx --configuration Release
dotnet build SstvVPrinter.slnx --configuration Release
```

Expected: all tests PASS; build succeeds with zero warnings.

- [ ] **Step 4: Write the manual runbook and result matrix**

Document exact commands for all eight WAVs, SHA-256 capture, expected approximate durations, and playback/virtual-audio routing into an existing SSTV receiver. The result table records encoder configuration, receiver/version, detected mode, decoded dimensions, color/channel correctness, sync/slant result, and notes. Never commit generated WAV files.

- [ ] **Step 5: Perform current-machine decode checks**

Generate and decode the four 1× color bars first. If all four pass, repeat at 2× and compare thin vertical details without claiming improvement from subjective inspection alone; retain screenshots or metrics outside Git and record conclusions in the Markdown result matrix.

- [ ] **Step 6: Final verification and commit**

Run:

```powershell
dotnet test SstvVPrinter.slnx --configuration Release
dotnet build SstvVPrinter.slnx --configuration Release
git diff --check
```

Expected: all tests PASS, build has zero warnings/errors, and the diff check is clean.

```powershell
git add SstvVPrinter.slnx src/SstvVPrinter.SstvTool tests/SstvVPrinter.Sstv.Tests docs/validation
git commit -m "feat: add SSTV encoder validation tool"
```

## Completion Gate

Do not connect the encoder to Windows audio playback until the four 1× modes decode correctly in an independent receiver and the automated timing suite is green. A 2× decode failure does not block standard 1× playback work, but it must remain labeled experimental until at least two independent receiver implementations decode it reliably.

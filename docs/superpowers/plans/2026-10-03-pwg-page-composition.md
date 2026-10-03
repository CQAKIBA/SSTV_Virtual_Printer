# PWG Page Composition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decode the first supported PWG Raster page from a Windows IPP print job and deterministically compose it into a header-bearing `RgbFrame` ready for the existing Martin 1, Scottie 1/2, or Robot 36 encoder.

**Architecture:** A new platform-neutral imaging library separates bounded PWG parsing, upright orientation normalization, detail-preserving fitting, deterministic bitmap text, and mode-specific frame composition. A small diagnostic CLI writes portable PPM previews for captured-job checks without coupling the library to Windows, IPP hosting, GUI frameworks, installed fonts, or image codecs.

**Tech Stack:** C# 14, .NET 10 LTS, xUnit; BCL only at runtime; existing `SstvVPrinter.Sstv` RGB and mode contracts.

**Spec:** `docs/superpowers/specs/2026-10-03-pwg-page-composition-design.md`

**Protocol references:** [CUPS Raster Format](https://openprinting.github.io/cups/libcups/spec-raster.html) and [PWG 5102.4 IPP Raster](https://ftp.pwg.org/pub/pwg/candidates/cs-ippraster10-20120420-5102.4.pdf).

## Global Constraints

- Decode only the first complete `RaS2` page and leave the caller's stream open.
- Accept only chunky sRGB RGB24: 8 bits/color, 24 bits/pixel, 3 colors, color order 0, color space 19, and `bytesPerLine == width * 3`.
- Enforce positive dimensions no larger than 20,000 per axis, at most 50,000,000 pixels, and checked byte/allocation arithmetic. These limits include A4 at 300 dpi while bounding hostile input.
- PWG page headers are exactly 1,796 bytes after the four-byte sync word. Integer fields are big-endian.
- Each raster record starts with a row repetition byte representing `count - 1`. Within its encoded row, controls 0–127 repeat one RGB color `control + 1` times; controls 129–255 copy `257 - control` literal RGB colors; control 128 is invalid. Runs must end at exactly `width` colors, and row repeats must end at or before `height`.
- Normalize orientation before fitting: 0 is identity; 1 is 90 degrees counter-clockwise; 2 is 180 degrees; 3 is 90 degrees clockwise. Orientations 1 and 3 swap logical dimensions.
- Fit the entire upright page without cropping or aspect distortion; unused pixels are black.
- Render 2× requests directly at 640 pixels wide; never enlarge a 320-pixel intermediate result.
- The 16-row identification header, timestamp fallback order, Robot 36 overlay rule, and callsign priority are exactly as specified in the approved design.
- All public pixel objects are immutable. Parsing, fitting, and composition publish no partial result on failure or cancellation.
- No Windows drawing APIs, `System.Drawing`, native codecs, external fonts, GUI, IPP listener integration, playback, settings, PTT, or optional comment field in this phase.

## Review Focus

- RLE control 128, exact scanline length, and a repeated row extending beyond page height must be rejected: `PwgRasterDecoderTests.Decode_rejects_reserved_or_overlong_runs` pins this in Task 2.
- Clockwise and counter-clockwise orientation must not be interchanged: `PwgOrientationTests.Decode_maps_all_orientations_with_asymmetric_markers` pins exact coordinate mappings in Task 3.
- Extreme aspect ratios must still produce a nonzero fitted axis without cropping: `DetailPreservingResamplerTests.Fit_extreme_aspect_ratios_keeps_one_visible_axis_pixel` pins this in Task 4.
- The detail enhancement must retain isolated black, white, and colored marks but stay bounded on gradients: `DetailPreservingResamplerTests.Downscale_retains_isolated_details` and `Downscale_does_not_overdrive_smooth_gradients` pin this in Task 4.
- Header layout must preserve the complete callsign and choose metadata fallback deterministically at 1× and 2×: `HeaderRendererTests.Layout_falls_back_before_colliding_with_callsign` pins this in Task 5.
- Robot 36 must choose a dedicated bar for exactly 16 top padding rows but overlay for 15: `SstvFrameComposerTests.Robot36_uses_padding_only_when_top_gap_is_at_least_sixteen` pins this in Task 6.

---

## Task 1: Imaging Project and Immutable Contracts

**Files:**
- Create: `src/SstvVPrinter.Imaging/SstvVPrinter.Imaging.csproj`
- Create: `src/SstvVPrinter.Imaging/Model/DecodedPage.cs`
- Create: `src/SstvVPrinter.Imaging/Model/PwgPageMetadata.cs`
- Create: `src/SstvVPrinter.Imaging/Model/PixelRect.cs`
- Create: `src/SstvVPrinter.Imaging/Composition/CompositionRequest.cs`
- Create: `src/SstvVPrinter.Imaging/Composition/HeaderPlacement.cs`
- Create: `tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj`
- Create: `tests/SstvVPrinter.Imaging.Tests/ModelTests.cs`
- Create: `tests/SstvVPrinter.Imaging.Tests/CompositionRequestTests.cs`
- Modify: `SstvVPrinter.slnx`

**Interfaces:**
- `DecodedPage.Create(int width, int height, ReadOnlySpan<byte> rgb24, PwgPageMetadata metadata)` copies input bytes and exposes `GetPixel(int x, int y)`.
- `PwgPageMetadata` records physical width/height, logical width/height, DPI, orientation, and source page dimensions in points.
- `PixelRect(int X, int Y, int Width, int Height)` validates nonnegative origin and positive extent.
- `CompositionRequest(SstvMode Mode, int HorizontalSampleMultiplier, string CallSign, DateTimeOffset AcceptedAt, HeaderPlacement Placement = Top)` normalizes the call sign to invariant uppercase and validates all configuration synchronously.

- [ ] **Step 1: Write failing immutable-page and rectangle tests**

Add tests that the page copies caller bytes, returns row-major RGB, rejects invalid dimensions/byte count, and rejects out-of-range coordinates. Add rectangle validation and checked right/bottom calculations.

- [ ] **Step 2: Run focused tests and confirm failure**

Run: `dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj --filter "FullyQualifiedName~ModelTests"`

Expected: FAIL because the project and contracts do not exist.

- [ ] **Step 3: Implement the immutable image contracts**

Use checked `width * height * 3`, copy all caller memory, and expose no writable backing buffer. Keep metadata descriptive; do not let it alter decoded pixels after creation.

- [ ] **Step 4: Write failing composition-request tests**

Cover all four modes, multipliers 1/2, lowercase-to-uppercase normalization, empty/unsupported call signs, unsupported multiplier/mode, and rejection of placement values other than `Top`. Accept only `A-Z`, `0-9`, space, slash, hyphen, colon, and period so every accepted character has a bundled glyph.

- [ ] **Step 5: Implement request validation and add projects to the solution**

Reference `SstvVPrinter.Sstv` from the imaging library and imaging tests. Add both new projects to `SstvVPrinter.slnx`.

- [ ] **Step 6: Verify and commit Task 1**

Run:

```powershell
dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj
dotnet build SstvVPrinter.slnx --configuration Release
```

Expected: all Task 1 tests PASS; build has zero warnings/errors.

```powershell
git add SstvVPrinter.slnx src/SstvVPrinter.Imaging tests/SstvVPrinter.Imaging.Tests
git commit -m "feat: add imaging composition contracts"
```

## Task 2: Bounded PWG Header Parsing and Row Decompression

**Files:**
- Create: `src/SstvVPrinter.Imaging/Pwg/PwgRasterException.cs`
- Create: `src/SstvVPrinter.Imaging/Pwg/PwgRasterHeader.cs`
- Create: `src/SstvVPrinter.Imaging/Pwg/PwgRasterDecoder.cs`
- Create: `tests/SstvVPrinter.Imaging.Tests/Pwg/PwgFixtureBuilder.cs`
- Create: `tests/SstvVPrinter.Imaging.Tests/Pwg/FragmentedReadStream.cs`
- Create: `tests/SstvVPrinter.Imaging.Tests/Pwg/PwgRasterDecoderTests.cs`

**Interfaces:**
- `PwgRasterDecoder.DecodeFirstPageAsync(Stream source, CancellationToken cancellationToken = default) -> ValueTask<DecodedPage>`.
- `PwgRasterHeader` is internal and parses only named big-endian fields at offsets 276/280 (DPI), 344 (orientation), 352/356 (page points), 372/376 (pixel dimensions), 384 (bits/color), 388 (bits/pixel), 392 (bytes/line), 396 (color order), 400 (color space), and 420 (number of colors).
- `PwgFixtureBuilder` exists only in tests and emits independently readable compact `RaS2` documents.

- [ ] **Step 1: Write failing header/profile tests**

Add `Decode_accepts_supported_srgb24_header`, `Decode_reads_big_endian_metadata`, and table-driven rejection tests for wrong sync, incomplete 1,796-byte header, zero/oversized dimensions, excessive pixel count, wrong bytes/line, bits/color, bits/pixel, color order, color space, color count, orientation, and overflow. Assert diagnostic messages name the rejected field.

- [ ] **Step 2: Implement exact asynchronous reads and header validation**

Use a local `ReadExactlyAsync` loop because one `ReadAsync` may return fewer bytes than requested. Do not require seekability. Validate all header fields and checked allocation sizes before renting or allocating page storage.

- [ ] **Step 3: Write failing RLE tests**

Cover repeated-color controls 0 and 127, literal controls 129 and 255, mixed runs ending exactly at width, repeated raster rows for counts 1 and 256, byte-at-a-time reads, truncation at every token position, reserved control 128, short/long rows, row repeats beyond remaining height, and cancellation while reading rows. Include `Decode_rejects_reserved_or_overlong_runs` exactly.

- [ ] **Step 4: Implement streaming row decompression**

Decode one physical row into a `width * 3` buffer, copy it into each repeated destination row, and check cancellation between rows and within long token loops. Reject a token before it crosses the remaining pixel count; require the decoded row to finish exactly at width. Stop immediately after the first page reaches declared height and do not inspect a second page.

- [ ] **Step 5: Add first-page and stream-ownership tests**

Add a two-page fixture whose second page is deliberately malformed and assert the first is returned. Wrap the source to record disposal and assert the decoder leaves it open on success, failure, and cancellation.

- [ ] **Step 6: Verify and commit Task 2**

Run: `dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj --filter "FullyQualifiedName~PwgRasterDecoderTests"`

Expected: all decoder tests PASS with zero warnings.

```powershell
git add src/SstvVPrinter.Imaging/Pwg tests/SstvVPrinter.Imaging.Tests/Pwg
git commit -m "feat: decode bounded PWG raster rows"
```

## Task 3: Upright Orientation Normalization

**Files:**
- Create: `src/SstvVPrinter.Imaging/Pwg/PwgOrientationNormalizer.cs`
- Modify: `src/SstvVPrinter.Imaging/Pwg/PwgRasterDecoder.cs`
- Create: `tests/SstvVPrinter.Imaging.Tests/Pwg/PwgOrientationTests.cs`

**Interfaces:**
- Internal `PwgOrientationNormalizer.Normalize(int physicalWidth, int physicalHeight, int orientation, ReadOnlySpan<byte> physicalRgb) -> (int Width, int Height, byte[] Rgb24)`.
- Destination-to-source mappings are exact: orientation 0 `(x,y)`; orientation 1 `(W-1-y,x)`; orientation 2 `(W-1-x,H-1-y)`; orientation 3 `(y,H-1-x)`.

- [ ] **Step 1: Write failing asymmetric orientation tests**

Use a non-square 2×3 source with six unique RGB markers. Add `Decode_maps_all_orientations_with_asymmetric_markers`, asserting every output pixel, output dimensions, and unchanged physical metadata for orientations 0–3.

- [ ] **Step 2: Run the orientation tests and confirm failure**

Run: `dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj --filter FullyQualifiedName~PwgOrientationTests`

Expected: orientation 1–3 cases FAIL before normalization exists.

- [ ] **Step 3: Implement normalization after physical decompression**

Allocate exactly one complete logical output buffer after the bounded physical page has decoded. Apply the documented destination-to-source mapping with checked offsets, update logical dimensions in metadata, and discard the physical buffer after returning the immutable page.

- [ ] **Step 4: Verify and commit Task 3**

Run: `dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj`

Expected: all tests PASS.

```powershell
git add src/SstvVPrinter.Imaging/Pwg tests/SstvVPrinter.Imaging.Tests/Pwg
git commit -m "feat: normalize PWG page orientation"
```

## Task 4: Detail-Preserving Aspect-Fit Resampler

**Files:**
- Create: `src/SstvVPrinter.Imaging/Resampling/FittedImage.cs`
- Create: `src/SstvVPrinter.Imaging/Resampling/SrgbTransfer.cs`
- Create: `src/SstvVPrinter.Imaging/Resampling/DetailPreservingResampler.cs`
- Create: `tests/SstvVPrinter.Imaging.Tests/Resampling/DetailPreservingResamplerTests.cs`

**Interfaces:**
- `DetailPreservingResampler.Fit(DecodedPage source, int targetWidth, int targetHeight, CancellationToken cancellationToken = default) -> FittedImage`.
- `FittedImage` owns an immutable RGB24 target canvas and reports the exact centered `ContentBounds`.

**Pinned algorithm:**
- Determine fitted dimensions with rational cross-products, round to nearest, clamp each visible axis to at least one pixel, and center with integer floor on the leading side.
- For shrinking, integrate exact source-pixel coverage for each destination footprint after converting channels through a 256-entry sRGB-to-linear lookup table.
- Track the source color with the largest absolute linear-luminance distance from the coverage-weighted mean. If the footprint covers more than one source pixel and that distance is at least 0.125, blend 20% of that extremal color into the area mean; otherwise use the mean unchanged.
- Convert to sRGB, then apply a 3×3 luminance-only unsharp mask inside the fitted bounds with amount 0.20, threshold 4/255, and clamp the luminance delta to ±0.10 before scaling RGB proportionally. Do not alter hue or saturation directly.
- For enlargement, use bilinear interpolation in linear light and skip extremum enhancement; apply the same bounded sharpening only when at least one axis was reduced.

- [ ] **Step 1: Write failing geometry and letterbox tests**

Cover matching, portrait, landscape, square, extremely wide, and extremely tall sources. Assert no crop, centered bounds, black outside pixels, deterministic rounding, and `Fit_extreme_aspect_ratios_keeps_one_visible_axis_pixel` for dimensions where naive integer division would produce zero.

- [ ] **Step 2: Implement fit geometry and plain linear-light area/bilinear sampling**

Keep accumulation order fixed (`y`, then `x`) and use `double` so output is byte-identical across repeated calls. Check cancellation at least once per destination row and periodically within wide footprint loops.

- [ ] **Step 3: Write failing detail-retention and safety tests**

Add black-on-white and white-on-black one-pixel strokes, a one-pixel primary-color mark, uniform fields, a smooth grayscale ramp, color gradients, direct 640-wide rendering, determinism, and cancellation. Compare against a test-only pure area average. Require isolated details to differ from their background by more than pure averaging does, while uniform fields remain exact and smooth-gradient channel error stays within the pinned enhancement bounds.

- [ ] **Step 4: Implement bounded extremum contribution and luminance sharpening**

Use Rec. 709 linear luminance coefficients 0.2126/0.7152/0.0722. Keep the pre-sharpen image separate so scan order cannot feed modified pixels back into the filter. Clamp every intermediate and final channel; do not permit NaN or infinity.

- [ ] **Step 5: Verify and commit Task 4**

Run: `dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj --filter FullyQualifiedName~DetailPreservingResamplerTests`

Expected: all resampler tests PASS with zero warnings.

```powershell
git add src/SstvVPrinter.Imaging/Resampling tests/SstvVPrinter.Imaging.Tests/Resampling
git commit -m "feat: add detail-preserving page fitting"
```

## Task 5: Deterministic Bitmap Font and Header Layout

**Files:**
- Create: `src/SstvVPrinter.Imaging/Header/BitmapFont5x7.cs`
- Create: `src/SstvVPrinter.Imaging/Header/HeaderLayout.cs`
- Create: `src/SstvVPrinter.Imaging/Header/HeaderRenderer.cs`
- Create: `tests/SstvVPrinter.Imaging.Tests/Header/BitmapFontTests.cs`
- Create: `tests/SstvVPrinter.Imaging.Tests/Header/HeaderRendererTests.cs`

**Interfaces:**
- Internal bundled `BitmapFont5x7` supports `A-Z`, `0-9`, space, slash, hyphen, colon, and period.
- Glyphs use five data columns plus one blank advance column and seven source rows doubled vertically, producing a 6×14 logical cell inside the 16-row header.
- `HeaderRenderer.MeasureAndSelect(CompositionRequest request, int canvasWidth) -> HeaderLayout` chooses one metadata form before drawing.
- `HeaderRenderer.Draw(Span<byte> rgb24, int width, int height, HeaderLayout layout)` draws opaque patches/text without font APIs.

- [ ] **Step 1: Write failing font tests**

Assert every accepted request character has a glyph, lowercase is never needed after request normalization, known glyph masks (`A`, `1`, `:`, `-`) are exact, row 0/15 margins stay blank for text, and rendering is deterministic.

- [ ] **Step 2: Implement the bundled font**

Store glyph rows as compile-time byte masks and render nearest-neighbor with horizontal scale 1 or 2 and vertical scale 2. Do not antialias. Unknown glyph lookup is an internal invariant failure rather than a replacement-character path.

- [ ] **Step 3: Write failing header layout tests**

Pin 1× patches to three consecutive 16×16 blocks and 2× patches to three 32×16 blocks. Pin black text, exact primaries, left-aligned metadata, right-aligned callsign, fixed JST conversion, labels `M1`/`S1`/`S2`/`R36`, and fallback order:

1. `yyyy-MM-dd HH:mm JST <mode>`
2. `MM-dd HH:mm JST <mode>`
3. `HH:mm JST <mode>`
4. `JST <mode>`

Use exact-fit, one-pixel-collision, and too-long-callsign cases at both multipliers. Add `Layout_falls_back_before_colliding_with_callsign`; assert callsign bytes are never truncated or shifted left of their right margin.

- [ ] **Step 4: Implement measurement-first header layout**

Convert `AcceptedAt` to fixed `TimeSpan.FromHours(9)` using `ToOffset`, independent of host locale/time-zone data. Scale every horizontal margin, gap, glyph, and patch by the multiplier; keep height and vertical text geometry at 16/14 pixels. Require at least one scaled blank cell between metadata and callsign. Reject the request before any canvas mutation if the shortest layout cannot fit.

- [ ] **Step 5: Verify and commit Task 5**

Run: `dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj --filter "FullyQualifiedName~BitmapFontTests|FullyQualifiedName~HeaderRendererTests"`

Expected: all header tests PASS.

```powershell
git add src/SstvVPrinter.Imaging/Header tests/SstvVPrinter.Imaging.Tests/Header
git commit -m "feat: render deterministic SSTV headers"
```

## Task 6: Mode-Specific SSTV Frame Composition

**Files:**
- Create: `src/SstvVPrinter.Imaging/Composition/SstvFrameComposer.cs`
- Create: `tests/SstvVPrinter.Imaging.Tests/Composition/SstvFrameComposerTests.cs`

**Interfaces:**
- `SstvFrameComposer.Compose(DecodedPage page, CompositionRequest request, CancellationToken cancellationToken = default) -> RgbFrame`.
- Constructor accepts a `DetailPreservingResampler`; production convenience constructor uses the default implementation.

- [ ] **Step 1: Write failing Martin/Scottie composition tests**

For M1/S1/S2 at 1× and 2×, assert exact 320/640×256 output, white header rows 0–15, content target beginning at row 16 with height 240, black letterboxing, correct page pixels, direct 640-wide rendering, and complete header foreground. Assert invalid header layout is rejected before invoking a spy resampler.

- [ ] **Step 2: Implement sequential-RGB composition**

Select canvas dimensions from `SstvModes.Get` and multiplier. Fit into `(0,16,width,240)`, copy the fitted target there, render the opaque-white header, then create the existing immutable `RgbFrame`.

- [ ] **Step 3: Write failing Robot 36 composition tests**

Cover 1×/2× output size 320/640×240, full-canvas fitting, a top natural gap of exactly 16 rows, a top gap of 15 rows, no gap, and odd channel values. Add `Robot36_uses_padding_only_when_top_gap_is_at_least_sixteen`. For overlay cases assert each channel is integer `(c + 255) / 2` (equivalent to nearest rounding here), while patches and text remain fully opaque. Assert choosing the header treatment never changes `ContentBounds`.

- [ ] **Step 4: Implement Robot 36 header treatment**

If `ContentBounds.Y >= 16`, replace rows 0–15 with opaque white; otherwise whiten those rows in place with `(channel + 255) / 2`. Draw the same foreground layout afterward. Never request a second fit or reserve header space.

- [ ] **Step 5: Add deterministic, cancellation, and encoder-contract tests**

Assert repeated compositions are byte-identical, cancellation during fitting returns no frame, all results satisfy `SstvEncodeOptions.Validate`, and composing one request concurrently on separate tasks has no shared mutable state.

- [ ] **Step 6: Verify and commit Task 6**

Run:

```powershell
dotnet test tests/SstvVPrinter.Imaging.Tests/SstvVPrinter.Imaging.Tests.csproj
dotnet test tests/SstvVPrinter.Sstv.Tests/SstvVPrinter.Sstv.Tests.csproj
```

Expected: all imaging and existing encoder tests PASS.

```powershell
git add src/SstvVPrinter.Imaging/Composition tests/SstvVPrinter.Imaging.Tests/Composition
git commit -m "feat: compose pages into SSTV frames"
```

## Task 7: PPM Preview Tool and Captured-PWG Validation

**Files:**
- Create: `src/SstvVPrinter.ImagingTool/SstvVPrinter.ImagingTool.csproj`
- Create: `src/SstvVPrinter.ImagingTool/Program.cs`
- Create: `src/SstvVPrinter.ImagingTool/PpmWriter.cs`
- Create: `tests/SstvVPrinter.Imaging.Tests/Tool/PpmWriterTests.cs`
- Create: `docs/validation/pwg-page-composition-runbook.md`
- Create: `docs/validation/pwg-page-composition-results.md`
- Modify: `SstvVPrinter.slnx`

**Interfaces:**
- CLI: `dotnet run --project src/SstvVPrinter.ImagingTool -- --input <absolute.pwg> --mode <martin1|scottie1|scottie2|robot36> --horizontal <1|2> --callsign <text> --accepted-at <ISO-8601> --output <absolute.ppm>`.
- PPM output is binary P6, maximum 255, row-major RGB, written to a temporary sibling and published without overwrite only after success.

- [ ] **Step 1: Write failing PPM writer tests**

Assert exact P6 header/payload, dimensions, RGB order, destination collision handling, and cancellation/failure leaving no published partial file.

- [ ] **Step 2: Implement the diagnostic CLI**

Parse only documented arguments, require absolute `.pwg` and `.ppm` paths, decode and compose one page, atomically write the preview, and print physical/logical dimensions, orientation, selected metadata form, fitted content bounds, mode, multiplier, and output path. Return nonzero with a concise field-specific error for invalid PWG, request, cancellation, or I/O failure.

- [ ] **Step 3: Add the project and automated tool tests**

Run:

```powershell
dotnet sln SstvVPrinter.slnx add src/SstvVPrinter.ImagingTool/SstvVPrinter.ImagingTool.csproj
dotnet test SstvVPrinter.slnx --configuration Release
dotnet build SstvVPrinter.slnx --configuration Release
```

Expected: all tests PASS; build has zero warnings/errors.

- [ ] **Step 4: Write the manual runbook and result table**

Document commands for one captured portrait PWG and the captured Acrobat Orientation=1 landscape PWG at representative modes and both widths. Record SHA-256, decoded physical/logical sizes, orientation, content bounds, header treatment, visual uprightness, aspect preservation, line/text legibility, and notes. Do not commit captured PWG or generated PPM files.

- [ ] **Step 5: Perform local captured-job checks**

Generate previews from the existing private captures. Inspect that the portrait page is upright, Orientation=1 is rotated in the correct direction, no page edge is cropped, the full callsign is present, Robot overlay/padding behavior matches reported bounds, and 2× contains independently resampled horizontal detail.

- [ ] **Step 6: Final verification and commit**

Run:

```powershell
dotnet test SstvVPrinter.slnx --configuration Release
dotnet build SstvVPrinter.slnx --configuration Release
git diff --check
```

Expected: all tests PASS, build has zero warnings/errors, diff check is clean, and both private captured-job previews pass visual inspection.

```powershell
git add SstvVPrinter.slnx src/SstvVPrinter.ImagingTool tests/SstvVPrinter.Imaging.Tests docs/validation
git commit -m "feat: add PWG composition preview validation"
```

## Completion Gate

Do not connect composition to the live IPP job path or begin GUI work until synthetic decoder/compositor tests are green and both a captured portrait page and Orientation=1 landscape page produce visually correct previews. A failure in the experimental 2× appearance does not block the standard 1× path, but it must be recorded before live integration.

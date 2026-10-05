# PWG Page Decoding and SSTV Frame Composition Design

## Purpose

Convert the first page of a Windows IPP print job into the immutable `RgbFrame` consumed by the existing SSTV encoder. The result preserves the complete printed page, favors legibility of short text and line art during aggressive downscaling, and always adds an early-scan identification header.

This phase is platform-neutral .NET code. It does not contain the IPP listener, job queue, GUI, settings persistence, audio playback, or PTT control. Keeping these boundaries allows the same decoder and compositor to run on Windows, Linux, and Raspberry Pi.

## Inputs and outputs

The decoder consumes a seekable or streaming PWG Raster document and returns the first decoded page as immutable RGB24 pixels plus source metadata needed for diagnostics. The compositor consumes that page and a fully specified composition request:

- SSTV mode: Martin 1, Scottie 1, Scottie 2, or Robot 36;
- horizontal sampling multiplier: 1 or 2;
- call sign;
- print-job acceptance timestamp;
- initial header placement, fixed to `Top` in this phase.

The compositor returns an existing `RgbFrame` at the encoder's exact required dimensions. Optional comments remain deferred.

## Supported PWG Raster profile

The IPP endpoint advertises one canonical input profile, and the decoder accepts that profile rather than attempting to be a general-purpose raster converter:

- PWG/CUPS big-endian `RaS2` sync word;
- `srgb_8` document type;
- 8 bits per color and 24 bits per pixel;
- three-channel RGB byte order;
- the PWG row compression used by the Microsoft IPP Class Driver;
- orientation values 0, 1, 2, and 3;
- first page only.

The page header is parsed using bounded, exact reads. Width, height, bytes per line, color space, resolution, and orientation are validated before allocating the decoded image. Checked arithmetic prevents overflow. Limits must comfortably accept A4 at 300 dpi while rejecting unreasonable dimensions, line sizes, decompressed byte counts, repeat counts, and allocation sizes.

Unsupported color spaces, bit depths, malformed runs, short rows, long rows, truncated streams, and incomplete headers fail with a diagnostic exception. A partially decoded page is never returned.

The decoder stops after the first complete page. It does not need to decode or retain subsequent pages because the product intentionally sends the first page selected by the printing application.

## Orientation normalization

The decoder exposes a logically upright page. It maps all four PWG orientation values into an explicit pixel-coordinate transform and swaps logical width and height for the two landscape orientations. Tests use asymmetric corner colors and markers so clockwise versus counterclockwise mistakes cannot pass.

Orientation normalization happens before fitting. The fitter and header compositor therefore operate without PWG-specific orientation rules.

## Detail-preserving page fitting

The printed page is fitted uniformly into a target rectangle without cropping or changing aspect ratio. Any unused space is neutral black.

The default downscaler is optimized for quickly prepared PC messages, Office documents, diagrams, text, and line art rather than purely photographic fidelity:

1. Accumulate source coverage for every destination pixel using area resampling in a linear-light working space.
2. Track local luminance extrema and chromatic detail within the same source footprint.
3. Preserve a restrained contribution from isolated dark-on-light strokes, light-on-dark strokes, and small colored marks that ordinary bilinear or pure averaging would erase.
4. Apply a weak luminance-only sharpening step after resampling.
5. Clamp the result without introducing overshoot or artificially saturating photographs.

The enhancement is deliberately conservative. It does not attempt OCR, vector reconstruction, or recovery of text that is fundamentally below the SSTV canvas resolution. A future photo-priority resampler may be added without changing the compositor contract.

For a 2× request, the page is rendered directly at the native 640-pixel width. It is not rendered at 320 pixels and interpolated afterward.

## Canvas geometry

### Martin 1 and Scottie 1/2

- 1× output: 320×256; 2× output: 640×256.
- The header occupies rows 0 through 15.
- The page content rectangle is 320×240 or 640×240 beginning at row 16.
- The page is fitted into that rectangle with black letterboxing as needed.

This preserves a 320×240 source without vertical reduction when its aspect ratio already matches the content rectangle.

### Robot 36

- 1× output: 320×240; 2× output: 640×240.
- The page is first fitted into the full output canvas without reserving header height.
- If the fitted image leaves at least 16 complete rows of unused space at the top, rows 0 through 15 become an opaque white header area within that natural letterbox space.
- Otherwise, rows 0 through 15 receive a 50% white overlay over the fitted image: each underlying channel `c` becomes the nearest integer to `(c + 255) / 2`.
- Header foreground elements are then drawn opaquely.

The Robot 36 header never changes page scale, aspect ratio, crop, or position merely to create header room.

## Header layout

The header is exactly 16 pixels high in every mode. Its left-to-right logical layout is:

```text
[RED][GREEN][BLUE]  timestamp timezone mode                    CALLSIGN
```

At 1×, each RGB reference patch is 16×16 pixels. At 2×, all horizontal geometry doubles while vertical geometry stays unchanged: each patch is 32×16, glyphs double horizontally, and gaps and margins double horizontally. This preserves the same received appearance when twice as many horizontal modulation updates occupy the same scan time.

The dedicated Martin/Scottie header has an opaque white background. Robot 36 uses either the opaque white natural-letterbox header or the defined 50% white overlay. Text is opaque black; RGB patches retain exact opaque primary colors.

Text uses a bundled deterministic bitmap font. It does not depend on installed fonts, locale rendering, antialiasing APIs, Windows graphics APIs, or native libraries. The supported header character set covers uppercase Latin letters, digits, space, slash, hyphen, colon, and the punctuation required by the timestamp. The call sign is normalized to uppercase and validated against that set.

The timestamp is the print job's acceptance time converted to the fixed UTC+09:00 Japan Standard Time offset without consulting the host's time-zone database. The full layout is `yyyy-MM-dd HH:mm JST <mode>`, where mode is `M1`, `S1`, `S2`, or `R36`. It does not change if composition or transmission happens later.

The metadata starts immediately to the right of the RGB patches and is left-aligned. The call sign is right-aligned and has priority. If the two text regions would collide, metadata falls back in this order: `MM-dd HH:mm JST <mode>`, `HH:mm JST <mode>`, then `JST <mode>`. The call sign is never truncated. A request whose validated call sign still cannot fit with the shortest metadata layout is rejected before composition.

The model retains a future `Left` or `Auto` placement boundary, but this phase renders only `Top`. A future left-side implementation will transform the layout so the call-sign end is transmitted first.

## Component boundaries

### `PwgRasterDecoder`

Consumes a byte stream and produces one immutable decoded RGB page plus diagnostic metadata. It owns PWG parsing, decompression, bounds checks, and orientation normalization. It has no SSTV-mode, fitting, header, UI, or file-system behavior.

### `DetailPreservingResampler`

Consumes immutable RGB pixels and a target rectangle and produces fitted RGB pixels plus exact placement bounds. It owns aspect-ratio calculation, area coverage, detail retention, sharpening, and black letterboxing. It has no PWG or header knowledge.

### `SstvFrameComposer`

Consumes a decoded page, composition request, resampler, and deterministic bitmap font. It selects the mode canvas, invokes fitting, chooses the Robot 36 header treatment from the returned placement bounds, draws the header, and returns `RgbFrame`. It has no IPP, encoder, audio, or GUI behavior.

## Error handling and cancellation

Configuration errors such as an empty or unsupported call sign, unsupported mode, unsupported multiplier, or unrepresentable header are rejected before expensive processing. Decoder and resampler operations accept cancellation and check it at bounded intervals, including within scanline and output-row loops.

All returned image objects are complete and immutable. Failure or cancellation publishes no partially composed frame and leaves input streams owned by their caller.

## Verification

Automated tests use compact synthetic PWG documents and independently derived literal pixel expectations. They cover:

- literal and repeated PWG runs, repeated raster rows, and reads split at every byte boundary;
- exact rejection of malformed, truncated, oversized, overlong, and unsupported page data;
- all four orientations using asymmetric corner markers;
- first-page selection from a multi-page stream;
- fitting of portrait, landscape, square, extremely wide, and extremely tall inputs without cropping;
- retention of representative one-source-pixel black, white, and colored details compared with plain area averaging;
- bounded sharpening without channel clipping or photo-wide saturation shifts;
- exact Martin/Scottie header and content bounds;
- Robot 36 natural-letterbox selection and 50% white-overlay arithmetic;
- exact RGB patches, metadata ordering, mode labels, right-aligned call sign, collision fallback, and rejection when fitting is impossible;
- corresponding 1×/2× header geometry and native 640-pixel page rendering;
- deterministic byte-identical output across repeated calls and operating systems;
- cancellation during decoding and resampling.

Captured Office, Paint, and Acrobat PWG files may contain private content and are not committed. They are used only for local manual integration checks. The initial integration gate requires at least one captured portrait job and one Orientation=1 landscape job to decode, fit, and produce visually correct composed frames.

## Out of scope

- IPP listener integration and job queue state;
- GUI and preview controls;
- settings persistence;
- optional comments;
- crop, fill, alignment, zoom, and photo-priority modes;
- automatic or left-side header placement;
- audio-device playback and PTT.

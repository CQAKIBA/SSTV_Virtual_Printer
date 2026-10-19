# Radiofax transmit modes design

Date: 2026-10-19

## Purpose

Add standards-compatible analogue Radiofax transmission to SSTV Virtual Printer without changing the existing print-to-preview-to-send workflow. The feature is intended for documents, line art, illustrations, and other printable material, and must be receivable by existing amateur-radio and weather-fax software.

The first release supports one received print page. Multi-page input is a future extension, not part of this design.

## User workflow

1. Print a document, normally in portrait orientation, to **SSTV Virtual Printer**.
2. The application accepts and previews the first page exactly as it does for SSTV.
3. Correct the source orientation with the existing −90° and +90° controls when needed.
4. Select either **Radiofax 120/288** or **Radiofax 120/576**.
5. Confirm the grayscale preview and header.
6. Use the existing **Send** or **Save WAV** action.

The queue, rotation controls, audio endpoint, volume monitoring, cancellation, playback, and WAV export remain common to SSTV and Radiofax.

## Supported modes

| Display name | Lines per minute | IOC | Total line grid | Active image width | Intended use |
| --- | ---: | ---: | ---: | ---: | --- |
| Radiofax 120/288 | 120 | 288 | 904 | 864 | Faster experiments and lower-detail documents |
| Radiofax 120/576 | 120 | 576 | 1809 | 1728 | Standard high-detail weather-fax output |

The internal line widths are implementation sampling grids for composing and previewing the continuous 0.5-second scan. They are not pixel counts transmitted as digital metadata. A receiver may resample the continuous line at a different display width. The difference between the total grid and active width represents the approximately 4.5% dead sector specified by WMO.

The optional SSTV **Horizontal resolution 2x** feature does not apply to Radiofax. Selecting either Radiofax mode forces an effective multiplier of 1 and disables the checkbox. The settings file retains the user's most recent SSTV 1x/2x preference even while a Radiofax mode is selected, so it is restored when an SSTV mode is selected again, including after an application restart.

## Signal sequence

The generated waveform uses a 48 kHz PCM sample rate and the existing application amplitude setting. Each Radiofax transmission contains these sections in order:

1. **IOC selection, 5 seconds**
   - IOC 576: alternating black and white at 300 Hz.
   - IOC 288: alternating black and white at 675 Hz.
   - Transitions are rectangular at tone-span level.
2. **Phasing, 30 seconds**
   - 120 LPM gives a 2 Hz line rate and exactly 0.5 seconds, or 24,000 PCM samples, per line.
   - Use the WMO asymmetric phasing waveform: white for 5% followed by black for 95% of each line.
   - The leading edge of white defines the phase reference.
3. **Image scan, variable length**
   - Scan each line from left to right and subsequent lines from top to bottom.
   - Begin each line with a white dead sector occupying 4.5% of the exact line duration, then scan the active image during the remaining 95.5%.
   - Map luminance linearly to instantaneous subcarrier frequency.
4. **Stop signal, 5 seconds**
   - Alternate black and white at 450 Hz.
5. **Black trailer, 10 seconds**
   - Transmit continuous black.

FM subcarrier values are:

- black: 1500 Hz;
- centre: 1900 Hz;
- white: 2300 Hz.

Intermediate luminance is mapped linearly between 1500 and 2300 Hz. The eight-step header reference uses the nearest 8-bit luminance values to the WMO intermediate-tone targets 1500, 1614, 1729, 1843, 1957, 2071, 2186, and 2300 Hz. Quantization may differ from a target by at most 1.1 Hz.

Fractional timing is rendered with accumulated-error distribution so every complete image line, including its dead sector, contains exactly 24,000 PCM samples regardless of the composition width. The complete WAV and live playback consume the same PCM artifact.

## Image composition

The decoded and manually rotated PWG Raster page is converted to grayscale using a deterministic perceptual-luminance conversion. It is fitted to the 864- or 1728-sample active Radiofax width while preserving its aspect ratio. The vertical dimension is derived from that fit and remains variable; no fixed page height, crop, or stretch is introduced. The dead sector is a signal-level margin and is not shown as part of the printable page in the composition preview.

The existing detail-preserving downsampling policy is reused. Radiofax processing does not apply thresholding, dithering, sharpening, or halftoning. A source application may prepare true black-and-white artwork when desired, while photographs and illustrations retain grayscale.

The preview displays the exact composed grayscale raster consumed by the encoder. Mode or rotation changes regenerate that raster through the existing preparation pipeline.

## Page-shape and duration safety

Radiofax has no fixed vertical page length, so an unchecked source dimension could create an hours-long transmission and a very large in-memory PCM artifact. Validate the normalized, manually rotated source page before Radiofax composition:

- the ratio of the longer side to the shorter side must be no greater than 2.0;
- equivalently, height divided by width must remain between 0.5 and 2.0;
- equality at 1:2 or 2:1 is accepted;
- IOC 288 body height is capped at 1728 active scan lines;
- IOC 576 body height is capped at 3456 active scan lines.

The line caps are defense-in-depth checks corresponding to twice each mode's active width. The identification header is additional: 32 lines for IOC 288 and 64 lines for IOC 576. Including the 45 seconds of selection, phasing, stop, and trailer signals, these limits keep total transmission time to approximately 15 minutes 25 seconds for IOC 288 and 29 minutes 57 seconds for IOC 576.

When a Radiofax source exceeds the aspect-ratio limit, the application must reject it before allocating or rendering PCM. **Send** and **Save WAV** are disabled, and status explains that the page is too elongated for Radiofax and asks the user to check rotation or print layout. Rotation, switching to an SSTV mode, and discarding the job remain available. A corrected rotation is revalidated and may enable transmission. The encoder independently rejects a frame exceeding its mode's maximum dimensions so non-UI callers cannot bypass the safety limit.

## Identification header

Radiofax retains the fixed top identification-header concept used by SSTV:

- left: an eight-step grayscale reference from black through white;
- following text: date, time, `JST`, and mode label (`FAX288` or `FAX576`);
- right: call sign.

The grayscale reference consists of eight adjacent square cells inside a black outline. The white cell remains visible because of the outline. It provides an immediate check for black crushing, white clipping, and compressed intermediate tones in the received image.

For IOC 288 the cells and header are 32 scan lines high; IOC 576 scales the whole header by two to 64 scan lines. This retains the common visual proportions and gives the higher-IOC mode additional text detail. The existing bitmap text style and call-sign emphasis remain, adapted to grayscale output.

## Code organization

Radiofax is a separate encoder path, not a VIS-based SSTV scan planner.

- Extend the existing persisted mode set with `Radiofax120Ioc288` and `Radiofax120Ioc576`, appended without changing existing enum values.
- Add Radiofax mode definitions containing IOC, LPM, composition width, labels, and duration data.
- Add a Radiofax planner/encoder that emits IOC selection, phasing, raster scan, stop, and trailer tone spans.
- Reuse the existing tone-span PCM renderer, PCM artifact, WAV writer, playback, and cancellation mechanisms.
- Add a Radiofax composition path beside the existing sequential-RGB and Robot 36 paths.
- Generalize mode-dependent UI and preparation dispatch only where necessary; do not mix Radiofax control signals into `SstvEncoder`.
- Update duration estimation so the displayed remaining time includes the variable image height and all control sections.
- Preserve existing preference files and values. Radiofax modes are new values; existing SSTV selections continue to deserialize unchanged.
- Update architecture and user documentation to distinguish SSTV VIS modes from analogue Radiofax modes.

The current assembly names may remain unchanged for this release to avoid a broad, unrelated rename. Type and namespace boundaries should nevertheless keep the new Radiofax encoder independent from SSTV-specific VIS and colour planners.

## Validation

Automated tests must cover:

- both IOC mode definitions, names, dimensions, and persisted values;
- 5-second IOC selection duration and 300/675 Hz black-white alternation;
- 30-second, 2 Hz asymmetric phasing with white 5%, black 95%, and white leading edge;
- exactly 24,000 PCM samples per image scan line at 48 kHz;
- a white 4.5% dead sector and 95.5% active image interval within every image line;
- black, centre gray, and white mapping to 1500, 1900, and 2300 Hz;
- all eight WMO reference levels;
- 5-second 450 Hz stop sequence and 10-second black trailer;
- portrait and landscape aspect preservation at IOC 288 and IOC 576;
- acceptance of 1:2 and 2:1 pages and rejection of any page whose longer-to-shorter-side ratio exceeds 2.0;
- disabled Send and Save WAV actions for a rejected Radiofax page while rotation, mode selection, and discard remain available;
- successful revalidation after rotation brings the page within the accepted range;
- encoder-level rejection above the per-mode maximum line count;
- variable-height grayscale composition and the scaled identification header;
- exact agreement between estimated and rendered duration;
- effective 1x and disabled 2x UI state in Radiofax modes;
- restoration of the prior SSTV 2x preference;
- preferences round trips for both new modes;
- cancellation during a long Radiofax render or playback;
- regression coverage for all four existing SSTV modes.

Manual interoperability testing must verify that at least one existing Radiofax receiver recognizes the IOC, locks phase and line rate, renders the expected orientation and aspect ratio, distinguishes all eight header levels, and stops cleanly. Tests should begin with IOC 288 to shorten iteration time, followed by IOC 576 for the final detail check.

## Deferred work

The following are explicitly outside the first Radiofax implementation:

- multi-page transmission;
- selectable first-page/all-pages input policy;
- shortened amateur-only control sequences;
- additional LPM values;
- thresholding, dithering, sharpening, or other document filters;
- Radiofax reception;
- PTT control;
- proprietary high-resolution extensions.

Future multi-page support should expose **Input settings → Multi-page → First page / All pages**. In all-pages mode, each page should be a complete independent Radiofax transmission with its own IOC selection, phasing, identification header, stop signal, and trailer so a receiver can reacquire synchronization between pages.

## References

- WMO-No. 386, *Manual on the Global Telecommunication System*, Part III, sections 5.1–5.5.
- U.S. National Weather Service worldwide marine Radiofax schedules, which identify 120 LPM / IOC 576 as the normal NWS broadcast format.

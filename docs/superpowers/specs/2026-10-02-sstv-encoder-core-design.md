# SSTV Encoder Core Design

## Purpose

Build a platform-neutral SSTV encoder that converts a mode-sized RGB frame into a phase-continuous 48 kHz mono PCM stream. The first output adapter writes 16-bit PCM WAV files for automated waveform analysis and manual decode testing. The same streaming core will later feed Windows audio output and can be reused on Linux or Raspberry Pi.

This phase implements Martin 1, Scottie 1, Scottie 2, and Robot 36. It does not resize printed pages, compose the callsign header, control PTT, or play audio.

## Scope and success criteria

The encoder supports eight configurations: four modes at horizontal sampling multipliers 1× and 2×. Every configuration must preserve the standard mode/VIS identity and total line duration. A 2× frame supplies twice as many horizontal modulation updates in the same scan interval; it is not advertised as a different SSTV mode.

The phase is complete when:

- all eight configurations generate bounded, streaming PCM at 48,000 samples/second;
- generated signals satisfy automated VIS, frequency, timing, scan-order, phase-continuity, and WAV-container tests;
- 1× and 2× signals for the same image and mode have the same ideal duration, within one output sample;
- color-bar WAV files for all four modes decode successfully in an existing SSTV receiver;
- cancellation never publishes a partial WAV as a completed artifact.

## Public model and boundaries

The encoder lives in a new pure .NET class library with no Windows APIs and no image-file decoder dependency. Its public input is an immutable RGB24 frame, a supported mode, a horizontal sampling multiplier, and a cancellation token.

Mode-sized frame requirements are:

| Mode | VIS | 1× input | 2× input | Lines |
|---|---:|---:|---:|---:|
| Martin 1 | 44 (`0x2C`) | 320×256 | 640×256 | 256 |
| Scottie 1 | 60 (`0x3C`) | 320×256 | 640×256 | 256 |
| Scottie 2 | 56 (`0x38`) | 320×256 | 640×256 | 256 |
| Robot 36 | 8 (`0x08`) | 320×240 | 640×240 | 240 |

The encoder rejects any other dimensions or multiplier. Image resizing, cropping, page decoding, and header composition belong to the later image-preprocessing subsystem. This boundary allows text and graphics to be rendered natively at 640 pixels before 2× encoding instead of inventing detail by interpolating a 320-pixel frame inside the encoder.

## Architecture

### Mode description

`SstvModeDefinition` stores immutable public facts: identifier, display name, VIS code, width, height, and scan-family selection. Family-specific timing does not get forced into a misleading universal line structure.

### Scan planners

Each scan planner converts one RGB line into a sequence of timed frequency spans:

- Martin 1 uses line-head sync and G-B-R component order.
- Scottie 1 and 2 use G-B-R order with the sync pulse between the blue and red scans.
- Robot 36 converts RGB to Y and chrominance, sends Y on every line, and alternates R-Y and B-Y information between paired lines.

The planners operate on logical pixel/sample values and do not allocate whole-frame audio. The 2× option changes the number of modulation updates in each component scan from 320 to 640 while leaving the scan duration unchanged. Robot 36 applies the same multiplier to the update density of its Y and chrominance scans; its standard shorter chrominance scan and alternating-line color structure remain unchanged.

### PCM renderer

The common renderer turns timed frequency spans into PCM blocks. It maintains:

- one continuous oscillator phase across every tone and pixel boundary;
- an absolute ideal sample position, so fractional span lengths are accumulated rather than independently rounded;
- fixed amplitude headroom below full scale;
- deterministic output for identical inputs and settings.

For every span boundary, the emitted sample count is the difference between the rounded cumulative ideal positions. This keeps every boundary within one 48 kHz sample of ideal time and prevents frame-length drift.

### Output adapters

The core exposes asynchronous PCM blocks of finite size. The WAV adapter converts floats to signed 16-bit little-endian mono samples and writes a standard RIFF/WAVE header. It writes to a unique temporary file, finalizes lengths only after successful completion, then atomically publishes the destination. A later audio adapter will consume the same PCM blocks without changing the encoder.

## Signal definitions

Video values map linearly from 8-bit component values to frequency:

`frequency = 1500 Hz + value × (800 Hz / 255)`

Thus black is 1500 Hz, midpoint 128 is approximately 1901.57 Hz, and white is 2300 Hz. Sync is 1200 Hz. VIS zero and one bits are 1300 Hz and 1100 Hz respectively.

The standard VIS preamble is:

1. 1900 Hz leader for 300 ms
2. 1200 Hz break for 10 ms
3. 1900 Hz leader for 300 ms
4. 1200 Hz start bit for 30 ms
5. seven VIS data bits, least-significant bit first, 30 ms each
6. one even-parity bit, 30 ms
7. 1200 Hz stop bit for 30 ms

The initial modes use these timing profiles:

| Mode/family | Line sequence and duration |
|---|---|
| Martin 1 | 1200 Hz sync 4.862 ms; 1500 Hz porch 0.572 ms; G 146.432 ms; 1500 Hz separator 0.572 ms; B 146.432 ms; separator 0.572 ms; R 146.432 ms; separator 0.572 ms |
| Scottie 1 | G 138.240 ms; 1500 Hz separator 1.500 ms; B 138.240 ms; separator 1.500 ms; 1200 Hz sync 9.000 ms; 1500 Hz porch 1.500 ms; R 138.240 ms |
| Scottie 2 | G 88.064 ms; 1500 Hz separator 1.500 ms; B 88.064 ms; separator 1.500 ms; 1200 Hz sync 9.000 ms; 1500 Hz porch 1.500 ms; R 88.064 ms |
| Robot 36 | 1200 Hz sync 9.000 ms; 1500 Hz porch 3.000 ms; Y 88.000 ms; alternating line marker 1500/2300 Hz for 4.500 ms; 1900 Hz porch 1.500 ms; alternating chrominance scan 44.000 ms |

Scottie emits the required initial sync before the first G-B-R line cycle. Robot 36 converts RGB24 to full-range BT.601-style components, rounds to the nearest integer, and clamps every result to 0 through 255:

- `Y = 0.299000 R + 0.587000 G + 0.114000 B`
- `R-Y = 128 + 0.500000 R - 0.418688 G - 0.081312 B`
- `B-Y = 128 - 0.168736 R - 0.331264 G + 0.500000 B`

For each adjacent pair of source lines, chrominance values are averaged vertically after conversion. The even line transmits the averaged R-Y values and the odd line transmits the same pair's averaged B-Y values. The Y scan always comes from the current source line.

Timing and scan order are based on J. L. Barber N7CXI's *Proposal for SSTV Mode Specifications* (Dayton, 2000), cross-checked against the [SSTV Handbook mode list](https://www.sstv-handbook.com/download/sstv_03.pdf), [pySSTV timing definitions](https://github.com/dnet/pySSTV/blob/master/pysstv/color.py), and the independently developed [sstv-encoder mode table and verification notes](https://github.com/SEKY443/sstv-encoder). No implementation code is copied from those projects.

## Error and cancellation behavior

All configuration errors are detected before audio generation:

- unsupported mode or horizontal multiplier;
- frame dimensions or RGB byte count that do not match the selected configuration;
- a sample rate other than the phase's fixed 48 kHz contract;
- an invalid amplitude or any non-finite generated PCM value.

I/O failures retain their original exception and abort publication. Cancellation is checked between bounded PCM blocks and within long scan generation loops. It closes and deletes only temporary output created by the current operation. Encoder instances hold no shared mutable state and may run concurrently.

## Verification strategy

Unit and integration tests cover:

- exact VIS bit order, even parity, tones, and durations for all four codes;
- black, midpoint, and white frequency mapping;
- Martin line-head sync versus Scottie mid-line sync;
- Robot 36 Y and alternating paired-line chrominance order;
- component boundary times and total line/frame times within one sample;
- oscillator phase continuity at frequency transitions;
- 1× and 2× equal total duration with twice the modulation-update count;
- RGB-to-Robot component conversion and clamping at primary and neutral colors;
- correct 48 kHz mono 16-bit PCM WAV headers and length fields;
- constant-memory block generation, concurrent encodes, cancellation cleanup, and I/O failure cleanup.

Tests generate compact synthetic frames and analyze the resulting PCM instead of committing large golden WAV files. A manual runbook generates color bars for all four modes and records decode results from an existing SSTV receiver. Waveform comparisons may be made against independent reference implementations, but byte-for-byte identity is not required because oscillator phase and sample rounding strategies can differ while remaining conformant.

## Deferred work

- PWG Raster decoding and first-page extraction
- orientation-aware page rendering, fitting, and fixed callsign header composition
- desktop send-settings UI and job store
- Windows audio-device selection and playback
- PTT, CAT, CI-V, RTS, and DTR control
- Windows 10 packaging and full end-to-end validation


# SSTV Virtual Printer Design

## 1. Purpose

SSTV Virtual Printer is a transmit-only Windows application that accepts printed output from Office applications, paint programs, browsers, and similar software, prepares the first printed page as an SSTV image, and plays the encoded SSTV signal through a selected audio device.

The application exists to let operators create images in capable general-purpose software instead of relying on the limited editing features of existing amateur-radio SSTV programs. Reception remains the responsibility of existing SSTV software.

The initial product is a local Windows application. Its internal boundaries should allow a future Linux or Raspberry Pi host, but remote operation is not an initial requirement.

## 2. Supported Environment

- Windows 10 version 21H2 or later
- Windows 11
- A Windows-compatible audio output device
- A source application capable of printing through the Windows print system

The application will use the Microsoft IPP Class Driver and a loopback-only IPP endpoint. It will not install a proprietary kernel-mode printer driver.

## 3. Goals

- Appear as `SSTV Virtual Printer` in normal application print dialogs.
- Accept a print job through a local IPP endpoint.
- Use only the first page delivered in the job.
- Present a preview and transmission settings before emitting audio.
- Always add an operator-identification header at the top of the transmitted image.
- Encode and play Martin 1, Scottie 1, Scottie 2, and Robot 36.
- Keep IPP reception, image composition, SSTV encoding, and audio playback independently testable.
- Avoid custom legacy V3/V4 printer drivers and their signing and servicing burden.

## 4. Non-goals for the Initial Release

- SSTV reception or decoding
- PTT control through CAT, CI-V, RTS/DTR, Hamlib, or other mechanisms
- Listening for print jobs from other machines on the LAN
- Authentication or administration for a network-visible printer
- Linux, macOS, or Raspberry Pi packages
- Continuous transmission of multiple printed pages
- General-purpose image drawing or text editing
- Manual crop, pan, or zoom controls
- Automatic left-side or orientation-dependent header placement
- Advanced image or color correction
- Long-term WAV library or recording management beyond explicit **Save WAV** export
- Support for every SSTV mode

## 5. User Workflow

### 5.1 First run

1. The application starts its loopback IPP host.
2. It registers an `SSTV Virtual Printer` queue with Windows, using the Microsoft IPP Class Driver.
3. It verifies that the queue points only to the local endpoint.
4. It shows a clear diagnostic if registration fails or requires elevated installation support.

### 5.2 Printing and transmission

1. The operator selects `SSTV Virtual Printer` in the source application's print dialog.
2. The IPP host accepts the job into the process-local FIFO queue.
3. The document decoder renders or decodes the first page. Remaining pages are ignored.
4. The desktop application opens or signals that a new job is waiting.
5. The transmission screen displays the final SSTV preview and settings.
6. The operator reviews the call sign, SSTV mode, horizontal 1x/2x sampling option, selected audio device, and the read-only Windows volume/mute indication.
7. The operator explicitly selects **Start transmission** or **Save WAV**.
8. The application composes the header and content at the selected mode's native resolution, generates PCM audio, and plays it.
9. The operator may stop playback at any time.

No print job is transmitted automatically.

### 5.3 Multiple jobs

If a job arrives while another job is being reviewed or transmitted, it enters a small FIFO waiting queue. It must not replace the active job. After transmission completes or is stopped, the operator may open the next waiting job.

The FIFO is intentionally non-persistent. Closing the window keeps the process and queue alive in the notification area; explicit application exit warns when jobs remain and then discards them. Jobs are not recovered after process termination.

## 6. User Interface

The primary transmission screen contains:

- A large preview of the exact image that will be encoded
- SSTV mode selection: Martin 1, Scottie 1, Scottie 2, or Robot 36
- A required call-sign field
- Audio output device selection
- Read-only Windows output volume and a prominent mute warning
- Send, Save WAV, Stop, and Discard controls
- Current transmission state and waiting-job count
- A notice when a received job contained more than one page

The application persists the call sign, default mode, horizontal 1x/2x option, selected audio device, and start-at-sign-in preference. It never changes Windows output volume or mute state.

## 7. Image Composition

### 7.1 Output canvas

The composer creates the exact native pixel canvas required by the selected SSTV mode. Mode definitions supply dimensions and encoding behavior. The initial expected dimensions are validated against the applicable SSTV specifications during implementation rather than duplicated across the UI.

### 7.2 Header

The header is stored independently from the printed page and applied during final composition. It occupies a fixed-height, opaque band at the top of the output canvas so that identification is transmitted early in the scan.

The header must:

- Display the configured call sign
- Use high-contrast colors and a font size chosen for SSTV readability
- Remain readable after encoding and decoding, not merely in the source preview
- Prevent transmission if the required call sign is empty

The internal placement model will support `Top`, `Left`, and `Auto`, but the initial UI and renderer implement only `Top`. This preserves a future option to place the header on the left for vertically oriented input without adding initial-release behavior.

### 7.3 Printed-page placement

The initial placement mode is **Fit entire page**. The page is scaled uniformly into the area remaining below the header. It is never cropped. Unused space is filled with a configurable background color whose initial default is neutral black.

The placement model should allow later addition of:

- Fit height
- Fit width aligned to top
- Fit width aligned to center
- Fit width aligned to bottom
- Manual scale and translation

These future values must not be exposed until their rendering and preview behavior are implemented.

## 8. Architecture

```text
Source application
    -> Windows print system / Microsoft IPP Class Driver
    -> Loopback IPP endpoint
    -> Print document decoder
    -> In-memory FIFO queue
    -> Image composer
    -> SSTV encoder
    -> PCM audio engine
    -> Selected Windows audio device
```

### 8.1 Printer registration

This component creates, verifies, repairs, and removes the Windows printer queue. It owns all Windows-specific queue-registration operations. It does not process print content.

### 8.2 IPP endpoint

The endpoint implements the minimum standards-compliant IPP behavior needed by the Microsoft IPP Class Driver. It advertises a deliberately small capability set: color output, a fixed initial resolution, and only document formats that the decoder can safely consume.

The initial technical probe will attempt to use `image/pwg-raster` as the sole print-job format. This lets the operating-system print pipeline render arbitrary application content before delivery. The probe must establish actual Windows 10 and Windows 11 behavior before this becomes an implementation dependency.

The endpoint binds to loopback only. LAN binding, service discovery, and remote administration are excluded from the initial release.

### 8.3 Print document decoder

The decoder converts a supported IPP document payload into the application's canonical page bitmap. It extracts the first page and reports the total page count when available. Unsupported or malformed data is rejected before a sendable job is created.

The decoder is selected through a format-based interface so that PDF, additional raster formats, or platform-specific inputs can be added later without changing downstream components.

### 8.4 Job queue

The process-local job queue retains while the application is running:

- A generated job identifier
- Receipt time
- Source application information when available
- Page count when available
- The canonical first-page bitmap or safely retained source payload
- Selected mode and composition settings
- State: received, ready, transmitting, completed, stopped, or failed
- Diagnostic failure information

It uses FIFO order and never allows a newly received job to replace the active job. Completed and explicitly discarded jobs are removed immediately. The queue is deliberately not written to disk; explicit exit warns before pending jobs are discarded.

### 8.5 Image composer

The composer is a platform-neutral library operation:

```text
page bitmap + header data + placement + mode canvas -> completed RGB image
```

It contains no IPP, UI, audio, or Windows queue code.

### 8.6 SSTV encoder

The encoder is a platform-neutral library operation:

```text
completed RGB image + mode definition + sample rate -> PCM sample stream
```

Mode definitions isolate native dimensions, VIS code, color representation, scan order, frequencies, and timing. Martin 1, Scottie 1, Scottie 2, and Robot 36 are independently testable encoders or strategies behind a common interface.

The encoder must derive sample boundaries deterministically and must not depend on UI timers or audio-callback timing for signal accuracy.

### 8.7 Audio engine

The audio engine enumerates Windows output devices, reads their current master volume/mute state for display, and plays a supplied WAV/PCM stream. It owns start, stop, progress, underrun reporting, and device-loss handling. It does not change system or endpoint volume, does not provide a test tone, and contains no SSTV timing rules.

A future transmission-control boundary surrounds playback with optional pre-send and post-send delays. The initial implementation uses a no-operation PTT provider. Future providers may implement RTS/DTR, CI-V, CAT, Hamlib, or external command invocation without modifying the encoder.

### 8.8 Desktop UI and host process

The Windows implementation uses C#, .NET, and Avalonia. One per-user process owns the loopback IPP host, in-memory FIFO, preparation pipeline, tray icon, and interactive UI.

The application is single-instance, starts the IPP host only in the primary process, and may start at sign-in through a per-user registration. Closing the title-bar window hides it to the notification area; explicit Exit warns only when jobs are pending.

## 9. Error Handling

- Invalid IPP request: return a protocol error and record a bounded diagnostic entry.
- Unsupported document format: reject the job and notify the desktop UI.
- Malformed raster or excessive dimensions: reject before allocating unbounded memory.
- Multiple pages: retain only the first page and show the source page count when known.
- Missing call sign: disable transmission and identify the missing field.
- Encoder failure: emit no audio and leave the job in a failed, retryable state.
- Missing audio device before start: block transmission and require a valid selection.
- Audio device loss during playback: stop immediately, report an incomplete transmission, and do not mark the job completed.
- New job during playback: enqueue it without changing the active job.
- Abnormal termination: discard the non-persistent queue; never transmit or reconstruct a job automatically.

Logs must avoid retaining unrelated document contents. Diagnostic logs record identifiers, formats, sizes, state transitions, errors, and timing; image payloads remain only in process memory for the active session.

## 10. Security and Resource Limits

- Bind the IPP listener exclusively to loopback in the initial release.
- Do not rely only on a firewall rule to prevent remote access.
- Apply explicit maximum job size, pixel dimensions, page count, queue length, and decode time.
- Treat all IPP attributes and document bytes as untrusted input.
- Use atomic job-state updates so an interrupted write cannot create a sendable partial job.
- Require explicit operator action before every transmission.
- Do not execute commands or embedded content from printed documents.

## 11. Verification Strategy

### 11.1 IPP compatibility

Test the loopback printer with:

- Windows 10 21H2 or the newest maintainable Windows 10 release available to the project
- Windows 11 23H2 where still supported for testing
- Windows 11 24H2 or later
- Word, Excel, PowerPoint, Paint, a representative graphics application, and major browsers
- Portrait and landscape jobs, color content, print cancellation, repeated jobs, and concurrent jobs

The first milestone is a throwaway interoperability probe that captures and identifies the actual payload emitted by each target Windows version when the IPP endpoint advertises PWG Raster. If the Microsoft driver does not consistently supply a usable format, the design must be revisited before product implementation proceeds. Candidate adaptations are advertising another standard raster format, adding PDF decoding, or narrowing supported Windows versions. A legacy V3/V4 driver remains a last resort.

### 11.2 Image composition

Golden-image tests verify:

- Exact output dimensions for every supported mode
- Fit-entire-page scaling for tall, wide, small, and oversized sources
- No cropping of the page content
- Correct top-header bounds
- Required call-sign and mode/time text rendering
- Stable output for identical inputs and settings

Decoded over-the-air samples must also confirm that the header remains readable after realistic SSTV degradation.

### 11.3 SSTV signal generation

For each mode, automated tests verify:

- VIS code
- Leader, break, sync, porch, and channel frequencies
- Segment sample counts and tolerances
- Color-channel order and conversion
- Scan-line count
- Total transmission duration
- Deterministic output for a known test image

Generated audio is decoded using more than one established SSTV receiver where practical. Reference WAV files and decoded images are retained as test fixtures when licensing permits.

### 11.4 Audio

Tests cover device selection, read-only volume/mute observation, normal completion, manual stop, device loss, buffer underrun, WAV export, and repeated playback. SSTV timing tests operate on generated samples independently of real-time device tests.

## 12. Delivery Sequence

1. Prove Windows 10 and Windows 11 interoperability with a minimal loopback IPP endpoint and PWG Raster intake.
2. Implement and validate the four platform-neutral SSTV encoders against established receivers.
3. Implement canonical page decoding, fit-entire-page composition, and the fixed top header.
4. Implement audio-device output and interruption behavior.
5. Integrate the desktop transmission screen and waiting-job workflow.
6. Add reliable startup, printer registration, repair, and uninstall behavior.
7. Run the application and operating-system compatibility matrix.

The IPP interoperability probe is the architectural gate. Failure to receive a stable, decodable standard document format triggers design review rather than immediate investment in a proprietary Windows printer driver.

## 13. Future Extensions

Potential later work includes additional SSTV modes, crop and alignment controls, automatic top/left header placement, PTT providers, Linux and Raspberry Pi hosts, and optional LAN printing. These features must preserve the component boundaries above and are not prerequisites for the initial Windows release.

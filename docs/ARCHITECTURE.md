# Architecture

SSTV Virtual Printer is a transmit-only pipeline:

```text
print client
  -> loopback IPP endpoint
  -> first PWG Raster page
  -> orientation normalization and manual rotation
  -> SSTV composition and fixed header
  -> PCM / WAV
  -> selected Windows audio endpoint
```

## Projects

- `SstvVPrinter.Ipp` implements the IPP message model, printer capabilities, endpoint, and optional raw capture sink. The desktop app reuses this library without enabling file capture.
- `SstvVPrinter.Imaging` decodes the first PWG Raster page, normalizes printer orientation, performs manual quarter-turn rotation and detail-preserving fitting, and composes the SSTV frame/header.
- `SstvVPrinter.Sstv` contains mode definitions, scan planning, VIS generation, PCM rendering, duration estimation, and WAV writing.
- `SstvVPrinter.App` contains print jobs, queueing, preferences, validation, transmission preparation, and platform abstractions.
- `SstvVPrinter.Desktop` is the Avalonia UI and Windows integration layer: hosting, audio endpoints/playback, printer/startup registration, notification area, foreground activation, and single-instance coordination.
- `SstvVPrinter.ImagingTool`, `SstvVPrinter.SstvTool`, and the standalone `SstvVPrinter.IppProbe` executable are diagnostic tools retained for repeatable protocol and image investigations.

## Print ingestion

The desktop host listens on `ipp://localhost:8631/ipp/print`. Loopback binding is the security boundary: remote print serving is not a supported deployment. Windows normally creates the queue through the Microsoft IPP Class Driver. Registration and repair are explicit UAC-elevated PowerShell operations; routine application use does not require elevation.

The endpoint accepts a job, stores it in memory, and hands it to the application queue. Only the first PWG Raster page is decoded. Printer-supplied orientation is normalized before the user's saved 0°/90°/180°/270° correction is applied.

## Composition and transmission

Composition is deterministic for a decoded page, accepted timestamp, call sign, mode, sampling multiplier, and rotation. The UI preview is made from the same composed frame used by the encoder. The fixed header identifies the transmission early; Robot 36 uses an overlay or natural padding where appropriate, while the other modes reserve the header area.

Preparation produces an in-memory transmission artifact. Playback and WAV export consume the same PCM stream contract. PTT is intentionally outside the current architecture.

## Preferences and lifetime

User preferences are stored under the user's local application data and include call sign, SSTV mode, horizontal multiplier, endpoint ID, rotation, and start-at-sign-in choice. Closing the main window hides it; **File → Exit** performs application shutdown. A named single-instance coordinator routes later launches back to the existing process.

Audio endpoint monitoring is read-only. It exposes stable endpoint IDs, friendly display names, master mute/volume, and—while playback is active—the application session volume. It never changes Windows mixer state.

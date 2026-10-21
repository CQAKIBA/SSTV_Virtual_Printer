# Changelog

All notable changes to this project are documented in this file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.2.0-beta.4] - 2026-10-21

### Added

- Four transmit-only RadioFAX variants for IOC 288/576 with selectable 10 ms or 20 ms line synchronization; IOC 576/20 ms is labeled as the JMH standard choice.
- Radiofax page-ratio safety validation covering the inclusive 1:2 through 2:1 range.
- Per-mode header placement: reserve space inside the fixed page or overlay the image.
- Optional deletion after successful send/WAV export, disabled by default for repeat tests.
- Approximate red transmission-line indicator in the preview.
- Print queue columns for date/time, source name, and UUID.

### Fixed

- Centered selectable 10/20 ms black synchronization pulses inside each 22.5 ms RadioFAX image-line dead sector for improved KG-FAX and Robot36 receiver compatibility.
- Kept RadioFAX output inside the source page height so fixed-A4 receivers do not truncate the tail after the identification header.
- Reset progress between PCM generation and playback, corrected playback remaining time, and restored Send/WAV actions after stopping.
- Stretched the protocol selector to the panel width.
- Corrected printer registration-state detection by passing PowerShell script arguments through `-File`.
- Removed the ignored `-Name` argument from IPP registration and ensured the Microsoft IPP Class Driver is available before registration or repair.

## [0.1.0-beta] - 2026-10-06

### Added

- Local loopback IPP virtual-printer workflow using PWG Raster input.
- First-page decoding, orientation normalization, persistent manual rotation, aspect-ratio-preserving fit, and detail-preserving reduction.
- Fixed RGB/JST/mode/call-sign header treatment for Martin 1, Scottie 1, Scottie 2, and Robot 36.
- SSTV PCM generation, direct Windows playback, and WAV export.
- Experimental 2x horizontal sampling.
- Avalonia preparation UI with print queue, exact preview, audio endpoint selection, volume/mute warnings, tray behavior, and single-instance restoration.
- UAC-assisted printer registration and repair.
- Automated protocol, image, application, and desktop tests.

### Known limitations

- Windows 11 is verified; Windows 10 and ARM64 hardware are not yet verified.
- PTT and receive functionality are not implemented.
- IPP serving outside loopback is unsupported.
- Binaries are unsigned.

[Unreleased]: https://github.com/CQAKIBA/SSTV_Virtual_Printer/compare/v0.2.0-beta.4...HEAD
[0.2.0-beta.4]: https://github.com/CQAKIBA/SSTV_Virtual_Printer/compare/v0.2.0-beta.3...v0.2.0-beta.4
[0.1.0-beta]: https://github.com/CQAKIBA/SSTV_Virtual_Printer/releases/tag/v0.1.0-beta

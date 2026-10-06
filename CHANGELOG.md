# Changelog

All notable changes to this project are documented in this file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Fixed

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

[Unreleased]: https://github.com/CQAKIBA/SSTV_Virtual_Printer/compare/v0.1.0-beta...HEAD
[0.1.0-beta]: https://github.com/CQAKIBA/SSTV_Virtual_Printer/releases/tag/v0.1.0-beta

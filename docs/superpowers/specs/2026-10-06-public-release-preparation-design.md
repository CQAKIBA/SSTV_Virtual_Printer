# SSTV Virtual Printer Public Release Preparation Design

**Date:** 2026-10-06  
**Target repository:** `https://github.com/CQAKIBA/SSTV_Virtual_Printer`  
**Initial release:** `v0.1.0-beta`

## Objective

Prepare SSTV Virtual Printer for its first public GitHub beta release without destabilizing the working print-to-SSTV path. The public repository must be understandable to users and contributors, build reproducibly, test automatically, and produce downloadable Windows release archives.

The project exists to address a practical limitation in existing amateur-radio SSTV applications: they are designed primarily around radio operation, while their image creation, editing, and integration workflows are often limited. SSTV Virtual Printer lets users create images in drawing, painting, illustration, DTP, office, or any other printable application, then pass the result to SSTV through the familiar Print command. Its broader purpose is to create opportunities for amateur radio and MOE / creative fan culture to meet.

## Release Status

The first public version is explicitly a beta:

- receive functionality is out of scope;
- PTT control is not implemented;
- the local IPP print-server path is still experimental;
- Windows 11 is the verified development environment;
- Windows 10 behavior is best effort and not yet verified;
- x64 is the recommended build;
- ARM64 is published as an experimental, hardware-untested build;
- binaries are unsigned and may trigger Windows SmartScreen.

## Public Repository Layout

Keep:

- `src/` application, protocol, imaging, encoder, and diagnostic tool projects;
- `tests/` automated tests;
- `docs/ARCHITECTURE.md` for the system and data flow;
- `docs/BUILDING.md` for local development, testing, and publishing;
- `README.md` as the primary English documentation;
- `README_JP.md` as the Japanese documentation;
- `LICENSE`, `CHANGELOG.md`, `CONTRIBUTING.md`, and `SECURITY.md`;
- `.github/workflows/ci.yml` and `.github/workflows/release.yml`.

Remove from the public tree:

- `docs/superpowers/`, which contains internal design and execution history;
- `docs/validation/`, after carrying forward useful conclusions into public documentation;
- clearly unused implementation remnants and their dedicated tests.

Keep the IPP probe, SSTV waveform tool, and imaging tool. They are useful for diagnostics, reproducible bug reports, and protocol validation. Do not rename projects or perform a large directory reorganization for the first beta.

## Documentation

### README languages

`README.md` is written in natural English and links prominently to `README_JP.md` with a Japanese-language link. `README_JP.md` provides the complete Japanese version. The English README is maintained as first-class documentation even though the project author primarily reads Japanese.

### Positioning

The README should prominently explain:

> Existing SSTV applications are good at radio communication, but their image creation and editing workflows are often limited and isolated from modern creative software. SSTV Virtual Printer bridges that gap: create your image in drawing, painting, illustration, DTP, or office applications—anything that can print—and send it to SSTV through the familiar Print command.

The Japanese version carries the same emphasis: users should create images in the applications best suited to creative work instead of being constrained by an SSTV application's editor.

### User-facing content

Document:

- supported modes: Martin 1, Scottie 1, Scottie 2, and Robot 36;
- local IPP printing from any application with a Print command;
- first-page-only processing;
- the fixed RGB/call-sign/JST/mode header;
- horizontal 2x sampling and manual ±90-degree rotation;
- preview, direct playback, and WAV export;
- Windows master and application volume monitoring;
- printer registration and repair from the application;
- tray behavior and single-instance behavior;
- installation and first-send instructions;
- current limitations and beta status;
- release architecture guidance;
- build and test instructions for developers.

Do not use a screenshot containing a real document or call sign in the first release. A sanitized promotional screenshot can be added later.

### Safety and legal notes

State that users are responsible for compliance with their amateur-radio licence, call-sign identification, occupied bandwidth, transmission control, and local regulations. Warn that PTT is absent, that VOX can be triggered by unintended audio, and that users should verify the preview and audio routing before transmission. Explain that the IPP listener binds to loopback by default and LAN print-server use is not a supported scenario.

## Licensing and Attribution

Use the MIT License with:

`Copyright (c) 2026 Daisuke JA1UMW / CQAKIBA.TOKYO`

Do not publish the author's email address. Keep `JA1UMW` in tests where it is useful test data. Replace the user-interface call-sign placeholder with a neutral instruction so it cannot be mistaken for a value to transmit. Existing saved user settings must remain untouched.

## Code Cleanup

Use a conservative cleanup policy:

- remove the unused `AudioEndpointViewModel` and its dedicated tests;
- search again for unreachable, duplicated, or unreferenced code and remove only cases that are unambiguous;
- do not split `DesktopComposition` or restructure the active print path solely for aesthetics;
- retain diagnostic command-line tools;
- preserve behavior with existing automated tests;
- add tests only when cleanup or publication behavior introduces a meaningful contract.

Add common repository metadata to `Directory.Build.props`, including author, copyright, repository URL, and version `0.1.0-beta`. Add a `global.json` that selects the .NET 10 SDK family. Expand `.gitignore` for common IDE, test, publish, and local artifact files.

## Continuous Integration

Add a Windows GitHub Actions workflow for pushes and pull requests. It must:

1. check out the repository;
2. install the .NET 10 SDK;
3. restore the solution;
4. build the solution in Release mode;
5. run all automated tests.

Use GitHub-maintained actions and least-privilege read permissions. The workflow is an implementation detail for maintainers; end users interact with GitHub Releases instead.

## Release Automation

A `v*` tag triggers the release workflow. It builds self-contained, untrimmed, multi-file publications for:

- `win-x64`, the recommended and supported release archive;
- `win-arm64`, marked `experimental` and explicitly described as hardware-untested.

Artifact names:

- `SSTV-Virtual-Printer-v0.1.0-beta-win-x64.zip`
- `SSTV-Virtual-Printer-v0.1.0-beta-win-arm64-experimental.zip`
- `SHA256SUMS.txt`

The workflow runs build and tests before publishing, creates ZIP archives, computes SHA-256 checksums, and creates a GitHub Release. Tags containing a hyphen, such as `v0.1.0-beta`, produce a prerelease. Release notes must recommend x64 and identify ARM64 as experimental. Trimming and single-file publishing are intentionally disabled to avoid unnecessary Avalonia, reflection, and Windows interop risk.

## Version and Compatibility

Set the initial public version to `0.1.0-beta`. Release archives are self-contained so end users do not need to install .NET separately. The documentation must distinguish the target framework's Windows API baseline from Microsoft's current support policy, and avoid claiming that every Windows 10 edition is officially supported.

## Validation

Before publication:

1. verify the working tree contains no tracked captures, credentials, personal paths, or email addresses;
2. run formatting and repository hygiene checks;
3. run the entire test suite;
4. build the solution in Release mode with zero warnings and errors;
5. publish `win-x64` and `win-arm64` self-contained outputs;
6. inspect both archives for expected files and absence of development artifacts;
7. launch and smoke-test the x64 archive;
8. verify README links and documented commands;
9. verify the remote repository before setting or changing `origin`;
10. push source only after explicit confirmation, then create the beta tag and verify the generated GitHub Release.

ARM64 runtime behavior remains unverified until a contributor reports results from real hardware.

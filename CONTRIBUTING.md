# Contributing

Thank you for helping SSTV Virtual Printer become more useful across radios, receivers, Windows versions, and creative applications.

## Prerequisites

- Windows 11 for the currently verified development environment.
- .NET SDK 10.0.401 or a compatible .NET 10 feature-band SDK selected by `global.json`.
- Git and PowerShell.

## Build and test

```powershell
dotnet restore SstvVPrinter.slnx
dotnet build SstvVPrinter.slnx -c Release --no-restore
dotnet test SstvVPrinter.slnx -c Release --no-build
```

Run the desktop application with:

```powershell
dotnet run --project src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj
```

During focused work, run the matching test project first, then run the entire solution before submitting a change. Keep warnings at zero; the repository treats warnings as errors.

## Bug reports

Include the exact Windows build, source application/version, portrait or landscape setting, SSTV mode, horizontal 1x/2x setting, and what the preview or receiver displayed. Logs and minimized synthetic inputs are helpful.

Do not commit or attach private documents, real station correspondence, captured print payloads, credentials, personal paths, or unnecessarily identifying screenshots. Reproduce with a small synthetic page whenever possible.

## Changes

- Keep behavior changes focused and covered by a test that fails before the fix.
- Preserve loopback-only defaults and read-only volume monitoring.
- Do not claim platform or receiver compatibility without recording the tested hardware/software.
- Keep English and Japanese README claims aligned.
- Use clear commit messages and explain user-visible trade-offs in the pull request.

See [docs/BUILDING.md](docs/BUILDING.md) for release publishing commands and diagnostic tools.

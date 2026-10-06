# Building and Validation

## Requirements

- Windows with PowerShell and Git.
- .NET SDK 10.0.401, or a compatible .NET 10 feature-band SDK allowed by `global.json`.

The desktop target `net10.0-windows10.0.19041.0` defines the Windows API contract used at compile time. It is not a claim that every Windows 10 release is currently supported by Microsoft or verified by this project. Development and manual testing have been performed on Windows 11; Windows 10 remains best effort and unverified.

## Restore, build, test, and run

```powershell
dotnet restore SstvVPrinter.slnx
dotnet build SstvVPrinter.slnx -c Release --no-restore
dotnet test SstvVPrinter.slnx -c Release --no-build
dotnet run --project src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj -c Release
```

Warnings are treated as errors and builds are deterministic.

## Self-contained publication

```powershell
dotnet publish src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/publish/win-x64
dotnet publish src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/publish/win-arm64
```

`win-x64` is the supported release architecture. `win-arm64` is built for volunteer testing and remains experimental until reported working on real ARM64 hardware. Multi-file, untrimmed output avoids unnecessary risk around Avalonia, reflection, and Windows interop.

## Diagnostic tools

### IPP probe

Starts a standalone loopback printer and records received payloads to an explicitly chosen directory:

```powershell
dotnet run --project src/SstvVPrinter.IppProbe/SstvVPrinter.IppProbe.csproj -- --capture-directory C:\absolute\safe\capture-folder --port 8631
```

Captured print jobs may contain the complete printed document. Never commit them or attach private inputs to public issues.

### SSTV waveform tool

Generates a color-bar WAV for one mode and horizontal sampling setting:

```powershell
dotnet run --project src/SstvVPrinter.SstvTool/SstvVPrinter.SstvTool.csproj -- --mode scottie2 --horizontal 1 --output C:\absolute\output.wav
```

Accepted modes are `martin1`, `scottie1`, `scottie2`, and `robot36`; horizontal values are `1` or `2`.

### Imaging tool

`SstvVPrinter.ImagingTool` converts a captured PWG Raster input into a diagnostic PPM using the production decoder and composer. Run it without valid arguments to see its exact required options.

## Validation status

- The local IPP / Microsoft IPP Class Driver path has accepted PWG Raster jobs on Windows 11 from Windows Explorer printing, Microsoft Paint, Adobe Acrobat Reader, Microsoft Excel, and PowerShell. Portrait and Acrobat landscape data were decoded after printer-orientation normalization.
- Martin 1, Scottie 1, Scottie 2, and Robot 36 color-bar WAVs were recognized and displayed by an Android SSTV decoder in both 1x and 2x horizontal variants. This is useful interoperability evidence, not exhaustive receiver certification; 2x remains experimental.
- The desktop UI, print queue, composition, audio preparation, and lifetime behavior have automated tests. Actual RF transmission, Windows 10, and ARM64 hardware still require external validation.

## Release process

Push and pull-request CI restores, builds, and tests on Windows. A tag matching `v*` runs the release workflow, which builds self-contained x64 and ARM64 ZIPs, creates `SHA256SUMS.txt`, and publishes a GitHub Release. A tag containing `-`, such as `v0.1.0-beta`, is a prerelease.

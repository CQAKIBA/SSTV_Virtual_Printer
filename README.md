# SSTV Virtual Printer

[日本語版はこちら](README_JP.md)

Turn the Windows **Print** command into an SSTV transmission workflow.

Existing SSTV applications are good at radio communication, but their image creation and editing workflows are often limited and isolated from modern creative software. SSTV Virtual Printer bridges that gap: create your image in drawing, painting, illustration, DTP, or office applications—anything that can print—and send it to SSTV through the familiar Print command.

The wider aim is to create more opportunities for amateur radio and MOE / creative fan culture to meet. Make the picture where making pictures is comfortable; let this application handle the path from the printed page to SSTV audio.

> [!WARNING]
> This is an unfinished `v0.1.0-beta`. Test with a dummy load or monitor receiver before transmitting on air. You are responsible for your amateur-radio licence, call-sign identification, occupied bandwidth, transmission control, and all local regulations.

## What it does

- Appears to Windows applications as a local IPP printer.
- Accepts the first printed page as PWG Raster and opens the preparation window.
- Fits the page into the selected SSTV frame without changing its aspect ratio.
- Adds an always-visible RGB, JST timestamp, mode, and call-sign header.
- Supports Martin 1, Scottie 1, Scottie 2, and Robot 36.
- Offers experimental 2x horizontal sampling for improved fine-detail reproduction.
- Corrects troublesome application/page orientations with persistent −90° and +90° controls.
- Previews the exact composed image, plays SSTV audio through a selected Windows output, or saves it as WAV.
- Shows Windows master volume continuously and application-session volume while transmitting.
- Keeps running in the notification area when its window is closed; launching it again restores the existing instance.
- Registers, verifies, and repairs its printer queue from the **Printer (P)** menu.

Only transmission is in scope. Reception remains the job of your preferred SSTV decoder.

## Project innovations

Amateur radio is not only a place to preserve established modes. It is one of the few fields where an individual can still question an old assumption, build the complete signal path, and test the result over a real channel. SSTV Virtual Printer is intended to encourage that kind of experimentation: traditional analogue SSTV still has unexplored room for new interfaces and better signal generation.

### Print as a universal SSTV interface

Most SSTV applications expect images to be imported, copied, or edited inside their own limited workflow. This project instead uses the operating system's print pipeline as an application-neutral image interface. Drawing, painting, illustration, DTP, office, and other printable software can become an SSTV image source without dedicated plug-ins or application-specific integration. If an application can print, it can participate.

### More detail from classic analogue SSTV

The horizontal scan in analogue SSTV is continuous; the familiar 320-pixel width is a sampling convention rather than a value explicitly carried in the signal. The optional **Horizontal resolution 2x** mode therefore keeps 640 horizontal source samples during signal synthesis instead of first reducing the image to 320. It does not change the VIS code, line count, scan timing, or transmission duration, so conventional 320-pixel receivers remain compatible.

Tests with existing receiver software showed reduced pixel-to-pixel bleeding and better apparent contrast, especially in fine, low-contrast line art. The result suggests that even traditional SSTV modes may still offer useful image-quality headroom through improved transmitter-side synthesis. This remains an experimental finding rather than a new SSTV standard, and the benefit depends on the audio path, radio channel, and decoder. Operators and developers are invited to measure it, challenge it, and improve it.

## Download

Download a ZIP from [GitHub Releases](https://github.com/CQAKIBA/SSTV_Virtual_Printer/releases):

- `win-x64` — recommended and tested architecture.
- `win-arm64-experimental` — supplied for volunteers with suitable hardware; not yet hardware-tested.

The archives are self-contained, so a separate .NET installation is not required. They are currently unsigned, and Windows SmartScreen may show a warning.

## First transmission

1. Extract the release ZIP to a writable folder and start `SstvVPrinter.Desktop.exe`.
2. Open **Printer (P) → Register**. Approve the Windows UAC prompt. Registration uses the local URL `ipp://localhost:8631/ipp/print`.
3. Enter your call sign, choose an audio output and SSTV mode, and optionally enable **Horizontal resolution 2x**.
4. In a drawing, painting, illustration, DTP, office, or other printable application, select **SSTV Virtual Printer** and print the page you want. Only the first received page is used.
5. Check the preview, call sign, orientation, mode, and audio route. Use −90° or +90° if the source application's print layout needs correction.
6. Choose **Send** for audio playback or **Save WAV** for a file. The app never changes Windows volume for you.

The last-used call sign, mode, 2x option, rotation, audio endpoint, and startup preference are retained. Closing the window hides it to the notification area; use **File (F) → Exit** to stop the process.

## Radio and audio safety

PTT control is **not implemented**. If you use VOX, unrelated system audio can key the transmitter, and an unexpectedly high device or mixer level can produce excessive audio. Verify routing and levels before connecting a transmitter. The volume indicators are read-only by design.

The IPP listener binds to loopback by default. Running this as a LAN print server is an unsupported experimental scenario and has not been hardened for untrusted networks.

## Beta limitations

- Windows 11 is the verified development environment.
- Windows 10 support is best effort and requires **version 22H2 (OS build 19045) with the latest Windows updates applied**; printing has been verified on x64 hardware. Testing confirmed that an older 21H2 / OS build 19044 installation lacked the required IPP setup path and could not be configured normally. To check your version, right-click **Start**, open **System**, and find **Version** and **OS build** under **Windows specifications**. Apply Windows Update before attempting printer registration.
- ARM64 packages are structurally built but untested on ARM64 hardware.
- The local IPP / Microsoft IPP Class Driver route remains experimental.
- PTT and receive functionality are absent.
- Only the first page of each print job is processed.
- Horizontal 2x has decoded successfully in limited tests but remains experimental.
- Binaries are not code-signed.

## Development

See [Building and validation](docs/BUILDING.md), [Architecture](docs/ARCHITECTURE.md), and [Contributing](CONTRIBUTING.md). Security reporting guidance is in [SECURITY.md](SECURITY.md).

## Licence

SSTV Virtual Printer is released under the [MIT License](LICENSE).

Copyright (c) 2026 Daisuke JA1UMW / CQAKIBA.TOKYO

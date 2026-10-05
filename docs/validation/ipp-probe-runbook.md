# Windows IPP Probe Runbook

## Start the probe

```powershell
$capturePath = Join-Path $env:TEMP 'sstv-ipp-captures'
dotnet run --project src/SstvVPrinter.IppProbe -- --capture-directory $capturePath --port 8631
```

The endpoint is `ipp://localhost:8631/ipp/print`. Keep the console open while exercising the queue.

## Add the probe printer

With the probe already running, an elevated PowerShell can perform IPP directed discovery:

```powershell
Add-Printer -Name 'SSTV Virtual Printer Probe' -IppURL 'ipp://localhost:8631/ipp/print'
```

This command was demonstrated on Windows 11 Home 25H2 build 26200.9457. The Settings route below remains useful on systems where the cmdlet or `-IppURL` parameter is unavailable.

1. Open **Settings > Bluetooth & devices > Printers & scanners**.
2. Select **Add device**, then **Add a new device manually**.
3. Select **Add a printer using an IP address or hostname**.
4. Select device type **IPP Device**.
5. Enter `ipp://localhost:8631/ipp/print`.
6. Name the queue `SSTV Virtual Printer Probe` if Windows asks.

Verify the driver:

```powershell
Get-Printer -Name 'SSTV Virtual Printer Probe' | Format-List Name,DriverName,PortName
```

`DriverName` must report `Microsoft IPP Class Driver`. Record the exact displayed value.

The probe must advertise a coherent identity and PWG Raster capability set. In particular, Windows directed discovery requires stable printer identity fields (`printer-uuid`, make/model, device ID, and related descriptive attributes), while rendering requires matching PWG Raster resolution, color, media, side, quality, and default attributes.

## Exercise the queue

Print one single-page color portrait document and one landscape document from every locally available target: Word, Excel, PowerPoint, Paint, a representative graphics application, and one major browser. For each job, confirm that the console reports `PWG=True`, nonzero dimensions, and the expected orientation. Record the result in `ipp-compatibility-results.md`; never add captured documents to Git.

Also test cancellation, two quickly submitted jobs, and restarting the probe before another job. A cancelled job must not publish JSON metadata.

## Remove the probe queue

```powershell
Remove-Printer -Name 'SSTV Virtual Printer Probe'
```

Stop the host with Ctrl+C. Captures remain in the temporary directory until explicitly removed.

# PWG Page Composition Validation Runbook

The captured PWG files can contain private printed material. Keep them under the ignored `captures/` directory and never add them to Git. Preview PPM files also belong in an ignored scratch directory.

## Build

```powershell
dotnet build src/SstvVPrinter.ImagingTool/SstvVPrinter.ImagingTool.csproj --configuration Release
```

## Portrait, Martin 1×

```powershell
dotnet run --project src/SstvVPrinter.ImagingTool --configuration Release --no-build -- `
  --input "C:\absolute\path\portrait.pwg" `
  --mode martin1 --horizontal 1 --callsign JA1UMW `
  --accepted-at "2026-10-02T11:48:31.2773808+00:00" `
  --output "C:\absolute\scratch\portrait-m1.ppm"
```

Confirm the page is upright and completely visible, black bars do not crop it, the 16-row header contains exact red/green/blue patches, timestamp/mode appear on the left, and the complete callsign is right-aligned.

## Orientation=1 landscape, Robot 36 2×

```powershell
dotnet run --project src/SstvVPrinter.ImagingTool --configuration Release --no-build -- `
  --input "C:\absolute\path\landscape.pwg" `
  --mode robot36 --horizontal 2 --callsign JA1UMW `
  --accepted-at "2026-10-02T11:55:34.225041+00:00" `
  --output "C:\absolute\scratch\landscape-r36-2x.ppm"
```

Confirm the reported logical dimensions are swapped from the physical dimensions, the page is rotated counter-clockwise into its upright reading direction, the content bounds match the visible black letterbox, and the header treatment agrees with the reported top gap. At 2×, inspect adjacent horizontal source details rather than a duplicated 320-pixel intermediate.

## Evidence

Record the input SHA-256 with `Get-FileHash -Algorithm SHA256`. Record physical/logical dimensions, orientation, content bounds, header treatment, visual uprightness, aspect preservation, callsign completeness, and the result of inspecting fine lines. Do not record private page text or attach the captures.

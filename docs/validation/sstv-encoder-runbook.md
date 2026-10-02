# SSTV encoder validation runbook

## Generate the eight fixtures

Create an output directory and replace `C:\SstvValidation` below if desired. Existing files are never overwritten.

```powershell
New-Item -ItemType Directory -Force C:\SstvValidation
dotnet run --project src/SstvVPrinter.SstvTool -c Release -- --mode martin1  --horizontal 1 --output C:\SstvValidation\martin1-1x.wav
dotnet run --project src/SstvVPrinter.SstvTool -c Release -- --mode martin1  --horizontal 2 --output C:\SstvValidation\martin1-2x.wav
dotnet run --project src/SstvVPrinter.SstvTool -c Release -- --mode scottie1 --horizontal 1 --output C:\SstvValidation\scottie1-1x.wav
dotnet run --project src/SstvVPrinter.SstvTool -c Release -- --mode scottie1 --horizontal 2 --output C:\SstvValidation\scottie1-2x.wav
dotnet run --project src/SstvVPrinter.SstvTool -c Release -- --mode scottie2 --horizontal 1 --output C:\SstvValidation\scottie2-1x.wav
dotnet run --project src/SstvVPrinter.SstvTool -c Release -- --mode scottie2 --horizontal 2 --output C:\SstvValidation\scottie2-2x.wav
dotnet run --project src/SstvVPrinter.SstvTool -c Release -- --mode robot36  --horizontal 1 --output C:\SstvValidation\robot36-1x.wav
dotnet run --project src/SstvVPrinter.SstvTool -c Release -- --mode robot36  --horizontal 2 --output C:\SstvValidation\robot36-2x.wav
Get-FileHash C:\SstvValidation\*.wav -Algorithm SHA256
```

Expected durations (1× and 2× are equal within one 48 kHz sample): Martin 1 `115.200176 s`, Scottie 1 `110.543320 s`, Scottie 2 `72.008152 s`, Robot 36 `36.910000 s`.

## Decode check

Route WAV playback into an independent SSTV receiver using a virtual audio cable or a physical loopback. Disable audio enhancements and sample-rate conversion where possible; use 48 kHz mono. Test all four 1× files first and record receiver/version, automatic mode detection, decoded size, channel order, sync, and slant in `sstv-encoder-results.md`.

Only after all 1× modes pass, decode the 2× files. Compare the bottom 16 rows of alternating one-pixel black/white detail. Record screenshots or objective edge/contrast measurements outside Git. Do not claim a 2× improvement from subjective inspection alone, and keep 2× labeled experimental until two independent receiver implementations decode it reliably.

Generated WAV files are validation artifacts and must not be committed.

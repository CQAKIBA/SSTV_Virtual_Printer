# SSTV Virtual Printer Public Beta Release Preparation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the working SSTV Virtual Printer repository into a clean, documented, reproducibly built `v0.1.0-beta` public project with downloadable x64 and experimental ARM64 release archives.

**Architecture:** Preserve the existing print, imaging, encoder, and desktop paths. Make only an unambiguous dead-code/UI-placeholder cleanup, then add repository-wide metadata, bilingual public documentation, and two Windows GitHub Actions workflows; finish by removing internal work records and validating the exact self-contained artifacts locally.

**Tech Stack:** .NET 10 / C# 14, Avalonia 12, ASP.NET Core local IPP listener, NAudio, xUnit, GitHub Actions on Windows.

**Spec:** `docs/superpowers/specs/2026-10-06-public-release-preparation-design.md`

## Global Constraints

- Version is exactly `0.1.0-beta`; release tag is `v0.1.0-beta`.
- Copyright is exactly `Copyright (c) 2026 Daisuke JA1UMW / CQAKIBA.TOKYO`.
- Repository URL is exactly `https://github.com/CQAKIBA/SSTV_Virtual_Printer`.
- Do not publish the author's email address, captures, credentials, personal paths, or real-document screenshots.
- Windows 11 is verified; Windows 10 is best effort and unverified.
- `win-x64` is recommended; `win-arm64` is experimental and hardware-untested.
- Release outputs are self-contained, untrimmed, multi-file ZIP archives.
- Preserve `JA1UMW` test data, saved user preferences, diagnostic tools, and all active runtime behavior.
- Do not push, create a tag, or publish a GitHub Release without a later explicit confirmation.

## Review Focus

- A fresh profile with no call sign must show neutral guidance, never a plausible call sign; Task 1 pins the XAML placeholder.
- A repository scan must find no public email, personal absolute path, capture, or secret-like file; Task 5 performs the hygiene scan.
- Both release RIDs must receive the same app version and must remain self-contained/untrimmed/multi-file; Tasks 2 and 4 pin metadata and publish arguments.
- A prerelease tag must not accidentally produce a stable release; Task 4 checks the workflow's hyphen-based prerelease expression and release notes.
- Documentation must not imply PTT, receive support, verified Windows 10 support, or verified ARM64 hardware; Task 3 checks those limitations in both languages.

---

### Task 1: Remove the Unused Audio View Model and Neutralize the Call-Sign Placeholder

**Files:**
- Delete: `src/SstvVPrinter.Desktop/ViewModels/AudioEndpointViewModel.cs`
- Delete: `tests/SstvVPrinter.Desktop.Tests/AudioEndpointViewModelTests.cs`
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml`
- Modify: `tests/SstvVPrinter.Desktop.Tests/MainWindowLayoutTests.cs`

**Interfaces:**
- Consumes: existing `CallSignInput` XAML element and layout-test XML loading convention.
- Produces: `CallSignInput.PlaceholderText == "Enter your call sign"`; no production type named `AudioEndpointViewModel`.

- [ ] **Step 1: Add a failing neutral-placeholder assertion**

Extend `Call_sign_commits_on_enter_or_focus_loss` to assert:

```csharp
Assert.Equal("Enter your call sign", textBox.Attribute("PlaceholderText")?.Value);
```

- [ ] **Step 2: Run the focused test and confirm the old placeholder fails**

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj -c Release --filter "FullyQualifiedName~MainWindowLayoutTests.Call_sign_commits_on_enter_or_focus_loss"`

Expected: FAIL because the current value is `JA1UMW`.

- [ ] **Step 3: Make the placeholder neutral and delete the proven-unused class and dedicated tests**

Set `PlaceholderText="Enter your call sign"`. Delete only the two `AudioEndpointViewModel` files named above; retain `AudioEndpointInfo`, `IAudioEndpointMonitor`, and the active audio logic in `MainWindowViewModel`/`DesktopComposition`.

- [ ] **Step 4: Verify references and the desktop test project**

Run: `rg -n "AudioEndpointViewModel|PlaceholderText=\"JA1UMW\"" src tests`

Expected: no matches.

Run: `dotnet test tests/SstvVPrinter.Desktop.Tests/SstvVPrinter.Desktop.Tests.csproj -c Release`

Expected: PASS with zero warnings and errors.

- [ ] **Step 5: Commit the conservative cleanup**

```powershell
git add src/SstvVPrinter.Desktop/Views/MainWindow.axaml tests/SstvVPrinter.Desktop.Tests/MainWindowLayoutTests.cs
git add -u -- src/SstvVPrinter.Desktop/ViewModels/AudioEndpointViewModel.cs tests/SstvVPrinter.Desktop.Tests/AudioEndpointViewModelTests.cs
git commit -m "refactor: remove unused audio view model"
```

### Task 2: Add License, Version, SDK, and Repository Hygiene Metadata

**Files:**
- Create: `LICENSE`
- Create: `global.json`
- Modify: `Directory.Build.props`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: all SDK-style projects through `Directory.Build.props`.
- Produces: MSBuild properties `Version=0.1.0-beta`, `Authors=Daisuke JA1UMW / CQAKIBA.TOKYO`, `Copyright=Copyright (c) 2026 Daisuke JA1UMW / CQAKIBA.TOKYO`, `RepositoryUrl=https://github.com/CQAKIBA/SSTV_Virtual_Printer`, and `RepositoryType=git`.

- [ ] **Step 1: Record the expected repository metadata with an MSBuild query**

Run before editing:

```powershell
dotnet msbuild src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj -getProperty:Version -getProperty:Authors -getProperty:Copyright -getProperty:RepositoryUrl -getProperty:RepositoryType
```

Expected: at least the release version and repository attribution are absent or incorrect.

- [ ] **Step 2: Add common metadata and SDK selection**

Add the exact properties from the Interfaces block to the existing `Directory.Build.props` property group. Create `global.json` with `sdk.version` set to `10.0.401`, `rollForward` set to `latestFeature`, and `allowPrerelease` set to `false`.

- [ ] **Step 3: Add the standard MIT license**

Create `LICENSE` from the canonical MIT text with the exact copyright line from Global Constraints.

- [ ] **Step 4: Expand ignore rules without hiding public source**

Keep existing rules and add common Visual Studio/Rider/VS Code user state, `TestResults/`, coverage outputs, `artifacts/`, `publish/`, `*.user`, `*.suo`, logs, temporary files, and OS metadata. Do not ignore `.github/`, `docs/`, tests, solution files, or lock/config files.

- [ ] **Step 5: Verify metadata, SDK selection, and ignore behavior**

Run: `dotnet --version`

Expected: a .NET 10 SDK is selected successfully by `global.json`.

Run the MSBuild query from Step 1.

Expected: all five properties exactly match the Interfaces block.

Run: `git check-ignore -v TestResults/sample.trx artifacts/sample.zip sample.user`

Expected: all three paths are ignored by `.gitignore`.

- [ ] **Step 6: Build and commit repository metadata**

Run: `dotnet build SstvVPrinter.slnx -c Release`

Expected: exit code 0, zero warnings, zero errors.

```powershell
git add .gitignore Directory.Build.props global.json LICENSE
git commit -m "chore: add public repository metadata"
```

### Task 3: Write the Bilingual Public Documentation

**Files:**
- Create: `README.md`
- Create: `README_JP.md`
- Create: `CHANGELOG.md`
- Create: `CONTRIBUTING.md`
- Create: `SECURITY.md`
- Create: `docs/ARCHITECTURE.md`
- Create: `docs/BUILDING.md`

**Interfaces:**
- Consumes: current application behavior, validation conclusions, commands, and limitations in the approved spec.
- Produces: complete English-first and Japanese user documentation plus contributor, security, architecture, and build references.

- [ ] **Step 1: Write `README.md` as the primary English page**

Open with a prominent `日本語版はこちら` link to `README_JP.md`. Include the exact positioning idea from the spec and the sentence: “Create your image in drawing, painting, illustration, DTP, or office applications—anything that can print—and send it to SSTV through the familiar Print command.” Explain the goal of connecting amateur radio with MOE / creative fan culture.

Cover features, supported SSTV modes, first-page processing, fixed header, 2x sampling, rotation, preview/playback/WAV, audio-volume monitoring, printer setup, tray/single-instance behavior, a concise first-send walkthrough, release downloads, safety, licence responsibility, VOX caution, loopback-only support, and the complete beta limitations from Global Constraints. Do not embed a real screenshot.

- [ ] **Step 2: Write the complete Japanese `README_JP.md`**

Mirror the English document's claims and structure naturally rather than mechanically. Link back to `README.md`; explicitly state receive and PTT are absent, Windows 10 and ARM64 hardware are unverified, the local IPP print-server path is experimental, and x64 is recommended.

- [ ] **Step 3: Add release and contributor policy documents**

Create:

- `CHANGELOG.md` using Keep a Changelog-style headings with `[Unreleased]` and `[0.1.0-beta] - 2026-10-06`, listing the beta's implemented feature set and limitations.
- `CONTRIBUTING.md` with prerequisites, restore/build/test commands, focused project-test guidance, issue/bug-report evidence, style expectations, and a reminder not to commit captures or personal documents.
- `SECURITY.md` directing private vulnerability reporting through GitHub's private vulnerability reporting feature when enabled, otherwise asking reporters to open a minimal non-sensitive advisory request; explicitly forbid posting secrets, station details, or private captures in a public issue. Do not include an email address.

- [ ] **Step 4: Consolidate technical conclusions into public docs**

Write `docs/ARCHITECTURE.md` covering this flow:

`print client -> loopback IPP endpoint -> first PWG Raster page -> orientation normalization/manual rotation -> SSTV composition/header -> PCM/WAV -> selected Windows audio endpoint`.

Document project responsibilities, loopback trust boundary, printer registration, persistent preferences, playback separation, and diagnostic tools.

Write `docs/BUILDING.md` with .NET 10 prerequisites, Windows commands for restore/build/test/run, diagnostic-tool purpose, self-contained x64/ARM64 publish commands, and the distinction between `net10.0-windows10.0.19041.0` API targeting and verified OS support. Carry forward only durable compatibility/decoder conclusions; omit machine-specific hashes, captures, and personal paths.

- [ ] **Step 5: Check required claims, links, and prohibited data**

Run targeted `rg` checks confirming both READMEs mention PTT, receive scope, Windows 10, ARM64, beta/experimental status, and `win-x64`; confirm `README.md` links to `README_JP.md` and vice versa.

Run: `rg -n -i "daisuke@|C:\\\\Users\\\\Daisuke|AppData|codex-clipboard" README.md README_JP.md CHANGELOG.md CONTRIBUTING.md SECURITY.md docs/ARCHITECTURE.md docs/BUILDING.md`

Expected: no matches.

- [ ] **Step 6: Commit public documentation**

```powershell
git add README.md README_JP.md CHANGELOG.md CONTRIBUTING.md SECURITY.md docs/ARCHITECTURE.md docs/BUILDING.md
git commit -m "docs: add public beta documentation"
```

### Task 4: Add Windows CI and GitHub Release Automation

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/release.yml`

**Interfaces:**
- Consumes: `SstvVPrinter.slnx`, .NET 10 SDK selection, desktop project, and `Version` metadata.
- Produces: push/PR verification and tag-driven x64/ARM64 prerelease ZIPs plus `SHA256SUMS.txt`.

- [ ] **Step 1: Add the CI workflow**

Create `ci.yml` for pushes and pull requests on `windows-latest` with `permissions: contents: read`, `actions/checkout@v7`, `actions/setup-dotnet@v6`, and sequential commands:

```powershell
dotnet restore SstvVPrinter.slnx
dotnet build SstvVPrinter.slnx -c Release --no-restore
dotnet test SstvVPrinter.slnx -c Release --no-build
```

- [ ] **Step 2: Add the release workflow build job**

Trigger only on pushed tags matching `v*`; grant `contents: write`. First run restore/build/test once. Publish `src/SstvVPrinter.Desktop/SstvVPrinter.Desktop.csproj` for a matrix of `win-x64` and `win-arm64` with `--self-contained true`, `PublishSingleFile=false`, `PublishTrimmed=false`, and output beneath `artifacts/publish/<rid>`.

Derive filenames from `github.ref_name`: x64 is `SSTV-Virtual-Printer-<tag>-win-x64.zip`; ARM64 is `SSTV-Virtual-Printer-<tag>-win-arm64-experimental.zip`. ZIP each publication with `Compress-Archive`, preserving the application files at the archive root.

- [ ] **Step 3: Add checksum and GitHub Release publication steps**

Generate `SHA256SUMS.txt` containing SHA-256 and filename for both ZIPs. Upload the three release files as workflow artifacts, then create the GitHub Release using `softprops/action-gh-release` pinned to a full commit SHA rather than a floating tag. Set `prerelease` from whether `github.ref_name` contains `-`; release notes must recommend x64 and label ARM64 hardware-untested/experimental.

- [ ] **Step 4: Statically verify workflow contracts**

Use `rg` to confirm:

- CI has `contents: read`, both triggers, .NET 10 setup, restore/build/test, and no write permission.
- Release has `contents: write`, `v*`, both RIDs, `--self-contained true`, `PublishSingleFile=false`, `PublishTrimmed=false`, both exact filename patterns, `SHA256SUMS.txt`, a full-SHA release action reference, and the hyphen-based prerelease expression.

Expected: every required contract has exactly one intentional match and no plaintext secret or email appears.

- [ ] **Step 5: Exercise the workflow's publish contract locally**

Run both exact `dotnet publish` commands into ignored `artifacts/publish/win-x64` and `artifacts/publish/win-arm64`. Verify each directory contains `SstvVPrinter.Desktop.exe`, `.dll`, `.deps.json`, `.runtimeconfig.json`, Avalonia/NAudio dependencies, and more than one file; verify no source, PDB, capture, or test assembly is present.

- [ ] **Step 6: Commit automation**

```powershell
git add .github/workflows/ci.yml .github/workflows/release.yml
git commit -m "ci: add build and beta release workflows"
```

### Task 5: Remove Internal Work Records and Perform the Public-Tree Audit

**Files:**
- Delete: `docs/superpowers/`
- Delete: `docs/validation/`
- Modify only if the audit finds an unambiguous public-release defect: files already named in Tasks 1-4.

**Interfaces:**
- Consumes: the public docs that replaced durable internal validation conclusions.
- Produces: a public-facing repository tree with no internal planning/runbook material.

- [ ] **Step 1: Verify the public docs contain the durable validation conclusions**

Confirm `docs/BUILDING.md` records Windows 11 verification, Windows 10 best-effort status, actual SSTV decoder success for supported modes, and the experimental status of horizontal 2x and ARM64 without copying capture hashes or machine-specific paths.

- [ ] **Step 2: Delete internal work records**

Delete `docs/superpowers/` and `docs/validation/` through the patch workflow. This intentionally removes this implementation plan and its source spec from the eventual public tree after they have served execution.

- [ ] **Step 3: Audit tracked filenames and text**

Run:

```powershell
git ls-files
rg -n -i "daisuke@|C:\\Users\\Daisuke|AppData\\Local\\Temp|codex-clipboard|BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY|password\s*[:=]|api[_-]?key\s*[:=]" -g '!bin/**' -g '!obj/**' -g '!artifacts/**' .
```

Expected: no email, personal absolute path, temporary attachment path, private key, or credible credential. Test call signs may remain. Inspect any generic false positive before changing code.

Run: `git ls-files | rg -i "(^|/)(captures?|bin|obj|TestResults|artifacts|publish)/|\.(wav|pwg|png|jpg|jpeg|zip|pdb|user|suo|trx)$"`

Expected: no tracked generated/capture/personal artifacts.

- [ ] **Step 4: Commit the public-tree cleanup**

```powershell
git add -u -- docs/superpowers docs/validation
git commit -m "chore: remove internal development records"
```

### Task 6: Run the Complete Release Candidate Verification

**Files:**
- No intended source changes.
- Local ignored output: `artifacts/release-check/`.

**Interfaces:**
- Consumes: the complete public tree and both publish configurations.
- Produces: evidence that the source is clean and the x64/ARM64 archives are structurally releasable; it does not push or tag.

- [ ] **Step 1: Verify formatting and repository integrity**

Run:

```powershell
dotnet format SstvVPrinter.slnx --verify-no-changes
git diff --check
git status --short
```

Expected: format and diff checks pass; the worktree is clean.

- [ ] **Step 2: Restore, build, and run the full suite**

Run:

```powershell
dotnet restore SstvVPrinter.slnx
dotnet build SstvVPrinter.slnx -c Release --no-restore
dotnet test SstvVPrinter.slnx -c Release --no-build
```

Expected: every command exits 0 with zero build warnings/errors and all tests pass.

- [ ] **Step 3: Produce the two release-candidate publications and archives**

Publish `win-x64` and `win-arm64` with the exact Task 4 flags into `artifacts/release-check/<rid>`. Create:

- `artifacts/release-check/SSTV-Virtual-Printer-v0.1.0-beta-win-x64.zip`
- `artifacts/release-check/SSTV-Virtual-Printer-v0.1.0-beta-win-arm64-experimental.zip`
- `artifacts/release-check/SHA256SUMS.txt`

Expected: checksum verification succeeds for both files and archive inspection satisfies Task 4 Step 5.

- [ ] **Step 4: Smoke-test the x64 archive**

Extract x64 to a new directory under `artifacts/release-check/`, launch the extracted executable, confirm the main window opens, tray/single-instance behavior remains functional, then exit through `File (F) -> Exit` and confirm the process ends. Do not register/unregister the printer or transmit RF as part of this release smoke test.

- [ ] **Step 5: Verify documentation links and Git state**

Check every relative Markdown link in `README.md`, `README_JP.md`, `CONTRIBUTING.md`, and `docs/*.md` resolves to an existing tracked file. Run `git status --short` and `git log --oneline -8`.

Expected: clean worktree and a reviewable sequence of task commits.

- [ ] **Step 6: Stop before publication and report the release candidate**

Report test counts, publish sizes, SHA-256 values, x64 smoke-test result, ARM64's untested status, and any remaining caveat. Show the exact proposed remote/tag commands, but do not run `git remote add/set-url`, `git push`, or `git tag` until the user explicitly approves publication.

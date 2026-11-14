# Transmission Workflow Fixes Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** RadioFAX の同期幅を明示した4モードを追加し、送信停止・進捗・ヘッダ配置・キュー表示・送信後削除・プレビュー走査線を順番に改善する。

**Architecture:** 変調条件は `SstvVPrinter.Sstv` のモード定義に集約し、画像合成条件は `TransmissionSettings` 経由で Imaging に渡す。送信フェーズと再生進捗は `TransmissionController` を唯一の進捗源として Desktop ViewModel に通知し、UI はその状態だけからボタン、残り時間、走査線を描画する。

**Tech Stack:** .NET 8, C#, Avalonia UI, xUnit

**Spec:** 2026-10-21 のユーザー承認済み要件（本会話）および `docs/superpowers/specs/2026-10-19-radiofax-transmit-design.md`

---

### Task 1: RadioFAXを同期幅付き4モードにする

**Files:**
- Modify: `src/SstvVPrinter.Sstv/SstvMode.cs`
- Modify: `src/SstvVPrinter.Sstv/Radiofax/RadiofaxModeDefinition.cs`
- Modify: `src/SstvVPrinter.Sstv/Radiofax/RadiofaxModes.cs`
- Modify: `src/SstvVPrinter.Sstv/Radiofax/RadiofaxPlanner.cs`
- Modify: `src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: affected mode-name tests and RadioFAX planner/encoder tests

1. Add failing tests for these exact display names and 10 ms / 20 ms black line-sync spans:
   - `RadioFAX - 120/288/10ms`
   - `RadioFAX - 120/288/20ms`
   - `RadioFAX - 120/576/10ms`
   - `RadioFAX - 120/576/20ms (JMH Standard)`
2. Replace the two provisional RadioFAX enum members with four cleanly named variants. No migration alias is required for the provisional preference values.
3. Store sync duration in `RadiofaxModeDefinition`. Keep each line at 500 ms by centering the black pulse inside the 22.5 ms dead sector: 6.25/10/6.25 ms or 1.25/20/1.25 ms white/black/white.
4. Run focused Sstv and Desktop mode tests, then commit.

### Task 2: Stop recovery and phase-correct progress

**Files:**
- Modify: `src/SstvVPrinter.App/Transmission/TransmissionController.cs`
- Modify: `src/SstvVPrinter.Desktop/Hosting/DesktopComposition.cs`
- Modify: `src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml`
- Modify: controller and ViewModel tests

1. Add failing tests proving Encoding and Transmitting each start at 0%, and Stopped restores Send/WAV for the still-active prepared job.
2. Remove the combined 20/80 scale. Reset progress on entering playback and calculate remaining time only from playback duration.
3. Render playback as `送信中 残り12:34 (56.7%)`; render generation as its own phase without a bogus remaining time.
4. Ensure cancellation does not clear `IsReady` or active-job eligibility when an already prepared transmission was merely stopped.
5. Run focused App/Desktop tests, then commit.

### Task 3: Add per-mode header overlay setting

**Files:**
- Modify: `src/SstvVPrinter.Imaging/Composition/HeaderPlacement.cs`
- Modify: `src/SstvVPrinter.Imaging/Composition/CompositionRequest.cs`
- Modify: `src/SstvVPrinter.Imaging/Composition/SstvFrameComposer.cs`
- Modify: `src/SstvVPrinter.Imaging/Composition/RadiofaxFrameComposer.cs`
- Modify: `src/SstvVPrinter.App/Transmission/TransmissionSettings.cs`
- Modify: `src/SstvVPrinter.App/Settings/UserPreferences.cs`
- Modify: `src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml`
- Modify: `src/SstvVPrinter.Desktop/Hosting/DesktopComposition.cs`
- Modify: composition/settings/UI tests

1. Add failing tests for `ReserveSpace` (fit the page below a header margin within the fixed output height) and `Overlay` (draw the header over the page within the same fixed output height).
2. Implement both behaviors for SSTV and RadioFAX without increasing the receiver-visible page extent. RadioFAX must keep the output inside the A4-derived body height so fixed-A4 receivers do not truncate the tail.
3. Add `ヘッダを画像に重ねる` beside/below horizontal resolution and persist the choice per transmission mode.
4. Default to `ReserveSpace`, which preserves all source content by treating the header as printer-style top margin.
5. Run focused Imaging/App/Desktop tests, then commit.

### Task 4: Improve queue and protocol layout

**Files:**
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml`
- Add/Modify: queue-row presentation ViewModel or converters as needed
- Modify: layout/ViewModel tests

1. Add failing UI-structure tests for columns `日時`, `ソース名`, `UUID` in that order.
2. Replace the UUID-first ListBox template with aligned column headers and rows while preserving selection binding.
3. Stretch the protocol ComboBox to the full panel width.
4. Run focused Desktop tests, then commit.

### Task 5: Make send-completion deletion optional

**Files:**
- Modify: `src/SstvVPrinter.App/Settings/UserPreferences.cs`
- Modify: `src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml`
- Modify: `src/SstvVPrinter.Desktop/Hosting/DesktopComposition.cs`
- Modify: settings/host/ViewModel tests

1. Add failing tests for a persisted `送信完了後に削除` preference.
2. Place the checkbox below the action-button grid.
3. Only call `Queue.CompleteActive()` after successful Send/WAV when checked; otherwise retain the prepared active job for repeat tests.
4. Default to retaining the job so the new behavior removes the current testing friction.
5. Run focused App/Desktop tests, then commit.

### Task 6: Show approximate transmitted line in preview

**Files:**
- Modify: `src/SstvVPrinter.Desktop/ViewModels/MainWindowViewModel.cs`
- Modify: `src/SstvVPrinter.Desktop/Views/MainWindow.axaml`
- Modify: `src/SstvVPrinter.Desktop/Hosting/DesktopComposition.cs`
- Modify: ViewModel/layout tests

1. Add failing tests mapping normalized image-scan progress to a clamped preview Y position.
2. Overlay a thin red horizontal line on the preview only during the image-bearing part of Transmitting.
3. Hide it during leader/control sections, Encoding, Stopped, Completed, and failures. Approximate one line per image row; do not model Scottie/Robot component sub-scans.
4. Use the existing playback progress notifications, avoiding a second timer.
5. Run focused Desktop tests, then commit.

### Task 7: Focused regression and prerelease handoff

**Files:**
- Modify: `CHANGELOG.md`
- Modify: `Directory.Build.props` only when assigning the next prerelease version

1. Run the focused RadioFAX, composition, transmission, settings, ViewModel, and layout test groups; do not rerun unrelated full-suite tests unless a shared-layer failure demands it.
2. Publish a local win-x64 Release build and smoke-check its output files.
3. Review the diff for compatibility and unrelated changes.
4. With user approval, tag/push and publish the next prerelease.

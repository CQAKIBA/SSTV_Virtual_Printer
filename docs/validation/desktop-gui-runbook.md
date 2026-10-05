# Desktop GUI validation runbook

## Scope

Use this checklist for the Windows 11 milestone. Perform the full Windows 10 pass after the milestone is stable. Record the date, OS build, application commit, audio device, and result beside each item.

## Launch and lifetime

- Launch the application and confirm only one window and one tray icon appear.
- Launch it a second time. Confirm the existing window is restored and no second IPP listener is started.
- Close the title-bar window. Confirm the process and IPP printer remain available and the tray **Open** command restores the window.
- With no queued job, choose **App > Exit** and confirm immediate exit.
- With a queued job, choose **App > Exit**, cancel the warning once, then accept it. Confirm queued data is discarded only after acceptance.
- Toggle **Start at sign-in**, restart the application, and verify the setting remains selected. Verify this uses the current-user startup entry and requires no elevation.

## Printer and intake

- Use **Printer > Verify**, **Register**, and **Repair**. Confirm the printer name is `SSTV Virtual Printer Probe` and URI is `ipp://localhost:8631/ipp/print`.
- Print one image from Explorer or Paint while the window is visible. Confirm the window activates, the first page enters the FIFO, and a preview appears.
- Hide the window and print again. Confirm it is restored or flashes for attention and the job is queued.
- Print a landscape PDF from Acrobat Reader. Confirm the page orientation and preview are correct.
- Print another document while transmitting. Confirm it waits in FIFO order and does not replace the active transmission.

## Preparation and display

- Enter a valid call sign and confirm the fixed top header contains RGB squares, JST time, mode, and the complete right-aligned call sign.
- Enter an invalid or empty call sign. Confirm Send and WAV save remain unavailable and no stale preview becomes active.
- Check Scottie 2, Scottie 1, Martin 1, and Robot 36 previews.
- Toggle horizontal **2x** and verify preview width/header geometry doubles while the mode timing remains correct.
- Resize the window above and below 900 px. Confirm the five right-side groups retain this order: Call sign, Hardware, Protocol, Status, Actions; at narrow width they stack below the preview.
- Check 100%, 125%, 150%, and 200% Windows display scaling and keyboard-only navigation.

## Audio status and transmission

- Select a real output endpoint. Confirm the displayed Windows master volume matches Settings/volume mixer.
- Mute the endpoint externally. Confirm `MUTED / ミュート中` appears prominently beside Send; unmute and confirm it clears. The application must not change volume or mute itself.
- Save a WAV and decode it using the known smartphone SSTV Decoder application. Confirm VIS mode and image/color bars decode correctly.
- Press Send. Confirm hardware/protocol/call-sign controls lock, status becomes orange/red, progress and remaining time update, and Stop becomes available.
- Press Stop during encoding and again during playback. Confirm both stop safely and repeated Stop clicks do not fail.
- Disconnect the selected device during playback. Confirm a visible failed state and that the temporary WAV is deleted.
- Complete one real transmission and confirm the next FIFO job becomes active.

## Result record

| Date | OS/build | Commit | Check area | Result | Notes |
|---|---|---|---|---|---|
| 2026-10-05 | Windows / .NET 10 | Post-review HEAD | Automated solution tests | Pass | 324 tests: SSTV 125, IPP 45, Imaging 99, App 32, Desktop 23 |
| 2026-10-05 | Windows / .NET 10 | Post-review HEAD | Release build | Pass | 0 warnings, 0 errors |
| Pending | Windows 11 | Pending | Full run | Not run | Requires interactive operator validation |
| Deferred | Windows 10 | Pending | Full run | Deferred | Agreed to perform after milestone stabilization |

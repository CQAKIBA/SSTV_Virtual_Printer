# SSTV Virtual Printer Desktop GUI Design

## 1. Status and scope

This document defines the approved desktop user interface and interactive-host behavior for SSTV Virtual Printer. It supplements the product design dated 2026-10-02 and takes precedence where that earlier document differs on GUI behavior, job persistence, audio-volume control, WAV export, or optional comments.

The initial target remains Windows 10 and Windows 11. The GUI is implemented with Avalonia UI and .NET so that the platform-neutral application logic can later be reused on Linux or Raspberry Pi. Windows integration is isolated behind interfaces rather than spread through view models.

Reception, PTT/CAT/CI-V control, image editing, and LAN operation remain outside this GUI milestone.

## 2. Interaction principles

- Receiving a print job never starts transmission automatically.
- The operator always sees the final composed image before transmission.
- Sending and saving a WAV file are explicit, separate actions.
- The application is visually distinct from conventional SSTV receiver software.
- Potentially dangerous or ineffective output states are obvious without allowing the application to change system volume unexpectedly.
- Frequently used controls remain on one screen; infrequent application settings live in the menu bar.

## 3. Visual direction

The application uses one fixed, vivid high-contrast theme. Theme switching is not part of the initial release.

- Near-black charcoal window and panel backgrounds
- Clear gray panel boundaries
- Cyan for normal interactive controls and WAV export
- Magenta or blue-purple for selection and job identity accents
- Lime green for a valid ready-to-send state and the primary send action
- Yellow for warnings that do not prevent operation
- Vivid red/orange for muted, failed, or transmitting states as appropriate
- A neutral surround around the image preview so colors in the transmitted image are not visually contaminated by the application chrome

During transmission, the identity/status area changes to red/orange, displays `TRANSMITTING / 送信中`, locks mutable settings, emphasizes Stop, and prominently shows progress and estimated remaining time.

## 4. Main-window layout

The application uses one primary window.

### 4.1 Menu bar

The menu bar contains infrequently changed actions, including:

- Enable or disable start at Windows sign-in
- Printer registration, verification, or repair entry points
- Diagnostics and log access
- About/help
- Complete application exit

Start at sign-in is enabled by default after installation. Its menu item is a checked toggle and can be disabled at any time.

### 4.2 Left region

The left region prioritizes a large preview of the exact final bitmap passed to the SSTV encoder. It includes the mode-specific header, image fit, overlay behavior, and horizontal 1x/2x choice.

Below the preview, the active print job is shown prominently. Waiting jobs appear as a compact FIFO list with source information when available. The operator may select a waiting job for inspection and delete an individual waiting job, but initial-release reordering is not supported.

### 4.3 Right control panel

The right panel uses this fixed top-to-bottom order:

1. **Call sign**
   - Required call-sign field
   - Empty or invalid content prevents sending
2. **Hardware settings**
   - Audio output-device selection
   - Read-only Windows volume value for the selected endpoint when available
   - Read-only mute status
3. **Protocol settings**
   - Martin 1, Scottie 1, Scottie 2, or Robot 36
   - Standard 1x or experimental horizontal 2x
4. **Status**
   - Ready, preparing, encoding, transmitting, stopped, completed, or failed
   - Estimated duration before transmission
   - Progress and remaining time during transmission
   - Actionable validation and device warnings
5. **Execution controls**
   - Save WAV
   - Send
   - Stop while preparing or transmitting
   - Discard current job

The Send button includes the chosen mode or estimated duration where space permits. No additional confirmation dialog is required because print intake already stops at this preparation screen and requires an explicit Send action.

## 5. Audio-volume visibility and safety

The GUI reads the selected Windows audio endpoint's master-volume and mute state through a Windows-specific audio-endpoint service.

- It does not change the endpoint's volume or mute state.
- A numeric or percentage volume value is displayed when available.
- A muted endpoint produces a prominent `MUTED / ミュート中` warning near the status and Send action.
- An unavailable volume reading is shown as unknown rather than as zero.
- Device loss or an invalid device prevents transmission and gives an actionable error.

The warning is advisory for mute because audio routing may intentionally use unusual hardware. The implementation may initially allow Send while muted, but it must never present the overall state as normal Ready without the warning. Whether mute becomes a hard block can be revisited after real-device testing.

## 6. Print-arrival and window behavior

When a new print job arrives, the application:

1. Enqueues it without overwriting another job.
2. Restores the main window if minimized or hidden.
3. Requests foreground activation and focus.
4. Falls back to taskbar flashing and a desktop notification if Windows refuses focus activation.

This behavior has no initial preference toggle. It can be made configurable later if it interferes with other work.

Closing the main window with the title-bar Close button hides it to the notification area. IPP listening continues. The notification-area menu can reopen the window or completely exit the application.

## 7. Job queue and lifetime

- Jobs are held in an in-memory FIFO queue.
- Each print action creates a separate job.
- Jobs received while another job is prepared or transmitted wait in order.
- Sending, saving, or discarding the active job advances to the next job.
- Saving a WAV file does not play audio.
- Closing or restarting the application does not restore pending jobs.
- Complete exit warns when active or waiting jobs will be lost.
- Temporary payload files, if required to safely accept IPP data, are implementation details and must be removed on normal shutdown and cleaned on the next start after an abnormal exit. They do not constitute a recoverable queue.

This deliberately favors preventing forgotten-job transmission over crash recovery.

## 8. Persisted preferences

The application persists these preferences across launches:

- Call sign
- Last selected SSTV mode
- Horizontal 1x/2x selection
- Last selected audio output device, with a safe fallback when unavailable
- Start-at-sign-in preference

Each job may use different values while it is active. The latest values become the defaults for subsequently received jobs. Print document contents and the pending queue are not persisted as user-restorable state.

An arbitrary header comment is not included in the initial GUI. Operators can place free-form text in the source image using the originating application.

## 9. Application structure

The desktop layer follows an MVVM-style separation:

```text
Avalonia Views
    -> View models / commands / observable state
    -> Application services and existing platform-neutral imaging/SSTV core
    -> Platform abstractions
         -> Windows foreground and notification-area integration
         -> Windows start-at-sign-in integration
         -> Windows audio-endpoint state reader
         -> Audio playback implementation
```

The IPP receiver publishes new jobs to an application-level queue service. UI code does not parse IPP, decode PWG Raster, compose images, or generate SSTV samples. Long-running composition, WAV generation, and playback never block the UI thread and support cancellation.

Only the adapters that genuinely require Windows APIs are Windows-specific. Avalonia views, view models, queue logic, validation, settings models, and the existing encoder/imaging libraries remain portable.

## 10. State and concurrency rules

- Only one job may be active for preparation, encoding, or playback.
- Only one audio transmission may run at a time.
- Mutable settings are disabled after transmission begins.
- Stop is idempotent and safe during generation or playback.
- A completed, stopped, or failed job is never sent again without a new explicit Send action.
- A new print job cannot steal focus inside the application from the currently transmitting job or replace its preview.
- UI state changes are driven by application state, not by timing assumptions in the view.

## 11. Errors and warnings

The main status panel provides plain-language messages for:

- Missing call sign
- Missing or disconnected audio output
- Muted Windows endpoint
- Unknown Windows volume state
- Composition or encoding failure
- WAV-save failure
- Playback failure or device loss
- Unsupported or malformed print job
- Attempted exit with pending jobs

Errors that prevent Send clearly identify the field or device requiring attention. Document contents are not copied into diagnostic logs.

## 12. Verification

Automated tests cover:

- View-model state transitions and command enablement
- FIFO arrival, selection, deletion, completion, and discard behavior
- Non-persistence of queued jobs
- Persistence and fallback of approved preferences
- Cancellation during encoding and playback
- Read-only mapping of volume, muted, unknown, and disconnected endpoint states
- Correct locking and status changes during transmission
- Foreground-request fallback behavior through mocked platform services

Avalonia UI tests or focused integration tests verify the principal bindings and that the main actions remain reachable at supported window sizes. Manual Windows tests verify:

- Windows 10 and Windows 11 rendering and scaling
- Notification-area Close/reopen/exit behavior
- Start-at-sign-in toggling
- Foreground activation, taskbar flashing, and notifications
- Real endpoint volume/mute observation
- Print arrival while hidden, preparing, and transmitting
- Keyboard navigation, visible focus, and high-contrast readability

## 13. Approved layout reference

The approved direction is the preview-priority layout: large final-image preview on the left and a vertically ordered control panel on the right. The visual brainstorming artifacts under `.superpowers/` are intentionally excluded from version control; this document is the durable design authority.

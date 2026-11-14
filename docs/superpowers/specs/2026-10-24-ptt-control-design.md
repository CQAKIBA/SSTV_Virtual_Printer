# PTT Control Design

## Intent

Add optional serial-port PTT control to the desktop application so SSTV transmission can key a connected transmitter automatically while still supporting the existing audio-only workflow. The controls live in the existing HARDWARE panel and must fail safely if a USB serial device is removed or a serial operation fails.

## Goals

- Offer `N/A` plus currently available COM ports as the PTT port selection.
- Support RTS and DTR as the initial PTT control methods.
- Let the operator open and close the selected port manually.
- Automatically attempt to open the selected port when manual PTT or transmission requires it.
- Provide a press-and-hold manual PTT test control.
- For automatic transmission, assert PTT, wait one second, play the audio, wait one second, then release PTT.
- Recover to a closed, unkeyed UI state without hanging when the device is removed or an operation fails.
- Persist the selected port and control method.
- Leave an extension point for transmitter-specific command protocols.

## Non-goals

- Configurable lead-in or tail delays in the first version.
- Serial baud rate, framing, or command configuration in the first RTS/DTR version.
- Automatically closing a healthy port after each transmission; closing remains a manual operation or an error/shutdown action.
- Supporting multiple simultaneously open PTT devices.

## Anti-goals

- Never leave the transmitter keyed after stop, failure, device removal, or application shutdown.
- Never allow a COM failure to freeze the UI thread or leave the UI reporting OPEN after the connection has been lost.
- Never make PTT hardware mandatory for the existing audio-only transmission path.

## Selected approach

### Considered approaches

1. Put serial PTT directly in `TransmissionController`. This gives direct transmission timing but introduces Windows serial concerns into the otherwise platform-neutral App layer and does not fit manual PTT well.
2. Operate the serial port from the window event handlers. This is initially small but distributes cleanup across send, stop, mouse/key release, exceptions, removal, and shutdown.
3. Add an independent PTT controller in the Windows Desktop layer and use it from both the UI and the send orchestration. **Selected.** It centralizes ownership, state, failure recovery, and future control methods without coupling the core transmission pipeline to a serial implementation.

### Port-list refresh approaches

1. Rescan continuously. This follows device changes but may mutate the list while it is being navigated.
2. Rescan every time the selection changes. This makes keyboard navigation unstable.
3. Scan at startup and whenever the drop-down opens, replacing the list only when its contents changed and preserving the current selection when possible. **Selected.** Closed-combo cursor navigation does not trigger a scan.

## UI design

Append the following controls inside the current HARDWARE panel, below the audio controls:

```text
PTT PORT
[N/A / COM1 / COM2 / ...       ↓] [OPEN or CLOSE]
[RTS / DTR                     ↓] [PTT]
```

- `N/A` is always the first port entry and is the default when no saved selection is available.
- With `N/A` selected, OPEN/CLOSE and manual PTT are disabled and transmission remains audio-only.
- The connection button displays `OPEN` while closed. Once successfully opened, it displays `CLOSE` and uses the existing transmit-red color to show the live open state.
- The PTT button is a momentary button: pointer or keyboard press asserts PTT immediately, and release, lost capture/focus, cancellation, or error releases it immediately.
- The PTT button is red whenever PTT is asserted, whether assertion came from the manual button or automatic transmission.
- While the port is open, port and method selection are disabled. While automatic transmission owns PTT, CLOSE and manual PTT interaction are disabled.
- The HARDWARE panel can no longer be disabled as one unit during transmission. Existing mutable audio settings and the PTT selectors are locked individually, while PTT state remains visible.
- PTT errors appear in the existing status/error area using a concise Japanese message, and the controls return to the closed state.

## State and ownership

The Desktop project owns an `IPttController` abstraction and a serial implementation. Its observable state is conceptually:

- selected port (`N/A` or port name)
- selected method (`RTS` or `DTR`)
- connection state (`Closed`, `Opening`, `Open`, `Faulted`)
- assertion state (`Released`, `Asserted`)
- last error

All open, close, assert, release, and disposal operations are serialized so manual and automatic actions cannot race. The controller owns at most one serial-port object. It publishes state changes for the view model on the Avalonia UI dispatcher.

RTS and DTR use the asserted/high signal as PTT ON and deasserted/low as PTT OFF. Polarity inversion is left as a future control-method option if hardware demand appears.

## Port discovery and removal

- Enumerate port names at application startup and immediately before opening the drop-down.
- Build the new list off-screen, sort COM names naturally, prepend `N/A`, and update the bound collection only if the contents changed.
- Preserve the selected name if it remains present. If a closed port has disappeared, select `N/A` and persist that selection.
- Do not use list refresh as the connection-health mechanism.
- While a port is open, perform a lightweight periodic presence check outside the UI thread. If the selected name disappears, treat it as a disconnect, cancel any active PTT lease/transmission, best-effort release the line, dispose the port, and publish `Closed` with an error.
- Any serial exception during assert, release, or close follows the same cleanup path. Cleanup is bounded and must not wait indefinitely on the serial device.

## Manual operation

Pressing manual PTT with a selected but closed port first attempts to open it. Only a successful open may assert the line. If the operator releases the button before the asynchronous open completes, the pending request is cancelled or invalidated and must never assert PTT afterward. Manual PTT has no one-second lead or tail delay because it is a direct test control. Releasing the button releases PTT but leaves the port open.

If automatic transmission already owns PTT, manual PTT does nothing. CLOSE is disabled while either automatic transmission or a manual press owns PTT; the operator releases manual PTT before closing the port.

## Automatic transmission flow

Wave generation remains unchanged and completes before the transmitter is keyed. Immediately before audio playback:

1. If the selected port is `N/A`, play audio using the existing flow with no PTT delays.
2. Otherwise, open the selected port if necessary. If opening fails, abort before audio playback and report the error.
3. Acquire exclusive automatic PTT ownership and assert the selected RTS or DTR line.
4. Wait a fixed one-second lead interval using the transmission cancellation token.
5. Play the prepared audio through the selected audio endpoint.
6. After normal playback completion, wait a fixed one-second tail interval.
7. Release PTT in a `finally` path and keep a healthy port open.

Stop, application shutdown, audio failure, serial failure, or device removal skips the remaining delay and requests immediate release. PTT-release failure is contained by disposing the serial port and reporting the controller as closed. Device removal also cancels audio playback so the overall job ends as stopped/failed rather than continuing unkeyed.

The PTT lead and tail intervals are part of transmission progress/state but not audio progress. The UI may continue to display the transmitting state during these intervals; its percentage remains at 0% during lead-in and 100% during tail-out.

## Preferences

Extend user preferences with optional PTT port name and PTT method. Defaults are `N/A` and RTS. Loading an older preferences JSON must retain those defaults. Selecting a port or method queues a preference save through the existing persistence path. OPEN/CLOSE and live assertion are runtime state and are not persisted.

At startup, retain a saved COM name only if it is present in the initial scan. If it is absent, select and persist `N/A`. Ports are never opened automatically at startup.

## Extensibility

Represent RTS and DTR as control-method implementations or strategies behind the PTT controller rather than branching in view event handlers. A later transmitter-specific command method can add its own configuration and assertion/release behavior without changing the automatic transmission sequence.

## Error handling

- OPEN failure: remain closed, do not assert or transmit, display the error.
- Device removed while idle/open: transition to closed and display the error.
- Device removed during PTT or transmission: cancel transmission, attempt immediate release/cleanup, transition to closed, display the error, and finish the job as failed. A user-requested STOP remains stopped rather than failed.
- STOP during the one-second lead, playback, or tail: cancel immediately and release PTT without waiting for the rest of the delay.
- Application exit: cancel active work, release PTT best-effort, close and dispose the port before Desktop composition disposal completes.
- Repeated CLOSE/dispose calls are idempotent.

## Testing

- Unit-test the PTT controller against a fake serial-port adapter: open/close, auto-open, RTS and DTR assertion, mutual exclusion, idempotent cleanup, exceptions, removal, and cancellation.
- Unit-test the automatic PTT transmission wrapper: one-second lead/tail ordering, no delay for `N/A`, abort on open failure, immediate cleanup on stop, and cleanup on playback failure.
- Unit-test view-model state: list preservation, `N/A`, control enablement, OPEN/CLOSE label/color, and manual/automatic PTT red state.
- Extend Avalonia layout/smoke tests for the new HARDWARE controls and ensure keyboard press/release behavior reaches the same manual PTT operations as pointer input.
- Extend preferences tests to cover round trips and backward-compatible loading of old JSON.
- Keep serial hardware out of automated tests by injecting discovery, port, delay, and removal-monitor abstractions.

## Builder decisions

- Choose concrete interface and class names consistent with the current project.
- Choose the exact bounded monitoring interval and cleanup timeout; approximately one second is appropriate for device-presence monitoring.
- Use the platform-supported .NET serial API and add its package reference if the target framework does not provide it implicitly.
- Keep UI strings consistent with the existing Japanese UI while retaining the requested labels `PTT PORT`, `N/A`, `OPEN`, `CLOSE`, `RTS`, `DTR`, and `PTT`.

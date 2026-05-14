# Deferred issues

Issues the user has flagged for later. Not blockers; pick them up between
larger work batches.

## Test runs trigger Windows Firewall permission popup every time

- **What:** Every `dotnet test` invocation that exercises code touching
  UDP/network surfaces fires a Windows Firewall "Allow access?" dialog.
- **Why it's annoying:** breaks unattended CI / local watch loops;
  forces the operator to click through.
- **Investigation needed (when picked up):**
  1. Identify which test fixture(s) bind a real socket. Likely
     candidates: anything under `Tests/AgValoniaGPS.Services.Tests/`
     that touches `IUdpCommunicationService` or `NtripClientService`
     for real, or any test that instantiates the simulator's
     `VirtualGpsReceiver` / `VirtualSteerModule` with real sockets
     (versus a mocked `IUdpCommunicationService`).
  2. Decide per fixture whether the real-socket behaviour is actually
     needed for the assertion, or whether NSubstitute-mocked
     `IUdpCommunicationService` would suffice.
  3. For the ones that genuinely need sockets — consider binding
     `127.0.0.1` only (Windows firewall normally exempts loopback,
     but PInvoke / Mapsui may force a non-loopback bind that
     triggers the prompt).
  4. Cross-check `Tests/AgValoniaGPS.IntegrationTests/VirtualModules/`
     which has its own `VirtualGpsReceiver`/`VirtualSteerModule` copies
     used by `Tests/AgValoniaGPS.Services.Tests/SimulatorDataFlowTests.cs`.

- **Not in scope right now.** Picked up when test wall-clock becomes a
  real issue or when CI gets stood up.

## Version SHA in About dialog should be copyable

The About dialog already shows the build SHA (`88202834` for v8 in the
session that flagged this), but it's plain text — operator has to retype
it into a bug report. Make it a selectable / one-click-copy field
(Avalonia `SelectableTextBlock` or a TextBlock with a small "copy"
button). Tiny UX win, useful every time the operator files a dump.

## Log Viewer button in Tools panel is disabled

`Shared/AgValoniaGPS.Views/Controls/Panels/ToolsPanel.axaml:35-36` has
a `LogViewer` MenuButton with no `Command="..."` binding, so it appears
greyed out / unresponsive. The dialog itself exists and works fine
(wired from `FileMenuPanel.axaml` via `ShowLogViewerDialogCommand`).
Fix is a one-line addition:

```xml
Command="{Binding ShowLogViewerDialogCommand}"
```

See also `RollCorrection` button (line 50-51) — same problem but no
destination exists yet (only edit fields inside Configuration →
Sources → Roll subtab). Either needs a quick-access roll-zero dialog
or removal from the Tools panel.


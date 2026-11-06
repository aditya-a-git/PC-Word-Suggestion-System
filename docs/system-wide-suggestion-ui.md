# System-wide suggestion UI

## Status

A separate WinForms popup component and deterministic UI logic are implemented. The component is not wired into an active external-app production adapter: the current TSF service is diagnostic only and the production Windows integration project contains only managed protocol/state logic. No external caret currently reaches this popup from a supported application. Do not call this end-to-end system-wide functionality.

## Components

- `PCWord.WindowsUI.Logic` provides candidate state and pure placement geometry. It keeps at most three case-insensitive unique candidates, resets selection on a newer context, rejects out-of-order state changes, and validates version/field identity before a candidate can be selected for acceptance.
- `PCWord.WindowsUI` provides `SuggestionPopupWindow` and `SuggestionPopupController`. The controller subscribes to the existing `DictionaryCompletionCoordinator.SuggestionsChanged` event, marshals updates to the UI thread, hides on empty/stale/unpositionable results, and delegates acceptance to `DictionaryCompletionCoordinator.AcceptAsync`. It never edits external text itself.
- The window renders “Complete word” or “Next word”, a maximum of three candidates, a visible selected marker, numbered labels, accessible candidate names/descriptions, system colors, and a high-contrast border. Mouse click selects and requests acceptance through the coordinator.
- The controller exposes `MoveSelection`, `AcceptSelectedAsync`, and `Dismiss` for a future approved input-key route. It installs no global hook and does not intercept the target application's keyboard. Keyboard navigation is therefore not presently available in external apps.

## Window activation and lifetime

The form uses `WS_EX_NOACTIVATE` and `WS_EX_TOOLWINDOW`, overrides `ShowWithoutActivation`, returns `MA_NOACTIVATE` for `WM_MOUSEACTIVATE`, overrides WinForms `ShowWithoutActivation`, and repositions/shows through `SetWindowPos` with `SWP_NOACTIVATE`. It is not shown in the taskbar. It is moved to `HWND_TOPMOST` only while shown; `Hide` explicitly demotes it to `HWND_NOTOPMOST`. These API choices are intended to preserve the current activation state; focus preservation still requires an interactive Windows test with the real target app.

## Positioning and DPI

The caller must provide actual screen-space caret bounds in the same physical-pixel coordinate space used by Win32. The window queries the monitor work area with `MonitorFromRect` and `GetMonitorInfo`, then uses the pure `PopupPlacement` function to place below the caret by default, above when needed, and clamp within the work area. It supports negative monitor coordinates and an RTL horizontal anchor. If there is no caret bound, the controller hides instead of guessing.

The UI project opts into WinForms `PerMonitorV2` and uses `AutoScaleMode.Dpi`; native positioning uses screen pixels. The geometry tests cover monitor bounds, negative coordinates, above/below, RTL, and oversized popup clamping. Visual behavior at 100%, 125%, 150%, and 200%, monitor transitions, and mixed-DPI movement has not been run. No claim of correct double-scaling behavior is made until that manual pass is complete. Microsoft recommends PMv2 for desktop applications and documents WinForms per-monitor scaling behavior ([WinForms DPI configuration](https://learn.microsoft.com/en-us/dotnet/framework/winforms/automatic-scaling-in-windows-forms), [Windows high-DPI development](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)).

## Stale results and acceptance

Every displayed candidate set comes from the coordinator update's context version and field ID. Before rendering, the controller checks `DictionaryCompletionCoordinator.IsCurrent`; selection state also binds those identity values. Before acceptance it checks them again and calls the existing coordinator, which calls the adapter. A click acceptance is serialized to prevent duplicate requests. A stale/refused result hides the window.

The external TSF adapter currently has no authorized write session. Therefore external acceptance cannot currently succeed through a production adapter. This UI component must not be paired with SendInput, clipboard injection, or direct document writes.

## Accessibility and interaction limits

Candidates expose accessible names/descriptions and system/high-contrast colors; selection uses both a glyph and state text rather than color alone. The top-level popup never takes keyboard focus. This means keyboard-only traversal of the popup through normal focus is not implemented; an approved TSF/input key route is required. There is no live-region announcement policy yet, so repeated updates have not been tested with screen readers.

## Automated verification

The pure UI logic tests verify candidate cap/deduplication, selection wraparound, context ordering/identity, and placement. They do not create a native window and cannot prove no-activation behavior, mouse behavior, focus preservation, UI Automation accessibility, or visual fidelity. Those require interactive Windows testing.

## Performance and compatibility

No popup creation/show/hide, positioning, rendering, keyboard, or end-to-end latency measurements have been collected. The component is reused rather than recreated per result, but no performance claim is made.

The compatibility matrix currently has no external app run for this UI. Test Notepad first with the production adapter supplying real caret bounds; verify the target retains focus while typing, clicking a candidate behaves as intended, and hiding the popup restores non-topmost state. Then test each DPI/monitor combination and app version before expanding support.


## Synthetic visual harness

Run the separate harness on Windows with:

```powershell
dotnet run --project .\tools\PCWord.WindowsUIHarness\PCWord.WindowsUIHarness.csproj
```

The button shows three synthetic candidates anchored at the mouse pointer and reports a click without editing any document. This exercises presentation and a basic mouse route only. It does not receive external caret metadata, does not verify focus against another app, does not route keyboard input, and does not substitute for the Notepad integration test.

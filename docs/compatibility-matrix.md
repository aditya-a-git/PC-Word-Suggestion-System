# Compatibility matrix

“Implemented” describes code present; “tested” requires an executed test on the named application and version. No cross-application compatibility is claimed.

| Environment / field | Text context | Caret / selection | Popup / explicit edit | Status and evidence |
|---|---|---|---|---|
| PCWord Stage 0 WinForms controlled editor | In-process context | WinForms selection start/length and focus | In-window child-control suggestion panel; keyboard acceptance replaces current word | Stage 3 implementation present; user has confirmed focus behavior manually. |
| Basic Win32 edit control (Notepad) | UIA probe checks whether TextPattern exposes bounded context | Probe reports selection and caret-geometry availability | No edit path | Probe implemented; manual result not yet recorded. |
| Browser text input | UIA probe checks provider support | Probe reports selection and caret-geometry availability | No edit path | Probe implemented; manual result not yet recorded. |
| Browser contenteditable | UIA probe checks provider support | Probe reports selection and caret-geometry availability | No edit path | Probe implemented; manual result not yet recorded. |
| Office editor | UIA probe checks provider support | Probe reports selection and caret-geometry availability | No edit path | Probe implemented; manual result not yet recorded. |
| VS Code editor | UIA probe checks provider support | Probe reports selection and caret-geometry availability | No edit path | Probe implemented; manual result not yet recorded. |
| Terminal | UIA probe checks provider support | Probe reports selection and caret-geometry availability | No edit path | Probe implemented; manual result not yet recorded. |
| Sensitive field | UIA probe skips context for provider-reported password fields; TSF classifier blocks Password, PIN, and Private input scopes | No global edit path | No global edit path | Detection depends on host metadata; unknown scope must remain blocked. Runtime scope behavior is not yet verified. |
| Elevated app, remote desktop, game/custom canvas | Unknown | Unknown | Unknown | Not tested; no compatibility claim. |

## Windows UI Automation probe

Run `dotnet run --project .\tools\PCWord.WindowsProbe\PCWord.WindowsProbe.csproj` on Windows. It reports metadata and character counts, not text. It skips surrounding context for fields UI Automation identifies as password fields and whenever a selection exists. The probe is read-only; it does not prove a safe edit path. Record actual results only after testing each application and field type.

## Stage 3 popup notes

- Suggestions begin at a two-character prefix; one-character prefixes are suppressed.
- Popup is a child control inside the Stage 0 editor window, not a top-level Form.
- Popup placement uses the app-owned RichTextBox caret coordinates only.
- Keyboard acceptance supports Ctrl+Space and Ctrl+1/Ctrl+2/Ctrl+3. Escape dismisses suggestions.
- Suggestions use a demo vocabulary; prediction-engine integration is separate work.
- The cross-process `IInputAdapter` is not implemented.

## Manual test record template

Record Windows build, application/version, exact field type, API/prototype and permission level. Record capability flags and pass/fail only; never copy typed text into notes. Do not infer results from another application in the same category.


## TSF input-scope safety status

The native TSF probe now treats Password, NumericPin, AlphanumericPin, NumericPassword, AlphanumericPassword, and Private input scopes as sensitive. The classifier and explicit opt-in gate have CTest targets, but they have not yet been verified in a Windows build/runtime in this work session. InputScope is not guaranteed to be present or correct in every control; an unavailable/failed scope remains Unknown and must not enable context reads. See [Windows integration status](windows-integration-status.md).

## Updated implementation status (2026-10-27)

| Environment / field | Text context | Caret / selection | Popup / explicit edit | Status and evidence |
|---|---|---|---|---|
| PCWord owned WinForms RichTextBox | Concrete bounded in-process `IInputAdapter`; maximum 128 units before and 32 after | Versioned selection/focus, caret offset, and app-owned screen bounds | Existing child panel; dictionary pipeline; exact current-prefix replacement with result verification | Implemented and covered by managed unit tests. Interactive Windows UI verification is still pending, so not marked runtime tested. |
| Other applications (Notepad, browsers, Office, VS Code, terminals) | UIA/TSF probes only; no production context feed | Probe metadata only | No external popup or edit path | Untested and unsupported by production integration. |
| Sensitive or unknown contexts | Adapter blocks marked sensitive and unavailable contexts; TSF classifier fails closed on unknown/error | No external edit path | No acceptance | Policy covered by tests; runtime host metadata not universally proven. |
| Elevated, remote, game/custom-rendered fields | No production adapter | Unknown | No edit path | Not tested; unsupported. |

The CI history includes failures in runs 77–82 from SDK-dependent InputScope enum names in native C++ compilation. The fix in `fa7ba0b` uses portable numeric policy values grounded in the Windows SDK; subsequent main runs 83 (`fa7ba0b`) and 84 (`c36e077`) passed. This does not verify runtime behavior in external applications.

The Stage 0 controlled-editor completion path is implemented but remains pending interactive Windows verification. Stage 4 and cross-application compatibility are not established.

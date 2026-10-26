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
| Sensitive field | Harness checkbox suppresses suggestions; probe skips context if UIA marks password | Harness only; probe checks provider selection | No global edit path | Global sensitivity detection is not implemented. |
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

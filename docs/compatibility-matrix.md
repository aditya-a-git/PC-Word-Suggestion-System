# Compatibility matrix

“Implemented” describes code present; “tested” requires an executed test on the named application and version. No cross-application compatibility is claimed.

| Environment / field | Text context | Caret / selection | Popup / explicit edit | Status and evidence |
|---|---|---|---|---|
| PCWord Stage 0 WinForms controlled editor | In-process bounded context | WinForms selection start/length and focus | Non-activating-style popup; keyboard acceptance replaces current word | Stage 3 implementation present. Manual execution on Windows is still required; no runtime evidence is recorded here. |
| Basic Win32 edit control (Notepad) | Unknown | Unknown | Unknown | Not tested; no production adapter. |
| Browser text input / contenteditable | Unknown | Unknown | Unknown | Not tested; no production adapter. |
| Office editor | Unknown | Unknown | Unknown | Not tested; no production adapter. |
| VS Code editor | Unknown | Unknown | Unknown | Not tested; no production adapter. |
| Terminal | Unknown | Unknown | Unknown | Not tested; no production adapter. |
| Password / sensitive field | Disabled when the controlled-editor checkbox is enabled | Harness only | Popup hidden; edit refused by model | Controlled simulation only; global sensitive-field detection is not implemented. |
| Elevated app, remote desktop, game/custom canvas | Unknown | Unknown | Unknown | Not tested; no compatibility claim. |

## Stage 3 popup notes

- Popup non-activation is implemented using WinForms window styles; manual testing must confirm that typing remains in the editor on the target Windows build.
- Popup position uses caret geometry from PCWord's own RichTextBox. No external caret coordinates have been acquired or verified.
- Suggestions are a hard-coded demo list; dictionary integration belongs to Stage 4.
- Keyboard acceptance supports Ctrl+Space and Ctrl+1/Ctrl+2/Ctrl+3. Escape dismisses suggestions. Mouse selection is not implemented.
- Acceptance checks the current editor text and selection snapshot before applying a local replacement. This is not an implementation of the Stage 2 cross-process IInputAdapter contract.

## Manual test record template

For each test, record Windows build, application name/version, exact field type, API/prototype and permission level. Use disposable text such as alpha beta, put the caret at start/middle/end, select a range, test punctuation, move focus, and apply a unique explicit replacement. Verify context/caret/edit separately. For Stage 3, additionally verify popup visibility, keyboard focus, screen-edge placement, Escape dismissal, stale acceptance refusal, and exact resulting text. Never use private or sensitive text. Do not infer results from another app in the same category.

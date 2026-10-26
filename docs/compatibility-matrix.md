# Compatibility matrix

“Implemented” describes code present; “tested” requires an executed test on the named application and version. No cross-application compatibility is claimed.

| Environment / field | Text context | Caret / selection | Popup / explicit edit | Status and evidence |
|---|---|---|---|---|
| PCWord Stage 0 WinForms controlled editor | In-process context | WinForms selection start/length and focus | In-window child-control suggestion panel; keyboard acceptance replaces current word | Stage 3 implementation present. Manual runtime verification remains required. |
| Basic Win32 edit control (Notepad) | Unknown | Unknown | Unknown | Not tested; no production adapter. |
| Browser text input / contenteditable | Unknown | Unknown | Unknown | Not tested; no production adapter. |
| Office editor | Unknown | Unknown | Unknown | Not tested; no production adapter. |
| VS Code editor | Unknown | Unknown | Unknown | Not tested; no production adapter. |
| Terminal | Unknown | Unknown | Unknown | Not tested; no production adapter. |
| Sensitive field | Disabled when the controlled-editor checkbox is enabled | Harness only | Popup hidden | Global sensitive-field detection is not implemented. |
| Elevated app, remote desktop, game/custom canvas | Unknown | Unknown | Unknown | Not tested; no compatibility claim. |

## Stage 3 popup notes

- Suggestions begin at a two-character prefix; one-character prefixes are suppressed.
- Popup is a child control inside the Stage 0 editor window, not a top-level Form. This avoids activating a separate window.
- Popup placement uses the app-owned RichTextBox caret coordinates only.
- Keyboard acceptance supports Ctrl+Space and Ctrl+1/Ctrl+2/Ctrl+3. Escape dismisses suggestions.
- Suggestions use a demo vocabulary; dictionary integration belongs to Stage 4.
- The Stage 2 cross-process IInputAdapter is not implemented.

## Manual test record template

Record Windows build, application/version, exact field type, API/prototype and permission level. For Stage 3, verify popup persistence while typing, focus retention, screen-edge placement, Escape dismissal, stale-context rejection, sensitive-field suppression, and exact text/caret after edits. Never use private or sensitive text. Do not infer results from another application in the same category.

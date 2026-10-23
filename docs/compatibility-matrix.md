# Stage 0 compatibility matrix

“Implemented” describes code present; “tested” requires an executed test on the named application and version. No cross-application compatibility is claimed.

| Environment / field | Text context | Caret / selection | Explicit edit | Status and evidence |
|---|---|---|---|---|
| PCWord Stage 0 WinForms controlled editor | In-process text snapshot; current word and up to 128 preceding UTF-16 units | WinForms selection start/length; focus events | Replaces current word or selection in owned field | Implemented harness; execution not verified in this environment (no .NET SDK). |
| Basic Win32 edit control (Notepad) | Unknown | Unknown | Unknown | Not tested; follow manual experiment. |
| Browser text input / contenteditable | Unknown | Unknown | Unknown | Not tested; follow manual experiment. |
| Office editor | Unknown | Unknown | Unknown | Not tested; follow manual experiment. |
| VS Code editor | Unknown | Unknown | Unknown | Not tested; follow manual experiment. |
| Terminal | Unknown | Unknown | Unknown | Not tested; follow manual experiment. |
| Password / sensitive field | Disabled in harness when marked sensitive | Harness only | Refused by model | Model behavior covered by authored automated test; system field detection unimplemented. |
| Elevated app, remote desktop, game/custom canvas | Unknown | Unknown | Unknown | Not tested; no compatibility claim. |

## Manual test record template

For each test, record Windows build, application name/version, exact field type, API/prototype and permission level. Use disposable text such as `alpha beta`, put the caret at start/middle/end, select a range, test punctuation, move focus, and apply a unique explicit replacement. Verify exact resulting text and caret. Record context/caret/edit independently as pass, fail or unavailable. Never use private or sensitive text. Do not infer results from another app in the same category.

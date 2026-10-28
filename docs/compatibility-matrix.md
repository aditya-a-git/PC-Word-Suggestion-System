# Compatibility matrix

“Implemented” means code exists. “Automated” means repository tests exercise the contract. “Runtime verified” requires an actual named-app test; no cross-application support is inferred.

| Environment / field | Context | Caret / selection | Suggestions / edit | Status |
|---|---|---|---|---|
| PCWord-owned WinForms RichTextBox | Bounded in-process snapshot, maximum 128 units before and 32 after | Versioned focus/selection/caret state; app-owned caret geometry | Existing child panel, dictionary candidates, explicit prefix-only edit with verification | Stage 4 implementation and managed tests complete. The benchmark uses synthetic control/input delivery. Live UI timing/painting was not measured in this run; runtime status remains unverified here. |
| Notepad/basic Win32 edit | Owner-observed: TSF activation, focus/edit notifications, collapsed selection, caret rectangle; `GetAppProperty` succeeds and `GetValue(GUID_PROP_INPUTSCOPE)` returns E_FAIL. Diagnostic bounded read is eligible only when enabled, allowlisted, structurally eligible, and opted in. | Owner-observed caret rectangle; no repository log supplied | No production prediction or edit path | Diagnostic observations reported by owner; not independently recorded in repo. Unsupported for production prediction. |
| Browser text input or contenteditable | UIA feasibility probe only | Provider-dependent metadata | No production popup or edit path | Password-field detection reported by owner, but no log in repo. No browser is allowlisted by default; unsupported / untested for prediction. |
| Office, VS Code, terminal | UIA/TSF probes only | Provider-dependent metadata | No production popup or edit path | Unsupported / untested. |
| Sensitive or unknown field | Owned editor has an explicit sensitive flag; TSF diagnostic classifier denies sensitive/unknown/failure | Not used for external prediction | No external edit | Fail-closed policy has automated tests; host metadata behavior is not universally proven. |
| Elevated, remote, game/custom-rendered fields | No production adapter | Unknown | No edit path | Unsupported / not tested. |

## Scope notes

- The existing system-owned popup remains inside the PCWord form; no system-wide overlay exists.
- `tools/PCWord.WindowsProbe` is read-only UI Automation feasibility code. `tools/PCWord.TsfProbe` is a diagnostic feasibility component, not the production adapter.
- Stage 4 freshness and pause behavior are tested using the current adapter contract and fakes. The native TSF policy tests do not prove host behavior.
- Record actual app/version/field results only after a runtime test. Store capabilities and pass/fail, never typed text.

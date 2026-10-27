# Windows integration implementation status

## Implemented

- The PCWord-owned WinForms `RichTextBox` has a concrete `IInputAdapter`. It reads at most 128 UTF-16 units before and 32 after the caret through bounded RichEdit range requests, publishes a per-control field ID and increasing context versions, and reports focus, selection, composition, sensitivity, caret offset, and screen bounds where available.
- The adapter suppresses collection unless enabled, focused, unselected, not composing, and not marked sensitive. It does not install a keyboard hook, retain a document copy, log text, or send context over a process boundary.
- `DictionaryCompletionCoordinator` routes eligible snapshots through the existing context manager, dictionary ranker, and casing formatter; it caps results at the existing three-candidate limit and rejects stale acceptance through version, field, candidate-provenance, and context checks.
- Explicit completion in the owned editor replaces only the expected current prefix, then verifies the resulting bounded text and caret. This adapter does not edit other applications.
- TSF diagnostics now identify failed COM/TSF methods and HRESULTs and retain fail-closed scope classification. The SDK portability failure in earlier CI runs was fixed in `fa7ba0b`.

## Not implemented or not established

- There is no production cross-process TSF-to-.NET adapter or IPC boundary.
- There is no TSF authorized write edit session or external-app acceptance operation. The current TSF component remains a diagnostic feasibility probe.
- There is no system-wide popup. The existing child suggestion panel is confined to PCWord's own window and caret coordinates.
- The owned-editor code has compiled and passed automated tests, but its live Windows UI behavior has not been manually verified in this task. No foreign application is claimed compatible.
- UI Automation TextPattern remains read-only and provider-dependent. It is not used as an automatic editing fallback.
- `ITfContext::GetAppProperty(GUID_PROP_INPUTSCOPE, ...)` is the documented property acquisition route. `ITfReadOnlyProperty::GetValue` may return `S_FALSE` for unsupported properties; the observed `E_FAIL` is unspecified. No demonstrated API misuse explains the recorded failure, so it remains unresolved and Unknown/denied.

## Privacy and fallback

Only the controlled editor can supply context to the completion pipeline. Unknown, unavailable, sensitive, selected, unfocused, disabled, or composing contexts produce no candidates and cannot be accepted. Candidate and context data are transient and bounded. No raw key events or typed text are logged or persisted. Other applications are left untouched.

## Verification

The latest GitHub Actions runs for `fa7ba0b` and `c36e077` succeeded. Earlier failures in runs 77–82 came from SDK-dependent InputScope identifiers in the native policy header; the portability fix was committed in `fa7ba0b`. Current local managed results and Windows manual checks are recorded in `docs/testing-guide.md`.

On Windows, run the synthetic-text manual procedure in the testing guide. The TSF failure reproduction and application compatibility matrix remain unverified until those manual runs are recorded.

## Stage 4 gate

Stage 4 remains gated. A controlled-editor dictionary path is implemented, but interactive correctness has not been demonstrated in this run, and no tested production integration for external apps exists. Do not infer system-wide support or treat compilation as satisfying the integration gate.

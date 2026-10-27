# Windows integration status

## Stage 4: controlled-editor dictionary completion

Implemented for PCWord's own WinForms `RichTextBox`:

- Bounded in-process adapter snapshots feed the existing context manager and local frequency-ranked dictionary. Each result carries its source context version and field identity; stale/equal snapshots are ignored and the UI checks both values before display.
- The adapter reads no more than 128 UTF-16 units before and 32 after the caret. It suppresses text reads for disabled, unfocused, selected, composing, sensitive, or unavailable states. Explicit acceptance revalidates current context and candidate provenance, edits only the intended prefix, then verifies text and caret.
- Empty results clear existing candidates. No confidence score was invented; the existing frequency ranking, two-character prefix minimum, and three-candidate cap remain the policy.
- Pause clears candidates and turns off bounded text acquisition and acceptance. Resume requests a new adapter snapshot; the previous candidate set is not restored.
- Numeric-only aggregate timings cover dictionary startup, context acquisition, lookup/ranking, result delivery, UI update, and end-to-end delivery. Rings are fixed at 2,048 values per stage. The benchmark command and actual measurements are in `docs/benchmarks.md`.

The Release solution build succeeded with zero warnings/errors; 126 managed tests passed. Native policy CTest passed 2/2. The measured benchmark used the bundled nine-entry sample dictionary and a synthetic consumer. It is not representative of a production vocabulary or actual WinForms painting. Live UI timing was not collected in this run; use the app's **Performance stats** view during a controlled-editor session to collect those values.

## Not implemented or not established

- No production cross-process TSF-to-.NET adapter or IPC boundary.
- No authorized TSF write edit session, external-app acceptance, or system-wide popup.
- No production UI Automation adapter; its TextPattern probe is read-only and provider-dependent.
- No foreign application is supported by this integration. Notepad, browsers, Office, VS Code, terminals, elevated applications, remote sessions and custom controls remain unsupported/untested.
- The observed `ITfReadOnlyProperty::GetValue(GUID_PROP_INPUTSCOPE)` `E_FAIL` remains unresolved. The documented property path and method-level diagnostics are in the TSF feasibility notes. Any missing, failed, malformed, or unknown safety metadata remains denied.

## Production integration gate (2026-10-27)

The requested production Windows integration was audited against current `main`. No production IPC, TSF-authorized write operation, external popup, or real host/control test harness exists in this revision. The TSF DLL remains a feasibility probe and must not be described as an adapter.

The `GetAppProperty(GUID_PROP_INPUTSCOPE)` then `ITfReadOnlyProperty::GetValue` sequence follows Microsoft's documented `ITfInputScope` example. The current source cannot determine from the observed `E_FAIL` alone whether the property is unsupported by that context owner or failed for another host-specific reason. Microsoft describes InputScope as text-input guidance; it is not a general proof that arbitrary field contents are non-sensitive. Accordingly, a failed, unavailable, or unrecognized property remains denied, and UI Automation must not be used as an automatic fallback.

The native DLL and policy tests build on the current Windows x64 toolchain; this verifies compilation and pure policy behavior only. No TSF profile was registered or activated, and no external application was tested in this run. The single targeted runtime step required to investigate the host-specific failure is: on a disposable Windows account, register the x64 diagnostic DLL per `docs/tsf-feasibility.md`, activate it in a standard text control containing synthetic text, and capture only operation names/HRESULTs from a debugger output viewer. Do not enable bounded context reads for this diagnostic step. This may clarify the failing host/API operation but cannot establish sensitivity safety or production support.

Production external prediction remains blocked until a supported safety signal and opt-in policy are established, then the native-to-managed request/response path, TSF edit-session acceptance, non-activating popup, and supported-host runtime tests are implemented and verified. No external application is currently supported.

## Offline and privacy status

The normal path reads the bundled dictionary locally and has no network client. A scoped source search found no network-client, text logging, or content-write API matches in `PCWord.Core`, `PCWord.Prediction`, or `PCWord.Stage0`. This was source inspection, not a runtime network capture. Input text, prefixes, candidates, and field identifiers are not included in metrics or logs; details are in `docs/privacy-model.md`.

## Stage 4 status and next stage

Stage 4 is complete for the existing controlled-editor path and its synthetic performance baseline. This does not complete TSF integration or claim arbitrary Windows application compatibility. Stage 5 may proceed independently using the local coordinator; its concrete n-gram and held-out evaluation plan is in `plan.md` and `docs/benchmarks.md`. No Stage 5 code or evaluation has been delivered.

The current dictionary ranker runs synchronously over the local in-memory index, so there is no cooperative cancellation point and no concurrent prediction response can overtake a later UI input event. The coordinator still rejects stale/equal snapshot versions, binds delivery to field identity, and the form rechecks both before rendering. If future inference becomes asynchronous or materially slower, add cancellation but retain these version/field checks at result delivery and acceptance.

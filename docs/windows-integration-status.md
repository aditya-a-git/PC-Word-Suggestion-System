# Windows integration status

## Settings and privacy controls (partial)

The controlled-editor executable now opens a dedicated Settings window backed by `UserSettingsService` and `JsonUserSettingsStore`. The persisted Enabled value is applied to the suggestion coordinator; disabling hides the popup and invalidates pending suggestion state. A service update is validated and saved before notification, and a failed save leaves the old runtime value in place.

The UI describes current privacy behavior and explicitly marks external application permissions, prediction-mode selection, personalization, metrics, and configurable runtime shortcuts unavailable where this build has no verified enforcement path. Settings schema version 1 remains backward compatible. Registry-backed diagnostic TSF values have not been migrated.

This does not complete production settings/privacy integration. In particular, the executable-name list is not a production external-app security boundary, and the TSF probe is not a production adapter. See [settings-and-privacy.md](settings-and-privacy.md) for implemented behavior, migration limits, and remaining gates.


## Stage 4: controlled-editor dictionary completion

Implemented for PCWord's own WinForms `RichTextBox`:

- Bounded in-process adapter snapshots feed the existing context manager and local ESDB commonness/variant-ranked dictionary. Each result carries its source context version and field identity; stale/equal snapshots are ignored and the UI checks both values before display.
- The adapter reads no more than 128 UTF-16 units before and 32 after the caret. It suppresses text reads for disabled, unfocused, selected, composing, sensitive, or unavailable states. Explicit acceptance revalidates current context and candidate provenance, edits only the intended prefix, then verifies text and caret.
- Empty results clear existing candidates. No confidence score was invented; the existing frequency ranking, two-character prefix minimum, and three-candidate cap remain the policy.
- Pause clears candidates and turns off bounded text acquisition and acceptance. Resume requests a new adapter snapshot; the previous candidate set is not restored.
- Numeric-only aggregate timings cover dictionary startup, context acquisition, lookup/ranking, result delivery, UI update, and end-to-end delivery. Rings are fixed at 2,048 values per stage. The benchmark command and actual measurements are in `docs/benchmarks.md`.

The Release solution build succeeded with zero warnings/errors; 126 managed tests passed. Native policy CTest passed 2/2. The measured benchmark used the bundled nine-entry sample dictionary and a synthetic consumer. It is not representative of a production vocabulary or actual WinForms painting. Live UI timing was not collected in this run; use the app's **Performance stats** view during a controlled-editor session to collect those values.

## Not implemented or not established

- No production cross-process TSF-to-.NET adapter or IPC boundary.
- No authorized TSF write edit session, external-app acceptance, or system-wide popup.
- No production UI Automation adapter; its TextPattern probe is read-only and provider-dependent.
- No foreign application is supported for production prediction. Notepad may be used for gated diagnostic reads; browsers, Office, VS Code, terminals, elevated applications, remote sessions and custom controls remain unsupported/untested for prediction.
- The observed `ITfReadOnlyProperty::GetValue(GUID_PROP_INPUTSCOPE)` `E_FAIL` remains unresolved. The documented property path is implemented. A missing/failed/unknown result remains Unknown; diagnostic bounded reads are now permitted for Unknown only when the exact host executable is user-allowlisted and all other policy gates pass. Sensitive scopes continue to deny.

## Production integration gate (2026-10-27)

The TSF probe remains diagnostic-only. There is no production IPC, TSF-authorized write operation, external popup, or real host/control test harness. It must not be described as a production prediction adapter.

The `GetAppProperty(GUID_PROP_INPUTSCOPE)` then `ITfReadOnlyProperty::GetValue` sequence follows Microsoft's documented `ITfInputScope` example. The current source cannot determine from the observed `E_FAIL` alone whether the property is unsupported by that context owner or failed for another host-specific reason. Microsoft describes InputScope as text-input guidance; it is not a general proof that arbitrary field contents are non-sensitive. Failed/unknown input scope is allowed only for the exact host executable name in the user allowlist, subject to the enabled toggle, structural checks, and independent diagnostic opt-in. The default allowlist contains only Notepad. Browser allowlisting is discouraged because unknown non-password fields may contain card numbers or OTPs.

The native DLL and policy tests build on the current Windows x64 toolchain; this verifies compilation and pure policy behavior only. Owner-reported TSF activation in Notepad is recorded below, but it has not been independently reproduced in this task. There is still no repository runtime evidence of bounded text reads under the new policy.

Owner-reported runtime evidence says registration failed non-elevated with `ITfInputProcessorProfiles::Register` returning `0x80004005`, and succeeded elevated. The repository does not independently verify this. The owner also reports TSF activation/focus/edit notifications, collapsed selection and caret geometry in Notepad, and `GetValue` returning E_FAIL. Password detection in browser fields is owner-reported but has no repository log. These reports are not automated test evidence.

The default managed settings are paused with a `notepad.exe` allowlist. The app writes `Enabled` (DWORD) and `Allowlist` (REG_MULTI_SZ) under `HKCU\Software\PCWord`; the TSF probe rereads settings per edit notification and denies if either value is missing, unreadable, or malformed. The user toggle uses Ctrl+Alt+Shift+P / MOD_NOREPEAT where registration succeeds; the app reports registration failure and the UI toggle remains available. File-name matching is exact/case-insensitive and can be spoofed by another binary with the same name; path/signature checking is deferred.

Production external prediction remains unsupported. The allowlist safety model only gates diagnostic bounded reads; it does not implement predictions, the native-to-managed request/response path, TSF edit-session acceptance, or a non-activating popup. No external application is supported for production prediction.

## Offline and privacy status

The normal path reads the bundled dictionary locally and has no network client. A scoped source search found no network-client, text logging, or content-write API matches in `PCWord.Core`, `PCWord.Prediction`, or `PCWord.Stage0`. This was source inspection, not a runtime network capture. Input text, prefixes, candidates, and field identifiers are not included in metrics or logs; details are in `docs/privacy-model.md`.

## Stage 4 status and next stage

Stage 4 is complete for the existing controlled-editor path and its synthetic performance baseline. Stage 5's bounded local n-gram predictor, evaluator and controlled-editor integration have been delivered, but the fixed-corpus held-out quality evaluation and UI verification remain incomplete; see `docs/benchmarks.md`. Neither stage completes TSF integration or claims arbitrary Windows application compatibility.

The current dictionary ranker runs synchronously over the local in-memory index, so there is no cooperative cancellation point and no concurrent prediction response can overtake a later UI input event. The coordinator still rejects stale/equal snapshot versions, binds delivery to field identity, and the form rechecks both before rendering. If future inference becomes asynchronous or materially slower, add cancellation but retain these version/field checks at result delivery and acceptance.

## Safety-policy verification run (2026-10-28)

The managed Release build passed with zero warnings/errors and 139/139 managed tests passed. The native x64 probe built successfully in `tools/PCWord.TsfProbe/build-validation`; CTest passed 2/2, including the full decision matrix, exact filename matching, and sensitive-scope regression tests. Tracked `build/` and `tools/PCWord.TsfProbe/build/` outputs were removed from the Git index only; local files were preserved because the existing DLL may be registered/locked. The new registry policy was not runtime-tested inside an activated TSF host, and no external-app production behavior is claimed as verified.


## Stage 6 compatibility reporting (observation only)

The TSF probe emits fixed status metadata for activation, focus and edit notifications, including selection/caret availability, bounded before/after character counts, InputScope stage/HRESULT, classification and policy decision. It does not emit text, candidates, window titles or field labels, and it does not insert text or display a popup. The new `PCWord.CompatReport` CLI parses only the probe marker records and generates aggregate Markdown rows grouped by process ID. Noise from other processes is not included in output. Raw DebugView logs must remain local and ignored.

GitHub Actions run [40702996019](https://github.com/aditya-a-git/PC-Word-Suggestion-System/actions/runs/40702996019) for commit `badf6aeeaf8339b273439aa71186099c450285fa` completed successfully: Release restore/build passed with 0 warnings/errors, managed tests passed 159/159, the native x64 probe built, and CTest passed 2/2. No TSF host session was performed. Owner-reported Notepad/browser observations remain undated and lack exact app versions and saved logs. All other external app/version/field rows remain Untested. No production external-app support is claimed.


## TSF feasibility recheck (2026-11-06)

**Implemented:** native diagnostic service implements `ITfTextInputProcessor`, `ITfThreadMgrEventSink`, and `ITfTextEditSink`; status-only focus/edit events; TSF selection/caret/InputScope queries; policy-gated bounded diagnostic read attempt; HKCU COM/profile registration; C++ deny/allow policy tests.

**Built and automated-policy-verified:** GitHub Actions run [42220760078](https://github.com/aditya-a-git/PC-Word-Suggestion-System/actions/runs/42220760078) passed solution build, managed tests, native x64 DLL build, and CTest 2/2.

**Not runtime-verified in an application:** service activation, actual external text-store access, caret/selection reads, lifecycle/focus behavior, registration privilege requirements, and app-specific sensitive-field behavior. Existing Notepad/browser notes are owner-reported, undated, and lack versions/logs.

**Not implemented:** TSF `ITfEditSession` write path / `RequestEditSession`, post-edit verification, production cross-process adapter/IPC, external prediction, or popup. Therefore external edit and document-integrity criteria are unverified, and no external application is claimed supported. Build and policy tests do not establish feasibility.

The safe next step is a controlled Windows runtime pass using synthetic text and the procedure in `docs/testing-guide.md`. Implement a write-session experiment only after context acquisition and invalidation are verified in a host.


## Production input adapter work (2026-11-06)

**Managed contract implemented and automated-test pending:** `src/PCWord.WindowsIntegration` provides bounded protocol validation and a cancellation/stale-result gate. Tests are wired into the existing managed test project. This component does not provide transport or connect to the TSF probe.

**Not implemented:** named-pipe transport, trusted peer authentication, production native TSF service, external-app context delivery, external suggestion UI, or authorized TSF write session.

**Not verified:** any production external-app flow, host compatibility, disconnection/restart behavior, acceptance, or end-to-end latency. The earlier TSF probe is diagnostic only. See `docs/windows-integration-architecture.md` for boundaries and verification gates. Stage 0 remains open.


## System-wide suggestion UI (2026-11-06)

**Implemented:** separate `PCWord.WindowsUI` WinForms popup and `PCWord.WindowsUI.Logic`. The form uses no-activate/tool-window styles, nonactivating show/position calls, topmost ordering only while visible, caret/work-area geometry, high-contrast system colors, accessible candidate names, and a three-candidate cap. The controller consumes `DictionaryCompletionCoordinator` versioned events and delegates acceptance back to the coordinator.

**Automated-test verified:** pure candidate state and popup placement tests run in the managed suite. The first CI run exposed an incorrect test expectation for a popup clamped at the right edge of a negative-coordinate monitor; the expectation has been corrected and a new run is in progress.

**Not runtime-verified:** focus preservation, mouse activation, caret alignment, DPI movement, monitor changes, visual output, or accessibility behavior. No production external adapter supplies caret data yet; no external app can use this popup end-to-end.

**Not implemented:** key routing from the target app, numbered acceptance shortcuts, production external TSF edit session, screen-reader announcement policy, or pipeline latency measurements. Do not claim system-wide functionality until the production adapter and interactive Windows checks are completed. See `docs/system-wide-suggestion-ui.md`.

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

Stage 4 is complete for the existing controlled-editor path and its synthetic performance baseline. This does not complete TSF integration or claim arbitrary Windows application compatibility. Stage 5 may proceed independently using the local coordinator; its concrete n-gram and held-out evaluation plan is in `plan.md` and `docs/benchmarks.md`. No Stage 5 code or evaluation has been delivered.

The current dictionary ranker runs synchronously over the local in-memory index, so there is no cooperative cancellation point and no concurrent prediction response can overtake a later UI input event. The coordinator still rejects stale/equal snapshot versions, binds delivery to field identity, and the form rechecks both before rendering. If future inference becomes asynchronous or materially slower, add cancellation but retain these version/field checks at result delivery and acceptance.

## Safety-policy verification run (2026-10-28)

The managed Release build passed with zero warnings/errors and 139/139 managed tests passed. The native x64 probe built successfully in `tools/PCWord.TsfProbe/build-validation`; CTest passed 2/2, including the full decision matrix, exact filename matching, and sensitive-scope regression tests. Tracked `build/` and `tools/PCWord.TsfProbe/build/` outputs were removed from the Git index only; local files were preserved because the existing DLL may be registered/locked. The new registry policy was not runtime-tested inside an activated TSF host, and no external-app production behavior is claimed as verified.


## Stage 6 compatibility reporting (observation only)

The TSF probe emits fixed status metadata for activation, focus and edit notifications, including selection/caret availability, bounded before/after character counts, InputScope stage/HRESULT, classification and policy decision. It does not emit text, candidates, window titles or field labels, and it does not insert text or display a popup. The new `PCWord.CompatReport` CLI parses only the probe marker records and generates aggregate Markdown rows grouped by process ID. Noise from other processes is not included in output. Raw DebugView logs must remain local and ignored.

GitHub Actions run [40702408590](https://github.com/aditya-a-git/PC-Word-Suggestion-System/actions/runs/40702408590) for commit `fa8ad0857488bfba0098833d4bbfea9305637165` completed successfully: Release restore/build passed, managed tests passed 159/159, the native x64 probe built, and CTest passed 2/2. The native compiler reported one unused-function warning; this follow-up removes that unused function and CI will verify the cleanup. No TSF host session was performed. Owner-reported Notepad/browser observations remain undated and lack exact app versions and saved logs. All other external app/version/field rows remain Untested. No production external-app support is claimed.

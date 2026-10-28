# TSF input safety status

## Current behavior

The probe receives TSF focus/edit notifications and can obtain caret geometry in the observed hosts. It does **not** log text content.

Diagnostic bounded context reads require all of:
- valid `HKCU\Software\PCWord` settings with `Enabled=1` and an exact allowlisted host executable name;
- available focused context, collapsed selection, no active TSF composition and caret geometry;
- no Sensitive InputScope classification; and
- explicit `PCWORD_TSF_ALLOW_CONTEXT_READ=1` in the host process environment.

The pure ordered safety decision is isolated in `InputSafetyPolicy.h`. Its full decision matrix and exact host-name tests are built by CTest. Settings are reread for every TSF edit notification, so an app pause change is applied at the next notification. Missing/unreadable/malformed settings deny. First-run managed app defaults are paused (`Enabled=0`) and `Allowlist=notepad.exe` only. Sensitive always denies; Unknown is allowed only for an allowlisted host, with the separate diagnostic opt-in still required.

## Input-scope retrieval status

The documented API path is `ITfContext::GetAppProperty(GUID_PROP_INPUTSCOPE, ...)`, followed by `ITfReadOnlyProperty::GetValue(cookie, range, ...)`, then querying the returned interface for `ITfInputScope`. See [Microsoft's ITfInputScope documentation](https://learn.microsoft.com/en-us/windows/win32/api/inputscope/nn-inputscope-itfinputscope).

The project owner reports `GUID_PROP_INPUTSCOPE GetValue` returning `0x80004005` (`E_FAIL`) for tested non-password fields. The API call sequence remains as documented; the failure is represented as Unknown. Under the new policy, Unknown may pass only for the exact allowlisted host after all settings, structure and opt-in gates pass. This task does not claim to resolve the property-access failure. The owner also reports browser password detection, but no repository log verifies which input-scope signal caused it.

The existing sensitive signal is the TSF InputScope array returned by `ITfInputScope::GetInputScopes` after property acquisition. `InputScopePolicy.h` classifies Password (31), Private (61), NumericPassword (63), NumericPIN (64), AlphanumericPIN (65), and AlphanumericPINSet (66) as Sensitive. Any one in a multi-scope value makes the result Sensitive, even alongside ordinary scopes. E_FAIL before `GetInputScopes` cannot be treated as password detection; it produces Unknown.

## Next verification

1. Configure and build on Windows:
   ```powershell
   cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build-validation -A x64
   cmake --build .\tools\PCWord.TsfProbe\build-validation --config Release
   ctest --test-dir .\tools\PCWord.TsfProbe\build-validation -C Release --output-on-failure
   ```
2. Launch PCWord once to initialize the default registry values, verify it displays paused state and hotkey registration status, then explicitly enable only for a controlled synthetic Notepad diagnostic.
3. Set `PCWORD_TSF_ALLOW_CONTEXT_READ=1` before launching Notepad. The host must be `notepad.exe`; missing/invalid settings or any sensitive scope must still deny.
4. Confirm the decision reason only; do not infer production compatibility or password protection from an Unknown-allowed-by-host result. Browsers are not allowlisted by default.

## Scope of this milestone

The policy implementation and tests do not establish host runtime behavior. The native DLL and CTest targets are built on Windows, but the new registry policy has not been runtime-tested inside an activated TSF host. Owner-reported activation/sensitivity observations are identified as such in the compatibility matrix. Registration requiring elevation is owner-reported, not independently verified here.


## Status-only event trace

Each TSF edit notification emits one fixed `edit-event` status record, including denied/unavailable states. It reports only collapsed-selection status, caret-rectangle availability, bounded before/after character counts (or -1), InputScope stage/HRESULT, fixed classification, and policy reason. Counts are read only after the existing allowlist, opt-in, sensitivity and structural gates allow a diagnostic read. No text is retained or logged. DebugView may interleave its two OutputDebugString calls and includes unrelated process output; use `PCWord.CompatReport` to produce aggregate rows only.

# TSF input safety status

## Current behavior

The probe receives TSF focus/edit notifications and can obtain caret geometry in the observed hosts. It does **not** log text content.

Context reads require both:
- explicit opt-in via `PCWORD_TSF_ALLOW_CONTEXT_READ=1` in the host process environment; and
- a positively observed `InputScopeStatus::NonSensitive` classification.

The safety decision is isolated in `InputSafetyPolicy.h`. Its compile-time tests are in `tests/InputSafetyPolicyTests.cpp` and are registered with CTest when `BUILD_TESTING` is enabled. `Sensitive` and `Unknown` both deny context reads.

## Input-scope retrieval status

The documented API path is `ITfContext::GetAppProperty(GUID_PROP_INPUTSCOPE, ...)`, followed by `ITfReadOnlyProperty::GetValue(cookie, range, ...)`, then querying the returned interface for `ITfInputScope`. See [Microsoft's ITfInputScope documentation](https://learn.microsoft.com/en-us/windows/win32/api/inputscope/nn-inputscope-itfinputscope).

The latest captured DebugView log shows `GUID_PROP_INPUTSCOPE GetValue` returning `0x80004005` (`E_FAIL`). The code now consistently returns `InputScopeStatus::Unknown` on that failure, so context reads remain denied. This commit does **not** claim to resolve the runtime property-access failure. The same log reported `opt-in=no`, so it also does not verify opt-in behavior in a fresh host process.

## Next verification

1. Configure and build on Windows:
   ```powershell
   cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build -A x64
   cmake --build .\tools\PCWord.TsfProbe\build --config Release
   ctest --test-dir .\tools\PCWord.TsfProbe\build -C Release --output-on-failure
   ```
2. Start a fresh test host from a shell where `PCWORD_TSF_ALLOW_CONTEXT_READ=1` is set before the host starts, and confirm the log reports `opt-in=yes`.
3. Investigate why the host's `ITfReadOnlyProperty::GetValue` returns `E_FAIL`; keep reads denied while the scope is unknown.
4. Once positive scope classification works, verify context acquisition in a controlled non-sensitive editor and confirm password fields remain blocked.

## Scope of this milestone

This change fixes an invalid integer return in the enum-class error path and adds compile-time tests for the privacy policy. It does not claim that the DLL has been built or runtime-tested on the developer machine, nor that the TSF property lookup is fixed.

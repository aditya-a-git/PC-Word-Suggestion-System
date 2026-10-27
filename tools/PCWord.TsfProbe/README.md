# PCWord TSF feasibility probe

Experimental native C++ COM TSF text service for evaluating Windows Text Services Framework against the existing UI Automation probe. It is not production-ready.

## Obtain the CI build

After the workflow completes successfully, open the [Stage 4 feasibility PR](https://github.com/aditya-a-git/PC-Word-Suggestion-System/pull/2), open its latest **.NET build and tests** workflow run, and download the `PCWordTsfProbe-win-x64` artifact. The artifact contains `PCWordTsfProbe.dll` and is retained for 7 days. Do not use an artifact from a failed or older run.

Alternatively, build on Windows with Visual Studio C++ tools and CMake 3.24+:

```powershell
cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build -A x64
cmake --build .\tools\PCWord.TsfProbe\build --config Release
```

The local DLL path is `tools\PCWord.TsfProbe\build\Release\PCWordTsfProbe.dll`.

## Safety and registration

Prefer a disposable Windows VM or separate Windows test account. Close sensitive applications and use synthetic text only. This DLL registers a per-user COM class and TSF profile under the current user's registry hive; it does not require machine-wide registration.

From a **normal, non-elevated 64-bit PowerShell** session, set `$dll` to the full path of the extracted DLL, then register it:

```powershell
$dll = (Resolve-Path ".\PCWordTsfProbe.dll").Path
& "$env:WINDIR\System32\regsvr32.exe" "$dll"
```

If Windows exposes the registered profile in its input/language settings, enable it only for the test account. Do not make it your everyday input method. If it does not appear or activation cannot be confirmed, stop and report that result rather than trying machine-wide registration.

To unregister after closing the test applications:

```powershell
& "$env:WINDIR\System32\regsvr32.exe" /u "$dll"
```

Confirm the DLL path before either command. Registration/unregistration must use the same DLL file and the same Windows user account.

## What it observes

The DLL reports activation, focus/edit notifications, selection availability, caret rectangle availability, and bounded character counts to debugger output. It never prints text content or edits documents. Context reads are disabled by default and require `PCWORD_TSF_ALLOW_CONTEXT_READ=1` in the target application's environment. Use only synthetic text. Unknown, password, PIN, and Private input scopes must fail closed. The Windows input-scope classifier is covered by the CTest target InputScopePolicyTests; the opt-in gate is covered by InputSafetyPolicyTests.

This is a feasibility spike only: no key interception, popup, prediction, or text replacement. Follow the Windows TSF test matrix in `docs/tsf-feasibility.md`. Runtime validation is still pending.


See [Windows integration status](../../docs/windows-integration-status.md) for the implementation boundary, Microsoft API references, and remaining verification gates. The native probe remains a feasibility tool; it is not yet a production system-wide adapter.

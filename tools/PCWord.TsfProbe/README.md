# PCWord TSF feasibility probe

Experimental native C++ COM TSF text service for evaluating Windows Text Services Framework against the existing UI Automation probe. It is not production-ready.

## Obtain the CI build

After the workflow completes successfully, open the [Stage 4 feasibility PR](https://github.com/aditya-a-git/PC-Word-Suggestion-System/pull/2), open its latest **.NET build and tests** workflow run, and download the `PCWordTsfProbe-win-x64` artifact. The artifact contains `PCWordTsfProbe.dll` and is retained for 7 days. Do not use an artifact from a failed or older run.

Alternatively, build on Windows with Visual Studio C++ tools and CMake 3.24+:

```powershell
cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build-validation -A x64
cmake --build .\tools\PCWord.TsfProbe\build-validation --config Release
```

The validation DLL path is `tools\PCWord.TsfProbe\build-validation\Release\PCWordTsfProbe.dll`. Keep any already-registered DLL in `tools\PCWord.TsfProbe\build\Release\` untouched.

## Safety and registration

Prefer a disposable Windows VM or separate Windows test account. Close sensitive applications and use synthetic text only. This DLL registers a per-user COM class and TSF profile under the current user's registry hive; it does not require machine-wide registration.

From a 64-bit PowerShell session, set `$dll` to the full path of the extracted DLL, then register it:

```powershell
$dll = (Resolve-Path ".\PCWordTsfProbe.dll").Path
& "$env:WINDIR\System32\regsvr32.exe" "$dll"
```

The project owner reports non-elevated registration failed with `ITfInputProcessorProfiles::Register` returning `0x80004005`, while elevated registration succeeded. This is owner-reported, not independently verified in the repository. If registration requires elevation on the test machine, use the same Windows account and retain per-user registration; do not use machine-wide registration. If the profile appears in Windows input/language settings, enable it only for the test account.

To unregister after closing the test applications:

```powershell
& "$env:WINDIR\System32\regsvr32.exe" /u "$dll"
```

Confirm the DLL path before either command. Registration/unregistration must use the same DLL file and the same Windows user account.

## What it observes

The DLL logs status-only activation, focus-change and edit-event records: selection/caret availability, bounded character counts, InputScope retrieval stage/HRESULT, classification, and policy decision. It never prints text content, app/window labels, or raw keystrokes. The log is emitted through split OutputDebugString calls and may be interleaved/noisy in DebugView; use the aggregate report tool and never publish raw captures. It never prints text, window titles, document names, field contents, or raw keystrokes, and it does not edit documents. A bounded diagnostic read requires `PCWORD_TSF_ALLOW_CONTEXT_READ=1`, `Enabled=1`, the host exe filename in `HKCU\Software\PCWord\Allowlist`, and eligible TSF structure. Defaults are paused with `notepad.exe` only. The DLL rereads registry settings for every edit notification; missing, unreadable, or malformed settings deny. Sensitive InputScope always denies; Unknown is permitted only for an allowlisted host with the diagnostic opt-in. CTest covers the decision matrix, filename matching and sensitive-scope regression cases.

This is a feasibility spike only: no key interception, popup, prediction, IPC, or text replacement. Building the DLL and policy tests does not verify that a host activates the service or exposes its context. Do not add browsers to the allowlist by default: non-password fields containing card numbers or OTPs may have Unknown scope. Filename matching can be spoofed by a differently located executable with the same name; path/signature checking is deferred. Follow the Windows TSF test matrix in `docs/tsf-feasibility.md`. Runtime validation of the new policy remains pending.


See [Windows integration status](../../docs/windows-integration-status.md) for the implementation boundary, Microsoft API references, and remaining verification gates. The native probe remains a feasibility tool; it is not yet a production system-wide adapter.

# TSF feasibility spike

The native probe in `tools/PCWord.TsfProbe` is an experimental C++ COM text service that compares TSF lifecycle/context availability with the existing UI Automation probe.

## Scope

- Observes service activation, document focus, edit notifications, collapsed-selection availability, and caret rectangle availability.
- Bounded surrounding-text counts are behind an explicit target-process environment opt-in.
- Text content is not emitted; no text is changed and no keystrokes are synthesized.
- This is not a production adapter and has no prediction or suggestion UI.

## Build and artifact

On Windows with Visual Studio C++ tools and CMake:

```powershell
cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build -A x64
cmake --build .\tools\PCWord.TsfProbe\build --config Release
```

The output is `tools\PCWord.TsfProbe\build\Release\PCWordTsfProbe.dll`. CI publishes the same DLL as the `PCWordTsfProbe-win-x64` artifact on successful workflow runs; download it from the latest successful run linked from the [feasibility PR](https://github.com/aditya-a-git/PC-Word-Suggestion-System/pull/2). Artifacts are retained for 7 days.

## Controlled runtime test

Use a disposable Windows VM or separate test account, synthetic text only, and a non-elevated 64-bit PowerShell session. The probe registers a per-user COM class and TSF profile under `HKEY_CURRENT_USER`; do not use machine-wide registration.

After extracting the artifact, from the directory containing the DLL:

```powershell
$dll = (Resolve-Path ".\PCWordTsfProbe.dll").Path
& "$env:WINDIR\System32\regsvr32.exe" "$dll"
```

If the profile is exposed in Windows input/language settings, enable it only for the test account. If it does not appear or activation cannot be confirmed, stop rather than trying elevated or machine-wide registration. Close test applications and unregister after testing:

```powershell
& "$env:WINDIR\System32\regsvr32.exe" /u "$dll"
```

The service logs status through Windows debugger output (for example, DebugView or Visual Studio's debugger output). It does not write a log file. Context reads are disabled unless `PCWORD_TSF_ALLOW_CONTEXT_READ=1` is inherited by the target application. Start with the opt-in **unset** and verify activation/focus/edit notifications first. Only if that works, close the application, set the variable in the PowerShell session, and launch the test application from that same session to test bounded counts:

```powershell
Remove-Item Env:PCWORD_TSF_ALLOW_CONTEXT_READ -ErrorAction SilentlyContinue
# Launch the test application normally, then inspect debugger output.
```

For the second pass, after closing the test app:

```powershell
$env:PCWORD_TSF_ALLOW_CONTEXT_READ = "1"
# Launch the test application from this same PowerShell session.
```

Unset the variable again after the test. Do not use real personal data or password/PIN fields. Input-scope metadata is not a universal sensitivity guarantee; unknown or ambiguous scope must be treated as unavailable.

## Manual comparison

Use synthetic text only. Test each field separately: Notepad, ordinary browser input, browser contenteditable, VS Code editor, and LeetCode editor. Record only app/version, field type, activation, context availability, collapsed selection, caret rectangle, bounded counts, and pass/fail. Never copy typed text into logs or notes.

For each app/field, record:
- Did the service activate?
- Were focus and edit notifications observed?
- Was the selection collapsed?
- Was caret geometry available?
- With context reads explicitly enabled, were bounded before/after counts reported or left unavailable?
- Did focus changes stop or redirect observations as expected?

Password/PIN fields must not be read. If input sensitivity cannot be determined reliably, treat context as unavailable. TSF input scope is not a universal sensitivity guarantee, and a field's provider may not expose TSF context at all.

## Decision gate

Do not integrate TSF into the prediction engine until Windows runtime tests establish field coverage, stable caret context, and reliable invalidation on focus/context changes. A separate edit-session experiment must verify stale-context rejection and no duplicated/lost characters before replacement is considered. Benchmarking remains deferred to the final stage.


The property API may be unsupported by a context owner; either S_FALSE or E_FAIL is treated as unavailable by this probe. Microsoft documents the `GetAppProperty` then `GetValue` sequence, and the implementation follows that sequence. The exact cause of a given E_FAIL still requires host-specific diagnostics; do not infer that the field is safe. InputScope is advisory, not a general sensitivity proof. See [Windows integration status](windows-integration-status.md) for API sources and the current implementation boundary.

For a targeted diagnostic, use a disposable account, a standard Windows text control, and synthetic text. Register the x64 DLL per the instructions above, leave `PCWORD_TSF_ALLOW_CONTEXT_READ` unset, and capture debugger output containing only operation names and HRESULTs. This checks activation and property-call results without reading bounded text. Unregister the same DLL afterward. Do not treat a successful property query as authorization to send content into a prediction service.

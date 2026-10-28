# TSF feasibility spike

The native probe in `tools/PCWord.TsfProbe` is an experimental C++ COM text service that compares TSF lifecycle/context availability with the existing UI Automation probe.

## Scope

- Observes service activation, document focus, edit notifications, collapsed-selection availability, and caret rectangle availability.
- Bounded reads are behind the full per-user settings, allowlist, structural, and diagnostic opt-in gates. Text and read counts are not logged.
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

Use a disposable Windows VM or separate test account, synthetic text only, and a 64-bit PowerShell session. The probe registers a per-user COM class and TSF profile under `HKEY_CURRENT_USER`; do not use machine-wide registration.

After extracting the artifact, from the directory containing the DLL:

```powershell
$dll = (Resolve-Path ".\PCWordTsfProbe.dll").Path
& "$env:WINDIR\System32\regsvr32.exe" "$dll"
```

The project owner reports that non-elevated registration failed at `ITfInputProcessorProfiles::Register` with `0x80004005`, while elevated registration succeeded. This observation is not independently verified by the repository. If elevation is required in the test environment, use the same account and keep registration per-user; do not use machine-wide registration. Enable the profile only for the test account. Close test applications and unregister after testing:

```powershell
& "$env:WINDIR\System32\regsvr32.exe" /u "$dll"
```

The service logs safety decision reason and allowed/denied through Windows debugger output (for example, DebugView or Visual Studio's debugger output); it does not write a log file or log text content. A diagnostic bounded read additionally requires `PCWORD_TSF_ALLOW_CONTEXT_READ=1`, `Enabled=1` and the host executable name in `HKCU\Software\PCWord\Allowlist`, plus eligible TSF state. Defaults are paused with only `notepad.exe` allowlisted; the managed app creates these defaults on first launch. The DLL rereads both registry values on each TSF edit notification and denies missing, unreadable or malformed values. Do not enable the external context toggle unless a diagnostic read is intended.

```powershell
Remove-Item Env:PCWORD_TSF_ALLOW_CONTEXT_READ -ErrorAction SilentlyContinue
# Launch the test application normally, then inspect debugger output.
```

For the second pass, after closing the test app:

```powershell
$env:PCWORD_TSF_ALLOW_CONTEXT_READ = "1"
# Launch the test application from this same PowerShell session.
```

Unset the variable again after the test. Do not use real personal data or password/PIN fields. Sensitive InputScope values always deny. Unknown or failed InputScope may pass only the exact allowlisted host gate and the additional opt-in/structural checks; the allowlist is not proof of field privacy. Do not add browsers by default because non-password fields such as card numbers and OTPs may have Unknown scope.

## Manual comparison

Use synthetic text only. Test each field separately only when it is explicitly allowlisted. Record app/version, field type, activation, policy decision reason, context availability, collapsed selection, caret rectangle availability, and pass/fail. Never record context counts or typed text in notes or logs. Do not add browsers to the default allowlist.

For each app/field, record:
- Did the service activate?
- Were focus and edit notifications observed?
- Was the selection collapsed?
- Was caret geometry available?
- Did the reason code match the settings, scope, structural state, host allowlist, and diagnostic opt-in?
- Did focus changes stop or redirect observations as expected?

Password/PIN fields must not be read. If input sensitivity cannot be determined reliably, treat context as unavailable. TSF input scope is not a universal sensitivity guarantee, and a field's provider may not expose TSF context at all.

## Decision gate

The new policy only gates diagnostic bounded reads. Do not integrate TSF into the prediction engine until Windows runtime tests establish field coverage, stable caret context, and reliable invalidation on focus/context changes. A separate edit-session experiment must verify stale-context rejection and no duplicated/lost characters before replacement is considered. Benchmarking remains deferred to the final stage.


The property API may be unsupported by a context owner; either S_FALSE or E_FAIL is classified Unknown by this probe. Microsoft documents the `GetAppProperty` then `GetValue` sequence, and the implementation follows that sequence. The exact cause of a given E_FAIL remains host-specific. Unknown scope passes only when the executable is allowlisted and all enabled, structural, and diagnostic opt-in gates pass; sensitive classifications still deny. See [Windows integration status](windows-integration-status.md) for current evidence and limitations.

For a targeted diagnostic, use a disposable account, a standard Windows text control, and synthetic text. Register the x64 DLL per the instructions above, leave `PCWORD_TSF_ALLOW_CONTEXT_READ` unset, and capture debugger output containing only operation names and HRESULTs. This checks activation and property-call results without reading bounded text. Unregister the same DLL afterward. Do not treat a successful property query as authorization to send content into a prediction service.


## Observation-level compatibility reporting

The probe's DebugView records are status-only. The optional `PCWord.CompatReport` tool consumes saved TSV output and produces aggregate per-process counts. Raw logs can contain output from unrelated processes; keep them local under the ignored report logs folder, never copy or commit them, and publish only generated rows. A report cannot establish safe insertion or popup behavior because neither external feature exists. Owner-reported Notepad/browser observations have no date/version/log and must remain explicitly unverified.

# Testing guide

## Managed solution

From the repository root:

```powershell
dotnet restore .\PCWord.sln
dotnet build .\PCWord.sln --configuration Release --no-restore
dotnet test .\PCWord.sln --configuration Release --no-restore --verbosity minimal
```

On 2026-10-27, `dotnet build PCWord.sln -c Release` passed with zero warnings/errors, and `dotnet test PCWord.sln -c Release --no-build` passed 126/126 managed tests for Stage 4. These are controlled-editor contracts, not external-app integration tests.

On 2026-10-28, `dotnet restore PCWord.sln`, `dotnet build PCWord.sln -c Release --no-restore`, and `dotnet test PCWord.sln -c Release --no-build` passed; build had zero warnings/errors and 139/139 managed tests passed. Added tests cover default-paused settings, allowlist validation and matching, and visible hotkey-registration failure state. Unit tests do not exercise the HKCU adapter or actual RegisterHotKey registration.

## Repeatable benchmark

```powershell
dotnet run --project .\tools\PCWord.Benchmark\PCWord.Benchmark.csproj --configuration Release -- --iterations 100000
```

The benchmark's managed end-to-end path includes synthetic snapshot construction, context management, dictionary ranking, result handoff and a synthetic consumer. It excludes RichTextBox/Win32 operations and actual painting. It separately reports lookup-only latency, startup, percentile window size, process CPU time, and working-set/managed-heap deltas. The measured output and limitations are recorded in `docs/benchmarks.md`.

## Native policy regression tests

Use a fresh ignored folder rather than modifying tracked output under `tools/PCWord.TsfProbe/build/`:

```powershell
cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build-validation -A x64
cmake --build .\tools\PCWord.TsfProbe\build-validation --config Release
ctest --test-dir .\tools\PCWord.TsfProbe\build-validation -C Release --output-on-failure
```

On 2026-10-28, CMake configured and built the x64 `PCWordTsfProbe.dll` in `tools/PCWord.TsfProbe/build-validation`; CTest passed 2/2. The full decision-matrix test exercises settings validity, paused state, all classifications, host allowlisting, every structural gate, and diagnostic opt-in. Filename cases cover case variants, spoofed prefix/suffix, separators, empty, ASCII/Unicode whitespace, and wildcards. Scope tests pin each sensitive classification already recognized by `InputScopePolicy.h`. These tests do not activate TSF or prove provider behavior. The existing `tools/PCWord.TsfProbe/build/` DLL was left on disk because it may be registered and the linker reported it locked.

Tracked `build/` and `tools/PCWord.TsfProbe/build/` outputs were removed from the Git index with `git rm --cached`; files were not deleted from disk.

## Runtime status and privacy inspection

The Windows controlled-editor UI was not launched in this run. Runtime timings for real RichTextBox acquisition and popup handling can be viewed using the app's **Performance stats** button. External apps remain unsupported; no Stage 0–3 retest is implied.

A scoped source inspection was run across the normal Core/Prediction/Stage0 path and the offline evaluator. It found no network-client APIs. Aggregate `Console.WriteLine` calls exist only in the evaluator and print numeric metrics/configuration; the model's `File.WriteAllText` is used only to serialize the locally generated model to the requested output path. No text-context or candidate logging/persistence API is used by live prediction. This is source inspection, not runtime network monitoring.

The source-search command was:

```powershell
rg -n -i "HttpClient|WebRequest|SocketsHttpHandler|WebSocket|TcpClient|UdpClient|NetworkStream" src\PCWord.Core src\PCWord.Prediction src\PCWord.Stage0 tools\PCWord.Evaluation
```

The query returned no matches. This does not constitute a runtime network capture or a repository-wide security audit. The normal path loads the bundled dictionary and optional local model.

## Stage 5 n-gram verification

The automated suite covers tokenizer normalization, unigram/bigram/trigram ranking, backoff, deterministic ties, vocabulary bounds, sentence and line reset, empty models, boundary eligibility, sensitive rejection, stale versions, and validated zero-prefix insertion. Run the Release suite/build commands above. For dataset evaluation:

```powershell
dotnet run --project .\tools\PCWord.Evaluation\PCWord.Evaluation.csproj --configuration Release -- <train.txt> <validation.txt> <test.txt> <local-model-output.json>
```

Supply three distinct UTF-8 files converted from the pinned Parquet splits; see `data/README.md`. The command selects order/backoff on validation, evaluates test once, and prints aggregate quality and resource metrics only. The synthetic manager-plus-model request timing excludes the WinForms adapter/UI and edit acceptance. The model contains corpus-derived vocabulary/counts; keep it local and do not train on private documents. The fixed-corpus run was blocked here by reset download connections.

The controlled-editor form loads `%LOCALAPPDATA%\PCWord\ngram-model.json` on launch if present. Manual check (not performed here): place a locally generated model there, open the WinForms editor, type synthetic non-sensitive context ending at a word boundary, verify up to three candidates, accept with Ctrl+Space, and confirm insertion preserves punctuation/trailing spaces. Then test partial words, selection, sensitive toggle, pause/resume, caret/focus changes, and that stale suggestions disappear. This remains unverified; arbitrary external applications are unsupported.


## Stage 6 compatibility observation procedure

Run only in a disposable Windows account or VM with synthetic text. Never type or record a real password, private message, customer data, or production command. Close sensitive applications first.

1. Build the probe on the test machine:
   ```powershell
   cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build-compat -A x64
   cmake --build .\tools\PCWord.TsfProbe\build-compat --config Release
   ```
2. In an elevated PowerShell, register the DLL once for the same test user. Elevation is an owner-reported requirement on one machine, not verified generally:
   ```powershell
   $dll = (Resolve-Path '.\tools\PCWord.TsfProbe\build-compat\Release\PCWordTsfProbe.dll').Path
   & "$env:WINDIR\System32\regsvr32.exe" "$dll"
   ```
   This is per-user registration; do not use machine-wide registration. In Windows keyboard/input settings switch to the PCWord TSF probe profile for pass 1.
3. Start DebugView as the same user with **Capture Win32** enabled. Save the capture only under `tools/PCWord.CompatReport/logs/`, which is ignored by Git.
4. For pass 1, ensure `PCWORD_TSF_ALLOW_CONTEXT_READ` is unset. In the same user's registry, explicitly enable the diagnostic policy and allow only the executable under test. Example for Notepad:
   ```powershell
   New-Item -Path 'HKCU:\Software\PCWord' -Force | Out-Null
   New-ItemProperty -Path 'HKCU:\Software\PCWord' -Name Enabled -PropertyType DWord -Value 1 -Force | Out-Null
   New-ItemProperty -Path 'HKCU:\Software\PCWord' -Name Allowlist -PropertyType MultiString -Value @('notepad.exe') -Force | Out-Null
   ```
   Change the exact executable name for each target. The environment opt-in alone does not authorize reads; both settings and structural/sensitivity gates are required.
5. For each application/version and each field category separately, use the action script below. Do not copy any typed text into logs, notes, screenshots or records.
6. Repeat with `PCWORD_TSF_ALLOW_CONTEXT_READ=1` set before launching the host for pass 2. Keep the same enabled policy and exact host allowlist. Re-launch the host after changing its environment.
7. Use the report tool and paste only its generated aggregate Markdown rows into the matrix:
   ```powershell
   dotnet run --project .\tools\PCWord.CompatReport\PCWord.CompatReport.csproj --configuration Release -- .\tools\PCWord.CompatReport\logs\capture.txt "App name" "Exact version" "Field category"
   ```
   The tool prints aggregate probe metadata only. Never attach or commit the raw capture.
8. Switch back to the previous keyboard, disable the probe policy, close test apps, and unregister the same DLL under the same account:
   ```powershell
   & "$env:WINDIR\System32\regsvr32.exe" /u "$dll"
   ```
   Restore any policy values changed for the test and delete the local raw capture when no longer needed.

### Fixed action script per field

For each non-password field, in order: type an ordinary synthetic word at a normal pace; type another synthetic string rapidly; backspace several characters; select a portion and replace it with synthetic text; type punctuation; move focus to a second field and back. For a password field, focus only and verify status; never type a password, even a real one. Capture only the probe records `Text service activated.`, `TSF document focus changed.`, and `edit-event ...`, plus generated counts. Each edit record contains selection/caret availability, bounded character counts, fixed InputScope stage/HRESULT, classification, decision and allowed/denied status. DebugView may split the `[PCWORD.TSF] ` marker and status into consecutive records; the report parser pairs them by process ID. Never copy, transcribe or otherwise record typed text.

### Target matrix (test each row separately)

- Basic Win32 Notepad
- Packaged Notepad
- Browser text input
- Browser contenteditable
- Browser password field (focus only; never type a real password)
- Word or similar office editor
- VS Code editor
- VS Code integrated terminal
- Windows Terminal
- Messaging app compose field
- XAML/UWP text box
- Elevated/admin application
- Remote Desktop session field
- Game or custom-rendered text field

A pass in one row says nothing about another app version or field category. The diagnostic probe does not insert text or show a popup.

### Regression checklist

When adapter, policy or popup code changes, rerun all matrix rows that change could affect, including relevant sensitive, elevated, remote, terminal and custom-rendered cases. Record app version, exact field category, date, probe status and generated aggregate row. Mark unavailable behavior Untested. Automated parser/policy tests stay in CI. Raw logs never go in docs or commits.

### Stage 6 implementation verification (2026-10-28)

GitHub Actions run [40702408590](https://github.com/aditya-a-git/PC-Word-Suggestion-System/actions/runs/40702408590) for commit `fa8ad0857488bfba0098833d4bbfea9305637165` completed successfully: Release restore and build passed, the managed suite passed 159/159 tests, the native x64 TSF probe configured and built, and CTest passed 2/2 policy tests. The probe build reported an unused `TraceDecision` C4505 warning; a follow-up removes that unused function and will be rechecked by CI. No external app host session was performed. The only external-app observations remain owner reports without dates, exact versions or saved logs.
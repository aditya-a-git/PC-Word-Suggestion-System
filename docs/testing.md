# Testing and evidence

## Automated commands

Windows x64 with .NET 10 SDK, Visual Studio C++/Windows SDK and CMake 3.24+:

```powershell
dotnet restore .\PCWord.sln
dotnet build .\PCWord.sln -c Release --no-restore
dotnet test .\PCWord.sln -c Release --no-build --logger "console;verbosity=normal"
cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build-validation -A x64
cmake --build .\tools\PCWord.TsfProbe\build-validation --config Release
ctest --test-dir .\tools\PCWord.TsfProbe\build-validation -C Release --output-on-failure
```

Portable managed tests on Linux/macOS require only .NET 10:

```sh
dotnet test tests/PCWord.Tests/PCWord.Tests.csproj -c Release
c++ -std=c++17 -Wall -Wextra -Werror tools/PCWord.TsfProbe/EditTransactionTests.cpp -o /tmp/pcword-edit-tests
/tmp/pcword-edit-tests
```

New regression coverage: sensitive scope ordering; copied snapshot bounds and
Unicode; ineligible IPC payloads; strict JSON decoding; duplicate/decreasing
versions; cancellation reentrancy, exceptions and cross-thread callbacks;
concurrent, repeated, failed and cancelled acceptance; newer context survival.

Native executables cover input policies, single-use/expired/stale transactions,
COM edit-session lifetime and exception boundaries, and actual range-helper code
against a deterministic COM provider model. The model exercises bounded reads,
Unicode, lock errors, text mismatch, partial writes, failed selection, incorrect
caret, neighbor corruption, no retry, and reference cleanup. Session tests exercise
both request HRESULT and session HRESULT, including denied/disconnected requests.
These are **unit/model tests**, not Windows application integration tests.

## Verification record — 2026-11-13

| Layer | Executed result |
|---|---|
| Baseline managed suite | 196/196 passed on Linux, .NET SDK 10.0.404 |
| Expanded managed suite | 216/216 passed, no skips, Linux |
| Portable native transaction suite | Passed using GCC C++17 with warnings as errors |
| Windows build, managed and native model tests | [Run 43435922549](https://github.com/aditya-a-git/PC-Word-Suggestion-System/actions/runs/43435922549), commit `9c554bc`: passed, managed 216, native 5/5 |
| First native build attempt | Run 43435188125 failed due to a shared GUID inside the wrong conditional branch; fixed and rerun |
| Real Notepad / other application | Not run; no interactive Windows host available in this task |
| Production IPC / authentication / installer | Not implemented; not tested |
| Live focus / DPI / sustained use | Not run |

Later validation evidence is recorded below when available. No app/version is
marked supported by these automated results.

## Required Notepad test

Follow [tsf-integration.md](tsf-integration.md) to build/register/activate the separate
experiment in a disposable Windows account. Record a template without inspecting
user text:

```powershell
.\tools\validation\New-NotepadTestReport.ps1 -OutputPath .\TestResults\Notepad-validation.json
```

The script records OS/build and available binary version metadata only. It does not
run tests, enable the TIP, edit documents or mark any case passed. Verify the exact
Notepad version and architecture manually if metadata is unavailable. Record one
report per Windows/app version and configuration; retain statuses/HRESULTs only.

Use a synthetic document with known content before and after the target. For each
case inspect the whole resulting document, selection, caret and undo/redo, and
record the fixed marker and both HRESULTs. Never treat missing markers as success.

| Case | Required outcome |
|---|---|
| Activation | Correct experimental profile activates in external Notepad; document/edit events appear |
| Positive edit | Capture `pcword`, apply within five seconds; exactly `pcword-verified`, caret at end, `edit=verified` |
| Full integrity | All other synthetic document content unchanged, including text beyond the bounded guard; undo/redo sensible |
| Duplicate apply | Second/repeated apply makes no further change; key repeats cannot write twice |
| Expiry | Wait over five seconds after capture; apply rejects |
| Text/selection/caret change | Type/delete/move/select after capture (including away then back); old ticket rejects |
| Focus change | Alt+Tab away/back between capture/apply; no old insertion, no text in another control |
| Empty document / selected text | No replacement; no crash or unexpected selection changes |
| Multiline/Unicode | Include surrogate pairs, mixed scripts, combining marks and RTL near the window edges; no split/corrupted text |
| Composition | Active IME/interim selection denies capture/write |
| Pause | Set Enabled=0 after capture; apply denies and consumes ticket |
| Sensitive scope | In a dedicated synthetic sensitive-control test, no read or write; use policy/model tests until this host is available |
| Denied lock / read-only | Request failure and session failure both leave no successful insertion report; no retry |
| Termination | Close/reopen Notepad and use a fresh capture; no retained edit targets |
| Cleanup | Disable/unregister experiment and verify registrations removed, preserving settings/user data |

Any unsupported setup/case is `not-run` or `blocked`, never passed. A failed or
uncertain write stops that test run; inspect the document and do not automatically
repeat the apply chord. A fresh capture is a new explicit experiment.

## Later gates

Only after this read/write gate passes should native/managed transport and the
existing prediction/popup flow be connected. That suite must cover disconnects,
service crashes/restarts, malformed frames, unauthorized peers, out-of-order
responses, rapid typing, focus during acceptance, selection changes and app restarts.

Then test 100/125/150/175/200% scaling, mixed DPI, negative monitor coordinates,
monitor transitions, RTL, scrolling, full-screen, accessibility and keyboard focus.
Run at least an eight-hour synthetic typing/acceptance/focus-switch soak and
repeated install/upgrade/repair/uninstall with interruption/rollback cases. Record
latency percentiles, CPU, memory, handles and threads against
[performance.md](performance.md). None of those later gates is currently passed.

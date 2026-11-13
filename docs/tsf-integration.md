# TSF read/write experiment

Status: implemented experimental code; real Notepad execution remains **unverified**.
Build and native model tests do not establish external application compatibility.
The original diagnostic DLL remains read-only. The new DLL is not a production adapter.

## Identity and activation

`PCWordTsfEditExperiment.dll` uses CLSID
`{19792AF1-8D11-4A63-9844-1EEF70243051}` and profile
`{19792AF2-8D11-4A63-9844-1EEF70243051}`. It cannot replace the diagnostic TIP's
registration. It shares only the explicit diagnostic settings at
`HKCU\Software\PCWord`. Both environment opt-ins, valid Enabled/Allowlist settings,
an exact `notepad.exe` host match, ordinary medium integrity, focused foreground
thread and the focused document manager's top context are required.

Thread focus loss, document changes, context removal and edit notifications
invalidate the current capture. Activation attaches the current focused document;
context push notifications only attach a focused top context. InputScope scanning
checks every value for sensitivity even when an earlier value is unknown.

The experiment deliberately permits Unknown scope in opted-in Notepad only, for
synthetic text. This is not adequate production field classification. Executable
names are not executable authentication. No other host is permitted by the edit
experiment even if the diagnostic allowlist contains it.

## Read and write contracts

Ctrl+Alt+Shift+F11 on **key release** requests a synchronous read session. The
capture accepts a collapsed, non-interim selection with no composition and usable
caret geometry. It reads at most 128 UTF-16 units before and 32 after the caret,
trims a split surrogate only at an outer window boundary, rejects malformed UTF-16
and embedded objects, and requires the exact synthetic token `pcword` immediately
before the caret. That token must have whitespace/document boundaries. Empty
contexts can be read but cannot arm this replacement experiment.

Only a COM range, monotonic generation, capture timestamp and SHA-256 fingerprint
remain after capture; raw text is transient. The ticket expires after five seconds.
A COM range preserves position identity within the attached context; process ID or
window handle alone is never sufficient. No absolute document offset is assumed
because not all TSF stores expose ACP offsets.

Ctrl+Alt+Shift+F12 on key release requests `TF_ES_SYNC | TF_ES_READWRITE`.
`DoEditSession` consumes the ticket and checks expiry/generation. It reacquires
focus, settings, sensitivity, selection and composition under the granted cookie,
compares current and captured ranges, rereads the bounded window and compares its
fingerprint, validates the target token and range, and calls `ITfRange::SetText`
with the fixed replacement `pcword-verified`. It then sets a collapsed selection at
the replacement end. Both the request HRESULT and session HRESULT are inspected.
Denied/contended/disconnected sessions are never retried. Ordinary keys are not eaten.

The same session rereads the replacement and a tracked guard range containing
both neighboring windows, compares the expected result, and verifies focus and
caret/selection. The fixed status is `verified`, `rejected` or `uncertain`.
Any possible mutation followed by failure is uncertain; there is no retry or
speculative rollback. Verification covers the edited range and bounded neighboring
text, **not every character outside the window**. The manual suite must compare
the whole synthetic document. This limitation is a release blocker.

`TF_ES_SYNC` is used only during explicit key handling, a documented TSF use. No
write is requested from OnEndEdit. There is no prediction, pipe wait, worker queue,
keyboard simulation or clipboard path inside an edit session. OS/provider calls
are synchronous; a hostile or hung provider can still stall its own host. No
hard latency guarantee is claimed until measured.

## Reproduction on Windows 11 x64

Use a disposable test account/VM and synthetic documents. Use a fresh directory
for each build; do not overwrite a DLL loaded in a running application.

```powershell
cmake -S .\tools\PCWord.TsfProbe -B .\tools\PCWord.TsfProbe\build-validation -A x64
cmake --build .\tools\PCWord.TsfProbe\build-validation --config Release
ctest --test-dir .\tools\PCWord.TsfProbe\build-validation -C Release --output-on-failure
$dll = (Resolve-Path .\tools\PCWord.TsfProbe\build-validation\Release\PCWordTsfEditExperiment.dll).Path
& "$env:WINDIR\System32\regsvr32.exe" $dll
```

COM class entries are explicitly under HKCU. TSF profile/category registration
uses Windows registration APIs; its exact privilege/storage behavior has **not**
been verified. Try non-elevated registration. If it fails or requires elevation,
record the HRESULT and stop; do not silently broaden registration scope. Enable
only the **PCWord Synthetic Edit Experiment** English (US) profile in the test
account. Registration alone is not evidence of activation.

In that disposable account only, create the explicit diagnostic settings:

```powershell
New-Item HKCU:\Software\PCWord -Force | Out-Null
New-ItemProperty HKCU:\Software\PCWord -Name Enabled -PropertyType DWord -Value 1 -Force | Out-Null
New-ItemProperty HKCU:\Software\PCWord -Name Allowlist -PropertyType MultiString -Value @('notepad.exe') -Force | Out-Null
$env:PCWORD_TSF_ALLOW_CONTEXT_READ = '1'
$env:PCWORD_TSF_ALLOW_EDIT_EXPERIMENT = '1'
Start-Process notepad.exe
```

Downloaded native DLLs require the matching x64 Microsoft Visual C++ runtime,
which is not bundled. A successful build does not verify a clean-machine load.

Close existing Notepad instances first. Modern packaged Notepad may reuse a process
that did not inherit these variables; absence of an activation/capture marker is a
failed setup, not a passed test. Do not use machine-wide environment variables.
Observe only `[PCWord.TSF]` status records with a debugger/DebugView. Never commit
raw captures from other processes.

Type `hello pcword world`, put the caret immediately after `pcword`, release the
capture chord, then release the apply chord within five seconds without typing or
moving the caret. Expect exactly `hello pcword-verified world`, a collapsed caret
at replacement end, and `edit=verified`. Repeat the failure cases in
[testing.md](testing.md). If any prerequisite fails, do not force an edit.

After testing, disable the experimental profile, close test applications and run:

```powershell
& "$env:WINDIR\System32\regsvr32.exe" /u $dll
Remove-Item Env:\PCWORD_TSF_ALLOW_CONTEXT_READ -ErrorAction SilentlyContinue
Remove-Item Env:\PCWORD_TSF_ALLOW_EDIT_EXPERIMENT -ErrorAction SilentlyContinue
Set-ItemProperty HKCU:\Software\PCWord -Name Enabled -Value 0
```

Verify removal of the experiment CLSID/profile and preserve any preexisting
settings. Do not delete unrelated PCWord data or use a different Windows account
for unregistering. Registration/cleanup remain manual, unverified workflows.

## API references

- [RequestEditSession](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfcontext-requesteditsession): synchronous key handling and dual HRESULTs.
- [SetText](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfrange-settext): range, lock and composition semantics.
- [PreserveKey](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfkeystrokemgr-preservekey): explicit TSF keyboard commands.
- [SetGravity](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfrange-setgravity): tracked boundary behavior; actual provider behavior still requires a live test.

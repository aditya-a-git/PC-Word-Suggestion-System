# Implementation report — 2026-11-13

Delivered on branch `codex/tsf-feasibility-hardening`, [draft PR #8](https://github.com/aditya-a-git/PC-Word-Suggestion-System/pull/8).
PCWord is **not production-ready**. The requested real-host TSF gate is still open;
this report distinguishes implemented behavior from demonstrated compatibility.

## Implemented

- Separate, opt-in native TSF capture/write experiment with its own registration
  identity, preserved capture/apply keys, synchronous authorized edit sessions,
  foreground/context/selection/composition checks, generation and expected-window
  validation, five-second single-use tickets, and bounded replacement/caret/neighbor
  verification. Uncertain writes are never retried.
- Sensitive InputScope precedence fix, focused-top-context/thread tracking,
  ordinary integrity checks, fixed profile-description length and atomic safe logs.
- Managed record-copy validation, strict bounded JSON protocol checks, rejection of
  ineligible context text, monotonic request versions, cancellation outside locks
  and one-shot asynchronous acceptance with stale-completion protection.
- Native transaction, COM session/request and range-provider model tests plus
  managed regressions. All baseline tests retained.
- Duplicate dictionary publish item removed; package dictionary hash checked.
  CI retains TRX/native XML results and separate evaluation/experiment artifacts.
- Audit, architecture, TSF/IPC/privacy/compatibility/installation documentation,
  performance budgets, measured synthetic benchmark, Windows manual test procedure,
  metadata-only report script, plan and incomplete-work list.

The dictionary, n-gram model, ranking, coordinator boundaries and existing popup
were reused. The experimental DLL shares source with the read-only probe but is a
separate target/CLSID; normal app startup does not activate it. No second predictor,
unsafe insertion fallback, production service or consumer installer was added.

## Files changed by responsibility

| Area | Main files |
|---|---|
| Native integration | `tools/PCWord.TsfProbe/TsfProbe.cpp`, `TsfEditExperiment.inl`, `TsfEditSession.h`, `TsfRangeOperations.h`, `TsfRangeEdit.h`, `EditTransaction.h`, CMake/DEF files |
| Native regression checks | InputScopePolicyTests, EditTransactionTests, TsfEditSessionTests, TsfRangeEditTests |
| Managed safety/state | Core InputContextSnapshot/TextEditValidator; Prediction context manager/coordinator; WindowsIntegration codec/gate |
| Managed regressions | WindowsIntegrationProtocolTests, DictionaryCompletionCoordinatorTests |
| Package/CI | Stage0 project content references, `.github/workflows/dotnet.yml` |
| Test handoff | `tools/validation/New-NotepadTestReport.ps1`, `docs/testing.md`, `docs/tsf-integration.md` |
| Product status | README, plan, next_steps and the requested architecture/security/compatibility/performance/limitations documents |

## Actual validation

Final source head: `1b6e098f82a8c5d74130c8c483592b0faa338d7c`.
[Windows CI run 43437527781](https://github.com/aditya-a-git/PC-Word-Suggestion-System/actions/runs/43437527781)
tested GitHub's PR merge commit `2453484ff69e188b378057260f9cb74d9eb304d7`.
The runner was Microsoft Windows Server 2025, image `windows-2025-vs2026`;
this is not a Windows 11 desktop compatibility test.

| Evidence category | Result |
|---|---|
| Implemented | Native experiment and independent hardening described above |
| Built successfully | Full Release managed solution, diagnostic and experimental native x64 DLLs |
| Unit/model-tested | 216 managed tests, no skips; 5/5 native executables; GCC portable transaction test |
| Packaging-tested | Self-contained win-x64 controlled-editor publish, required dictionary hash, artifact upload |
| Script-tested | Windows metadata report generation; all 19 application cases remain not-run |
| Integration-tested | No production native/managed channel exists; not run |
| Verified in real external app | Not run: no Notepad activation/read/edit/document-integrity evidence |
| Verified Windows desktop installation | Not run: installer/upgrade/repair/uninstall and interactive GUI behavior unverified |
| Hosted Windows execution | Build, unit/model tests, report script and package checks passed |

The final run has no compiler/build warnings or errors. GitHub emitted an action
runtime deprecation notice for the repository's existing v4 actions; it did not
fail the run. Earlier native conditional-compilation and managed test syntax errors
were fixed before the successful runs. An earlier publish file-lock retry prompted
the duplicate-content fix. No failed test was deleted or weakened.

Synthetic Linux dictionary lookup p95 was 0.9741 ms; managed pipeline p95 was
1.0281 ms. These exclude external APIs, IPC and popup rendering. See
[performance.md](performance.md) for environment, method, memory/CPU and limits.

## Acceptance status and remaining work

Priority 1 is **partial**: an authorized experiment, stale rejection, verification
and model failure tests exist. Real external context acquisition, TSF activation,
selection/caret accuracy, authorized writes and whole-document integrity remain
unverified. Guard-window verification cannot prove a provider changed nothing
outside that window. No real application support is claimed.

Priorities 2–4 remain **blocked/not complete**: authenticated named pipes, the
external prediction/popup flow, representative application/privacy/DPI/soak tests
and consumer installation. The supplied requirements explicitly prohibit advancing
dependent stages before the real-host feasibility criteria are verified. The exact
next test is in [tsf-integration.md](tsf-integration.md); only incomplete work is in
[next_steps.txt](../next_steps.txt).

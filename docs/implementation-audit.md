# Implementation audit — 2026-11-13

Baseline: `a957e772ebc9ddca803b20778f08e2d3d943f442`. Inspection covered the
solution, both Stage0 directories, native probes, managed tests, workflow,
README, context, plan and integration/feasibility/privacy documentation.
`next_steps.txt` was absent. No AGENTS.md was present in the checkout.

| Component | Actual implementation | Action / dependency |
|---|---|---|
| Core | Bounded snapshots, adapter/edit contracts, settings and suggestion state | Reuse; validate snapshots again at trust boundaries because record `with` bypasses constructor validation |
| Prediction | ESDB dictionary/prefix index/ranker, synchronous coordinator, context manager, bounded inference n-gram model | Preserve algorithms and data; fix acceptance lifecycle before asynchronous adapters |
| WindowsIntegration | JSON contract and stale-request gate only | No transport or peer authentication; production IPC waits for the TSF gate |
| WindowsUI / Logic | Independent no-activate WinForms window, coordinator bridge, placement/state logic | Reuse; live focus/DPI and external input routing unverified |
| src/PCWord.Stage0 | Real controlled-editor adapter, explicit edit, post-edit range/caret check, settings | Existing working demo, not evidence of external compatibility |
| Stage0/ | Older standalone RichTextBox feasibility project outside solution | Historical experiment; do not present as TSF or duplicate it |
| TsfProbe | In-process COM TIP, edit/focus notifications, scope policy, bounded read diagnostics, registration | Harden scope/focus checks; add separately identified opt-in edit experiment |
| WindowsProbe | Read-only UI Automation diagnostic | No safe write backend; retain diagnostic scope |
| Tests / tools | Managed unit tests, two native policy executables, benchmark/evaluation/report tools | Add regression and edit transaction tests; retain all existing tests |
| CI | Windows managed build/tests, native x64 build/CTest, self-contained demo ZIP | Latest baseline run 42466289421 succeeded; previous run 42466057851 failed before the default interpolation fix |

Concrete findings before edits:

* `ClassifyScopes` returns Unknown on the first unknown scope before inspecting
  later sensitive values. The diagnostic host policy permits Unknown, so an
  array such as Default + Password can bypass sensitive-field denial.
* A pushed context is attached without checking the focused document manager;
  the callback treats pointer equality with the attached context as focus proof.
  Thread focus loss is not observed. This is insufficient for authorizing reads.
* Diagnostic reads do not establish a write contract, stale-operation rejection,
  or post-edit integrity. There is no `ITfEditSession` implementation.
* Managed snapshot init properties can bypass constructor bounds. The protocol
  trusts an already-created snapshot, and constructor exceptions during decode
  escape the codec's documented malformed-frame failure type.
* `StaleRequestGate` invokes cancellation callbacks under its lock. User callbacks
  can deadlock or re-enter it, and can interrupt invalidation by throwing.
* Coordinator acceptance is not one-shot across asynchronous callers. A previous
  success without a new snapshot leaves its candidates eligible for reuse.
* No production pipe, native IPC client, authenticated deployment identity,
  system-wide prediction host, installer or interactive compatibility evidence.

Execution environment: Linux cloud workspace with a locally installed .NET 10
SDK. Windows CI is available. A hosted Windows build does not demonstrate TSF
activation in interactive Notepad. No Windows GUI measurements are invented.

Dependency order: native read/write experiment and safety regressions → real
Notepad evidence → authenticated IPC → existing coordinator/popup vertical slice
→ compatibility, sustained use and installation. Dependent production stages
remain blocked until the real-host gate passes, as required by the task.

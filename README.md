# PCWord

PCWord is a Windows desktop typing prototype. The PCWord-owned WinForms editor supports local partial-word completion from a size-60 ESDB vocabulary containing American and both British spelling categories, plus an optional local n-gram next-word model at eligible word boundaries. The dictionary preserves spelling variants as separate searchable words and ranks by ESDB commonness size and deterministic completion quality; this is not a corpus-frequency ranking. Both prediction paths show up to three candidates and use explicit acceptance, dismissal, and a pause toggle.

**Release status:** development prototype only. No installer or production external-app adapter exists, and external applications are not supported for prediction. See [release readiness](docs/release-readiness.md), [installation limits](docs/installation.md), and [uninstallation limits](docs/uninstallation.md).

## Current hardening work

A separate **PCWordTsfEditExperiment.dll** now implements a guarded, explicit
`ITfEditSession` experiment for synthetic Notepad text. It has its own COM/profile
identity, two opt-in flags, capture/apply shortcuts, stale-context checks, a
five-second one-shot ticket, and bounded post-edit/caret verification. The original
**PCWordTsfProbe.dll remains read-only**. The experiment is not connected to
prediction, IPC or the popup and has not been run in real Notepad in this task.

Sensitive-scope precedence, foreground context checks, managed snapshot/protocol
validation, cancellation reentrancy and duplicate acceptance are hardened. The
expanded managed suite passes 216 tests; native model/COM tests and Windows CI
results are recorded in [testing](docs/testing.md).

The [implementation report](docs/implementation-report.md) records exact source/CI revisions and remaining gates.

For your Windows test, follow [the exact Notepad experiment procedure](docs/tsf-integration.md).
See [architecture](docs/architecture.md), [compatibility](docs/application-compatibility.md),
[security](docs/ipc-security.md), [limitations](docs/known-limitations.md), and
[remaining work](next_steps.txt). Production transport, external suggestions and
consumer installation remain gated on actual Windows application evidence.

## Build, test, and run

Requirements: Windows, .NET 10 SDK, and the Windows Desktop targeting pack.

```powershell
dotnet restore .\PCWord.sln
dotnet build .\PCWord.sln --configuration Release --no-restore
dotnet test .\PCWord.sln --configuration Release --no-restore --verbosity minimal
dotnet run --project .\src\PCWord.Stage0\PCWord.Stage0.csproj --configuration Release
```

The editor reads bounded context only from its own RichTextBox. The **Suggestions enabled** control pauses collection, predictions, display, and acceptance. The sensitive-field control suppresses context and edits. Use only synthetic text in experiments.

## Benchmark

```powershell
dotnet run --project .\tools\PCWord.Benchmark\PCWord.Benchmark.csproj --configuration Release -- --iterations 100000
```

The benchmark reports dictionary lookup separately from the synthetic managed end-to-end path, plus startup, CPU time, and memory deltas. To measure the bundled bilingual dictionary, pass `--dictionary .\artifacts\dictionary\esdb-en-bilingual.tsv`. It prints no prefix or candidate text. The bundled `sample.tsv` contains nine fixture entries and is only a smoke baseline. Method and measured values are in `docs/benchmarks.md`. The running app also has an on-demand **Performance stats** view with bounded aggregate timings.

## Windows feasibility tools

`tools/PCWord.WindowsProbe` is read-only UI Automation feasibility code. `tools/PCWord.TsfProbe` remains diagnostic-only. Its bounded context read requires the separate `PCWORD_TSF_ALLOW_CONTEXT_READ=1` process opt-in plus the per-user enabled setting and exact host executable allowlist. The default is paused and `notepad.exe` only. Sensitive InputScope classifications always deny; Unknown is allowed only for an allowlisted host. This does not make external applications production-supported.

The Windows build verifies native compilation and five input-policy, transaction, COM-session and range-model test executables. The TSF profile was not registered or activated by this build/test run. The owner has reported Notepad observations, documented separately, but the new registry policy has no runtime host verification. Production cross-process prediction, authorized TSF edits, and a system popup are not implemented; external apps remain unsupported for prediction. See `docs/windows-integration-status.md` for the exact status.

## Known limitations

- Production prediction is implemented only for PCWord's own controlled editor. Every external app remains unsupported for production use.
- The native TSF component is an observation/feasibility probe only; it has no production adapter, safe external insertion path or system popup. Diagnostic observation does not establish application compatibility.
- Sensitive, unknown, elevated, remote, terminal and custom-rendered fields are denied or unsupported by default. Diagnostic reads remain opt-in and allowlist-gated.
- External app/version/field rows are Untested unless explicitly identified as an owner report; no cross-app support claim is made.

## Scope and limitations

- The prediction and editing code path implemented here is limited to PCWord's own editor. The TSF probe can perform diagnostic bounded reads under the gates above, but it is not a production prediction adapter for other applications.
- No global keyboard hook, production cross-process IPC/editing, connected external-app popup, network prediction, or persistence of typed text is implemented. TSF editing exists only in the separate opt-in synthetic experiment described above.
- External TSF policy settings are stored under `HKCU\Software\PCWord`: an Enabled DWORD and an Allowlist multi-string containing executable file names only. The managed app starts this feature paused by default and reports whether Ctrl+Alt+Shift+P was registered. Names are matched case-insensitively and exactly, but file-name matching can be spoofed by a differently located binary; path/signature checks are a later hardening step. Do not add browsers by default: if InputScope is Unknown, ordinary non-password fields such as card-number or OTP fields would be allowed.
- No confidence score or corpus frequency is inferred; dictionary ranking uses ESDB size/variant metadata, with a two-character minimum prefix and a three-candidate cap.
- Stage 4's controlled-editor dictionary path and local benchmark are implemented. Runtime measurements from the live WinForms UI are available in the app but were not collected in this run. External Windows integration remains unsupported.
- Stage 5 provides a bounded offline model, pinned WikiText-2 preparation/evaluation, and controlled-editor integration. It loads an optional local model from `%LOCALAPPDATA%\PCWord\ngram-model.json`; generate one with the Stage 5 evaluator. The 2026-11-08 WikiText test run measured 16.29% top-1 and 25.15% top-3 versus 7.27% and 13.73% for the unigram baseline. No corpus or derived model is bundled because the source card's license statements conflict. See `data/README.md` and `docs/stage-5-evaluation.md`.

Dictionary data source, licensing, generation and runtime format are documented in `docs/dictionary-sources.md`. See also `docs/testing-guide.md`, `docs/benchmarks.md`, `docs/windows-integration-status.md`, `docs/compatibility-matrix.md`, `docs/privacy-model.md`, and `docs/architecture-decisions.md`.

# PCWord

PCWord is a Windows desktop typing prototype. The PCWord-owned WinForms editor supports frequency-ranked partial-word completion and an optional local n-gram next-word model at eligible word boundaries. Both show up to three candidates and use explicit acceptance, dismissal, and a pause toggle.

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

The benchmark reports dictionary lookup separately from the synthetic managed end-to-end path, plus startup, CPU time, and memory deltas. It prints no prefix or candidate text. The bundled dictionary contains nine sample entries; results are a repeatable smoke baseline, not a production-dictionary performance claim. Method and measured values are in `docs/benchmarks.md`. The running app also has an on-demand **Performance stats** view with bounded aggregate timings.

## Windows feasibility tools

`tools/PCWord.WindowsProbe` is read-only UI Automation feasibility code. `tools/PCWord.TsfProbe` remains diagnostic-only. Its bounded context read requires the separate `PCWORD_TSF_ALLOW_CONTEXT_READ=1` process opt-in plus the per-user enabled setting and exact host executable allowlist. The default is paused and `notepad.exe` only. Sensitive InputScope classifications always deny; Unknown is allowed only for an allowlisted host. This does not make external applications production-supported.

The current Windows build verifies native compilation and the two input-scope/safety policy test executables only. The TSF profile was not registered or activated by this build/test run. The owner has reported Notepad observations, documented separately, but the new registry policy has no runtime host verification. Production cross-process prediction, authorized TSF edits, and a system popup are not implemented; external apps remain unsupported for prediction. See `docs/windows-integration-status.md` for the exact status.

## Scope and limitations

- The prediction and editing code path implemented here is limited to PCWord's own editor. The TSF probe can perform diagnostic bounded reads under the gates above, but it is not a production prediction adapter for other applications.
- No global keyboard hook, cross-process IPC/editing, TSF edit session, external-app popup, network prediction, or persistence of typed text is implemented.
- External TSF policy settings are stored under `HKCU\Software\PCWord`: an Enabled DWORD and an Allowlist multi-string containing executable file names only. The managed app starts this feature paused by default and reports whether Ctrl+Alt+Shift+P was registered. Names are matched case-insensitively and exactly, but file-name matching can be spoofed by a differently located binary; path/signature checks are a later hardening step. Do not add browsers by default: if InputScope is Unknown, ordinary non-password fields such as card-number or OTP fields would be allowed.
- No confidence score is inferred; current dictionary ranking is frequency-based with a two-character minimum prefix and a three-candidate cap.
- Stage 4's controlled-editor dictionary path and local benchmark are implemented. Runtime measurements from the live WinForms UI are available in the app but were not collected in this run. External Windows integration remains unsupported.
- Stage 5 code provides a bounded offline model, evaluation tool, and controlled-editor integration. It loads an optional model from `%LOCALAPPDATA%\PCWord\ngram-model.json`; generate one with the Stage 5 evaluator. No corpus or model is bundled. WikiText evaluation could not be run in this environment because dataset download connections reset, so Stage 5 quality/performance exit criteria remain incomplete. See `data/README.md` and `docs/benchmarks.md`.

See `docs/testing-guide.md`, `docs/benchmarks.md`, `docs/windows-integration-status.md`, `docs/compatibility-matrix.md`, `docs/privacy-model.md`, and `docs/architecture-decisions.md`.

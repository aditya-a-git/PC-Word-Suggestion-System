# PCWord

PCWord is a Windows desktop word-completion prototype. Stage 4 connects the PCWord-owned WinForms editor to the existing local frequency-ranked dictionary. It shows up to three completions for eligible partial words, supports explicit acceptance and dismissal, and includes a pause toggle.

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

`tools/PCWord.WindowsProbe` is read-only UI Automation feasibility code. `tools/PCWord.TsfProbe` is a native TSF diagnostic probe with fail-closed input-scope policy. Neither is a production adapter. TSF `GetValue(GUID_PROP_INPUTSCOPE)` failure remains unresolved and unknown fields remain denied.

## Scope and limitations

- The production code path implemented here is limited to PCWord's own editor. It is not an adapter for other applications.
- No global keyboard hook, cross-process IPC/editing, TSF edit session, external-app popup, network prediction, or persistence of typed text is implemented.
- No confidence score is inferred; current dictionary ranking is frequency-based with a two-character minimum prefix and a three-candidate cap.
- Stage 4's controlled-editor dictionary path and local benchmark are implemented. Runtime measurements from the live WinForms UI are available in the app but were not collected in this run. External Windows integration remains unsupported.
- Stage 5 is planned only. Its n-gram predictor and evaluation have not been implemented.

See `docs/testing-guide.md`, `docs/benchmarks.md`, `docs/windows-integration-status.md`, `docs/compatibility-matrix.md`, `docs/privacy-model.md`, and `docs/architecture-decisions.md`.

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

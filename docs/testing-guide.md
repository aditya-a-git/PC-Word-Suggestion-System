# Testing guide

## Managed solution

From the repository root:

```powershell
dotnet restore .\PCWord.sln
dotnet build .\PCWord.sln --configuration Release --no-restore
dotnet test .\PCWord.sln --configuration Release --no-restore --verbosity minimal
```

On 2026-10-27, `dotnet build PCWord.sln -c Release` passed with zero warnings/errors, and `dotnet test PCWord.sln -c Release --no-build` passed 126/126 managed tests. Existing cases cover rapid consecutive versions/fields, focus and field invalidation, disable/re-enable freshness, sensitive/unavailable contexts, empty-result clearing, obsolete candidate rejection, and metrics with no text-shaped API/storage. These are controlled-editor contracts, not external-app integration tests.

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

On 2026-10-27, CMake configured and built `PCWordTsfProbe.dll` with Visual Studio 18 / MSVC 19.51 and Windows SDK 10.0.26100; CTest passed 2/2 policy executables. They do not activate TSF or prove provider behavior. No TSF profile was registered or activated in that run.

## Runtime status and privacy inspection

The Windows controlled-editor UI was not launched in this run. Runtime timings for real RichTextBox acquisition and popup handling can be viewed using the app's **Performance stats** button. External apps remain unsupported; no Stage 0–3 retest is implied.

A scoped source search was run:

```powershell
rg -n -i "HttpClient|WebRequest|SocketsHttpHandler|WebSocket|TcpClient|UdpClient|NetworkStream|Process\.Start|Console\.Write|Debug\.Write|Trace\.Write|File\.Write|File\.Append|StreamWriter" src\PCWord.Core src\PCWord.Prediction src\PCWord.Stage0
```

It found no network-client, logging, or content-write API matches in those prediction-path projects. This is source inspection, not a runtime network capture or a repository-wide security audit. The normal path loads the bundled dictionary locally.

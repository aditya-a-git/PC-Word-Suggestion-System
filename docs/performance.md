# Performance budgets and measurements

Initial budgets for the eventual external path (targets, not verified guarantees):

| Stage | Target |
|---|---|
| TSF bounded acquisition callback | p95 ≤5 ms, p99 ≤10 ms |
| Local authenticated IPC round trip | p95 ≤10 ms; finite request deadline 250 ms |
| Dictionary prediction | p95 ≤30 ms |
| Context through popup display | p95 ≤100 ms, p99 ≤200 ms |
| Explicit acceptance and verification | p95 ≤50 ms; never retry uncertain writes |
| Pending work | One current prediction per context; explicit bounded connection/queue budgets before transport implementation |
| Eight-hour steady workload | No text corruption, leaked handles/threads or monotonically increasing retained memory; investigate >10% post-warmup growth |

No native callback/IPC/popup/acceptance timing is claimed. Synchronous TSF/provider
calls cannot be forcibly timed out safely; latency/availability are part of the
real-host feasibility gate. Memory/CPU budgets for the broker must be selected from
actual Windows baselines before release.

Measured 2026-11-13 in a Linux x64 cloud VM, kernel 6.18.57, 3 exposed AMD EPYC 9V74
vCPUs, .NET SDK 10.0.404, Release, unchanged bundled 84,057-entry dictionary:

```sh
dotnet run --project tools/PCWord.Benchmark/PCWord.Benchmark.csproj -c Release -- \
  --iterations 10000 --dictionary artifacts/dictionary/esdb-en-bilingual.tsv
```

100 warmups; 10,000 iterations per phase; percentiles use the existing final 2,048
sample ring, not all 10,000 samples. No optimization or ranking change was made.

| Measurement | Result |
|---|---|
| Dictionary load/index, one startup sample | 179.403 ms |
| Lookup median / p95 | 0.0127 / 0.9741 ms |
| Synthetic managed pipeline median / p95 | 0.0136 / 1.0281 ms |
| Pipeline process CPU, aggregate | 1,756.271 ms |
| Initialization working-set delta | +52,617,216 bytes |
| Initialization managed-heap delta | +22,028,088 bytes |
| Pipeline working set before / after | 80,375,808 / 95,887,360 bytes |
| Pipeline managed heap before / after | 22,235,648 / 22,226,424 bytes |

This synthetic path excludes external text access, native IPC, OS caret reads,
WinForms painting and user interaction. A short working-set delta is not a leak
or leak-free verdict. No sustained-use result is available. Historical Windows
benchmark/evaluation data remains in [benchmarks.md](benchmarks.md).

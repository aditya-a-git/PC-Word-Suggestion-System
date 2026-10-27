# Stage 4 performance baseline

## Method

Run the benchmark from the repository root:

```powershell
dotnet run --project .\tools\PCWord.Benchmark\PCWord.Benchmark.csproj --configuration Release -- --iterations 100000
```

It uses only `src/PCWord.Prediction/Dictionary/sample.tsv` and fixed synthetic prefixes. It first warms the dictionary/index and coordinator, then separately measures direct dictionary lookup and the managed pipeline. It does not print or persist prefixes, context, candidates, or document text.

The synthetic end-to-end phase includes bounded snapshot construction, `PredictionContextManager`, dictionary lookup/ranking/casing, adapter candidate handoff, and a small synthetic result consumer. It excludes RichTextBox/Win32 context reads and actual WinForms painting. The app itself separately instruments real context acquisition and popup event handling in fixed-size in-memory timing rings; the on-demand Performance stats view exposes aggregate counts and percentiles.

Each metric ring retains at most 2,048 samples and tracks a lifetime count. Reported percentiles use the retained most recent window. No timing record contains text, field identity, candidate, or per-user event identifier.

## Measured run

Environment: local Windows environment, .NET 10 Release build. Command used:

```powershell
dotnet run --project .\tools\PCWord.Benchmark\PCWord.Benchmark.csproj --configuration Release --no-build -- --iterations 100000
```

The bundled dictionary had **9 entries**. Results from that run:

| Stage / metric | Samples | Median | p95 |
|---|---:|---:|---:|
| Dictionary startup (load + index) | 1 | 9.226 ms | n/a (single observation) |
| Dictionary lookup only | 100,000 | 0.0009 ms | 0.0028 ms |
| Synthetic context acquisition | 100,000 | 0.0002 ms | 0.0003 ms |
| Lookup + ranking + casing | 100,000 | 0.0023 ms | 0.0041 ms |
| Result delivery | 100,000 | 0.0002 ms | 0.0003 ms |
| Synthetic result-consumer/UI handoff | 100,000 | 0.0000 ms | 0.0001 ms |
| Synthetic managed end-to-end | 100,000 | 0.0033 ms | 0.0051 ms |

Percentiles use the most recent 2,048 samples for this run. The zero display for UI median is below the formatter's 0.0001 ms display precision, not evidence of zero work. Startup has one observation and no meaningful percentile.

Process CPU time for the end-to-end phase was **500.000 ms total** (includes runtime and GC overhead; no per-prediction CPU claim). Working set moved from **44,691,456** to **53,325,824 bytes** (**+8,634,368 bytes**); managed heap after forced collection moved from **392,152** to **391,096 bytes** (**-1,056 bytes**). These are whole-process before/after snapshots, not peak memory attributable solely to prediction.

## Limits and next measurements

The nine-word bundled sample is intentionally tiny and does not represent a real language dictionary. These numbers establish a repeatable functional smoke baseline only; they do not prove performance with a large vocabulary. The synthetic end-to-end timing does not include Windows control reads, real event scheduling, popup layout/painting, or human-visible latency. The application collects those stage timings at runtime, but a live UI metrics sample was **not collected** in this run. No CPU/memory baseline for a production-sized dictionary is available.

No confidence threshold is applied. Candidate order is based on the existing dictionary frequency ranker; results are limited to three and partial-word prediction requires a two-character prefix. Empty or ineligible contexts clear the current candidates.

## Stage 5 evaluation plan (planned, not implemented)

- Use Salesforce WikiText **`wikitext-2-raw-v1`** with train for n-gram counts, validation for configuration selection, and test only for final reporting. Pin the dataset revision, keep the preprocessing script and checksums, and review the source's current license/attribution before importing any text. Do not include private documents. See the [dataset card](https://huggingface.co/datasets/Salesforce/wikitext) and the original [WikiText paper](https://arxiv.org/abs/1609.07843).
- Score next-token top-1 and top-3 exact match after a documented tokenizer/normalizer, reporting eligible token count, OOV rate, and coverage so filtering cannot inflate accuracy silently.
- Start with a local backoff n-gram model (small bounded orders, smoothed counts, explicit unknown-token behavior). Compare it to a frequency-only unigram baseline on the same held-out examples.
- Measure initialization, per-request median/p95, working-set and managed-memory deltas, and process CPU time using the same reproducible harness style. Report quality/performance by split and include tokenizer, vocabulary, model size, and dataset revision.
- Exercise the predictor through the existing controlled-editor coordinator with stale-version, pause, sensitive-context, and offline tests. This work must not depend on production TSF or external-app support.
- Do not add a neural model unless the n-gram evaluation shows a meaningful quality gap that justifies its measured cost. Do not mark Stage 5 complete until the implementation, evaluation outputs, data provenance, and tests are delivered.

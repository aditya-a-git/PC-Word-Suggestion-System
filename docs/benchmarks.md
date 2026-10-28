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

## Stage 5 target evaluation protocol

- Use Salesforce WikiText **`wikitext-2-raw-v1`** with train for n-gram counts, validation for configuration selection, and test only for final reporting. Pin the dataset revision, keep the preprocessing script and checksums, and review the source's current license/attribution before importing any text. Do not include private documents. See the [dataset card](https://huggingface.co/datasets/Salesforce/wikitext) and the original [WikiText paper](https://arxiv.org/abs/1609.07843).
- Score next-token top-1 and top-3 exact match after a documented tokenizer/normalizer, reporting eligible token count, OOV rate, and coverage so filtering cannot inflate accuracy silently.
- Start with a local backoff n-gram model (small bounded orders, smoothed counts, explicit unknown-token behavior). Compare it to a frequency-only unigram baseline on the same held-out examples.
- Measure initialization, per-request median/p95, working-set and managed-memory deltas, and process CPU time using the same reproducible harness style. Report quality/performance by split and include tokenizer, vocabulary, model size, and dataset revision.
- Exercise the predictor through the existing controlled-editor coordinator with stale-version, pause, sensitive-context, and offline tests. This work must not depend on production TSF or external-app support.
- Do not add a neural model unless the n-gram evaluation shows a meaningful quality gap that justifies its measured cost. Do not mark Stage 5 complete until the implementation, evaluation outputs, data provenance, and tests are delivered.

## Stage 5 implementation and evaluation status (2026-10-28)

The new `tools/PCWord.Evaluation` command trains only from the supplied training text, selects order (1–3) and backoff factor (0.2, 0.4, 0.6, 0.8) by validation top-3 then top-1, and evaluates the test file once. It reports test sample count, OOV, target vocabulary coverage, top-1/top-3, unigram baseline, model build/load time, serialized model size, lookup median/p95, synthetic manager-plus-model request median/p95 (bounded 2,048 sample window), complete test-pass elapsed time, process CPU, managed-memory delta, and working-set delta. OOV targets remain in the accuracy denominator and cannot match; no synthetic unknown-token score is assigned. The manager-plus-model path includes snapshot construction and context eligibility; it excludes RichTextBox reads, candidate delivery, popup/UI work and edit acceptance. The output model stores the selected order/factor and stays where the user directs it. It prints aggregate values only.

Run after converting verified pinned Parquet files to three separate UTF-8 `.txt` files as documented in `data/README.md`:

```powershell
dotnet run --project .\tools\PCWord.Evaluation\PCWord.Evaluation.csproj --configuration Release -- <train.txt> <validation.txt> <test.txt> <local-model-output.json>
```

**Actual fixed-corpus results: unavailable.** The three attempted HTTPS downloads from the pinned Salesforce/Hugging Face revision all failed with `curl: (35) Recv failure: Connection was reset`. No corpus splits were obtained, their listed SHA-256 values were not independently recomputed, and no WikiText quality or model performance measurements are claimed. The evaluator and model code compiled, but the benchmark has not yet been run on the target corpus. No model is bundled, so a normal checkout will not show next-word candidates until the user supplies a locally generated model.

**Synthetic harness smoke only (not a dataset result):** with 4 short authored lines, the evaluator selected order 2/backoff 0.2 on its synthetic validation split; test n=5, target coverage 100%, OOV 0%, top-1 80%, top-3 80%, unigram top-1 0%, top-3 40%. Model build 26.47 ms, reload 8.43 ms, model size 539 bytes, lookup median/p95 0.01890/0.02980 ms, synthetic manager-plus-model request median/p95 0.01510/1.55630 ms, test evaluation 0.22 ms, process CPU 109.38 ms, managed delta +136,336 bytes, working set delta +8,269,824 bytes. These tiny-run figures only demonstrate that the harness emits measurements and that model save/load works; they have no quality or resource significance for WikiText or production.

The optional controlled-editor path records n-gram lookup in the bounded `CompletionMetrics` ring and continues to measure context acquisition, delivery, UI update, and coordinator end-to-end time. Its live WinForms UI measurement and interactive acceptance path were not run in this environment. The Stage 4 nine-word dictionary baseline above is unchanged and is not evidence for Stage 5 quality. External applications remain unsupported.

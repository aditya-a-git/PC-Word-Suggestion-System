# Stage 5 evaluation: WikiText-2

## Status

The pinned WikiText-2 run completed on 2026-11-08. All three source Parquet checksums matched their published hashes before conversion. The trainer used train only, order/interpolation weight were chosen on validation, and the final report used test once. The generated corpus-derived model remains local because the dataset card's license statements conflict.

## Architecture audit

- **Model and training:** `src/PCWord.Prediction/Inference/NGramModel.cs` contains the existing word unigram/bigram/trigram model and Unicode tokenizer. `Train` counts only the supplied lines, then retains the 50,000 most frequent tokens, up to 250,000 context keys per order, and up to 32 continuations per context. Training runs offline in `tools/PCWord.Evaluation`; normal application use does not train.
- **Dataset preparation and model generation:** `tools/PCWord.Evaluation/prepare_wikitext.py` verifies the pinned Parquet hashes and exports rows without printing text. `tools/PCWord.Evaluation/Program.cs` trains from train, selects order and interpolation weight on validation, evaluates test, and saves the model JSON to the requested local path. The JSON report contains aggregate metrics and split filenames only.
- **Serialization and loading:** `NGramModel.Save` serializes unigram and context counts as JSON. `Load` rejects missing/oversized files, invalid counts, over-limit vocabularies/contexts, and unsupported configurations; it then compacts context data into shared word references and bounded arrays for inference. The serialized file is capped at 128 MiB.
- **Context extraction and eligibility:** `PredictionContextManager` consumes bounded snapshots from the existing input adapter. It rejects stale versions, sensitive/unknown fields, selection, composition, lost focus, and unavailable context. It provides preceding text to the model only at an eligible word boundary; partial-word completions still use `CandidateRanker` and the bilingual dictionary.
- **Runtime delivery and UI:** `DictionaryCompletionCoordinator` owns model calls and candidate provenance for the same current field/version checks used by completion. The controlled editor loads `%LOCALAPPDATA%\PCWord\ngram-model.json` at startup. The WinForms popup receives the coordinator result and accepts through the existing adapter. The separate `SuggestionPopupController` also labels the mode as next-word or complete-word when used with the coordinator.
- **Fallback and privacy:** Missing or invalid local model returns `null`, so next-word suggestions are absent while dictionary completion remains available. Prediction exceptions fail closed and editing continues. No model or text is sent over a network; typed context is neither logged nor saved by the predictor.

## Dataset provenance

- Dataset: Salesforce WikiText, configuration `wikitext-2-raw-v1`.
- Pinned revision: `b08601e04326c79dfdd32d625aee71d232d685c3`.
- Dataset card: [pinned Hugging Face dataset card](https://huggingface.co/datasets/Salesforce/wikitext/blob/b08601e04326c79dfdd32d625aee71d232d685c3/README.md).
- Original description: [WikiText paper](https://arxiv.org/abs/1609.07843).
- License: the dataset card presents conflicting licensing statements (CC BY-SA 3.0 and GFDL metadata, with CC BY-SA 4.0 stated in its prose). The data and derived model are not redistributed by this repository while rights remain unresolved.
- Verified SHA-256: train `e83889baabc497075506f91975be5fac0d45c5290b6b20582c8cd1e853d0c9f7`; validation `204929b7ff9d6184953f867dedb860e40aa69c078fc1e54b3baaa8fb28511c4c`; test `5f1bea067869d04849c0f975a2b29c4ff47d867f484f5010ea5e861eab246d91`.

## Preprocessing and split discipline

The checked-in `tools/PCWord.Evaluation/prepare_wikitext.py` verifies the pinned Parquet SHA-256 values and streams each `text` column to UTF-8 files, preserving row order and one row per line. The verified split row counts were train 36,718, validation 3,760, and test 4,358. Training counts are built from train only. Order/interpolation weight are selected on validation top-3, then top-1, with deterministic ties preferring the smaller order then smaller weight. Test is read only after selection for final reporting.

The existing PCWord tokenizer lowercases Unicode letters, digits, and combining marks invariantly. All other characters separate tokens. Period, exclamation mark, question mark, CR/LF, and row boundaries terminate history. Whitespace has no token; apostrophes and hyphens are separators. One prediction target is scored for each token after the first token in each segment. The first token has no left context and is reported as skipped. The target is never included in the context passed to the predictor. Context is built from at most the previous two tokens, matching the model's maximum effective history.

## Metrics

The evaluator uses `NGramModel.Predict` and `PredictionContextManager` eligibility, with identical examples for the unigram baseline.

- Eligible examples: prediction targets after the first token of each punctuation/line/row segment that fit the bounded controlled-editor context.
- Top-k accuracy over all eligible targets: exact match in the first k candidates divided by all eligible examples. OOV targets remain in this denominator and count as misses.
- Vocabulary coverage: eligible targets present in the trained model vocabulary divided by all eligible targets.
- OOV rate: eligible targets absent from model vocabulary divided by all eligible targets.
- In-vocabulary accuracy: exact top-k hits divided by eligible targets present in the model vocabulary.
- Skipped: segment-initial tokens without left context; overlong contexts, if any, are excluded from eligible examples and separately counted.
- Context buckets: one-token and two-token histories. The model uses trigram counts when the two-token context exists and backs off through bigram/unigram tables.
- Prediction latency: warmed, per-example median and nearest-rank p95 for context-manager eligibility plus `NGramModel.Predict`, capped at 25,000 samples. It excludes dataset I/O and real WinForms control/painting work.
- Model loading is timed separately after local serialization. Training time, serialized size, process CPU, managed heap delta, working-set delta, and total test scoring duration are included in JSON.

## Results

| Result | Value |
|---|---|
| Metric | Original bounded backoff model | Interpolated model + 50k vocabulary |
|---|---:|---:|
| Validation-selected order / interpolation weight | 3 / 0.4 (old backoff semantics) | 3 / 0.8 |
| Test eligible examples | 195,336 | 195,336 |
| Test first-token skips / overlong skips | 11,145 / 0 | 11,145 / 0 |
| Vocabulary coverage / OOV rate | 92.02% / 7.98% | 95.20% / 4.80% |
| N-gram top-1 / top-3 / top-5, all eligible | 16.10% / 25.12% / 29.70% | 16.29% / 25.15% / 29.72% |
| Unigram top-1 / top-3 / top-5, all eligible | 7.27% / 13.73% / 18.47% | 7.27% / 13.73% / 18.47% |
| Serialized model size | 16,425,922 bytes | 17,307,562 bytes |
| Train / reload time | 9.70 s / 1.81 s | 10.16 s / 1.48 s |
| Retained managed heap delta during model load | Not measured | +122,716,992 bytes |
| Warmed context-manager + model latency, median / p95 | 0.0146 / 0.0260 ms | 0.0215 / 0.0502 ms |

The interpolation change improved all-eligible top-1 by 0.19 percentage points and top-3 by 0.03 points versus the prior algorithm. Expanding the vocabulary raised test coverage by 3.18 points while keeping the model below 18 MB. Increasing per-context candidate storage from 32 to 64 added about 0.02 points to top-3 and nearly doubled p95 latency, so the shipped cap remains 32.

The selected model outperformed the unigram baseline by 9.02 percentage points on top-1 and 11.43 points on top-3. This measures next-token prediction on Wikipedia prose with this tokenizer, not conversational usefulness. The test in-vocabulary-conditioned scores are top-1 17.11%, top-3 26.42%, top-5 31.22%; the all-eligible scores above remain the main result.

Training took 10.16 seconds, loading took 1.48 seconds, and the warmed prediction path measured median 0.0215 ms / p95 0.0502 ms over 25,000 examples. The compact runtime representation reduced the retained managed heap delta during model load to about 117 MiB. The evaluator's whole-process deltas were +193,733,584 managed bytes and +230,789,120 working-set bytes; these include training, validation sweeps, allocations, serialization, and test scoring, so they are not runtime model-only memory.

The dataset hashes independently verified as train `e83889baabc497075506f91975be5fac0d45c5290b6b20582c8cd1e853d0c9f7`, validation `204929b7ff9d6184953f867dedb860e40aa69c078fc1e54b3baaa8fb28511c4c`, and test `5f1bea067869d04849c0f975a2b29c4ff47d867f484f5010ea5e861eab246d91`. The complete aggregate report is available as [stage-5-evaluation-results.json](stage-5-evaluation-results.json); it contains metrics and file names only, no corpus text.

The model is not bundled. To build and install a local model, download the pinned Parquet files as described in `data/README.md`, run the checked-in preparation script, then pass `%LOCALAPPDATA%\PCWord\ngram-model.json` as the model-output argument. Normal inference stays offline and uses the local file only.

## Reproduction

From a Windows checkout with network access, follow the pinned download/hash/conversion commands in `data/README.md`, then run:

```powershell
dotnet restore .\PCWord.sln
python .\tools\PCWord.Evaluation\prepare_wikitext.py .\wikitext-2-raw-v1 .\splits
$model = Join-Path $env:LOCALAPPDATA 'PCWord\ngram-model.json'
dotnet run --project .\tools\PCWord.Evaluation\PCWord.Evaluation.csproj --configuration Release -- .\splits\train.txt .\splits\validation.txt .\splits\test.txt $model .\splits\evaluation-results.json
```

The corpus and generated model are local files and must not be committed. The command prints aggregate numbers only and writes a machine-readable result. The local .NET testhost did not connect in this environment, so the managed test suite is being verified in GitHub Actions. Interactive WinForms acceptance with the model installed was not performed; run the manual checklist in `docs/testing-guide.md` with disposable text before claiming the full UI path is verified.


## Verification on main

Commit 1127aa0 passed GitHub Actions run [42161998128](https://github.com/aditya-a-git/PC-Word-Suggestion-System/actions/runs/42161998128): Release restore/build passed, managed tests passed 173/173, and the native TSF probe configured/built with policy tests passing. This verifies build and automated behavior only; it does not provide WikiText metrics or interactive WinForms evidence.

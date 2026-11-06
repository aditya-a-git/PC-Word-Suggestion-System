# Stage 5 evaluation: WikiText-2

## Status

The evaluation harness is implemented and builds through the repository workflow. A final WikiText-2 corpus run is **not available**: on 2026-11-06 this environment could not resolve `huggingface.co` (`curl: (6) Could not resolve host`). No data was substituted. Metrics and corpus-backed latency are deliberately reported as unavailable. Stage 5 remains incomplete until evaluation is run against verified splits and the WinForms checks are completed.

## Dataset provenance

- Dataset: Salesforce WikiText, configuration `wikitext-2-raw-v1`.
- Pinned revision: `b08601e04326c79dfdd32d625aee71d232d685c3`.
- Dataset card: [pinned Hugging Face dataset card](https://huggingface.co/datasets/Salesforce/wikitext/blob/b08601e04326c79dfdd32d625aee71d232d685c3/README.md).
- Original description: [WikiText paper](https://arxiv.org/abs/1609.07843).
- License: the dataset card presents conflicting licensing statements (CC BY-SA 3.0 and GFDL metadata, with CC BY-SA 4.0 stated in its prose). The data and derived model are not redistributed by this repository while rights remain unresolved.
- Expected SHA-256 from the pinned file metadata: train `e83889baabc497075506f91975be5fac0d45c5290b6b20582c8cd1e853d0c9f7`; validation `204929b7ff9d6184953f867dedb860e40aa69c078fc1e54b3baaa8fb28511c4c`; test `5f1bea067869d04849c0f975a2b29c4ff47d867f484f5010ea5e861eab246d91`. These values were not recomputed because the downloads were unavailable.

## Preprocessing and split discipline

Download the three Parquet files at the pinned revision and verify each SHA-256 before conversion, following `data/README.md`. Export each row's `text` as UTF-8 with one row per line to separate train, validation, and test files. Training counts are built from train only. Order/backoff are selected on validation top-3, then top-1, with deterministic ties preferring the smaller order then smaller backoff. Test is read only after selection for final reporting.

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
| Train/validation/test checksums verified locally | No; source files could not be retrieved |
| Eligible test examples and skipped counts | Unavailable |
| N-gram top-1/top-3/top-5 | Unavailable |
| Unigram baseline top-1/top-3/top-5 | Unavailable |
| Vocabulary coverage and OOV rate | Unavailable |
| Model training/loading time and size | Unavailable on WikiText |
| Prediction median/p95 | Unavailable on WikiText |
| Interactive WinForms verification | Not performed in this environment |

No synthetic smoke result is presented as WikiText evidence.

## Reproduction

From a Windows checkout with network access, follow the pinned download/hash/conversion commands in `data/README.md`, then run:

```powershell
dotnet restore .\PCWord.sln
dotnet run --project .\tools\PCWord.Evaluation\PCWord.Evaluation.csproj --configuration Release -- .\splits\train.txt .\splits\validation.txt .\splits\test.txt .\artifacts\stage-5\local-model.json .\artifacts\stage-5\evaluation-results.json
```

The corpus and generated model are local files and must not be committed. The command prints aggregate numbers only and writes a machine-readable result. Then run the WinForms manual checklist in `docs/testing-guide.md` with disposable text.

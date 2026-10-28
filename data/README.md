# Dictionary data provenance and licensing

## Current checked-in fixture

`src/PCWord.Prediction/Dictionary/sample.tsv` is a **synthetic demonstration fixture authored for this repository**. It contains nine manually selected example words. The integer values are illustrative ranking weights, not measured corpus counts or claims about real-world usage.

- Format: UTF-8 TSV, one `word<TAB>positive-integer-frequency` record per line.
- Source: created for the PCWord sample; no external corpus is embedded in this fixture.
- Licensing: the sample text and illustrative values are original project test/demo material and may be distributed under the repository's project license. The repository currently has no top-level LICENSE file, so a repository-wide license must be selected separately before asserting a project license.
- Suitability: development and deterministic tests only. Do not use these nine rows to evaluate real-world suggestion quality.

## Selected source for a future English frequency dictionary

Use the **Google Books Ngram Corpus, English 1-gram dataset, version 20200217** as the initial reproducible source for an English baseline dictionary.

- Dataset index: https://storage.googleapis.com/books/ngrams/books/datasetsv3.html
- English 1-gram exports: https://storage.googleapis.com/books/ngrams/books/20200217/eng/eng-1-ngrams_exports.html
- License stated by the dataset publisher: **Creative Commons Attribution 3.0 Unported (CC BY 3.0)**.
- Source fields: n-gram, year, match count, and volume count. A word's frequency for PCWord must be produced by a documented aggregation across years; it must not be described as a raw single-year count.
- Attribution: credit Google Books Ngram Corpus, link to the dataset, identify the version, retain the CC BY 3.0 notice, and include the license link https://creativecommons.org/licenses/by/3.0/.
- The source documents that the corpus omits n-grams appearing 40 times or fewer across the corpus. This affects vocabulary coverage.
- This is book-derived historical frequency data, not a perfect measure of contemporary typing. It may underrepresent newer slang, technical terms, names, and everyday conversational language.

## Import requirements before bundling real data

1. Download only the selected English 1-gram files needed for the import.
2. Preserve a source manifest containing the source URL, dataset version, retrieval date, license, attribution, and transformation script/version.
3. Aggregate `match_count` across the chosen year range with a documented rule; use a wide integer type and reject invalid numeric values.
4. Filter non-word tokens and decide how to handle case variants, diacritics, apostrophes, hyphens, and Unicode normalization. Keep those rules in code and tests.
5. Convert the result to PCWord's TSV format only after verifying the transformation and its attribution requirements.
6. Keep the original source's license and attribution with any distributed derived dictionary. Do not imply that the current synthetic sample came from Google Books.

The real corpus has **not** been downloaded or bundled by this documentation change. Production dictionary integration and quality evaluation remain separate implementation work.

## Stage 5 next-word evaluation source

The Stage 5 evaluator is designed for Salesforce **WikiText `wikitext-2-raw-v1`**, using the immutable Hugging Face repository revision `b08601e04326c79dfdd32d625aee71d232d685c3` ([pinned dataset card](https://huggingface.co/datasets/Salesforce/wikitext/blob/b08601e04326c79dfdd32d625aee71d232d685c3/README.md)). Source files are Parquet; do not check the corpus or generated model into this repository. The dataset card metadata reports both **CC BY-SA 3.0** and **GFDL**, while the card text also refers to CC BY-SA 4.0. Because those statements conflict, PCWord treats the source and derived model conservatively as non-redistributable until the rights holder's intended terms are confirmed. Cite Salesforce/Hugging Face and the [WikiText paper](https://arxiv.org/abs/1609.07843) in any external report.

Pinned files and SHA-256 values shown by the pinned Hugging Face file pages:

| Split | Path | SHA-256 |
|---|---|---|
| train | `wikitext-2-raw-v1/train-00000-of-00001.parquet` | `e83889baabc497075506f91975be5fac0d45c5290b6b20582c8cd1e853d0c9f7` |
| validation | `wikitext-2-raw-v1/validation-00000-of-00001.parquet` | `204929b7ff9d6184953f867dedb860e40aa69c078fc1e54b3baaa8fb28511c4c` |
| test | `wikitext-2-raw-v1/test-00000-of-00001.parquet` | `5f1bea067869d04849c0f975a2b29c4ff47d867f484f5010ea5e861eab246d91` |

Verify downloaded Parquet files with `Get-FileHash -Algorithm SHA256` before conversion. The evaluator consumes UTF-8 `.txt` split files. With Python and `pyarrow` installed, convert without printing row contents:

Download the immutable revision into the folder structure used below:

```powershell
$revision = 'b08601e04326c79dfdd32d625aee71d232d685c3'
$splitRoot = '.\wikitext-2-raw-v1'
New-Item -ItemType Directory -Force -Path $splitRoot | Out-Null
foreach ($split in 'train', 'validation', 'test') {
  $name = "$split-00000-of-00001.parquet"
  curl.exe -L --fail --output (Join-Path $splitRoot $name) "https://huggingface.co/datasets/Salesforce/wikitext/resolve/$revision/wikitext-2-raw-v1/$name"
}
Get-ChildItem $splitRoot -Filter '*.parquet' | Get-FileHash -Algorithm SHA256
```

```powershell
python -c "import pyarrow.parquet as p; from pathlib import Path; src=Path('wikitext-2-raw-v1'); dst=Path('splits'); dst.mkdir(exist_ok=True); [(dst/(s+'.txt')).write_text(''.join((v or '')+'\n' for v in p.read_table(src/(s+'-00000-of-00001.parquet'))['text'].to_pylist()), encoding='utf-8') for s in ('train','validation','test')]"
```

Use one file for each split and never reuse validation or test content for training. Each row/line and each `.`, `!`, or `?` starts a fresh token history. Normalization is Unicode letters, digits, and combining marks, invariant lowercase; punctuation separates tokens, while `. ! ?` terminate sentence history. This is Wikipedia prose and does not establish quality for chat, code, names, or specialized writing. Corpus download was attempted during Stage 5 implementation but the network connection reset; no source file hashes were independently recomputed in this environment.

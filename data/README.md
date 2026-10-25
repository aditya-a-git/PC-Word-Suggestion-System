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

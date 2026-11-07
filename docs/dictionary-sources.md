# PCWord dictionary sources and generation

## Source and selection

The bundled completion vocabulary is generated from the English Speller Database / SCOWL v2 source repository [`en-wl/wordlist`](https://github.com/en-wl/wordlist), tag `rel-2026.02.25`, commit `7e99edab8e32f9f9ea2b15f249ca8d4d67237410`. The builder selects size 60 and all three requested spelling categories: `A` (American), `B` (British `-ise`), and `Z` (British `-ize`). Shared spelling code `_` is included for each category. Variant levels 0–6 are retained; archaic, uncommon, and invalid levels are excluded by this default. The three dialect totals overlap because identical words and shared spellings occur in more than one category.

The generated asset retains the exact lowercase form as an independently searchable word, dialect membership, SCOWL variant group IDs, minimum source size, POS codes, variant level, and lemma words where available. Source size is a vetted commonness tier, not a corpus frequency. No external frequency dataset is used. The existing controlled-editor tokenizer accepts Unicode letters/digits and underscore; the builder excludes proper-noun POS, upper-only, nonword/special/word-part categories, offensive/vulgar level 1 and 2 usage notes, and tokens that tokenizer cannot treat as one word (for example spaces, hyphens, apostrophes, and punctuation). These rules and maximum token length are in `tools/PCWord.DictionaryBuilder/config.json`; the generation report records included and filtered unique word counts.

## Reproduce

Requirements for build-time regeneration: Python 3.7+ with built-in `sqlite3`, and Git. Python is not used by PCWord at runtime.

```powershell
git clone --depth 1 --branch rel-2026.02.25 https://github.com/en-wl/wordlist.git .\_esdb-source
Push-Location .\_esdb-source
$env:PYTHONUTF8 = '1'
python .\combine.py create-db .\scowl.db
Pop-Location
python .\tools\PCWord.DictionaryBuilder\build_dictionary.py --database .\_esdb-source\scowl.db
```

The generated runtime files are `artifacts/dictionary/esdb-en-bilingual.tsv` and the identical linked copy under `src/PCWord.Prediction/Dictionary/`. The builder also writes a JSON report beside the artifact. The checked-in `sample.tsv` remains a small fixture only.

## Runtime format and integration

The app loads a local UTF-8 tab-separated file once at startup. The version header is `#pcword-dictionary-v2`; each subsequent row has seven fields: word, comma-separated dialect codes, comma-separated ESDB group IDs, minimum commonness size, comma-separated POS codes, variant level, and comma-separated lemma words. `DictionaryLoader` validates records and retains Unicode NFC forms. `PrefixIndex` uses a case-insensitive sorted array and binary-search range start, then walks only the matching prefix range. `CandidateRanker` sorts by lower (more common) SCOWL size, shorter completion, lower variant level, then stable case-insensitive/ordinal spelling. This is deterministic lexical metadata ranking, not a claim of exact frequency or corpus quality.

The controlled editor loads the packaged dictionary from its output directory. All lookup and ranking run locally; no dictionary downloads or typed-text transmission occur. The dictionary remains separate from the optional n-gram next-word model.

## Licensing

The selected size-60 American and British spellings are generated from SCOWL v2. The SCOWL copyright and permission notice is preserved in [`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md). The optional Australian and UKACD data are not selected. Do not raise the size above 80 or add Australian data without reviewing the additional notices in the pinned source's `Copyright` file. The upstream README notes that WordNet was used in initial POS assignment and that WordNet terms might apply when distributing database-derived data; this release includes source POS codes, so the applicable notice is also reproduced in `THIRD-PARTY-NOTICES.md`.

## Verified baseline statistics

The prior bundled asset contained 79,343 unique American-only words with the same neutral weight `1` on every entry. The bilingual size-60 asset generated from the pinned source contains 84,057 unique suggestions. American membership is 81,510, British `-ise` is 81,369, and British `-ize` is 81,383; counts overlap. The asset retains 48,608 unique ESDB group IDs. Of 131,694 unique size-60 source forms considered before application filters, 47,637 were excluded. These counts are in `artifacts/dictionary/esdb-en-bilingual.report.json`; do not compare them as coverage or prediction-accuracy measurements.

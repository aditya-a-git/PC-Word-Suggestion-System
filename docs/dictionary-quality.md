# Dictionary quality: ESDB US vocabulary

## Status

The expanded vocabulary is integrated for local prefix completion. It contains **79,343 unique entries**, compared with the original nine-entry synthetic fixture. This is a vocabulary expansion result, not a claim of improved next-token accuracy or verified corpus coverage.

The selected ESDB export does **not** carry per-word frequency counts or size tiers. The generated TSV therefore uses neutral weight `1` for every entry. Equal weights make the existing deterministic alphabetical tie-breaker decide the order within a prefix. Frequency-based candidate quality is not established and will need a separate frequency-bearing source.

## Source and redistribution

- Source: [English Speller Database (ESDB), en_US plain wordlist](https://github.com/en-wl/wordlist-diff/blob/rel-2026.02.25/en_US.txt), generated from [en-wl/wordlist release 2026.02.25](https://github.com/en-wl/wordlist/releases/tag/rel-2026.02.25).
- Pinned source revision: `en-wl/wordlist-diff` tag `rel-2026.02.25`, commit `71d7dd07676edb60ade43552e10b41314b7e9287`; the release points to source commit `7e99eda`.
- Upstream source SHA-256 (UTF-8 bytes): `4ff7e0b6d86763e1e042ffd746e94cdf4432618702deac303a1669e2a838db04`.
- Source size/count: 1,033,485 UTF-8 bytes; 109,902 nonblank entries.
- License: Kevin Atkinson’s ESDB/SCOWL permissive notice grants use, copying, modification, distribution and sale of word lists created from it, conditional on retaining the copyright and permission notice in supporting documentation. The US official speller wordlist does not carry the Australian or UKACD addenda. The source notice notes that COCA-derived data was used; the selected output is the official US speller wordlist covered by the notice before its separator.
- Attribution and notice: retain “Copyright 2000-2026 by Kevin Atkinson” and the upstream permission notice in this document/distribution. ESDB credits Alan Beale for 12dicts and ENABLE2K. No claim of endorsement is made.

The upstream release page identifies the default US dictionary as ESDB size 60, a vetted baseline for spell-checking. This is lexical/speller data and not a conversational frequency corpus.

## Processing

Raw source is not checked in. The pinned upstream file was read as UTF-8, normalized to Unicode NFC, filtered, ordinally deduplicated, and sorted ordinally before TSV serialization. Only lowercase Unicode letter sequences with optional combining marks are retained. The pipeline drops blank lines, uppercase entries (including proper nouns), apostrophe/possessive forms, phrases, punctuation-bearing tokens, and malformed entries. The generated TSV format is UTF-8, one `word<TAB>weight` record per line; all weights are 1.

Reproduce after obtaining the pinned source file locally:

```powershell
dotnet run --project .\tools\PCWord.DictionaryImport -- --sha256 4ff7e0b6d86763e1e042ffd746e94cdf4432618702deac303a1669e2a838db04 .\source\en_US.txt
```

The importer verifies the exact input checksum before writing `artifacts/dictionary/esdb-en-US.tsv`. No network access is required by the importer. The output is bundled with the prediction library and copied to the controlled editor output. The original nine-entry `sample.tsv` remains as a test/benchmark baseline.

## Statistics and evaluation

| Measure | Before | Expanded |
|---|---:|---:|
| Vocabulary entries | 9 | 79,343 |
| Entries with true corpus frequency | 0 | 0 |
| Entries with ranking weight | 9 illustrative values | 79,343 neutral values |
| Corpus coverage / OOV | Not measured | Not measured |
| Prefix top-1/top-3 quality | Not measured | Not measured |

A fair corpus-based dictionary coverage comparison still needs a licensed, locally available held-out corpus with pinned splits and verified checksums. Do not use next-word model results as dictionary coverage. The current WikiText evaluation reports next-word prediction and explicitly does not use the sample dictionary for its quality score; its licensing metadata is also unresolved.

## Architecture

`DictionaryLoader` reads the local TSV synchronously at app startup, validates positive integer weights, and deduplicates entries case-insensitively by keeping the first record. `PrefixIndex` retains the existing sorted-array/binary-search prefix lookup. `CandidateRanker` retains deterministic ordering by weight descending, then case-insensitive and ordinal word order, and emits up to three candidates through `DictionaryCompletionCoordinator`. Loading remains synchronous and startup-time; no download occurs while typing.

## Benchmarks

The existing benchmark measures a tiny nine-entry fixture. It is not evidence about the new vocabulary. Use the benchmark tool with the generated dictionary and the sample fixture under the same warm-up, iteration count, runtime and machine. Record load/index time, process memory delta, prefix lookup median/p95, and coordinator path median/p95. Include short, long, high-fanout and miss prefixes before publishing a comparison.

No expanded-dictionary performance numbers, memory delta, GUI run, or corpus quality metrics are claimed in this change. They require running the current `.NET 10` repository on Windows with the generated artifact and a pinned corpus. See `docs/benchmarks.md` for the existing sample baseline and remaining run protocol.

## Limitations and next work

- Commonness tiers/frequency values are absent in the selected export; suggestions within a matching prefix are alphabetical. Select and license a frequency corpus before claiming frequency-aware ranking.
- Proper nouns and possessive forms are intentionally excluded by this completion-oriented processing policy. Acronyms, apostrophe words, hyphenated forms and phrases are not supported by this artifact.
- ESDB is an evolving spelling resource; update only by pinning a new source tag/checksum and repeating the comparison.
- WinForms behavior and loading-failure fallback must still be manually exercised on Windows.
- Measure actual expanded-dictionary startup and lookup performance and run coverage on a legally usable held-out corpus before marking the quality stage complete.

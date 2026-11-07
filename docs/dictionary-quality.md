# Dictionary quality and verification

The prior bundled American-only TSV contained 79,343 unique words and assigned every word the same placeholder value. The new pinned ESDB size-60 vocabulary contains 84,057 unique single-token suggestions across American, British `-ise`, and British `-ize` spelling categories. American: 81,510 entries; British `-ise`: 81,369; British `-ize`: 81,383. These dialect totals overlap. The generated report records 48,608 unique ESDB variant-group IDs and 47,637 unique source forms excluded by the configured proper-noun, category, usage, and runtime-token filters.

The new asset retains word forms independently, dialect membership, variant-group IDs, minimum SCOWL size, POS codes, variant level, and lemma forms. Lower SCOWL size represents the source's more common size tier; it is not an observed word frequency. Ranking is deterministic: lower size, shorter completion, lower variant level, case-insensitive spelling, then ordinal spelling. No external frequency data or contextual model changes are included.

## Generation and runtime

The source tag, filters, configuration, command, TSV schema, license notes, and data selection are documented in [dictionary-sources.md](dictionary-sources.md). The Python/SQLite tooling runs only during regeneration. At runtime, the .NET loader reads the packaged local TSV once, and the existing sorted-array/binary-search prefix index locates the matching prefix range. The separate next-word model and controlled-editor privacy behavior are unchanged.

The runtime asset is copied into the prediction project output and linked into the controlled-editor package. Tests load the packaged asset and check American/British spelling variants, minimum asset size, metadata parsing, malformed input, Unicode prefixes, duplicate entries, and ranking determinism.

## Performance and quality limits

The benchmark executable now accepts both `--iterations` and `--dictionary` options, tests short/common, longer, and miss prefixes, and reports load/index time plus initialization memory deltas. Measured values for the full asset are recorded in [benchmarks.md](benchmarks.md) after running the Release benchmark on this Windows/.NET 10 environment.

No held-out corpus completion accuracy, OOV rate, top-1/top-3 quality, live WinForms typing latency, or manual UI verification is claimed. This milestone establishes reproducible vocabulary and source-metadata ranking; corpus-frequency ranking remains future work requiring a separately licensed frequency source.

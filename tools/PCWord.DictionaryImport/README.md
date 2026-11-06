# ESDB dictionary importer

The importer reads a locally downloaded, pinned plain word list, verifies its SHA-256 before parsing, and writes deterministic UTF-8 TSV to `artifacts/dictionary/esdb-en-US.tsv`. It performs no network access.

Run from the repository root:

```powershell
dotnet run --project .\tools\PCWord.DictionaryImport -- --sha256 <verified-sha256> .\source\en_US.txt
```

Only lowercase alphabetic entries (Unicode letters and combining marks) are retained. Input is normalized to NFC, case variants are deduplicated ordinally, possessives, proper nouns, phrases, punctuation-bearing forms and malformed tokens are dropped. Each retained entry has constant weight `1`: ESDB's plain en_US wordlist has no per-entry frequencies or commonness tiers, so these weights carry no frequency claim. The selection policy corresponds to the source's default en_US list.

The importer refuses an incorrect checksum and does not overwrite the source file. Attribution/license information and the pinned upstream SHA-256 belong in `docs/dictionary-quality.md` after the exact source bytes have been verified locally.

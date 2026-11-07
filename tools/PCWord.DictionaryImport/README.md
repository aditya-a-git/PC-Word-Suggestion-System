# Legacy US word-list importer (superseded)

This old importer is retained only for historical compatibility. It creates an American-only word list with neutral weights and is **not used by the application**. Do not use it to regenerate the bundled dictionary.

Use the metadata-preserving bilingual builder instead:

```powershell
python .\tools\PCWord.DictionaryBuilder\build_dictionary.py --database .\_esdb-source\scowl.db
```

See `tools/PCWord.DictionaryBuilder/README.md` and `docs/dictionary-sources.md` for source pinning, filters, licensing, and regeneration.

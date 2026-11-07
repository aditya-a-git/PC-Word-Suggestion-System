#!/usr/bin/env python3
"""Build PCWord's compact runtime dictionary from a pinned ESDB SQLite database."""

import argparse
import collections
import json
import pathlib
import sqlite3
import sys
import unicodedata

ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT_CONFIG = pathlib.Path(__file__).with_name("config.json")
DIALECTS = (("A", "US"), ("B", "GB"), ("Z", "GB"))


def load_config(path):
    config = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(config.get("max_size"), int) or not 35 <= config["max_size"] <= 95:
        raise ValueError("max_size must be an ESDB size between 35 and 95.")
    if not isinstance(config.get("max_variant_level"), int) or not 0 <= config["max_variant_level"] <= 9:
        raise ValueError("max_variant_level must be between 0 and 9.")
    if not config.get("dialects") or any(code not in {item[0] for item in DIALECTS} for code in config["dialects"]):
        raise ValueError("dialects must be a non-empty subset of A, B, and Z.")
    return config


def is_runtime_word(word, config):
    if not word or word != word.lower() or len(word) > config["max_word_length"]:
        return False
    if not any(char.isalpha() for char in word):
        return False
    return all(char.isalnum() or char == "_" for char in word)


def build(database, output, config_path):
    config = load_config(config_path)
    db = sqlite3.connect(f"file:{database.resolve().as_posix()}?mode=ro", uri=True)
    db.row_factory = sqlite3.Row
    records = collections.defaultdict(lambda: {
        "dialects": set(), "groups": set(), "sizes": [], "variants": [],
        "poses": set(), "lemmas": set(),
    })
    counts = collections.Counter()
    source_words = set()
    selected_dialects = tuple(item for item in DIALECTS if item[0] in config["dialects"])
    sql = """
        SELECT v.word, v.spelling, v.size, v.variant_level, v.pos,
               v.pos_category, v.pos_class, v.usage_note, w.group_id, l.lemma, l.lemma_pos
          FROM scowl_v0 AS v
          JOIN words AS w ON w.word = v.word AND w.pos = v.pos
          JOIN lemmas AS l ON l.lemma_id = w.lemma_id
         WHERE v.size <= ? AND v.variant_level <= ?
           AND ((v.spelling IN ('_', ?) AND v.region IN ('', ?))
             OR (v.spelling = '_' AND v.region = ''))
         ORDER BY v.word COLLATE BINARY, v.spelling, v.size, v.variant_level, v.pos
    """

    for code, region in selected_dialects:
        rows = db.execute(sql, (config["max_size"], config["max_variant_level"], code, region))
        for row in rows:
            word = unicodedata.normalize("NFC", row["word"])
            counts["source_rows"] += 1
            source_words.add(word)
            if row["pos_category"] in config["exclude_pos_categories"]:
                counts["filtered_nonword_category_rows"] += 1
                continue
            if row["pos_class"] == "upper":
                counts["filtered_upper_only_rows"] += 1
                continue
            if row["usage_note"] in config["exclude_usage_notes"]:
                counts["filtered_sensitive_usage_rows"] += 1
                continue
            if not config["include_proper_nouns"] and any(
                row["pos"].startswith(prefix) for prefix in config["exclude_pos_prefixes"]
            ):
                counts["filtered_proper_noun_rows"] += 1
                continue
            if not is_runtime_word(word, config):
                counts["filtered_unsupported_token"] += 1
                continue
            record = records[word]
            record["dialects"].add(code)
            record["groups"].add(str(row["group_id"]))
            record["sizes"].append(int(row["size"]))
            record["variants"].append(int(row["variant_level"]))
            record["poses"].add(row["pos"])
            if row["lemma"] and is_runtime_word(unicodedata.normalize("NFC", row["lemma"]), config):
                record["lemmas"].add(unicodedata.normalize("NFC", row["lemma"]))

    if not records:
        raise ValueError("No dictionary entries matched; check the ESDB database and schema.")

    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8", newline="\n") as stream:
        stream.write("#pcword-dictionary-v2\n")
        for word in sorted(records, key=lambda value: (value.casefold(), value)):
            record = records[word]
            stream.write("\t".join((
                word,
                ",".join(code for code, _ in selected_dialects if code in record["dialects"]),
                ",".join(sorted(record["groups"], key=int)),
                str(min(record["sizes"])),
                ",".join(sorted(record["poses"])),
                str(min(record["variants"])),
                ",".join(sorted(record["lemmas"], key=lambda value: (value.casefold(), value))) or "-",
            )) + "\n")

    counts["unique_entries"] = len(records)
    counts["candidate_unique_words"] = len(source_words)
    counts["filtered_unique_words"] = len(source_words - set(records))
    counts["american_entries"] = sum("A" in value["dialects"] for value in records.values())
    counts["british_ise_entries"] = sum("B" in value["dialects"] for value in records.values())
    counts["british_ize_entries"] = sum("Z" in value["dialects"] for value in records.values())
    counts["variant_groups"] = len({group for value in records.values() for group in value["groups"]})
    counts["filtered_out_rows"] = sum(counts[key] for key in (
        "filtered_nonword_category_rows", "filtered_upper_only_rows",
        "filtered_sensitive_usage_rows", "filtered_proper_noun_rows",
        "filtered_unsupported_token",
    ))

    report_path = output.with_suffix(".report.json")
    report_path.write_text(json.dumps({
        "source": config["source"],
        "configuration": "tools/PCWord.DictionaryBuilder/config.json",
        "commonness_note": "SCOWL size is a vetted commonness tier, not corpus frequency.",
        **counts,
        "dialect_counts_overlap": True,
        "variant_group_count_is_unique_esdb_group_ids": True,
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), **counts}, ensure_ascii=False, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--database", type=pathlib.Path, required=True, help="scowl.db created from the pinned ESDB release")
    parser.add_argument("--output", type=pathlib.Path, default=ROOT / "artifacts/dictionary/esdb-en-bilingual.tsv")
    parser.add_argument("--config", type=pathlib.Path, default=DEFAULT_CONFIG)
    args = parser.parse_args()
    build(args.database, args.output, args.config)


if __name__ == "__main__":
    try:
        main()
    except (OSError, sqlite3.Error, ValueError, json.JSONDecodeError) as error:
        print(f"Dictionary build failed: {error}", file=sys.stderr)
        raise SystemExit(1)

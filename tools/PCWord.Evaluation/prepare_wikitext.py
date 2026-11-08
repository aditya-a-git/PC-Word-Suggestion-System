#!/usr/bin/env python3
"""Verify pinned WikiText-2 Parquet splits and export their text rows as UTF-8."""

import argparse
import hashlib
import json
import pathlib
import sys

REVISION = "b08601e04326c79dfdd32d625aee71d232d685c3"
SPLITS = {
    "train": ("train-00000-of-00001.parquet", "e83889baabc497075506f91975be5fac0d45c5290b6b20582c8cd1e853d0c9f7"),
    "validation": ("validation-00000-of-00001.parquet", "204929b7ff9d6184953f867dedb860e40aa69c078fc1e54b3baaa8fb28511c4c"),
    "test": ("test-00000-of-00001.parquet", "5f1bea067869d04849c0f975a2b29c4ff47d867f484f5010ea5e861eab246d91"),
}


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("parquet_dir", type=pathlib.Path, help="Directory containing the three pinned Parquet files")
    parser.add_argument("output_dir", type=pathlib.Path, help="Local directory for train.txt, validation.txt, and test.txt")
    args = parser.parse_args()

    try:
        import pyarrow.parquet as parquet
    except ImportError as error:
        raise SystemExit("Install pyarrow in the offline preparation environment, then rerun this script.") from error

    args.output_dir.mkdir(parents=True, exist_ok=True)
    manifest = {
        "dataset": "Salesforce WikiText-2 raw",
        "configuration": "wikitext-2-raw-v1",
        "revision": REVISION,
        "source": f"https://huggingface.co/datasets/Salesforce/wikitext/tree/{REVISION}/wikitext-2-raw-v1",
        "preprocessing": "Parquet text column, row order preserved, each row written as UTF-8 followed by LF; no text normalization here (PCWord tokenizer performs invariant Unicode tokenization).",
        "splits": {},
    }

    for split, (filename, expected_hash) in SPLITS.items():
        source = args.parquet_dir / filename
        if not source.is_file():
            raise FileNotFoundError(f"Missing {split} source file: {filename}")
        actual_hash = sha256(source)
        if actual_hash != expected_hash:
            raise ValueError(f"SHA-256 mismatch for {filename}; refusing conversion.")

        destination = args.output_dir / f"{split}.txt"
        row_count = 0
        with destination.open("w", encoding="utf-8", newline="\n") as output:
            for batch in parquet.ParquetFile(source).iter_batches(columns=["text"], batch_size=8192):
                for value in batch.column(0).to_pylist():
                    output.write((value or "").replace("\r", " ").replace("\n", " "))
                    output.write("\n")
                    row_count += 1
        manifest["splits"][split] = {
            "file": filename,
            "sha256": actual_hash,
            "rows": row_count,
            "text_file": destination.name,
        }
        print(f"Verified and exported {split}: {row_count} rows; text contents omitted.")

    (args.output_dir / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError) as error:
        print(f"WikiText preparation failed: {error}", file=sys.stderr)
        raise SystemExit(1)

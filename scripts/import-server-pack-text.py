#!/usr/bin/env python3
"""Import a server pack's db/*.txt files as a UTF-8-only snapshot."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import sys
import tempfile
from collections import Counter, namedtuple
from pathlib import Path


PACK_ID_PATTERN = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
ImportResult = namedtuple("ImportResult", "copied encodings written target")


def decode_text(raw: bytes, path: Path) -> tuple[str, str]:
    """Decode without lossy replacement, preferring UTF-8 (with optional BOM)."""
    try:
        return raw.decode("utf-8-sig", errors="strict"), "utf-8-sig"
    except UnicodeDecodeError:
        try:
            return raw.decode("cp949", errors="strict"), "cp949"
        except UnicodeDecodeError as error:
            raise UnicodeError(f"cannot decode as UTF-8 or CP949: {path}") from error


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def display_source(source: Path, repo_root: Path) -> str:
    resolved_source = source.resolve()
    try:
        return resolved_source.relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return str(resolved_source)


def validate_target_db(target_db: Path, pack_root: Path) -> None:
    """Require an exact <pack_root>/db target; reject links escaping it."""
    resolved_pack = pack_root.resolve(strict=False)
    resolved_db = target_db.resolve(strict=False)
    if target_db.name != "db" or resolved_db.parent != resolved_pack:
        raise ValueError(f"refusing to clear db outside exact pack parent: {target_db}")


def clear_target_db(target_db: Path, pack_root: Path) -> None:
    """Remove only an exact <pack_root>/db target."""
    validate_target_db(target_db, pack_root)
    if target_db.exists() or target_db.is_symlink():
        if target_db.is_dir() and not target_db.is_symlink():
            shutil.rmtree(target_db)
        else:
            target_db.unlink()


def import_pack(
    source: Path | str,
    pack_id: str,
    repo_root: Path | str,
    *,
    write: bool = False,
) -> ImportResult:
    if not PACK_ID_PATTERN.fullmatch(pack_id):
        raise ValueError("pack id must be a lowercase slug containing only a-z, 0-9, and single hyphens")

    source = Path(source).resolve()
    repo_root = Path(repo_root).resolve()
    source_db = source / "db"
    if not source_db.is_dir():
        raise FileNotFoundError(f"source db directory does not exist: {source_db}")

    output_root = (repo_root / "data" / "server-packs").resolve(strict=False)
    pack_root = (output_root / pack_id).resolve(strict=False)
    if pack_root.parent != output_root:
        raise ValueError(f"output escaped data/server-packs: {pack_root}")

    source_files = sorted(
        (path for path in source_db.rglob("*") if path.is_file() and path.suffix.casefold() == ".txt"),
        key=lambda path: path.relative_to(source_db).as_posix(),
    )
    if not source_files:
        raise ValueError(f"source db contains no .txt files: {source_db}")

    resolved_source_db = source_db.resolve()
    decoded: list[tuple[Path, str]] = []
    encodings: Counter[str] = Counter()
    for source_file in source_files:
        try:
            source_file.resolve().relative_to(resolved_source_db)
        except ValueError as error:
            raise ValueError(f"source text file escapes db through a link: {source_file}") from error
        text, encoding = decode_text(source_file.read_bytes(), source_file)
        decoded.append((source_file.relative_to(source_db), text))
        encodings[encoding] += 1

    executables = sorted(
        (path for path in source.iterdir() if path.is_file() and path.suffix.casefold() == ".exe"),
        key=lambda path: path.name.casefold(),
    )
    executable_metadata = None
    if len(executables) == 1:
        executable_metadata = {
            "name": executables[0].name,
            "sha256": sha256_file(executables[0]),
        }

    metadata = {
        "source": display_source(source, repo_root),
        "encoding": {
            "input": {
                "cp949": encodings["cp949"],
                "utf-8-sig": encodings["utf-8-sig"],
            },
            "output": "utf-8",
        },
        "copied_text_files": len(decoded),
        "top_level_exe": executable_metadata,
    }

    if write:
        pack_root.mkdir(parents=True, exist_ok=True)
        target_db = pack_root / "db"
        metadata_path = pack_root / "source.json"
        if metadata_path.resolve(strict=False).parent != pack_root:
            raise ValueError(f"source metadata escaped exact pack parent: {metadata_path}")
        if metadata_path.is_symlink() or (metadata_path.exists() and not metadata_path.is_file()):
            raise ValueError(f"source metadata target is not a regular file: {metadata_path}")

        with tempfile.TemporaryDirectory(prefix=".text-import-", dir=pack_root) as staging_name:
            staging_root = Path(staging_name)
            staging_db = staging_root / "db"
            staging_db.mkdir()
            for relative_path, text in decoded:
                destination = staging_db / relative_path
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(text.encode("utf-8"))
            staging_metadata = staging_root / "source.json"
            staging_metadata.write_text(
                json.dumps(metadata, ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8",
                newline="\n",
            )

            validate_target_db(target_db, pack_root)
            previous_db = staging_root / "previous-db"
            had_previous_db = target_db.exists() or target_db.is_symlink()
            if had_previous_db:
                os.replace(target_db, previous_db)
            try:
                os.replace(staging_db, target_db)
                os.replace(staging_metadata, metadata_path)
            except OSError:
                if target_db.exists() or target_db.is_symlink():
                    clear_target_db(target_db, pack_root)
                if had_previous_db and previous_db.exists():
                    os.replace(previous_db, target_db)
                raise

    return ImportResult(len(decoded), dict(encodings), write, pack_root)


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="raw server-pack directory containing db/")
    parser.add_argument("pack_id", help="lowercase output slug under data/server-packs/")
    parser.add_argument("--write", action="store_true", help="write the snapshot (default: dry-run)")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    repo_root = Path(__file__).resolve().parent.parent
    try:
        result = import_pack(args.source, args.pack_id, repo_root, write=args.write)
    except (FileNotFoundError, OSError, UnicodeError, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 2

    mode = "wrote" if result.written else "dry-run"
    print(
        f"{mode}: {result.copied} .txt files "
        f"(utf-8-sig={result.encodings.get('utf-8-sig', 0)}, cp949={result.encodings.get('cp949', 0)}) "
        f"-> {result.target}"
    )
    if not result.written:
        print("no files written; pass --write to replace the target db snapshot")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

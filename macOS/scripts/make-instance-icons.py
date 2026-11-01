#!/usr/bin/env python3
"""Preserve the pinned upstream PageInstanceLeft vector shapes as template PDFs."""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json


def main():
    base = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=base / "Resources/InstanceNavigation")
    parser.add_argument("--check", action="store_true", help="Verify committed assets without writing files")
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location("pcl_vectors", base / "scripts/make-version-action-icons.py")
    vectors = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(vectors)
    if not args.check:
        args.output.mkdir(parents=True, exist_ok=True)
    report = {}
    for name, asset in vectors.load_icon_sources()["instanceNavigation"].items():
        geometry = asset["geometry"]
        nonzero = geometry.startswith("F1 ")
        geometry = geometry.removeprefix("F1 ")
        commands = vectors.parse_path(geometry)
        data = vectors.make_pdf(commands, vectors.geometry_bounds(commands), even_odd=not nonzero)
        vectors.write_or_check(args.output / (name + ".pdf"), data, args.check)
        report[name] = {"source": asset["source"], "geometrySHA256": hashlib.sha256(geometry.encode()).hexdigest(), "pdfSHA256": hashlib.sha256(data).hexdigest()}
    manifest = (json.dumps(report, ensure_ascii=False, indent=2) + "\n").encode()
    vectors.write_or_check(args.output / "source-map.json", manifest, args.check)
    print(f"{'Verified' if args.check else 'Generated'} {len(report)} original instance navigation icons.")


if __name__ == "__main__":
    main()

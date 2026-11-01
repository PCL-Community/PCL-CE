#!/usr/bin/env python3
"""Generate template PDFs from the exact original PCL title-navigation paths.

FormMain.xaml's MyRadioButton Logo geometries are filled paths, not SF Symbols
or approximate stroked replacements. The shared converter preserves WPF's
uniform aspect ratio and default even-odd fill rule without rasterization.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path

NAMES = {"Launch": "BtnTitleSelect0", "Download": "BtnTitleSelect1", "Settings": "BtnTitleSelect3", "More": "BtnTitleSelect4"}


def main() -> None:
    base = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=base / "Resources/TopNavigation")
    parser.add_argument("--check", action="store_true", help="Verify committed assets without writing files")
    args = parser.parse_args()
    helper_path = Path(__file__).with_name("make-version-action-icons.py")
    spec = importlib.util.spec_from_file_location("pcl_geometry_converter", helper_path)
    assert spec is not None and spec.loader is not None
    converter = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(converter)
    inputs = converter.load_icon_sources()["topNavigation"]
    if not args.check:
        args.output.mkdir(parents=True, exist_ok=True)
    report = {}
    for name, asset in inputs.items():
        geometry = asset["geometry"]
        commands = converter.parse_path(geometry)
        bounds = converter.geometry_bounds(commands)
        pdf = converter.make_pdf(commands, bounds)
        converter.write_or_check(args.output / (name + ".pdf"), pdf, args.check)
        report[name] = {
            "source": asset["source"],
            "sourceElement": asset["sourceElement"],
            "sourceLine": asset["sourceLine"],
            "sourceLogoScale": asset["sourceLogoScale"],
            "sourceGeometrySHA256": hashlib.sha256(geometry.encode()).hexdigest(),
            "pdfSHA256": hashlib.sha256(pdf).hexdigest(),
            "geometryBounds": list(bounds),
            "pdfCommands": len(commands),
            "sourceBaseSize": 16,
            "macBaseSize": 20,
            "macLayoutScale": 1.25,
            "fillRule": "evenOdd",
        }
    manifest = (json.dumps(report, ensure_ascii=False, indent=2) + "\n").encode()
    converter.write_or_check(args.output / "source-map.json", manifest, args.check)
    print(f"{'Verified' if args.check else 'Generated'} {len(report)} original PCL title-navigation vectors in {args.output}")


if __name__ == "__main__":
    main()

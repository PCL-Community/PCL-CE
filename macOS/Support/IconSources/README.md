# Pinned original PCL artwork

These inputs preserve the artwork already used by the macOS port. They are copied
from [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL) commit
`93ede5d9e638c99fcec853a9ba1eecd91ceee7f5`; they are not replacements generated from
the current PCL-CE Windows source.

`geometry.json` records the exact XAML/VB paths, geometry strings, source selectors,
source lines, rendering flags, and SHA-256 hashes of the source files. Its bitmap
entries identify the original ICO and PNG files alongside it. Source-derived
ellipses and rotations retain their explicit rendering metadata.

The original PCL licence is preserved in [`macOS/LICENCE`](../../LICENCE). These
files and the generated PCL artwork remain subject to that licence and upstream
attribution; copying them here does not relicense them.

The four `scripts/make-*-icons.py` generators use only these local inputs and the
Python standard library. Run each with `--check` to verify its committed PDFs,
PNGs, and source map without modifying them. `scripts/make-icon.swift` defaults
to `AppIcon.ico` here. Ordinary app builds use the committed generated resources
and do not require icon regeneration, the Windows source tree, or network access
for artwork.

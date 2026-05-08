# SVG conversion pipeline

Converts the AgOpen graphic artist's layered Illustrator SVGs into
single-icon-per-file simplified SVGs ready to paste into
`Shared/AgValoniaGPS.Views/Icons/Glyphs.axaml`.

The artist's original v1 export pattern bundled all icons in one SVG
document and toggled visibility via CSS class rules (`display: none`).
`picosvg` alone can't honor those rules, so it bakes every layer into
its output, producing identical output for every file. This script
pre-strips the hidden layers so picosvg flattens only what's actually
visible.

## Usage

```bash
pip install picosvg
PICOSVG=$(python3 -c "import sys; print(sys.executable.rsplit('/', 1)[0] + '/picosvg')" 2>/dev/null || which picosvg)

for f in Settings Vehicle Field Tools FieldTools SteerConfig; do
  python3 Plans/GlyphWordButtons/scripts/strip_hidden.py \
    "/path/to/SVG/${f}.svg" "/tmp/${f}_stripped.svg"
  "$PICOSVG" --drop_unsupported "/tmp/${f}_stripped.svg" \
    > "/tmp/${f}_clean.svg"
done
```

Each `_clean.svg` ends up as a 24×24 SVG with one or more flat `<path>`
elements. Concatenate the `d` attribute(s) into a single
`<StreamGeometry>` resource and drop into `Glyphs.axaml`.

## Note

The artist's v2 export (May 2026) ships single-icon-per-file SVGs
already, so the strip step is unnecessary — paths can be extracted
directly. This script is retained for the v1-style multi-icon files
that may resurface from older asset packs.

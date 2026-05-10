# AgValoniaGPS icon catalog

Snapshot of every PNG/GIF icon currently referenced by an AXAML file in
the AgValoniaGPS UI, grouped by source panel/dialog/wizard. Built
for the AgOpen graphic artist to work through folder-by-folder.

## Layout

- `Panels/` — drawer / floating / nav panels (`Shared/AgValoniaGPS.Views/Controls/Panels/`)
- `Dialogs/` — modal dialogs (`Shared/AgValoniaGPS.Views/Controls/Dialogs/`)
- `Dialogs/Configuration/` — sub-tabs of the unified Configuration dialog
- `Wizards/` — wizard step views (`Shared/AgValoniaGPS.Views/Controls/Wizards/`)
- `_orphans/` — PNGs/GIFs that exist on disk but aren't referenced by any AXAML

Some Configuration sub-tabs use animated `.gif` demonstrations rather
than static icons (timing animations). Those land in the catalog too,
but they're a different problem — they don't translate to a single
glyph. Skip them when designing replacements unless we discuss further.

## Per-folder contents

Each folder contains:
1. The PNGs that the corresponding AXAML file references.
2. A `README.md` listing each button: tooltip / command / icon set
   (toggles list both their on and off variants).

## Glyph style guidance for replacements

- Authoring canvas: 24×24 viewBox.
- Final output: a single flat `<path>` (or a few flat paths) per
  glyph, suitable for pasting into
  `Shared/AgValoniaGPS.Views/Icons/Glyphs.axaml` as a
  `<StreamGeometry>` resource.
- Filled silhouettes preferred for visual weight, matching the v2
  set the artist already provided for the LeftNav.
- Two-state toggles (on / off) become a single glyph; the engaged
  state is conveyed by the button's solid green background, not by
  swapping glyphs.

## Workflow

1. Pick a folder.
2. Author replacement SVG(s).
3. Drop the SVG into the same folder alongside the original PNG.
4. Hand the folder back; we wire it into Glyphs.axaml + the panel.

## Counts

- Total unique PNG/GIF files in `Assets/Icons/`: 298
- Files referenced by AXAML: 199
- Orphans on disk: 99
- AXAML files contributing icons: 42

## Regenerate

```bash
python3 Plans/GlyphWordButtons/scripts/build_icon_catalog.py
```

Re-running wipes and rebuilds `Plans/GlyphWordButtons/icon-catalog/`.

#!/usr/bin/env python3
"""
Build an icon catalog for the AgOpen graphic artist.

Walks every .axaml under Shared/AgValoniaGPS.Views/ and groups icon
references by source file, producing:

  Plans/GlyphWordButtons/icon-catalog/
    Panels/<PanelName>/
      <icon1>.png
      <icon2>.png
      README.md   (button purpose, command, state variants)
    Dialogs/<DialogName>/...
    Wizards/<WizardName>/...
    _orphans/     (icons in Assets/Icons/ never referenced by AXAML)
    README.md     (top-level guidance)

The artist works folder-by-folder. Each finished folder feeds back into
Glyphs.axaml + the relevant panel's button definitions.
"""
from __future__ import annotations

import re
import shutil
from collections import defaultdict
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
VIEWS_ROOT = REPO_ROOT / "Shared" / "AgValoniaGPS.Views"
ICON_DIR = VIEWS_ROOT / "Assets" / "Icons"
CATALOG = REPO_ROOT / "Plans" / "GlyphWordButtons" / "icon-catalog"

ICON_PATTERN = re.compile(r'Assets/Icons/([\w\-/]+\.(?:png|gif))')

# Match a Button (or GlyphButton, etc.) and the surrounding XML so we can
# pull its tooltip + command. We capture each opening Button tag plus its
# inner content up to the matching close.
BUTTON_BLOCK = re.compile(
    r'<(Button|c:GlyphButton|controls:GlyphButton)\b'    # opening
    r'(?P<attrs>[^>]*?)'                                  # attributes on the open tag
    r'(?:/>|>(?P<inner>.*?)</(?:Button|c:GlyphButton|controls:GlyphButton)>)',
    re.DOTALL,
)

TOOLTIP_RE = re.compile(r'ToolTip\.Tip="([^"]+)"')
COMMAND_RE = re.compile(r'\bCommand="\{Binding\s+([^}\s]+)')
SOURCE_RE  = re.compile(r'Source="avares://[^"]+/Assets/Icons/([\w\-/]+\.(?:png|gif))"')


def category_for(axaml_path: Path) -> tuple[str, str]:
    """Return (top_category, panel_name) for a given AXAML path."""
    rel = axaml_path.relative_to(VIEWS_ROOT)
    parts = rel.parts
    name = rel.stem
    if "Wizards" in parts:
        return "Wizards", name
    if "Dialogs" in parts:
        # Configuration sub-tabs get nested under Dialogs/Configuration/<Tab>
        if "Configuration" in parts and parts[-1] != "ConfigurationDialog.axaml":
            return "Dialogs/Configuration", name
        return "Dialogs", name
    if "Panels" in parts:
        return "Panels", name
    return "Other", name


def extract_button_metadata(axaml_text: str) -> list[dict]:
    """Pull (icon, tooltip, command) tuples from each Button block. A single
    button can reference more than one icon (toggle on/off variants)."""
    rows: list[dict] = []
    for match in BUTTON_BLOCK.finditer(axaml_text):
        block = match.group(0)
        icons = sorted(set(SOURCE_RE.findall(block)))
        if not icons:
            continue
        tooltip = TOOLTIP_RE.search(match.group("attrs") or "")
        command = COMMAND_RE.search(match.group("attrs") or "")
        rows.append({
            "icons": icons,
            "tooltip": tooltip.group(1) if tooltip else "",
            "command": command.group(1) if command else "",
        })
    return rows


def main() -> None:
    if CATALOG.exists():
        shutil.rmtree(CATALOG)
    CATALOG.mkdir(parents=True)

    referenced: set[str] = set()
    by_panel: dict[tuple[str, str], dict] = defaultdict(lambda: {
        "axaml": None,
        "rows": [],
        "icons": set(),
    })

    for axaml in sorted(VIEWS_ROOT.rglob("*.axaml")):
        text = axaml.read_text()
        icons = set(ICON_PATTERN.findall(text))
        if not icons:
            continue
        cat, panel = category_for(axaml)
        rows = extract_button_metadata(text)
        bucket = by_panel[(cat, panel)]
        bucket["axaml"] = axaml.relative_to(REPO_ROOT)
        bucket["rows"] = rows
        bucket["icons"] = icons
        referenced.update(icons)

    # Per-panel folders + READMEs + PNG copies
    for (cat, panel), data in sorted(by_panel.items()):
        folder = CATALOG / cat / panel
        folder.mkdir(parents=True, exist_ok=True)

        # Copy each icon (skip if missing on disk — a stale ref). The source
        # may live in a subdirectory of Assets/Icons (e.g. Config/Dark/...);
        # we flatten to basename in the catalog so the artist sees one icon
        # per file rather than nested trees.
        missing: list[str] = []
        for icon_relpath in sorted(data["icons"]):
            src = ICON_DIR / icon_relpath
            dest_name = Path(icon_relpath).name
            if src.exists():
                shutil.copy2(src, folder / dest_name)
            else:
                missing.append(icon_relpath)

        # Write README
        lines = [
            f"# {panel}",
            "",
            f"**Source:** `{data['axaml']}`",
            "",
            f"**Icon count:** {len(data['icons'])}",
            "",
            "## Buttons",
            "",
        ]
        if not data["rows"]:
            lines.append("_No button blocks found by parser; see source for context._")
        for row in data["rows"]:
            label = row["tooltip"] or row["command"] or "(unlabeled)"
            icons_str = " · ".join(f"`{i}`" for i in row["icons"])
            cmd = f" → `{row['command']}`" if row["command"] else ""
            lines.append(f"- **{label}**{cmd} — {icons_str}")
        if missing:
            lines += [
                "",
                "## Missing PNGs (referenced but not on disk)",
                "",
                *(f"- `{m}`" for m in missing),
            ]
        lines.append("")
        (folder / "README.md").write_text("\n".join(lines))

    # Orphans: PNGs in Assets/Icons that no AXAML references. Walk the full
    # tree (Config/ + Config/Dark/ + ...), and compare relative paths so a
    # dark-mode variant under Config/Dark/X.png isn't reported as orphan
    # just because the light-mode X.png is referenced.
    on_disk: set[str] = set()
    for ext in ("*.png", "*.gif"):
        for p in ICON_DIR.rglob(ext):
            on_disk.add(str(p.relative_to(ICON_DIR)).replace("\\", "/"))
    orphans = sorted(on_disk - referenced)
    if orphans:
        orphan_dir = CATALOG / "_orphans"
        orphan_dir.mkdir(parents=True, exist_ok=True)
        for icon_relpath in orphans:
            src = ICON_DIR / icon_relpath
            # Flatten subpath to basename, prefix with subdir if there'd be
            # a name collision at the flat level.
            dest_name = Path(icon_relpath).name
            shutil.copy2(src, orphan_dir / dest_name)
        (orphan_dir / "README.md").write_text(
            "# Orphans\n\n"
            "These PNGs live in `Shared/AgValoniaGPS.Views/Assets/Icons/` but\n"
            "no .axaml file references them. Likely candidates: stale assets\n"
            "from earlier UI iterations, or icons used dynamically from code\n"
            "(grep `.cs` files to confirm before deleting).\n\n"
            f"Total: {len(orphans)}\n\n"
            + "\n".join(f"- `{i}`" for i in orphans)
            + "\n"
        )

    # Top-level README
    summary = [
        "# AgValoniaGPS icon catalog",
        "",
        "Snapshot of every PNG/GIF icon currently referenced by an AXAML file in",
        "the AgValoniaGPS UI, grouped by source panel/dialog/wizard. Built",
        "for the AgOpen graphic artist to work through folder-by-folder.",
        "",
        "## Layout",
        "",
        "- `Panels/` — drawer / floating / nav panels (`Shared/AgValoniaGPS.Views/Controls/Panels/`)",
        "- `Dialogs/` — modal dialogs (`Shared/AgValoniaGPS.Views/Controls/Dialogs/`)",
        "- `Dialogs/Configuration/` — sub-tabs of the unified Configuration dialog",
        "- `Wizards/` — wizard step views (`Shared/AgValoniaGPS.Views/Controls/Wizards/`)",
        "- `_orphans/` — PNGs/GIFs that exist on disk but aren't referenced by any AXAML",
        "",
        "Some Configuration sub-tabs use animated `.gif` demonstrations rather",
        "than static icons (timing animations). Those land in the catalog too,",
        "but they're a different problem — they don't translate to a single",
        "glyph. Skip them when designing replacements unless we discuss further.",
        "",
        "## Per-folder contents",
        "",
        "Each folder contains:",
        "1. The PNGs that the corresponding AXAML file references.",
        "2. A `README.md` listing each button: tooltip / command / icon set",
        "   (toggles list both their on and off variants).",
        "",
        "## Glyph style guidance for replacements",
        "",
        "- Authoring canvas: 24×24 viewBox.",
        "- Final output: a single flat `<path>` (or a few flat paths) per",
        "  glyph, suitable for pasting into",
        "  `Shared/AgValoniaGPS.Views/Icons/Glyphs.axaml` as a",
        "  `<StreamGeometry>` resource.",
        "- Filled silhouettes preferred for visual weight, matching the v2",
        "  set the artist already provided for the LeftNav.",
        "- Two-state toggles (on / off) become a single glyph; the engaged",
        "  state is conveyed by the button's solid green background, not by",
        "  swapping glyphs.",
        "",
        "## Workflow",
        "",
        "1. Pick a folder.",
        "2. Author replacement SVG(s).",
        "3. Drop the SVG into the same folder alongside the original PNG.",
        "4. Hand the folder back; we wire it into Glyphs.axaml + the panel.",
        "",
        "## Counts",
        "",
        f"- Total unique PNG/GIF files in `Assets/Icons/`: {len(on_disk)}",
        f"- Files referenced by AXAML: {len(referenced)}",
        f"- Orphans on disk: {len(orphans)}",
        f"- AXAML files contributing icons: {len(by_panel)}",
        "",
        "## Regenerate",
        "",
        "```bash",
        "python3 Plans/GlyphWordButtons/scripts/build_icon_catalog.py",
        "```",
        "",
        "Re-running wipes and rebuilds `Plans/GlyphWordButtons/icon-catalog/`.",
        "",
    ]
    (CATALOG / "README.md").write_text("\n".join(summary))

    print(f"Built {CATALOG}")
    print(f"  AXAML files: {len(by_panel)}")
    print(f"  Referenced PNGs: {len(referenced)}")
    print(f"  Orphans: {len(orphans)}")


if __name__ == "__main__":
    main()

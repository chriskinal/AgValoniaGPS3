#!/usr/bin/env python3
"""
Strip hidden groups from artist's multi-icon SVGs.

The artist's working files contain all icons in every file; a CSS class
on the visible group has no `display: none` rule, while every other
group's class does. This script parses the CSS, identifies the
display:none classes, removes any element carrying one of them, and
writes a simplified SVG ready for picosvg flattening.
"""
import re
import sys
from lxml import etree as ET

SVG_NS = "http://www.w3.org/2000/svg"

def main(in_path: str, out_path: str) -> None:
    parser = ET.XMLParser(remove_blank_text=False)
    tree = ET.parse(in_path, parser)
    root = tree.getroot()

    # 1. Parse the <style> block for `display: none` selectors.
    hidden = set()
    for style in root.iter(f"{{{SVG_NS}}}style"):
        css = style.text or ""
        # Each rule block: `selectors { props }`
        for m in re.finditer(r"([^{}]+)\{([^}]+)\}", css):
            selectors_text, props = m.group(1), m.group(2)
            if "display:" in props.replace(" ", "") and "none" in props:
                # Pull each `.stN` out of the selector list
                for cls in re.findall(r"\.(st\d+)", selectors_text):
                    hidden.add(cls)

    # 2. Walk the tree; collect any element whose class is in `hidden`,
    #    then remove them in a second pass (iterating-while-mutating
    #    is unreliable in lxml — element-removal can desync the iterator).
    to_remove = []
    for elem in root.iter():
        cls_attr = elem.get("class")
        if not cls_attr:
            continue
        if set(cls_attr.split()) & hidden:
            to_remove.append(elem)

    for elem in to_remove:
        parent = elem.getparent()
        if parent is not None:
            parent.remove(elem)

    # Leave <style> blocks intact — picosvg will resolve them into
    # inline attributes (preserving fill/stroke). Stripping them here
    # would lose the .st0 { fill: #fff } rule the visible icon needs.

    tree.write(out_path, xml_declaration=True, encoding="UTF-8")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("usage: strip_hidden.py <in.svg> <out.svg>", file=sys.stderr)
        sys.exit(1)
    main(sys.argv[1], sys.argv[2])

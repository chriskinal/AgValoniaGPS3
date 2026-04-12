#!/usr/bin/env python3
"""Assemble screenshot catalog into a labeled PDF.

Usage: python3 screenshots-to-pdf.py <catalog_dir> [output.pdf] [--small]

The catalog_dir should contain dark/ and light/ subdirectories with PNG files.
Each page gets a header label showing the theme and screenshot name.
Use --small to resize images for smaller file size (~2 MB vs ~8 MB).
"""

import os
import sys
from PIL import Image, ImageDraw, ImageFont

def main():
    if len(sys.argv) < 2:
        print(f"Usage: {sys.argv[0]} <catalog_dir> [output.pdf] [--small]")
        sys.exit(1)

    catalog_dir = sys.argv[1]
    small = "--small" in sys.argv
    remaining = [a for a in sys.argv[2:] if a != "--small"]
    output = remaining[0] if remaining else "AgValoniaGPS_UI_Catalog.pdf"

    pages = []

    for theme in ["dark", "light"]:
        theme_dir = os.path.join(catalog_dir, theme)
        if not os.path.isdir(theme_dir):
            print(f"WARNING: {theme_dir} not found, skipping")
            continue

        files = sorted(f for f in os.listdir(theme_dir) if f.endswith(".png"))

        for f in files:
            path = os.path.join(theme_dir, f)
            img = Image.open(path).convert("RGB")

            if small:
                img = img.resize((640, 480), Image.LANCZOS)

            # Add label header
            name = f.replace(".png", "").replace("_", " ").title()
            label = f"{theme.upper()}: {name}"
            labeled = add_label(img, label, theme)

            pages.append(labeled)
            print(f"  {theme}/{f}")

    if not pages:
        print("No images found!")
        sys.exit(1)

    pages[0].save(output, save_all=True, append_images=pages[1:], resolution=96)
    size_mb = os.path.getsize(output) / (1024 * 1024)
    print(f"\nPDF: {output} ({len(pages)} pages, {size_mb:.1f} MB)")


def add_label(img, label, theme):
    """Add a text label bar at the top of the image."""
    bar_height = 30
    width, height = img.size

    # Create new image with label bar
    new_img = Image.new("RGB", (width, height + bar_height))

    # Draw label bar
    draw = ImageDraw.Draw(new_img)
    bg_color = (40, 40, 40) if theme == "dark" else (220, 220, 220)
    text_color = (200, 200, 200) if theme == "dark" else (30, 30, 30)
    draw.rectangle([0, 0, width, bar_height], fill=bg_color)

    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 16)
    except (IOError, OSError):
        font = ImageFont.load_default()

    draw.text((10, 6), label, fill=text_color, font=font)

    # Paste original image below label
    new_img.paste(img, (0, bar_height))
    return new_img


if __name__ == "__main__":
    main()

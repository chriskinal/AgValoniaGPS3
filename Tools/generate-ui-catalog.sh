#!/bin/bash
# Generate UI screenshot catalog PDF
# Usage: ./generate-ui-catalog.sh [output.pdf]
#
# Runs the integration test in headless --catalog mode, then
# assembles all screenshots into a single PDF.
#
# Requirements: dotnet, python3, Pillow (pip3 install Pillow)

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT="${1:-$REPO_ROOT/AgValoniaGPS_UI_Catalog.pdf}"

echo "=== Capturing screenshots ==="
dotnet run --project "$REPO_ROOT/Tests/AgValoniaGPS.IntegrationTests/" -- --headless --catalog

CATDIR="$REPO_ROOT/Tests/AgValoniaGPS.IntegrationTests/bin/Debug/net10.0/screenshots/catalog"

if [ ! -d "$CATDIR/dark" ] || [ ! -d "$CATDIR/light" ]; then
    echo "ERROR: Screenshots not found in $CATDIR"
    exit 1
fi

echo ""
echo "=== Generating PDF ==="
python3 "$SCRIPT_DIR/screenshots-to-pdf.py" "$CATDIR" "$OUTPUT"

echo ""
echo "Output: $OUTPUT"

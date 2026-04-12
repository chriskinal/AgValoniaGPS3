#!/bin/bash
# Unpack and display AgValoniaGPS debug dump
# Usage: ./unpack-dump.sh <dump.zip> [output_dir]

set -e

if [ -z "$1" ]; then
    echo "Usage: $0 <debug_dump.zip> [output_dir]"
    exit 1
fi

ZIP="$1"
OUTDIR="${2:-$(mktemp -d /tmp/agvdump.XXXXXX)}"

mkdir -p "$OUTDIR"
unzip -o -q "$ZIP" -d "$OUTDIR"

echo "=== System Info ==="
cat "$OUTDIR/system_info.txt" 2>/dev/null || echo "(not found)"

echo ""
echo "=== Active Profile ==="
cat "$OUTDIR/active_profile_name.txt" 2>/dev/null || echo "(not found)"

echo ""
echo "=== Configuration ==="
cat "$OUTDIR/configuration.json" 2>/dev/null | python3 -m json.tool 2>/dev/null || cat "$OUTDIR/configuration.json" 2>/dev/null || echo "(not found)"

echo ""
echo "=== Runtime State ==="
cat "$OUTDIR/runtime_state.json" 2>/dev/null | python3 -m json.tool 2>/dev/null || cat "$OUTDIR/runtime_state.json" 2>/dev/null || echo "(not found)"

echo ""
echo "=== App Settings ==="
cat "$OUTDIR/appsettings.json" 2>/dev/null | python3 -m json.tool 2>/dev/null || cat "$OUTDIR/appsettings.json" 2>/dev/null || echo "(not found)"

echo ""
echo "=== Logs ==="
cat "$OUTDIR/logs.txt" 2>/dev/null || echo "(not found)"

# List any field files
FIELD_FILES=$(find "$OUTDIR" -name "field_*" -o -name "*.geojson" -o -name "Boundary.txt" -o -name "Field.txt" 2>/dev/null)
if [ -n "$FIELD_FILES" ]; then
    echo ""
    echo "=== Field Files ==="
    for f in $FIELD_FILES; do
        echo "--- $(basename "$f") ---"
        cat "$f"
        echo ""
    done
fi

echo ""
echo "Unpacked to: $OUTDIR"

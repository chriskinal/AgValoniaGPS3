#!/bin/bash
# Generate test video from integration test screenshots
# Usage: ./generate-test-video.sh [test-mode] [output.mp4]
#
# test-mode: --field-test | --uturn-test (default: --uturn-test)
# output:    path to output MP4 (default: ./test_video.mp4)
#
# Requirements: dotnet, ffmpeg, python3, Pillow

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEST_MODE="${1:---uturn-test}"
OUTPUT="${2:-$REPO_ROOT/test_video.mp4}"

echo "=== Running integration test: $TEST_MODE ==="
dotnet run --project "$REPO_ROOT/Tests/AgValoniaGPS.IntegrationTests/" -- --headless "$TEST_MODE"

# Find the GIF output
case "$TEST_MODE" in
    --field-test)
        GIF_DIR="$REPO_ROOT/Tests/AgValoniaGPS.IntegrationTests/bin/Debug/net10.0/screenshots/field-test"
        GIF="$GIF_DIR/autosteer_drive.gif"
        ;;
    --uturn-test)
        GIF_DIR="$REPO_ROOT/Tests/AgValoniaGPS.IntegrationTests/bin/Debug/net10.0/screenshots/uturn-test"
        GIF="$GIF_DIR/uturn_test.gif"
        ;;
    *)
        echo "Unknown test mode: $TEST_MODE"
        exit 1
        ;;
esac

if [ ! -f "$GIF" ]; then
    echo "ERROR: GIF not found at $GIF"
    exit 1
fi

echo ""
echo "=== Generating MP4 ==="
ffmpeg -y -i "$GIF" \
    -f lavfi -i anullsrc=r=44100:cl=mono \
    -vf "scale=960:720,setpts=0.5*PTS" \
    -r 2 -c:v libx264 -pix_fmt yuv420p \
    -c:a aac -shortest -movflags faststart \
    "$OUTPUT"

SIZE=$(du -h "$OUTPUT" | cut -f1)
echo ""
echo "Output: $OUTPUT ($SIZE)"

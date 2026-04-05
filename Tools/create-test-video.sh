#!/bin/bash
# Create MP4 video from integration test GIF output
# Usage: ./create-test-video.sh [input.gif] [output.mp4]
set -e

INPUT="${1:-Tests/AgValoniaGPS.IntegrationTests/bin/Debug/net10.0/screenshots/headless/pass_rendering.gif}"
OUTPUT="${2:-/tmp/claude/test_video.mp4}"

mkdir -p "$(dirname "$OUTPUT")"

ffmpeg -y -i "$INPUT" \
    -f lavfi -i anullsrc=r=44100:cl=mono \
    -vf "scale=1280:960:flags=lanczos" \
    -r 10 -c:v libx264 -crf 18 -pix_fmt yuv420p \
    -c:a aac -shortest -movflags faststart \
    "$OUTPUT"

SIZE=$(du -h "$OUTPUT" | cut -f1)
echo "Output: $OUTPUT ($SIZE)"

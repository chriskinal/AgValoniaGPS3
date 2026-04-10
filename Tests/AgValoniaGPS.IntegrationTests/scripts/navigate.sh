#!/bin/bash
# Helper script for Remote Test Server UI navigation
# Usage: ./navigate.sh <command> [args...]

BASE="http://localhost:5123"

case "$1" in
  state)
    curl -s "$BASE/state" | python3 -m json.tool
    ;;
  screenshot)
    OUT="${2:-/tmp/claude/screenshot.png}"
    curl -s "$BASE/screenshot" -o "$OUT"
    echo "Screenshot saved to $OUT"
    ;;
  click)
    curl -s -X POST "$BASE/click" -H "Content-Type: application/json" \
      -d "{\"x\":$2,\"y\":$3,\"button\":\"${4:-left}\"}" -o "${5:-/tmp/claude/after_click.png}"
    echo "Clicked ($2, $3) button=${4:-left}"
    ;;
  doubleclick)
    curl -s -X POST "$BASE/doubleclick" -H "Content-Type: application/json" \
      -d "{\"x\":$2,\"y\":$3}" -o "${4:-/tmp/claude/after_doubleclick.png}"
    echo "Double-clicked ($2, $3)"
    ;;
  key)
    curl -s -X POST "$BASE/key" -H "Content-Type: application/json" \
      -d "{\"key\":\"$2\"}" -o "${3:-/tmp/claude/after_key.png}"
    echo "Key pressed: $2"
    ;;
  type)
    curl -s -X POST "$BASE/type" -H "Content-Type: application/json" \
      -d "{\"text\":\"$2\"}" -o "${3:-/tmp/claude/after_type.png}"
    echo "Typed: $2"
    ;;
  command)
    curl -s -X POST "$BASE/command" -H "Content-Type: application/json" \
      -d "{\"name\":\"$2\"}" | python3 -m json.tool
    ;;
  scroll)
    curl -s -X POST "$BASE/scroll" -H "Content-Type: application/json" \
      -d "{\"x\":$2,\"y\":$3,\"delta\":$4}" -o "${5:-/tmp/claude/after_scroll.png}"
    echo "Scrolled at ($2, $3) delta=$4"
    ;;
  elements)
    curl -s "$BASE/elements" | python3 -m json.tool
    ;;
  wait)
    curl -s -X POST "$BASE/wait" -H "Content-Type: application/json" \
      -d "{\"ms\":${2:-500}}" -o "${3:-/tmp/claude/after_wait.png}"
    echo "Waited ${2:-500}ms"
    ;;
  *)
    echo "Usage: $0 {state|screenshot|click|doubleclick|key|type|command|scroll|elements|wait}"
    ;;
esac

#!/usr/bin/env bash
set -euo pipefail

theme="${1:?usage: capture-showcase.sh THEME OUTPUT}"
output="${2:?usage: capture-showcase.sh THEME OUTPUT}"
app="${SHIFTDIFF_APP:-artifacts/app/ShiftDiff.App.dll}"

mkdir -p "$(dirname "$output")"
export SHIFTDIFF_THEME="$theme"

dotnet "$app" \
  docs/showcase/base \
  docs/showcase/local \
  docs/showcase/remote &
app_pid=$!

cleanup() {
  kill "$app_pid" 2>/dev/null || true
  wait "$app_pid" 2>/dev/null || true
}
trap cleanup EXIT

window_id=""
for _ in $(seq 1 30); do
  window_id="$(xdotool search --onlyvisible --name 'ShiftDiff' 2>/dev/null | head -n 1 || true)"
  if [[ -n "$window_id" ]]; then
    break
  fi
  sleep 1
done

if [[ -z "$window_id" ]]; then
  echo "ShiftDiff window did not become visible" >&2
  exit 1
fi

# Let async file loading and comparison finish before capturing the real window.
sleep 5
import -window "$window_id" "$output"
identify "$output"

#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="$ROOT/src/ImageForensics.App/Resources/Raw"
COMMIT="87416418657359cb625c412a48b6e1d6d41c29bd"
BASE="https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/$COMMIT"

mkdir -p "$DEST"

fetch_model() {
  local name="$1"
  local expected="$2"
  local output="$DEST/$name"
  local tmp="$output.tmp"

  echo "Fetching pinned OCR model: $name"
  curl --fail --location --retry 3 --retry-delay 2 "$BASE/$name" --output "$tmp"

  python3 - "$tmp" "$expected" <<'PY'
import hashlib, os, sys
path, expected = sys.argv[1], sys.argv[2].lower()
with open(path, "rb") as f:
    data = f.read()
actual = hashlib.sha1(b"blob " + str(len(data)).encode("ascii") + b"\0" + data).hexdigest()
if actual != expected:
    raise SystemExit(f"Git blob SHA mismatch for {os.path.basename(path)}: expected {expected}, got {actual}")
print(f"Verified {os.path.basename(path)} git-blob-sha1={actual} size={len(data)}")
PY

  mv "$tmp" "$output"
}

fetch_model "ara.traineddata" "c8d129c67821c1592cac46836686b057ecc203dc"
fetch_model "eng.traineddata" "bbef4675053b5b468cdb477053e28b1c698ba08e"

echo "OCR models ready in $DEST"

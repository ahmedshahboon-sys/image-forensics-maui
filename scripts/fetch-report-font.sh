#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="$ROOT/src/ImageForensics.Reporting/Resources"
COMMIT="ffebf8c1ee449e544955a7e813c54f9b73848eac"
FILE="hinted/ttf/NotoSansArabic/NotoSansArabic-Regular.ttf"
EXPECTED_BLOB_SHA="ce21d4f5ac8765b51b9eedb1bb5c0069ee8115a4"
URL="https://raw.githubusercontent.com/notofonts/noto-fonts/$COMMIT/$FILE"
OUTPUT="$DEST/NotoSansArabic-Regular.ttf"
TMP="$OUTPUT.tmp"

mkdir -p "$DEST"

echo "Fetching pinned report font: NotoSansArabic-Regular.ttf"
curl --fail --location --retry 3 --retry-delay 2 "$URL" --output "$TMP"

python3 - "$TMP" "$EXPECTED_BLOB_SHA" <<'PY'
import hashlib, os, sys

path, expected = sys.argv[1], sys.argv[2].lower()

with open(path, "rb") as handle:
    data = handle.read()

actual = hashlib.sha1(
    b"blob " + str(len(data)).encode("ascii") + b"\0" + data
).hexdigest()

if actual != expected:
    raise SystemExit(
        f"Git blob SHA mismatch for {os.path.basename(path)}: "
        f"expected {expected}, got {actual}"
    )

print(
    f"Verified {os.path.basename(path)} "
    f"git-blob-sha1={actual} size={len(data)}"
)
PY

mv "$TMP" "$OUTPUT"
echo "Report font ready in $DEST"

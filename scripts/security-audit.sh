#!/usr/bin/env bash
set -euo pipefail

MANIFEST="src/ImageForensics.App/Platforms/Android/AndroidManifest.xml"

test -f "$MANIFEST"

if grep -Eq 'android.permission.(INTERNET|ACCESS_FINE_LOCATION|ACCESS_COARSE_LOCATION|CAMERA|READ_EXTERNAL_STORAGE|WRITE_EXTERNAL_STORAGE|MANAGE_EXTERNAL_STORAGE)' "$MANIFEST"; then
  echo "Unexpected dangerous/unneeded permission in offline baseline:"
  grep -En 'android.permission.' "$MANIFEST" || true
  exit 1
fi

grep -q 'android:allowBackup="false"' "$MANIFEST"
grep -q 'android:usesCleartextTraffic="false"' "$MANIFEST"

echo "Android least-permission manifest audit passed."

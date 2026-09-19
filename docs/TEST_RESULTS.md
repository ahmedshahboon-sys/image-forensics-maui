# Test and validation status

## Automated unit tests

The current CI suite covers:
- magic-byte detection and extension mismatch;
- hashing/file identity behavior;
- corrupt/empty input handling;
- JPEG/PNG container parsing;
- trailing data;
- privacy/forensic rules;
- report CSV escaping;
- supporting analysis primitives.

The latest successful release-baseline CI before this documentation update completed **11 tests with 0 failures**.

## CI gates

A release-baseline run is considered successful only when all of these pass:
- .NET 10 setup;
- MAUI Android workload installation;
- unit tests;
- Android project restore;
- signed APK build;
- APK signature verification;
- zipalign verification;
- ZIP/APK integrity check;
- SHA-256 generation;
- artifact upload.

## Device QA still required before public production release

Cloud CI can validate the binary but does not replace real-device QA. Before a public store/release rollout, verify on representative Android devices:
- install and cold launch;
- File Picker;
- share image into the app;
- quick/deep scan of JPEG/PNG/WebP/HEIF where supported;
- image without metadata;
- image with explicit GPS;
- corrupt image without crash;
- large-image cancellation;
- cleaner leaves the original untouched;
- JSON/TXT/PDF export and Android Share Sheet;
- map intent;
- Light/Dark and RTL layouts.

Record device model, Android version and result for each production candidate.

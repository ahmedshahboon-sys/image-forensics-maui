# Test and validation status — 1.0.0

## Automated coverage

The suite includes:
- hashing/file identity and magic-byte mismatch;
- JPEG markers / PNG chunks / trailing data;
- EXIF Make/Model and explicit GPS parsing;
- privacy rules and cleaner verification;
- corrupt-image handling;
- huge-image controlled rejection;
- batch cancellation;
- report TXT/JSON/PDF generation;
- Arabic Unicode PDF generation;
- comparison and pixel metrics;
- QR decoding;
- hidden-data and steganography primitives;
- privacy-safe history/logging;
- online-mode policy;
- generated synthetic corpus;
- performance regression budgets.

The generated corpus contains no real-person photos.

## Release-candidate CI gates

A green candidate requires:
- .NET/MAUI setup;
- pinned OCR model verification;
- pinned PDF font verification;
- Android least-permission security audit;
- automated tests;
- Release APK build;
- signing;
- apksigner verification;
- zipalign verification;
- ZIP/APK integrity;
- Android Emulator install/open smoke;
- SHA-256 generation;
- artifact upload.

## Device QA boundary

Cloud CI and emulator QA do not replace representative physical-device testing. Native OCR visual accuracy, chooser/share-sheet behavior, maps apps, vendor HEIF codecs and device-specific memory behavior should be spot-checked before a broad public/store rollout.

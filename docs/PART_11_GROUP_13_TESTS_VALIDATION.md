# Part 11 — Group 13: Tests & Validation

The automated suite now covers the master-prompt validation areas with synthetic, locally generated fixtures.

## Unit/integration coverage

- hashing and file identity;
- magic bytes and extension mismatch;
- JPEG markers and PNG chunks;
- EXIF Make/Model parsing;
- explicit EXIF GPS parsing;
- privacy rules and verified metadata cleaning;
- corrupt input handling;
- huge decoded-image safety limits;
- batch cancellation semantics;
- TXT/JSON/PDF report generation;
- comparison and pixel metrics;
- hidden-data scanning;
- QR decoding;
- privacy-safe history and logging;
- online feature policy and security rules.

## Local synthetic corpus

Tests generate fixtures at runtime instead of committing photos of real people:

- JPEG with EXIF Make/Model;
- JPEG without EXIF;
- explicit GPS JPEG;
- PNG with textual metadata chunk;
- screenshot-like PNG;
- re-encoded/double-compressed JPEG;
- corrupt JPEG;
- JPEG with appended bytes;
- JPEG with embedded EXIF thumbnail;
- QR PNG;
- high-contrast Arabic OCR PNG.

This keeps the public repository free of unlicensed personal photos.

## OCR limitation

The Arabic OCR fixture is generated and decoded by the image pipeline in the portable test suite. Actual native Tesseract Arabic OCR execution is Android/runtime-specific and remains a physical-device QA item for Group 15; the test suite does not pretend that a Linux image-decoding test proves Android OCR accuracy.

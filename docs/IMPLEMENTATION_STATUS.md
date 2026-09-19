# Implementation status

## Group 0 — Foundation
Implemented baseline:
- Modular solution structure.
- MAUI dependency composition.
- Privacy-safe logging abstraction and local JSON-line logger.
- Central file-size limits.
- Cancellation and progress contracts.
- Android-first app shell.
- GitHub Actions CI for tests and APK publish.
- Private cache copy for Android content URIs with size cap and cleanup.

## Group 1 — File identity & technical inspection
Implemented baseline:
- filename/size/extension/MIME.
- magic-byte detection and extension/content mismatch.
- SHA-256, SHA-1, MD5.
- CRC32 primitive.
- full-file entropy.
- dimensions/aspect ratio/encoded format/color type/alpha/frames/orientation via SkiaSharp.
- aHash/dHash/pHash and similarity helper.
- safe streaming reads and timestamps when available.

Remaining refinements:
- DPI/PPI and palette reporting.
- broader RAW/HEIF validation.
- stronger batch comparison UI.

## Group 2 — Metadata
Implemented baseline:
- MetadataExtractor 2.9.3.
- generic EXIF/IPTC/XMP/ICC/etc. directory/tag enumeration.
- raw + parsed values, source, meaning, confidence.
- GPS decimal coordinates when explicit GPS metadata exists.
- parser error collection.

## Group 3 — Container structure
Implemented baseline:
- safe JPEG marker parser.
- APP/DQT/DHT/SOF/SOS/COM/EOI reporting.
- trailing-byte detection.
- safe PNG chunk parser including IHDR/IDAT/IEND/text/eXIf/iCCP.
- invalid/oversized segment guards.
- unit tests for JPEG trailing data and PNG IEND.

## Group 6 — QR/Barcode
Initial offline file decoder implemented using ZXing + SkiaSharp.

## Group 9 — Reporting
Initial JSON/TXT report model and writer implemented.

Next execution target:
Groups 4–8 forensic consistency, hidden-data heuristics, cleaner, comparison/batch, then UI/reporting/security/test/release completion.

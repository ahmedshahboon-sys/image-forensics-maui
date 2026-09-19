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
- SHA-256, SHA-1, MD5 and CRC32 primitive.
- full-file entropy.
- dimensions/aspect ratio/encoded format/color type/alpha/frames/orientation via SkiaSharp.
- aHash/dHash/pHash and Hamming-similarity helper.
- safe streaming reads.

Remaining refinements: DPI/PPI, palette reporting, broader RAW/HEIF validation and stronger comparison UI.

## Group 2 — Metadata
Implemented baseline:
- MetadataExtractor 2.9.3.
- EXIF/IPTC/XMP/ICC/etc. directory/tag enumeration.
- raw + parsed values, source, meaning, confidence.
- explicit GPS decimal coordinates only when GPS metadata exists.
- parser error collection.

## Group 3 — Container structure
Implemented baseline:
- safe JPEG marker parser and PNG chunk parser.
- APP/DQT/DHT/SOF/SOS/COM/EOI reporting.
- IHDR/IDAT/IEND/text/eXIf/iCCP reporting.
- trailing-byte detection and oversized/truncated guards.

## Group 4 — Consistency checks
Initial rules implemented:
- extension versus magic signature mismatch.
- trailing data after container end.
- Software metadata indicator with explicit non-conclusive limitation.
- metadata pixel dimensions versus decoded dimensions.
- EXIF timestamp-order anomaly.
Every rule returns Evidence + Confidence + Limitation and never declares an image genuine/fake.

## Group 5 — Hidden data heuristics
Initial read-only scanner implemented:
- ZIP, 7z, RAR, PDF, PE/MZ and GZip magic-byte discovery with offsets.
- results are Possible indicators only.
- no extraction and no execution.
- bounded result count and streaming scan.

## Group 6 — QR/Barcode
Initial offline decoder implemented using ZXing + SkiaSharp.

## Group 9 — Reporting
Initial JSON/TXT report model and writer implemented.

Next execution target:
Privacy cleaner, comparison/batch, deeper image heuristics, UI/report export, security/test/performance/release completion.

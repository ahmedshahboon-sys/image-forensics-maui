# Implementation status

## Group 0 — Foundation
Baseline implemented: modular solution, DI, local privacy-safe logging, configurable limits, cancellation/progress, Android MAUI shell, GitHub Actions CI and private-cache handling.

## Group 1 — File identity & technical inspection
Implemented: MIME/signature mismatch, SHA-256/SHA-1/MD5/CRC32 primitive, entropy, dimensions/aspect/encoding/color/alpha/frames/orientation, aHash/dHash/pHash and similarity.
Remaining refinements: DPI/PPI, palette, broader RAW/HEIF validation.

## Group 2 — Metadata
Implemented: MetadataExtractor-based generic EXIF/IPTC/XMP/ICC/etc. enumeration, raw/parsed/source/confidence fields, explicit GPS extraction and parser errors.

## Group 3 — Container structure
Implemented baseline JPEG marker and PNG chunk parsers with offsets, lengths, end-marker/trailing-data detection and size/truncation guards.

## Group 4 — Consistency checks
Implemented initial Evidence + Confidence + Limitation rules for extension/signature mismatch, trailing data, Software field, metadata dimension mismatch and timestamp ordering. No genuine/fake verdict is produced.

## Group 5 — Hidden data
Implemented read-only streaming magic-signature heuristics for archives/documents/executables. No extraction and no execution.

## Group 6 — QR/Barcode
Initial offline decoder implemented via ZXing + SkiaSharp.

## Group 7 — Privacy & metadata cleaner
Implemented privacy-risk classification for GPS, device model, serial, owner/copyright, timestamps, software, XMP/IPTC, thumbnails and trailing data.
Metadata cleaner now creates a NEW pixel-reencoded JPEG/PNG, applies EXIF orientation to pixels, does not overwrite the source, re-reads metadata, re-hashes the original and reports whether GPS was removed.
Animated/multi-frame input is explicitly refused rather than silently flattened.

## Group 8 — Comparison lab
Implemented comparison service for exact SHA-256, dimensions, aHash/dHash/pHash similarity and metadata field differences.
Visual overlay/difference heatmap and batch UI remain.

## Group 9 — Reporting
Initial JSON/TXT report model/writer exists.

Next execution target:
Deeper image heuristics, difference maps/batch, report export/share, full result UI, Android sharing/GPS intents, security hardening, expanded tests/performance and release validation.

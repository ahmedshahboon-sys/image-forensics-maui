# Implementation status

Groups 0–15 now have a release-oriented baseline implementation.

## Complete baseline
- Architecture/DI, cancellation/progress, safe limits and cache handling.
- File identity, MIME/magic mismatch, SHA-256/SHA-1/MD5/CRC32 helper, entropy.
- Technical image properties and perceptual hashes.
- Metadata enumeration and explicit GPS.
- JPEG/PNG structure and trailing bytes.
- Consistency evidence engine with confidence + limitation.
- Hidden embedded-signature scanning without extraction/execution.
- Privacy risk report and non-destructive metadata cleaner.
- Exact/perceptual/metadata comparison plus difference-map visualization.
- QR/barcode offline decoding.
- Pixel heuristics: approximate JPEG quality, chroma subsampling, block-boundary ratio, simple noise consistency, ELA helper, coarse copy-move tile candidates.
- Visual tools: ELA, bit planes, RGB channels, difference map.
- Reports: JSON, TXT, PDF; batch CSV.
- Android share-in, share-out, GPS map intent, Light/Dark toggle.
- Unit tests and GitHub Actions beta APK pipeline.
- Separate production signing workflow for APK/AAB.

## Explicit limitations
- Arabic offline OCR is deferred until a reliable redistributable on-device engine/model is selected.
- RAW/HEIF depth depends on platform/library codec support.
- Advanced double-JPEG, splice/resampling and scientific noise-source attribution remain probabilistic/research-grade. The app does not fabricate certainty.
- CI beta signing key is ephemeral. Production releases require the user's persistent signing key in GitHub Secrets.

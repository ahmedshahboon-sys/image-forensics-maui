# Architecture

## Modules
- App: .NET MAUI UI and dependency composition.
- Core: contracts, shared models, confidence semantics, limits.
- Forensics: read-only technical and forensic inspection.
- Metadata: EXIF/IPTC/XMP/ICC parsing.
- Imaging: image decoding and pixel-domain analysis.
- Storage: optional local history/logging with privacy mode.
- Reporting: TXT/JSON/CSV/PDF output.
- Platform.Android: Android-only intents/share/platform integrations.
- Tests: deterministic unit tests and local corpus tests.

## Design rules
1. Offline-first.
2. No automatic upload.
3. Long work accepts CancellationToken and reports progress.
4. Large files use streaming whenever possible.
5. No embedded payload is executed.
6. Results distinguish confirmed facts from probabilistic indicators.
7. Original evidence files are never modified by cleaners.

# Part 10 — Group 12: Security

Implemented/enforced:
- Android least-permission CI audit.
- No INTERNET permission in the offline APK.
- No Location permission for EXIF GPS.
- No CAMERA/storage-management permission in the baseline.
- Android backup disabled and cleartext traffic disabled.
- Configurable 512 MiB default file cap is enforced both while copying into app-private cache and during file identity inspection.
- Configured scan timeout is now enforced centrally by ScanCoordinator using a linked CancellationToken.
- App-private temp names are GUID-based; external filenames are never used as cache paths.
- Metadata cleaner sanitizes derivative filenames and writes a new file only.
- No archive/payload extraction or execution.
- QR/barcode URLs are display-only; they are never auto-opened.
- Online Mode delegates only explicitly confirmed browser actions and does not add INTERNET permission.
- Privacy Mode can disable history writes.
- Local scan history excludes image bytes, OCR text, GPS coordinates and source paths.
- Privacy-safe logger now drops sensitive key categories before serialization.

CI now fails if the Android manifest gains INTERNET, location, CAMERA or broad external-storage permissions without an intentional review.

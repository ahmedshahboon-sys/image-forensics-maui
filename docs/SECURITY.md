# Security and privacy baseline

- Least permissions.
- Reading EXIF GPS does not require Android location permission.
- No QR link is opened automatically.
- No embedded content is executed.
- No extracted executable is launched.
- Configurable file-size limits and cancellation protect against resource exhaustion.
- Logs must not contain image bytes, OCR text, GPS coordinates, or full source paths by default.
- Temporary files are isolated and must be deleted after use.
- Online integrations, when added, are disabled by default and require explicit consent.


## Optional online mode

Online Mode defaults to off. Current online actions use the external browser only:
- reverse-image-search home page: no image bytes are passed by the app;
- hash reputation search: only SHA-256 is placed in the external URL after user confirmation.

The base APK still declares no INTERNET permission and performs no background upload.

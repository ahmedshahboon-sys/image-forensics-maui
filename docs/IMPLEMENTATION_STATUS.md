# Implementation status

## Group 0 — Foundation
Initial implementation:
- Modular solution structure.
- MAUI dependency composition.
- Privacy-safe logging abstraction and local JSON-line logger.
- Central file-size limits.
- Cancellation and progress contracts.
- Android-first app shell.
- GitHub Actions CI for tests and APK publish.

## Group 1 — File identity
Initial implementation:
- filename/size/extension/MIME.
- magic-byte detection.
- extension/content mismatch.
- SHA-256, SHA-1, MD5.
- full-file entropy.
- safe streaming reads.
- timestamps when available.

Remaining:
- dimensions, orientation, bit depth, alpha, color space, compression, frames, DPI, palette.
- CRC32, aHash/dHash/pHash and multi-image similarity.
- broader format coverage and RAW metadata reading.

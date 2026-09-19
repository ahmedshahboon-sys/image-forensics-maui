# Forensic limitations

The application deliberately separates confirmed technical facts from heuristic indicators.

- Extension/signature mismatch confirms a mismatch only. It does not establish forgery.
- A Software metadata tag can be written by normal camera, phone, cloud and editing workflows.
- Missing EXIF is not evidence that a file came from WhatsApp, Facebook or any specific service.
- ELA is a visualization aid, not a verdict.
- Eight-pixel JPEG block-boundary metrics are affected by normal JPEG encoding, screenshots, resizing and scene structure.
- Perceptual hashes estimate visual similarity and are not cryptographic identity tests.
- Embedded magic-byte signatures can occur coincidentally inside compressed streams. No discovered payload is executed.
- Bit-plane patterns are not proof of steganography.
- Metadata timestamps may reflect time zones, clock errors, exports and later software writes.
- The tool reports evidence, confidence and limitations rather than labeling an image original or fake.

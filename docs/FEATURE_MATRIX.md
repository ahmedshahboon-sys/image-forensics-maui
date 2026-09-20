# Feature matrix — 1.0.0

## Implemented

Identity/technical:
- signature/MIME/extension mismatch;
- entropy, SHA-256/SHA-1/MD5 comparison/CRC32;
- dimensions/orientation/alpha/frames/bit depth/DPI/palette where available;
- aHash/dHash/pHash.

Metadata/container:
- EXIF/GPS/IPTC/XMP/ICC/JFIF and available maker/container metadata;
- explicit GPS display/copy/maps intent;
- JPEG/PNG/WebP/GIF structure and trailing-data checks.

Forensics:
- timestamp/device/software consistency rules;
- JPEG quantization, estimated quality, chroma;
- block/grid/noise/histogram/edge/resampling indicators;
- ELA helper;
- copy-move coarse candidates;
- RGB/bit planes/entropy map/LSB statistics;
- embedded signatures/printable strings/high-entropy indicators.

Visible content:
- offline Arabic/English OCR;
- offline QR/barcode;
- visible URL/email/phone extraction as text only.

Privacy:
- privacy risk report;
- verified clean-copy workflow that preserves the original;
- local compact history + Privacy Mode;
- privacy-safe log redaction.

Comparison/batch:
- exact/perceptual/pixel comparison;
- metadata/ICC diff;
- resize/crop/compression candidates;
- difference/heatmap/overlay/contact sheet;
- batch CSV/JSON with GPS/privacy/duplicate filters.

Reporting/UI:
- structured TXT/JSON/PDF;
- Arabic PDF shaping;
- report SHA-256 manifest;
- RTL, Light/Dark, result sections;
- metadata search/copy/explain;
- zoom and RGB histogram.

Online:
- optional mode disabled by default;
- explicit-consent external-browser workflow;
- no secret background upload.

Security/performance:
- least-permission audit;
- file/decoded-pixel caps and timeouts;
- secure temp handling;
- bounded preview decoding where evidence semantics permit;
- session-only latest scan cache;
- sequential cancellable batch.

Release:
- CI Release APK;
- package signature/zipalign/integrity verification;
- Android Emulator install/open smoke;
- production workflow for persistent-key APK + AAB.

## Heuristic by design

ELA, JPEG quality, block/grid, noise, resampling, copy-move and steganography signals are investigative indicators only. They never produce an automatic genuine/fake verdict.

## Deferred / limited

- Camera capture: optional; omitted to preserve least permissions.
- Face recognition/person identity: intentionally prohibited/not implemented.
- Face detection/count/blur: not bundled in 1.0.0 because no additional validated local model was introduced.
- Object/logo/license-plate detection: optional and deferred pending a validated local model.
- EXIF embedded-thumbnail preview/comparison: metadata presence is detected, but thumbnail bytes are not exposed by the current safe abstraction.
- RAW/HEIF deep support: dependent on platform/library codec support.
- Windows build: architecture is prepared, but Android is the shipped target.
- Physical-device QA: CI uses Android Emulator; a representative real-device pass remains recommended before broad rollout.

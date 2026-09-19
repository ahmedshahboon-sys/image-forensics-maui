# Final handoff — 0.9.0 Beta

## Delivered
- Complete source tree on `main`.
- Android-first .NET MAUI app.
- CI beta APK pipeline.
- Production APK/AAB signing workflow.
- README, build, release, security, architecture, feature, testing and third-party license documentation.
- SHA-256 is generated for every APK artifact.

## Implemented baseline
See `docs/FEATURE_MATRIX.md` and `docs/IMPLEMENTATION_STATUS.md`.

## Intentionally deferred / limited
- Arabic offline OCR: no reliable redistributable engine/model is bundled yet.
- RAW/HEIF depth: platform/library dependent.
- Advanced scientific double-compression, splice/resampling and camera-source/noise attribution: not represented as certainty.
- Windows executable: architecture is prepared, but Android is the release target.
- Optional online integrations: disabled/not included in the baseline to preserve offline-first privacy.

## Release rule
Do not label an image “genuine” or “fake” from a single heuristic. Every forensic indicator must be read with its evidence, confidence and limitation.

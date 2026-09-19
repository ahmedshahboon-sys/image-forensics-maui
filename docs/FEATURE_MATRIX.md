# Feature matrix

Implemented: file identity, signature/MIME mismatch, hashes, entropy, dimensions, orientation, alpha/frames, generic metadata, explicit GPS, JPEG/PNG structure, trailing data, perceptual hashes, QR/barcode, privacy risks, safe metadata cleaner, comparison, coarse difference map, ELA helper, RGB/bit-plane visualizations, block/noise/clone heuristics, hidden embedded-signature scan, JSON/TXT/PDF, batch CSV, Android share-in, Android share-out, GPS map intent, cancellation, safe size caps and CI APK.

Partially implemented / heuristic by design: JPEG quality estimate, chroma subsampling, block-boundary analysis, basic noise consistency, ELA, coarse copy-move candidates. These never produce a genuine/fake verdict.

Deferred with explicit reason: Arabic offline OCR. A reliable redistributable Android Arabic OCR engine/model is not bundled yet; shipping a weak or cloud-uploading fallback would violate the offline-first/privacy requirements. RAW/HEIF decoding depth depends on platform codec/library support. Full scientific splice/resampling detection remains research-grade and is not represented as certainty.

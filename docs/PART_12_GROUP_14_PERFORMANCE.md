# Part 12 — Group 14: Performance

Performance work is intentionally conservative because forensic pixel analysis must not silently change evidence semantics.

Implemented:
- Fast Scan remains limited to streaming file identity/hashes plus lightweight decoder technical inspection.
- Perceptual hashing uses a bounded preview decode rather than decoding a large image at full resolution.
- QR/barcode scanning uses a bounded preview decode.
- Pixel comparison works from bounded aspect-preserving previews before 256x256 normalized comparison.
- Pixel forensic heuristics work from a bounded preview while JPEG quantization/container facts continue to be read from the original bytes.
- LSB/steganography analysis deliberately keeps original decoded pixels because resampling would destroy the very LSB evidence being measured.
- Batch remains sequential to keep mobile RAM predictable.
- All long-running analysis paths retain CancellationToken support.
- ScanCoordinator keeps only the most recent quick/deep report in an in-memory session cache keyed by path, length and modification time.
- No persistent image cache is introduced.

CI performance budgets:
- ordinary ~2 MP Fast Scan primitives: under 3 seconds;
- representative portable deep primitives on a 1024x768 synthetic JPEG: under 15 seconds, a deliberately generous CI ceiling to avoid runner-noise flakes.

These CI budgets are regression guards, not claims that every Android device will have identical timings.

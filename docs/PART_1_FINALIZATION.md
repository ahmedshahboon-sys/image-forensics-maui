# Part 1 finalization — Groups 1–3

This part follows the master prompt sections for File Identity, Metadata Deep Inspection and Image Container/Binary Structure.

## Group 1 improvements
- CRC32 is now calculated in the same streaming pass as SHA-256/SHA-1/MD5.
- HEIF/HEIC detection no longer treats every ISO-BMFF file as HEIF; major brands are checked.
- AVIF major-brand detection was added.
- Technical results now expose:
  - encoded bit depth when it can be read safely;
  - horizontal/vertical DPI when explicitly encoded in JPEG JFIF, PNG pHYs or BMP;
  - palette information for PNG/GIF/BMP where it can be established;
  - color-space text from the decoder when available;
  - a human-readable compression/encoding description.
- Existing dimensions, aspect ratio, orientation, alpha, decoded bits-per-pixel, frame count, entropy and perceptual hashes remain.

## Group 2 improvements
- Metadata meanings were expanded for camera/lens serials, offsets, exposure, aperture, ISO, focal length, flash, white balance, metering, orientation, digital zoom, comments, XP fields and descriptions.
- Structured GPS now optionally includes altitude, speed, direction and GPS date/time when those fields are present.
- Corrupt/unsupported metadata parsing is returned as a controlled parser error rather than crashing the whole scan.
- Raw value, parsed value, meaning, confidence and source continue to be retained per field.

## Group 3 improvements
- Existing safe JPEG and PNG parsers remain.
- JPEG/PNG trailing data now identifies common appended JPEG/PNG/GIF/ZIP/PDF signatures without executing or extracting anything.
- PNG unusual critical chunks are surfaced as structural warnings.
- Added safe WebP RIFF parser for VP8/VP8L/VP8X, ALPH, ANIM/ANMF, EXIF, XMP and ICCP chunks.
- Added safe GIF parser for logical structure, color tables, frames, comments, application extensions, graphic-control extensions and loop metadata.
- All parsers are read-only and enforce segment/sub-block safety caps.

## Explicit limitations
- HEIF/HEIC and RAW deep container parsing remain library/platform dependent; this part improves identification and metadata handling without claiming unsupported binary interpretation.
- DPI is only reported when explicitly present in a supported header/metadata representation. It is not invented from image dimensions.
- Palette capacity and palette presence are reported conservatively.

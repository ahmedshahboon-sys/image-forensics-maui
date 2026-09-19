# Part 5 — Group 7: Privacy Inspector & Metadata Cleaner

This part implements the privacy-report and clean-copy requirements from the master prompt.

## Privacy Risk Report

Confirmed-presence checks cover:
- GPS metadata, including GPS-directory fields even when coordinates cannot be parsed;
- device/camera make/model;
- serial-number fields;
- owner / artist / author / copyright fields;
- capture/edit/GPS timestamp fields;
- software / creator-tool / history fields;
- embedded thumbnail or preview metadata;
- XMP;
- IPTC;
- comments, descriptions, captions, keywords and textual container chunks;
- bytes appended after the expected end of the primary image container.

A privacy risk means that a potentially sensitive field/container feature is present. It is not a claim that the file is malicious or forged.

## Clean copy

The cleaner:
- never writes to the source path;
- hashes the source before and after cleaning to verify it remained byte-for-byte unchanged;
- rejects animated/multi-frame images instead of silently flattening them;
- enforces a decoded-pixel safety cap;
- decodes the visual pixels;
- applies EXIF/encoded orientation to the pixels;
- re-encodes into a new JPEG for opaque images or PNG when alpha is needed;
- thereby strips EXIF/GPS/XMP/IPTC/comments/thumbnails and appended payloads as far as the chosen encoder allows;
- sanitizes the generated output filename.

## Mandatory verification pass

After writing the clean copy, the app re-runs:
- metadata inspection;
- container inspection;
- privacy-risk inspection;
- output decode/orientation verification;
- output dimension verification;
- source SHA-256 verification.

The UI shows before/after:
- metadata field counts;
- privacy risk counts and codes;
- trailing-byte counts;
- orientation;
- dimensions;
- hashes.

If verification finds any remaining targeted sensitive privacy indicator, trailing payload, unexpected orientation/dimensions, or a source-hash change, the result is marked failed and the Android share sheet is **not opened automatically**.

## Limitations

- Re-encoding changes image bytes and, for opaque images encoded as JPEG, may introduce lossy recompression. The clean copy is a privacy derivative, not a forensic-preservation copy.
- Animated/multi-frame cleaning remains deliberately unsupported because flattening would change the content semantics.
- ICC/color-management information is not classified as a sensitive privacy field by this group.

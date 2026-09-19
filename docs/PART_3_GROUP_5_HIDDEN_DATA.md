# Part 3 — Group 5: Steganography / Hidden Data

This part implements the read-only hidden-data baseline from the master prompt.

## File-level hidden-data inspection
Implemented with streaming reads and bounded result counts:
- embedded ZIP / empty ZIP signatures;
- 7-Zip;
- RAR;
- PDF;
- Windows PE/MZ;
- ELF;
- GZip;
- SQLite;
- OLE/CFB;
- embedded JPEG / PNG / GIF / WebP signatures;
- long printable ASCII strings;
- high-entropy file windows;
- explicit trailing-payload-region detection for JPEG, PNG and WebP when a reliable primary-container end can be established.

No embedded object is executed or automatically extracted.

## LSB / bit-plane heuristics
Added a separate pixel-domain steganography analyzer:
- RGB least-significant-bit one ratios;
- adjacent sampled LSB transition rate;
- binary entropy for all eight RGB bit planes;
- conservative near-random LSB indicator;
- channel-imbalance indicator;
- bit-plane entropy contrast indicator.

All LSB findings are Possible or Unknown. None is treated as proof of steganography.

## Visual tools
Existing:
- RGB channel visualization;
- selectable bit-plane visualization.

Added:
- local 16×16 luminance entropy map exported as PNG.

These are diagnostic images only.

## Safety
- offline processing;
- no payload execution;
- no automatic executable extraction;
- bounded signature/string/entropy findings;
- decoded-pixel cap for LSB analysis;
- sampled-pixel cap;
- CancellationToken support;
- no claim that high entropy, random-looking LSBs or appended data automatically means malicious content.

## Important interpretation limitation
Compressed image streams naturally contain high entropy, and ordinary camera/processing pipelines often produce near-random LSB statistics. These features are useful for locating areas that merit investigation, not for issuing a binary steganography verdict.

# Part 6 — Group 8: Comparison Lab

## Two-image comparison

The comparison service now includes:

- exact SHA-256 equality;
- dimensions and aspect ratios;
- X/Y scale factors;
- aHash, dHash and pHash similarity;
- normalized RGB similarity;
- center-crop directional similarity;
- pixel MAE and RMSE;
- PSNR when finite;
- JPEG quality estimates when available;
- chroma-subsampling comparison;
- full metadata diff;
- separate ICC/color-profile diff;
- conservative uniform-resize and center-crop candidates.

Every derived relationship is expressed as a candidate with an explicit limitation. Similarity does **not** prove a common source, editing history, authenticity or direction of derivation.

## Comparison artifacts

The Android workflow can generate and share:

- grayscale amplified difference map;
- difference heatmap;
- 50/50 normalized overlay;
- side-by-side contact sheet;
- comparison JSON;
- comparison TXT.

These are diagnostic visualizations. The normalized overlay/difference views resize the inputs for comparison and are not pixel-coordinate registration proof.

## Batch

Batch processing remains capped at 50 selected files and now records:

- SHA-256;
- detected format and byte size;
- dimensions and aspect ratio;
- explicit GPS presence;
- privacy-risk count;
- forensic-indicator count;
- barcode count;
- OCR character count;
- aHash/dHash/pHash;
- exact duplicate link by SHA-256;
- near-duplicate candidate by pHash >= 0.95;
- per-file error text.

Batch export now produces both CSV and JSON.

The UI can filter the last batch result by:
- all;
- GPS;
- privacy risks;
- exact duplicates;
- near-duplicate candidates;
- errors.

Near-duplicate grouping is a perceptual-hash heuristic only and is not proof that one file came from another.

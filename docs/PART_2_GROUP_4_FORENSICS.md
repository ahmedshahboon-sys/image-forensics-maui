# Part 2 — Group 4 Forensic Checks

This part implements the Group 4 baseline from the master instructions while preserving the rule that a heuristic must never become a final authenticity verdict.

## Metadata and consistency checks
Implemented:
- filename extension vs magic-byte mismatch;
- appended/trailing data fact;
- structural container warning aggregation;
- conflicting Make / Model / Lens Model / Software values;
- multiple serial-like values as a cautious possible inconsistency;
- EXIF/decoder orientation mismatch;
- EXIF pixel dimensions vs decoded dimensions;
- extreme decoded dimensions/aspect-ratio facts;
- original/digitized/modified timestamp ordering;
- duplicate original-capture timestamp conflicts;
- thumbnail/main-image aspect-ratio mismatch when explicit thumbnail dimensions are available.

Every result contains:
- Evidence;
- Confidence;
- Limitation.

## JPEG / compression checks
Implemented:
- JPEG quantization table extraction;
- approximate JPEG quality derived from the first 8-bit luminance quantization table;
- chroma subsampling;
- 8×8 block-boundary ratio;
- 8-pixel grid-phase dominance;
- combined possible double-compression-grid indicator.

The double-compression result remains **Possible**, never Confirmed.

## Pixel-domain checks
Implemented:
- regional high-pass/noise coefficient of variation;
- ELA mean difference;
- ELA regional coefficient of variation;
- regional compression inconsistency indicator;
- simple resampling-periodicity heuristic;
- edge density;
- luminance histogram peakiness;
- coarse non-adjacent copy/move tile candidates.

## Safety and interpretation
- No result says “original”, “genuine”, “fake” or “forged” as a final verdict.
- ELA is explicitly treated as supporting evidence only.
- Resampling, noise, double-JPEG and clone/copy-move results are probabilistic.
- No external upload is used.
- Pixel analysis keeps decoded-pixel and preview-size caps.
- Cancellation remains supported.

## Deferred refinement
Scientific-grade source-camera attribution, CFA/PRNU analysis, coefficient-domain JPEG histograms and court-grade splice localization are outside this baseline and are not represented as certainty.

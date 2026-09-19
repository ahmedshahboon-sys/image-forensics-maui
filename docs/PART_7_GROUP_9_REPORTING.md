# Part 7 — Group 9: Reporting

Group 9 implements the reporting requirements from the master prompt.

## Scan report structure

TXT and PDF reports are organized into the required twelve sections:

1. Summary
2. Confirmed facts
3. Metadata
4. GPS
5. Camera/device
6. File structure
7. Compression
8. Forensic indicators
9. Hidden-data indicators
10. OCR/QR/barcode
11. Privacy risks
12. Confidence & limitations

The summary records:
- app version;
- scan time;
- file name and size;
- detected type;
- source SHA-256;
- algorithms actually used by the available scan result.

## Confidence discipline

Technical/forensic findings carry an explicit confidence label wherever the model supports it.

The report includes a four-level confidence glossary:
- Confirmed
- Likely
- Possible
- Unknown

The limitations section explicitly states that probabilistic signals do not prove authenticity, forgery, source, identity or location.

## JSON

JSON is now wrapped in a versioned report envelope containing:
- schema version;
- report type;
- export generation time;
- app version;
- source SHA-256;
- algorithm list;
- confidence glossary;
- global limitations;
- full structured ScanReport.

## PDF Unicode / Arabic

The previous PDF path replaced all non-ASCII characters with question marks. That path was removed.

PDF text now uses:
- SkiaSharp 4.152.0;
- SkiaSharp.HarfBuzz 4.152.0;
- system font fallback capable of Arabic glyphs;
- HarfBuzz complex-script shaping;
- RTL alignment for Arabic-containing lines;
- shaped-width wrapping rather than fixed ASCII character slicing.

The report therefore preserves Arabic text instead of intentionally discarding it.

## Export integrity

Android export now shares:
- JSON;
- TXT;
- PDF;
- a SHA-256 manifest covering all three exported report files.

The SHA-256 inside the forensic report remains the hash of the analyzed source image, while the manifest hashes the generated report artifacts.

## Batch

Batch export remains:
- CSV;
- JSON.

## Limitation

GitHub Actions can verify that PDF generation succeeds and that the result is a valid PDF file. Final visual inspection of Arabic shaping on a physical Android device remains part of final device QA.

# Part 4 — Group 6: OCR / QR / Barcode / Visible Content

## Offline OCR

Implemented with Tesseract through TesseractOcrMaui.

Bundled languages:
- Arabic: ara
- English: eng

The build fetches the official tessdata_fast language files from pinned commit:

87416418657359cb625c412a48b6e1d6d41c29bd

The binary traineddata files are not committed. The fetch script verifies each downloaded file against its expected Git blob SHA before packaging.

Runtime OCR:
- runs on-device;
- does not upload the selected image;
- normalizes decoded input to an internal PNG;
- applies decoded-pixel and preview-size caps;
- limits native OCR to one concurrent engine operation;
- returns the Tesseract confidence value.

Cancellation is honored before and after the native OCR call. The current wrapper does not expose a CancellationToken for forcibly interrupting a native recognition already in progress.

## QR / barcode

Existing local ZXing scanning remains enabled. Raw QR/barcode text is displayed. HTTP/HTTPS QR payloads are additionally labeled as QR URL. They are never opened automatically.

## Visible text extraction

From OCR text and barcode payloads, the app identifies:
- HTTP/HTTPS URLs;
- email addresses;
- phone-number-like text.

These values remain text-only and are not dialed or opened automatically.

## UI

A deep scan includes OCR results in the report. The UI adds an explicit "نسخ نص OCR" action.

## Deliberately excluded

No person identity or face recognition is implemented. No visual geolocation is inferred from faces or backgrounds. Object/logo/license-plate recognition remains deferred because no sufficiently validated offline model baseline is bundled.

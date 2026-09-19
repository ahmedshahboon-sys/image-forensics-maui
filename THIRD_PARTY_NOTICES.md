# Third-party libraries and licenses

This document lists direct NuGet dependencies used by the project. License names were checked against NuGet package metadata for the pinned versions.

## Runtime

| Package | Version | Purpose | License |
|---|---:|---|---|
| Microsoft.Maui.Controls | 10.0.20 | Android UI / .NET MAUI | MIT |
| MetadataExtractor | 2.9.3 | EXIF/IPTC/XMP/ICC and metadata parsing | Apache-2.0 |
| SkiaSharp | 4.152.0 | image decoding, pixel analysis, visualizations, PDF drawing | MIT |
| SkiaSharp.HarfBuzz | 4.152.0 | complex-script text shaping for Unicode/Arabic PDF reports | MIT |
| ZXing.Net.Bindings.SkiaSharp | 0.16.24 | QR/barcode decoding over SkiaSharp | Apache-2.0 |
| TesseractOcrMaui | 1.5.2 | .NET MAUI wrapper around native Tesseract OCR | Apache-2.0 |
| Tesseract tessdata_fast (ara, eng) | pinned repository commit | Offline Arabic/English OCR language data | Apache-2.0 |

## Test-only

| Package | Version | Purpose | License |
|---|---:|---|---|
| Microsoft.NET.Test.Sdk | 17.14.1 | .NET test host | MIT |
| xunit | 2.9.3 | unit testing | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.1 | test runner integration | Apache-2.0 |

Transitive dependencies keep their own licenses. Before redistributing a production binary, preserve any notices required by the actual package graph produced by `dotnet restore`.


## OCR model acquisition

The binary `.traineddata` files are not committed. `scripts/fetch-ocr-models.sh` downloads the pinned Arabic and English models from the official tessdata_fast repository and verifies their expected Git blob SHA before packaging.

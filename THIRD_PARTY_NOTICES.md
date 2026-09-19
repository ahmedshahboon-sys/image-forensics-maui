# Third-party libraries and licenses

This document lists direct NuGet dependencies used by the project. License names were checked against NuGet package metadata for the pinned versions.

## Runtime

| Package | Version | Purpose | License |
|---|---:|---|---|
| Microsoft.Maui.Controls | 10.0.20 | Android UI / .NET MAUI | MIT |
| MetadataExtractor | 2.9.3 | EXIF/IPTC/XMP/ICC and metadata parsing | Apache-2.0 |
| SkiaSharp | 4.152.0 | image decoding, pixel analysis, visualizations, PDF drawing | MIT |
| ZXing.Net.Bindings.SkiaSharp | 0.16.24 | QR/barcode decoding over SkiaSharp | Apache-2.0 |

## Test-only

| Package | Version | Purpose | License |
|---|---:|---|---|
| Microsoft.NET.Test.Sdk | 17.14.1 | .NET test host | MIT |
| xunit | 2.9.3 | unit testing | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.1 | test runner integration | Apache-2.0 |

Transitive dependencies keep their own licenses. Before redistributing a production binary, preserve any notices required by the actual package graph produced by `dotnet restore`.

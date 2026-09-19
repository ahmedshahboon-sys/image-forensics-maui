using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using ImageForensics.Reporting;

namespace ImageForensics.App;

public sealed class ScanCoordinator
{
    private readonly IFileIdentityInspector _identity;
    private readonly IImageTechnicalInspector _technical;
    private readonly IMetadataInspector _metadata;
    private readonly IContainerInspector _container;
    private readonly IPerceptualHashService _hashes;
    private readonly IBarcodeInspector _barcode;
    private readonly IConsistencyRuleEngine _rules;
    private readonly IHiddenDataInspector _hidden;
    private readonly IPrivacyRiskAnalyzer _privacy;
    private readonly IImageHeuristicsService _heuristics;
    private readonly ISteganographyAnalyzer _steganography;

    public ScanCoordinator(
        IFileIdentityInspector identity,
        IImageTechnicalInspector technical,
        IMetadataInspector metadata,
        IContainerInspector container,
        IPerceptualHashService hashes,
        IBarcodeInspector barcode,
        IConsistencyRuleEngine rules,
        IHiddenDataInspector hidden,
        IPrivacyRiskAnalyzer privacy,
        IImageHeuristicsService heuristics,
        ISteganographyAnalyzer steganography)
    {
        _identity = identity;
        _technical = technical;
        _metadata = metadata;
        _container = container;
        _hashes = hashes;
        _barcode = barcode;
        _rules = rules;
        _hidden = hidden;
        _privacy = privacy;
        _heuristics = heuristics;
        _steganography = steganography;
    }

    public async Task<ScanReport> QuickScanAsync(
        string path,
        string? mime = null,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(
            new(
                "identity",
                0.15,
                "File identity and hashes"));

        var identity =
            await _identity.InspectAsync(
                path,
                mime,
                progress,
                ct);

        progress?.Report(
            new(
                "technical",
                0.70,
                "Image technical properties"));

        var tech =
            await _technical.InspectAsync(
                path,
                ct);

        progress?.Report(
            new(
                "done",
                1.0,
                "Completed"));

        return new ScanReport(
            "0.9.0-beta",
            DateTimeOffset.UtcNow,
            identity,
            tech,
            null,
            null,
            null,
            Array.Empty<BarcodeHit>(),
            Array.Empty<EvidenceItem>());
    }

    public async Task<ScanReport> DeepScanAsync(
        string path,
        string? mime = null,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(
            new(
                "identity",
                0.05,
                "File identity and hashes"));

        var identity =
            await _identity.InspectAsync(
                path,
                mime,
                progress,
                ct);

        progress?.Report(
            new(
                "technical",
                0.18,
                "Image technical properties"));

        var tech =
            await _technical.InspectAsync(
                path,
                ct);

        progress?.Report(
            new(
                "metadata",
                0.29,
                "EXIF/IPTC/XMP/ICC"));

        var meta =
            await _metadata.InspectAsync(
                path,
                ct);

        progress?.Report(
            new(
                "container",
                0.40,
                "Container structure"));

        var container =
            await _container.InspectAsync(
                path,
                ct);

        progress?.Report(
            new(
                "hashes",
                0.50,
                "Perceptual hashes"));

        var hashes =
            await _hashes.ComputeAsync(
                path,
                ct);

        progress?.Report(
            new(
                "qr",
                0.58,
                "QR and barcode"));

        var barcodes =
            await _barcode.InspectAsync(
                path,
                ct);

        progress?.Report(
            new(
                "rules",
                0.65,
                "Consistency checks"));

        var indicators =
            _rules.Analyze(
                    new(
                        identity,
                        tech,
                        meta,
                        container))
                .ToList();

        progress?.Report(
            new(
                "hidden",
                0.72,
                "Hidden-data signatures, strings and entropy"));

        var hidden =
            await _hidden.InspectAsync(
                path,
                ct);

        progress?.Report(
            new(
                "steganography",
                0.79,
                "LSB and bit-plane statistics"));

        SteganographyResult? steganography = null;

        try
        {
            steganography =
                await _steganography.AnalyzeAsync(
                    path,
                    ct);

            indicators.AddRange(
                steganography.Indicators);
        }
        catch (InvalidDataException ex)
        {
            indicators.Add(
                new EvidenceItem(
                    "stego.skipped",
                    "Steganography pixel analysis skipped",
                    ex.Message,
                    ForensicConfidence.Unknown,
                    "Safety/decoder limits prevented LSB analysis.",
                    "File-level signature, entropy, metadata and container analysis remain available."));
        }

        progress?.Report(
            new(
                "privacy",
                0.84,
                "Privacy risks"));

        var privacy =
            _privacy.Analyze(
                meta,
                container);

        progress?.Report(
            new(
                "pixels",
                0.91,
                "Pixel-domain heuristics"));

        ImageHeuristicsResult? heuristics = null;

        try
        {
            heuristics =
                await _heuristics.AnalyzeAsync(
                    path,
                    ct);

            indicators.AddRange(
                heuristics.Indicators);
        }
        catch (InvalidDataException ex)
        {
            indicators.Add(
                new EvidenceItem(
                    "pixels.skipped",
                    "Pixel-domain heuristics skipped",
                    ex.Message,
                    ForensicConfidence.Unknown,
                    "Safety limit prevented full pixel analysis.",
                    "Core metadata/container analysis is still valid."));
        }

        progress?.Report(
            new(
                "done",
                1.0,
                "Completed"));

        return new ScanReport(
            "0.9.0-beta",
            DateTimeOffset.UtcNow,
            identity,
            tech,
            meta,
            container,
            hashes,
            barcodes,
            indicators,
            hidden,
            privacy,
            heuristics,
            steganography);
    }
}

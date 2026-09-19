using ImageForensics.Core.Models;

namespace ImageForensics.Reporting;

public sealed record ScanReport(
    string AppVersion,
    DateTimeOffset ScannedAtUtc,
    FileIdentityResult Identity,
    ImageTechnicalInfo? Technical,
    MetadataInspectionResult? Metadata,
    ContainerInspectionResult? Container,
    PerceptualHashResult? PerceptualHashes,
    IReadOnlyList<BarcodeHit> Barcodes,
    IReadOnlyList<EvidenceItem> Indicators,
    IReadOnlyList<HiddenDataFinding>? HiddenData = null,
    PrivacyRiskReport? Privacy = null,
    ImageHeuristicsResult? ImageHeuristics = null,
    SteganographyResult? Steganography = null,
    OcrInspectionResult? Ocr = null,
    IReadOnlyList<VisibleTextEntity>? VisibleTextEntities = null);

public sealed record BatchReportRow(
    string FileName,
    string Sha256,
    string DetectedType,
    long SizeBytes,
    int Width,
    int Height,
    double AspectRatio,
    bool HasGps,
    int PrivacyRiskCount,
    int IndicatorCount,
    int BarcodeCount,
    int OcrCharacterCount,
    string AHash,
    string DHash,
    string PHash,
    string? DuplicateOf,
    string? NearDuplicateOf,
    string? Error);

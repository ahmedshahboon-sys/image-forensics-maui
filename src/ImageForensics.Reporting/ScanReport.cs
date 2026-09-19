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
    IReadOnlyList<EvidenceItem> Indicators);

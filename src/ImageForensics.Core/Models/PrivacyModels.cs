namespace ImageForensics.Core.Models;

public sealed record PrivacyRisk(
    string Code,
    string Title,
    string Evidence,
    ForensicConfidence Confidence);

public sealed record PrivacyRiskReport(IReadOnlyList<PrivacyRisk> Risks);

public sealed record MetadataCleanResult(
    string OutputPath,
    string OutputFormat,
    string OriginalSha256,
    string CleanSha256,
    int MetadataFieldsBefore,
    int MetadataFieldsAfter,
    bool GpsRemoved,
    bool OriginalUntouched);

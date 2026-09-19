namespace ImageForensics.Core.Models;

public sealed record PrivacyRisk(
    string Code,
    string Title,
    string Evidence,
    ForensicConfidence Confidence);

public sealed record PrivacyRiskReport(
    IReadOnlyList<PrivacyRisk> Risks)
{
    public bool HasSensitiveData => Risks.Count > 0;
}

public sealed record MetadataCleanResult(
    string OutputPath,
    string OutputFormat,
    string OriginalSha256,
    string CleanSha256,
    int MetadataFieldsBefore,
    int MetadataFieldsAfter,
    bool GpsRemoved,
    bool OriginalUntouched,
    int PrivacyRisksBefore,
    int PrivacyRisksAfter,
    IReadOnlyList<string> PrivacyRiskCodesBefore,
    IReadOnlyList<string> PrivacyRiskCodesAfter,
    long TrailingBytesBefore,
    long TrailingBytesAfter,
    bool OrientationApplied,
    string SourceOrientation,
    string CleanOrientation,
    int SourceWidth,
    int SourceHeight,
    int CleanWidth,
    int CleanHeight,
    bool VerificationPassed,
    IReadOnlyList<string> VerificationNotes);

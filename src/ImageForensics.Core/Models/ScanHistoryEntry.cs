namespace ImageForensics.Core.Models;

public sealed record ScanHistoryEntry(
    DateTimeOffset ScannedAtUtc,
    string FileName,
    string DetectedType,
    long SizeBytes,
    string Sha256,
    bool DeepScan,
    int IndicatorCount,
    int PrivacyRiskCount);

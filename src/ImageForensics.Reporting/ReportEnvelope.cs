namespace ImageForensics.Reporting;

public sealed record ReportEnvelope(
    string SchemaVersion,
    string ReportType,
    DateTimeOffset GeneratedAtUtc,
    string AppVersion,
    string SourceSha256,
    IReadOnlyList<string> Algorithms,
    IReadOnlyDictionary<string, string> ConfidenceScale,
    IReadOnlyList<string> GlobalLimitations,
    ScanReport Report);

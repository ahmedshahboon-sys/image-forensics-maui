namespace ImageForensics.Core.Models;

public sealed record MetadataField(
    string Directory,
    string Tag,
    string? RawValue,
    string? ParsedValue,
    string Meaning,
    ForensicConfidence Confidence,
    string Source);

public sealed record GpsInfo(double Latitude, double Longitude);

public sealed record MetadataInspectionResult(
    IReadOnlyList<MetadataField> Fields,
    GpsInfo? Gps,
    IReadOnlyList<string> Errors);

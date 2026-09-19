namespace ImageForensics.Core.Models;

public sealed record ContainerSegment(
    long Offset,
    long Length,
    string Type,
    string Description,
    bool Suspicious = false);

public sealed record ContainerInspectionResult(
    string Format,
    IReadOnlyList<ContainerSegment> Segments,
    long TrailingBytes,
    IReadOnlyList<string> Warnings);

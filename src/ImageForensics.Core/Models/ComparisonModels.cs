namespace ImageForensics.Core.Models;

public sealed record MetadataDifference(
    string Directory,
    string Tag,
    string? LeftValue,
    string? RightValue);

public sealed record ImageComparisonResult(
    bool ExactMatch,
    string LeftSha256,
    string RightSha256,
    int LeftWidth,
    int LeftHeight,
    int RightWidth,
    int RightHeight,
    double AHashSimilarity,
    double DHashSimilarity,
    double PHashSimilarity,
    IReadOnlyList<MetadataDifference> MetadataDifferences);

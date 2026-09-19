namespace ImageForensics.Core.Models;

public sealed record MetadataDifference(
    string Directory,
    string Tag,
    string? LeftValue,
    string? RightValue);

public sealed record PixelComparisonMetrics(
    double NormalizedRgbSimilarity,
    double CenterCropSimilarity,
    double MeanAbsoluteError,
    double RootMeanSquareError,
    double? PsnrDb,
    int ComparedWidth,
    int ComparedHeight);

public sealed record ImageComparisonResult(
    bool ExactMatch,
    string LeftSha256,
    string RightSha256,
    int LeftWidth,
    int LeftHeight,
    int RightWidth,
    int RightHeight,
    double LeftAspectRatio,
    double RightAspectRatio,
    double ScaleX,
    double ScaleY,
    string DimensionRelation,
    bool UniformResizeCandidate,
    bool CenterCropCandidate,
    double AHashSimilarity,
    double DHashSimilarity,
    double PHashSimilarity,
    PixelComparisonMetrics PixelMetrics,
    double? LeftEstimatedJpegQuality,
    double? RightEstimatedJpegQuality,
    string? LeftChromaSubsampling,
    string? RightChromaSubsampling,
    IReadOnlyList<MetadataDifference> MetadataDifferences,
    IReadOnlyList<MetadataDifference> IccDifferences,
    IReadOnlyList<EvidenceItem> Indicators);

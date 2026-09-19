namespace ImageForensics.Core.Models;

public sealed record ImageHeuristicsResult(
    double? EstimatedJpegQuality,
    string? ChromaSubsampling,
    double BlockBoundaryRatio,
    double NoiseCoefficientOfVariation,
    double ElaMeanDifference,
    int CloneCandidatePairs,
    IReadOnlyList<EvidenceItem> Indicators);

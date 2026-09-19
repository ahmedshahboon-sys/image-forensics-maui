namespace ImageForensics.Core.Models;

public sealed record ImageHeuristicsResult(
    double? EstimatedJpegQuality,
    string? ChromaSubsampling,
    int JpegQuantizationTableCount,
    double? JpegQuantizationMean,
    double BlockBoundaryRatio,
    double GridPhaseDominance,
    double ResamplingPeriodicityScore,
    double NoiseCoefficientOfVariation,
    double ElaMeanDifference,
    double ElaRegionalCoefficientOfVariation,
    double EdgeDensity,
    double HistogramPeakiness,
    int CloneCandidatePairs,
    IReadOnlyList<EvidenceItem> Indicators);

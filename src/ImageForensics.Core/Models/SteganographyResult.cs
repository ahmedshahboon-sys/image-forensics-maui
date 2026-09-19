namespace ImageForensics.Core.Models;

public sealed record SteganographyResult(
    double RedLsbOneRatio,
    double GreenLsbOneRatio,
    double BlueLsbOneRatio,
    double LsbTransitionRate,
    IReadOnlyList<double> BitPlaneEntropies,
    long SampledPixels,
    IReadOnlyList<EvidenceItem> Indicators);

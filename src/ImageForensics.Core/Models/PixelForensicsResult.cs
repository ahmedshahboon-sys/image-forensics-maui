namespace ImageForensics.Core.Models;

public sealed record PixelForensicsResult(
    double MeanLuma,
    double LumaStdDev,
    double DarkPixelFraction,
    double BrightPixelFraction,
    double MeanHorizontalEdge,
    double MeanVerticalEdge,
    double JpegBlockBoundaryRatio,
    IReadOnlyList<EvidenceItem> Indicators);

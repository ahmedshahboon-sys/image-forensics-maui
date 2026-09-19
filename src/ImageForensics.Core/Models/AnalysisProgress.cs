namespace ImageForensics.Core.Models;

public sealed record AnalysisProgress(string Stage, double Percent, string? Detail = null);

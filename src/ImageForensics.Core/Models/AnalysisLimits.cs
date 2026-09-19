namespace ImageForensics.Core.Models;

public sealed record AnalysisLimits
{
    public long MaxFileBytes { get; init; } = 512L * 1024 * 1024;
    public int BufferBytes { get; init; } = 128 * 1024;
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromMinutes(2);
}

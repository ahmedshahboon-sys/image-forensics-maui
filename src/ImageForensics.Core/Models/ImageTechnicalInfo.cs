namespace ImageForensics.Core.Models;

public sealed record ImageTechnicalInfo
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required double AspectRatio { get; init; }
    public required string EncodedFormat { get; init; }
    public required string ColorType { get; init; }
    public required string AlphaType { get; init; }
    public required bool HasAlpha { get; init; }
    public required int BitsPerPixel { get; init; }
    public required int FrameCount { get; init; }
    public required string Orientation { get; init; }
}

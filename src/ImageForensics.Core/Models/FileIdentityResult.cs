namespace ImageForensics.Core.Models;

public sealed record FileIdentityResult
{
    public required string FileName { get; init; }
    public string? SafeSource { get; init; }
    public required long SizeBytes { get; init; }
    public required string Extension { get; init; }
    public required string DeclaredMime { get; init; }
    public required string DetectedType { get; init; }
    public required string DetectedMime { get; init; }
    public required bool ExtensionMismatch { get; init; }
    public DateTimeOffset? CreatedUtc { get; init; }
    public DateTimeOffset? ModifiedUtc { get; init; }
    public required double EntropyBitsPerByte { get; init; }
    public required string Sha256 { get; init; }
    public required string Sha1 { get; init; }
    public required string Md5 { get; init; }
    public required string SignatureHex { get; init; }
}

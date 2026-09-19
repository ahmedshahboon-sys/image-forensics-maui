namespace ImageForensics.Core.Models;

public sealed record DiagnosticImageResult(
    string Path,
    string Kind,
    string Limitation);

namespace ImageForensics.Core.Models;

public sealed record HiddenDataFinding(
    long Offset,
    string Kind,
    string Evidence,
    ForensicConfidence Confidence,
    string Limitation);

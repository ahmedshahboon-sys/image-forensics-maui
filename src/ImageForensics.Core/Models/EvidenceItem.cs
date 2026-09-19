namespace ImageForensics.Core.Models;

public sealed record EvidenceItem(
    string Code,
    string Title,
    string Detail,
    ForensicConfidence Confidence,
    string Evidence,
    string Limitation);

using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IConsistencyRuleEngine
{
    IReadOnlyList<EvidenceItem> Analyze(ForensicAnalysisContext context);
}

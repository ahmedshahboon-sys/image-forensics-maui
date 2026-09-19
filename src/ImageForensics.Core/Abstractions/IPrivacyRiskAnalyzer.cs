using ImageForensics.Core.Models;

namespace ImageForensics.Core.Abstractions;

public interface IPrivacyRiskAnalyzer
{
    PrivacyRiskReport Analyze(MetadataInspectionResult metadata, ContainerInspectionResult container);
}

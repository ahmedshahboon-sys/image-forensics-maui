namespace ImageForensics.Core.Models;

public sealed record ForensicAnalysisContext(
    FileIdentityResult Identity,
    ImageTechnicalInfo Technical,
    MetadataInspectionResult Metadata,
    ContainerInspectionResult Container);

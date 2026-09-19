using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using ImageForensics.Reporting;

namespace ImageForensics.App;

public sealed class ScanCoordinator
{
    private readonly IFileIdentityInspector _identity;
    private readonly IImageTechnicalInspector _technical;
    private readonly IMetadataInspector _metadata;
    private readonly IContainerInspector _container;
    private readonly IPerceptualHashService _hashes;
    private readonly IBarcodeInspector _barcode;
    private readonly IConsistencyRuleEngine _rules;
    private readonly IHiddenDataInspector _hidden;
    private readonly IPrivacyRiskAnalyzer _privacy;
    private readonly IImageHeuristicsService _heuristics;

    public ScanCoordinator(
        IFileIdentityInspector identity,IImageTechnicalInspector technical,IMetadataInspector metadata,
        IContainerInspector container,IPerceptualHashService hashes,IBarcodeInspector barcode,
        IConsistencyRuleEngine rules,IHiddenDataInspector hidden,IPrivacyRiskAnalyzer privacy,
        IImageHeuristicsService heuristics)
    {
        _identity=identity;_technical=technical;_metadata=metadata;_container=container;_hashes=hashes;
        _barcode=barcode;_rules=rules;_hidden=hidden;_privacy=privacy;_heuristics=heuristics;
    }

    public async Task<ScanReport> DeepScanAsync(string path,string? mime=null,IProgress<AnalysisProgress>? progress=null,CancellationToken ct=default)
    {
        progress?.Report(new("identity",0.05,"File identity and hashes"));
        var identity=await _identity.InspectAsync(path,mime,progress,ct);
        progress?.Report(new("technical",0.20,"Image technical properties"));
        var tech=await _technical.InspectAsync(path,ct);
        progress?.Report(new("metadata",0.32,"EXIF/IPTC/XMP/ICC"));
        var meta=await _metadata.InspectAsync(path,ct);
        progress?.Report(new("container",0.45,"Container structure"));
        var container=await _container.InspectAsync(path,ct);
        progress?.Report(new("hashes",0.55,"Perceptual hashes"));
        var hashes=await _hashes.ComputeAsync(path,ct);
        progress?.Report(new("qr",0.62,"QR and barcode"));
        var barcodes=await _barcode.InspectAsync(path,ct);
        progress?.Report(new("rules",0.70,"Consistency checks"));
        var indicators=_rules.Analyze(new(identity,tech,meta,container)).ToList();
        progress?.Report(new("hidden",0.78,"Hidden-data heuristics"));
        var hidden=await _hidden.InspectAsync(path,ct);
        progress?.Report(new("privacy",0.84,"Privacy risks"));
        var privacy=_privacy.Analyze(meta,container);
        progress?.Report(new("pixels",0.90,"Pixel-domain heuristics"));
        ImageHeuristicsResult? heuristics=null;
        try{heuristics=await _heuristics.AnalyzeAsync(path,ct);indicators.AddRange(heuristics.Indicators);}
        catch(InvalidDataException ex)
        {
            indicators.Add(new EvidenceItem("pixels.skipped","Pixel-domain heuristics skipped",ex.Message,ForensicConfidence.Unknown,
                "Safety limit prevented full pixel analysis.","Core metadata/container analysis is still valid."));
        }
        progress?.Report(new("done",1.0,"Completed"));
        return new ScanReport("0.9.0-beta",DateTimeOffset.UtcNow,identity,tech,meta,container,hashes,barcodes,indicators,hidden,privacy,heuristics);
    }
}

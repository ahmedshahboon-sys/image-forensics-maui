using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using ImageForensics.Forensics.Comparison;
using ImageForensics.Forensics.Containers;
using ImageForensics.Forensics.FileIdentity;
using ImageForensics.Forensics.HiddenData;
using ImageForensics.Forensics.Privacy;
using ImageForensics.Forensics.Rules;
using ImageForensics.Imaging;
using ImageForensics.Metadata;
using ImageForensics.Reporting;
using Microsoft.Extensions.Logging;

namespace ImageForensics.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder =
            MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();

        builder.Services.AddSingleton(
            new AnalysisLimits());
        builder.Services.AddSingleton<IFileIdentityInspector, SafeFileIdentityInspector>();
        builder.Services.AddSingleton<IImageTechnicalInspector, ImageTechnicalInspector>();
        builder.Services.AddSingleton<IMetadataInspector, MetadataInspector>();
        builder.Services.AddSingleton<IContainerInspector, SafeContainerInspector>();
        builder.Services.AddSingleton<IPerceptualHashService, PerceptualHashService>();
        builder.Services.AddSingleton<IBarcodeInspector, BarcodeInspector>();
        builder.Services.AddSingleton<IConsistencyRuleEngine, ConsistencyRuleEngine>();
        builder.Services.AddSingleton<IHiddenDataInspector, HiddenDataInspector>();
        builder.Services.AddSingleton<IPrivacyRiskAnalyzer, PrivacyRiskAnalyzer>();
        builder.Services.AddSingleton<IMetadataCleaner, MetadataCleaner>();
        builder.Services.AddSingleton<IImageComparisonService, ImageComparisonService>();
        builder.Services.AddSingleton<IImageHeuristicsService, ImageHeuristicsService>();
        builder.Services.AddSingleton<ISteganographyAnalyzer, SteganographyAnalyzer>();
        builder.Services.AddSingleton<IImageVisualizationService, ImageVisualizationService>();
        builder.Services.AddSingleton<ReportWriter>();
        builder.Services.AddSingleton<ScanCoordinator>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

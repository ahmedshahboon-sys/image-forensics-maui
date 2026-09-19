using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using ImageForensics.Forensics.Containers;
using ImageForensics.Forensics.FileIdentity;
using ImageForensics.Forensics.HiddenData;
using ImageForensics.Forensics.Rules;
using ImageForensics.Imaging;
using ImageForensics.Metadata;
using Microsoft.Extensions.Logging;

namespace ImageForensics.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton(new AnalysisLimits());
        builder.Services.AddSingleton<IFileIdentityInspector, SafeFileIdentityInspector>();
        builder.Services.AddSingleton<IImageTechnicalInspector, ImageTechnicalInspector>();
        builder.Services.AddSingleton<IMetadataInspector, MetadataInspector>();
        builder.Services.AddSingleton<IContainerInspector, SafeContainerInspector>();
        builder.Services.AddSingleton<IPerceptualHashService, PerceptualHashService>();
        builder.Services.AddSingleton<IBarcodeInspector, BarcodeInspector>();
        builder.Services.AddSingleton<IConsistencyRuleEngine, ConsistencyRuleEngine>();
        builder.Services.AddSingleton<IHiddenDataInspector, HiddenDataInspector>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}

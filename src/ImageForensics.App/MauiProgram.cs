using ImageForensics.Core.Abstractions;
using ImageForensics.Core.Models;
using ImageForensics.Forensics.FileIdentity;
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
        builder.Services.AddSingleton<MainPage>();
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}

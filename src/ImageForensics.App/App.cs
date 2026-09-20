namespace ImageForensics.App;

public sealed class App : Application
{
    private readonly MainPage _mainPage;

    public App(MainPage mainPage)
    {
        _mainPage = mainPage;
        UserAppTheme = AppTheme.Unspecified;
    }

    protected override Window CreateWindow(
        IActivationState? activationState)
        => new(
            new NavigationPage(
                _mainPage));
}
